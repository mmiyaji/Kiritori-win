using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Services.Recording
{
    internal sealed class FfmpegPipeOptions
    {
        public string OutputPath;    // 例: @"C:\temp\out.mp4"
        public int Width;
        public int Height;
        public int Fps = 30;
        public OutputKind Kind = OutputKind.Mp4;
        public string FfmpegPath = "ffmpeg"; // PATH or 同梱相対パス
    }

    internal sealed class FfmpegPipeRecorder : IDisposable
    {
        public FfmpegPipeOptions Options { get; }
        private Process _proc;
        private Stream _stdin;
        private byte[] _lineBuf; // 1ラインぶん (Width * 4)
        private bool _started;
        private readonly object _sync = new object();
        private CancellationTokenSource _cts;
        private volatile bool _disposed;
        private volatile int _state; // 0=Stopped,1=Starting,2=Running,3=Stopping
        private EventHandler _onExited; // 解除のため保持

        // CFR ワーカー（あなたの環境に既にあればそのまま使われます）
        private volatile bool _run;
        private Thread _worker;          // 既存実装があればそのまま
        private Bitmap _latest;          // 既存実装があればそのまま
        private readonly object _frameLock = new object();
        private readonly System.Text.StringBuilder _stderrBuf = new System.Text.StringBuilder(8192);
        private const int _stderrMax = 32768;
        // private volatile int _exitCode = int.MinValue;
        public int GracefulExitTimeoutMs = 15000;

        // 進捗ログ用
        private int _writtenFrames;
        private readonly Stopwatch _sinceStart = new Stopwatch();
        

        public FfmpegPipeRecorder(FfmpegPipeOptions opt)
        {
            Options = opt ?? throw new ArgumentNullException(nameof(opt));
            if (Options.Width <= 0 || Options.Height <= 0) throw new ArgumentException("invalid size");
            if (Options.Fps <= 0 || Options.Fps > 240) throw new ArgumentException("invalid fps");
            _lineBuf = new byte[Options.Width * 4];
        }

        // -----------------------------------------
        // Start: プロセス起動＋CFRワーカー開始＋Exitedハンドラ登録
        // -----------------------------------------
        public void Start(string ffmpegPath = null, string arguments = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FfmpegPipeRecorder));

            lock (_sync)
            {
                if (_state == 2 || _state == 1) return; // すでに実行/起動中
                _state = 1;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                // ffmpeg パス／引数の既定化
                var exe = string.IsNullOrWhiteSpace(ffmpegPath) ? (Options.FfmpegPath ?? "ffmpeg") : ffmpegPath;
                var args = string.IsNullOrWhiteSpace(arguments) ? BuildArgs(Options) : arguments;

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _onExited = new EventHandler(OnProcessExited);

                try
                {
                    if (!proc.Start())
                    {
                        _state = 0;
                        throw new InvalidOperationException("Failed to start ffmpeg.");
                    }

                    // 起動成功 → フィールドへ確定代入
                    _proc = proc;
                    _stdin = proc.StandardInput.BaseStream;
                    proc.Exited += _onExited;

                    // stderr 読み取り開始（ログ用途）
                    Task.Run(() => DrainStderr(proc, _cts.Token));

                    // 進捗カウンタ初期化
                    _writtenFrames = 0;
                    _sinceStart.Restart();

                    // CFR ワーカー開始（必要な場合）
                    if (_worker == null || !_worker.IsAlive)
                    {
                        _run = true;
                        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "FfmpegPipeRecorder.Worker" };
                        _worker.Start();
                    }

                    _started = true;
                    _state = 2; // Running
                    Log.Debug($"REC Start: {exe} {args}", "REC");
                }
                catch
                {
                    try { proc.Exited -= _onExited; } catch { /* ignore */ }
                    SafeKillAndDispose(proc);
                    _onExited = null;
                    _stdin = null;
                    _proc = null;
                    _state = 0;
                    throw;
                }
            }
        }

        // -----------------------------------------
        // Stop: 正常停止（CFRワーカー停止 → EOF → 待機）
        // -----------------------------------------
        public async Task StopAsync(int waitExitMs = 2000)
        {
            // すでに停止中ならスキップ
            lock (_sync)
            {
                if (_state == 0 || _state == 3) return;
                _state = 3; // Stopping
            }

            // 1) ワーカー停止
            _run = false;
            try { _worker?.Join(2000); } catch { /* ignore */ }
            _worker = null;

            CancellationTokenSource cts = null;
            Process proc = null;
            Stream stdin = null;

            // 2) 参照スナップショット＆Exited解除（遅延発火対策）
            lock (_sync)
            {
                cts = _cts; _cts = null;
                proc = _proc;
                stdin = _stdin;

                if (proc != null && _onExited != null)
                {
                    try { proc.Exited -= _onExited; } catch { /* ignore */ }
                }
                _onExited = null;
            }

            // 3) EOF を伝える
            try { stdin?.Flush(); } catch { /* ignore */ }
            try { stdin?.Dispose(); } catch { /* ignore */ }

            // 4) バックグラウンド処理へキャンセル
            try { cts?.Cancel(); } catch { /* ignore */ }
            cts?.Dispose();

            // 5) ffmpeg の終了待ち（タイムアウトなら Kill）
            if (proc != null)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        var t = Task.Run(() => proc.WaitForExit(waitExitMs));
                        var ok = await t.ConfigureAwait(false);
                        if (!ok && !proc.HasExited)
                        {
                            try { proc.Kill(); } catch { /* ignore */ }
                            try { proc.WaitForExit(); } catch { /* ignore */ }
                        }
                    }
                }
                finally
                {
                    SafeKillAndDispose(proc);
                }
            }

            // 6) 状態クリア
            lock (_sync)
            {
                _stdin = null;
                _proc = null;
                _started = false;
                if (_state != 0) _state = 0; // Stopped
            }

            Log.Debug($"REC Stopped: frames={_writtenFrames}, elapsed={_sinceStart.Elapsed.TotalSeconds:F1}s", "REC");
        }

        // -----------------------------------------
        // Exited: 二重実行/NREを防止
        // -----------------------------------------
        private void OnProcessExited(object sender, EventArgs e)
        {
            // すでに停止処理中なら何もしない
            if (_state == 0 || _state == 3) return;

            lock (_sync)
            {
                if (_state == 0 || _state == 3) return;

                var proc = sender as Process ?? _proc;

                // まずイベント解除
                if (proc != null && _onExited != null)
                {
                    try { proc.Exited -= _onExited; } catch { /* ignore */ }
                }
                _onExited = null;

                // 入力/トークンをクリーンアップ
                try { _stdin?.Dispose(); } catch { /* ignore */ }
                _stdin = null;

                try { _cts?.Cancel(); } catch { /* ignore */ }
                _cts?.Dispose();
                _cts = null;

                SafeKillAndDispose(proc);
                _proc = null;

                _run = false;
                _started = false;
                _state = 0;
            }

            Log.Debug($"REC Exited: frames={_writtenFrames}, elapsed={_sinceStart.Elapsed.TotalSeconds:F1}s", "REC");
        }

        // -----------------------------------------
        // 補助: stderr 読み取り（任意）
        // -----------------------------------------
        private void DrainStderr(Process p, CancellationToken ct)
        {
            try
            {
                var r = p.StandardError;
                var buf = new char[1024];
                while (!ct.IsCancellationRequested)
                {
                    var n = r.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    // 必要ならログへ
                    // Log.Debug(new string(buf, 0, n), "FFmpeg");
                }
            }
            catch { /* ignore */ }
        }

        // -----------------------------------------
        // 補助: 安全に破棄
        // -----------------------------------------
        private static void SafeKillAndDispose(Process p)
        {
            if (p == null) return;
            try { if (!p.HasExited) p.Kill(); } catch { /* ignore */ }
            try { p.Dispose(); } catch { /* ignore */ }
        }

        private void WorkerLoop()
        {
            var sw = Stopwatch.StartNew();
            double periodMs = 1000.0 / Options.Fps;
            long next = 0;
            Bitmap lastSent = null;

            while (_run)
            {
                var elapsed = sw.Elapsed.TotalMilliseconds;
                var due = (next + 1) * periodMs;
                int sleep = (int)Math.Max(0, due - elapsed);
                if (sleep > 0) Thread.Sleep(sleep);
                next++;

                Bitmap toSend = null;
                lock (_frameLock)
                {
                    if (_latest != null)
                    {
                        toSend = _latest;
                        _latest = null;
                    }
                }
                if (toSend == null) toSend = lastSent;

                if (toSend != null)
                {
                    // ワーカーから送る
                    TryWriteFrame(toSend);
                    if (!ReferenceEquals(lastSent, toSend))
                    {
                        lastSent?.Dispose();
                        lastSent = new Bitmap(toSend); // 繰り返し用
                    }
                }
            }

            lastSent?.Dispose();
            lock (_frameLock) { _latest?.Dispose(); _latest = null; }
        }

        private static string BuildArgs(FfmpegPipeOptions o)
        {
            // 受け：raw BGRA、サイズ＆フレームレート固定
            var commonIn = $"-f rawvideo -pix_fmt bgra -s {o.Width}x{o.Height} -r {o.Fps} -i pipe:0 -an";

            if (o.Kind == OutputKind.Mp4)
            {
                // 低レイテンシ寄り、再生互換性の高い yuv420p、FastStart
                return $"{commonIn} -vsync cfr -c:v libx264 -preset veryfast -tune zerolatency -pix_fmt yuv420p -movflags +faststart -y \"{o.OutputPath}\"";
            }
            else // GIF
            {
                // パレット生成＋適用。入力は固定FPS想定
                // 注意: GIFは重いのでFPSは15程度を推奨（出力側でfpsフィルタ）
                // 例: 入力r=30のまま受けて、出力で fps=15 に落とす
                return $"{commonIn} -vf \"fps={Math.Min(15, o.Fps)},split[s0][s1];[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse=new=1\" -y \"{o.OutputPath}\"";
            }
        }

        /// <summary>到着フレームを差し替える（CFR ワーカー使用時）。</summary>
        public void UpdateLatestFrame(Bitmap src)
        {
            if (!_started || src == null) return;
            if (src.Width != Options.Width || src.Height != Options.Height)
            {
                Log.Debug($"UpdateLatestFrame: size mismatch src={src.Width}x{src.Height} != rec={Options.Width}x{Options.Height}", "REC");
                return;
            }
            var clone = new Bitmap(src);
            lock (_frameLock)
            {
                var old = _latest;
                _latest = clone;
                old?.Dispose();
            }
        }

        /// <summary>1フレーム書き込み（ワーカー未使用時に直接呼ぶ）。失敗時は false を返す。</summary>
        public bool TryWriteFrame(Bitmap src)
        {
            // _started かつ _stdin が有効であること
            var stdin = _stdin; // ローカルスナップショットで競合回避
            if (!_started || stdin == null) return false;

            if (_proc?.HasExited == true) return false;
            if (src == null) return false;
            if (src.Width != Options.Width || src.Height != Options.Height) return false;

            BitmapData bd = null;
            try
            {
                bd = src.LockBits(new Rectangle(0, 0, src.Width, src.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                int lineBytes = Options.Width * 4;
                int stride = bd.Stride;
                IntPtr scan0 = bd.Scan0;

                unsafe
                {
                    byte* pBase = (byte*)scan0.ToPointer();
                    if (stride > 0)
                    {
                        for (int y = 0; y < Options.Height; y++)
                        {
                            Marshal.Copy(new IntPtr(pBase + y * stride), _lineBuf, 0, lineBytes);
                            stdin.Write(_lineBuf, 0, lineBytes);
                        }
                    }
                    else
                    {
                        byte* pRow0 = pBase + (Options.Height - 1) * (-stride);
                        for (int y = 0; y < Options.Height; y++)
                        {
                            Marshal.Copy(new IntPtr(pRow0 + y * (-stride)), _lineBuf, 0, lineBytes);
                            stdin.Write(_lineBuf, 0, lineBytes);
                        }
                    }
                }

                _writtenFrames++;
                if ((_writtenFrames % 120) == 0)
                    Log.Debug($"wrote {_writtenFrames} frames, elapsed={_sinceStart.Elapsed.TotalSeconds:F1}s", "REC");

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug("TryWriteFrame EX: " + ex.Message, "REC");
                return false;
            }
            finally
            {
                if (bd != null) src.UnlockBits(bd);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { StopAsync(GracefulExitTimeoutMs).GetAwaiter().GetResult(); } catch { /* ignore */ }
        }
    }
}

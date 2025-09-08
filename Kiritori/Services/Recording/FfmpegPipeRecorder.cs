using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
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

        // CFR ワーカー（あなたの環境に既にあればそのまま使われます）
        private volatile bool _run;
        private Thread _worker;          // 既存実装があればそのまま
        private Bitmap _latest;          // 既存実装があればそのまま
        private readonly object _frameLock = new object();
        private readonly System.Text.StringBuilder _stderrBuf = new System.Text.StringBuilder(8192);
        private const int _stderrMax = 32768;
        private volatile int _exitCode = int.MinValue;
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

        public void Start()
        {
            if (_started) { Log.Debug("Start: already started", "REC"); return; }

            // ---- ffmpeg.exe の自動解決（同梱→PATH）
            var resolved = Options.FfmpegPath;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = FfmpegLocator.Resolve();
                if (string.IsNullOrWhiteSpace(resolved))
                {
                    MessageBox.Show(null,
                        "FFmpeg が見つかりませんでした。Extensionsタブからインストールしてください。",
                        "FFmpeg not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return; // or throw
                }
            }

            Options.FfmpegPath = resolved;

            if (string.IsNullOrWhiteSpace(Options.FfmpegPath) || !File.Exists(Options.FfmpegPath))
            {
                var msg = SR.T("Text.Ffmpeg.Notfound",
                            "FFmpeg was not found. Please check your settings to install or include it.");
                var cap = SR.T("Text.Ffmpeg.NotfoundTitle", "FFmpeg not found");

                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        msg,
                        cap,
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch
                {
                    // UI スレッド以外でも安全にログだけは残す
                    Log.Debug("FFmpeg not found: " + (Options.FfmpegPath ?? "(null)"), "REC");
                }
                return; // 例外を投げずに録画開始を中断
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Options.OutputPath) ?? ".");

            var args = BuildArgs(Options);

            Log.Info($"Resolved ffmpeg path: '{Options.FfmpegPath}'", "REC");
            Log.Debug($"Start ffmpeg: path='{Options.FfmpegPath}' args='{args}'", "REC");
            Log.Debug($"Output='{Options.OutputPath}', {Options.Width}x{Options.Height}@{Options.Fps}fps, Kind={Options.Kind}", "REC");

            var psi = new ProcessStartInfo
            {
                FileName = Options.FfmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.Exited += (s, e) =>
            {
                try
                {
                    _exitCode = _proc.ExitCode;
                    Log.Debug($"ffmpeg Exited: code={_exitCode}", "REC");
                }
                catch { }
            };
            _proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log.Debug("[ffmpeg] " + e.Data, "REC");
                    if (_stderrBuf.Length > _stderrMax) _stderrBuf.Remove(0, _stderrBuf.Length - _stderrMax);
                    _stderrBuf.AppendLine(e.Data);
                }
            };
            if (!_proc.Start())
                throw new Win32Exception("failed to start ffmpeg");

            _proc.BeginErrorReadLine();
            _stdin = _proc.StandardInput.BaseStream;
            _started = true;
            _writtenFrames = 0;
            _sinceStart.Restart();

            _run = true;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "FfmpegCfrWriter" };
            _worker.Start();

            Log.Debug("ffmpeg started and stdin opened", "REC");
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
            if (!_started || _stdin == null)
            {
                Log.Debug("TryWriteFrame: not started/stdin null", "REC");
                return false;
            }
            if (_proc?.HasExited == true)
            {
                Log.Debug("TryWriteFrame: ffmpeg already exited", "REC");
                return false;
            }
            if (src == null)
            {
                Log.Debug("TryWriteFrame: src is null", "REC");
                return false;
            }
            if (src.Width != Options.Width || src.Height != Options.Height)
            {
                Log.Debug($"TryWriteFrame: size mismatch src={src.Width}x{src.Height} != rec={Options.Width}x{Options.Height}", "REC");
                return false;
            }

            BitmapData bd = null;
            try
            {
                // 32bpp ARGB でロック（BGRA として送る）
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
                        // 上→下
                        for (int y = 0; y < Options.Height; y++)
                        {
                            Marshal.Copy(new IntPtr(pBase + y * stride), _lineBuf, 0, lineBytes);
                            _stdin.Write(_lineBuf, 0, lineBytes);
                        }
                    }
                    else
                    {
                        // 下→上（負ストライド対応）
                        byte* pRow0 = pBase + (Options.Height - 1) * (-stride);
                        for (int y = 0; y < Options.Height; y++)
                        {
                            Marshal.Copy(new IntPtr(pRow0 + y * (-stride)), _lineBuf, 0, lineBytes);
                            _stdin.Write(_lineBuf, 0, lineBytes);
                        }
                    }
                }

                _writtenFrames++;
                if ((_writtenFrames % 120) == 0) // だいたい4秒ごと@30fps
                {
                    Log.Debug($"wrote {_writtenFrames} frames, elapsed={_sinceStart.Elapsed.TotalSeconds:F1}s", "REC");
                }
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
            Log.Debug("Dispose: begin", "REC");

            // 1) 録画スレッド停止
            _run = false;
            try { _worker?.Join(2000); } catch { }
            _worker = null;

            // 2) 入力を閉じて ffmpeg に EOF を伝える
            try { _stdin?.Flush(); } catch (Exception ex) { Log.Debug("stdin Flush EX: " + ex.Message, "REC"); }
            try { _stdin?.Close(); } catch (Exception ex) { Log.Debug("stdin Close EX: " + ex.Message, "REC"); }
            _stdin = null;

            // 3) 充分に待つ（moov/palette が書かれる）
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    Log.Debug("waiting ffmpeg exit...", "REC");
                    var exited = _proc.WaitForExit(GracefulExitTimeoutMs);
                    Log.Debug("ffmpeg exited=" + exited, "REC");
                    if (!exited)
                    {
                        Log.Debug("ffmpeg not exited in time -> Kill()", "REC");
                        _proc.Kill();
                        _proc.WaitForExit(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Wait/Kill EX: " + ex.Message, "REC");
            }

            try { _proc?.Dispose(); } catch { }
            _proc = null;
            _started = false;

            Log.Debug($"Dispose: end. frames={_writtenFrames}, duration={_sinceStart.Elapsed.TotalSeconds:F1}s, out='{Options?.OutputPath}'", "REC");
            if (_stderrBuf.Length > 0)
                Log.Debug("ffmpeg stderr(last):\n" + _stderrBuf.ToString(), "REC");
        }
    }
}

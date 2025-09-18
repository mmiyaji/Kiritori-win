using Kiritori.Services.Logging;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kiritori.Views.LiveCapture
{
    internal class GdiCaptureBackend : LiveCaptureBackend, IDisposable
    {
        public event Action<Bitmap> FrameArrived;
        private volatile int _maxFps = 15;
        public int MaxFps { get => _maxFps; set => _maxFps = value; }

        public IntPtr ExcludeWindow { get; set; } // 使わないなら未設定でOK

        // CaptureRect は論理px（スクリーン座標）
        private readonly object _rectLock = new object();
        private Rectangle _captureRect;
        public Rectangle CaptureRect
        {
            get { lock (_rectLock) return _captureRect; }
            set { lock (_rectLock) _captureRect = value; }
        }
        public Rectangle CaptureRectPhysical { get; set; } = Rectangle.Empty;

        private volatile bool _running;
        private Thread _thread;

        // バッファまわり（破棄と生成を同じロックで保護）
        private readonly object _bufLock = new object();
        private Bitmap _buffer;
        private Graphics _g;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "GdiCaptureBackend" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            var t = _thread;
            if (t != null && t.IsAlive)
            {
                try { t.Join(500); } catch { /* ignore */ }
            }
            _thread = null;
        }

        private void CaptureLoop()
        {
            try
            {
                while (_running)
                {
                    Rectangle rLogical;
                    lock (_rectLock) rLogical = _captureRect;

                    if (rLogical.Width <= 0 || rLogical.Height <= 0)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    // var rPhysical = DpiUtil.LogicalToPhysical(rLogical);
                    var rPhysical = ResolvePhysical();

                    // バッファを確実に用意
                    EnsureBuffers(rPhysical.Size);

                    IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
                    IntPtr hdcSrc = IntPtr.Zero;
                    IntPtr hdcDst = IntPtr.Zero;

                    try
                    {
                        hdcSrc = NativeMethods.GetWindowDC(desktopWnd);
                        if (hdcSrc == IntPtr.Zero) { Thread.Sleep(5); continue; }

                        // Graphics/HDC を安全に取得
                        lock (_bufLock)
                        {
                            if (_g == null || _buffer == null || _buffer.Width != rPhysical.Width || _buffer.Height != rPhysical.Height)
                            {
                                // 破棄直後やサイズ変更の競合。次ループで再準備。
                                continue;
                            }

                            hdcDst = _g.GetHdc();
                        }
                        Log.Trace($"[Backend] BitBlt from Phys={rPhysical}  size={rPhysical.Width}x{rPhysical.Height}", "LivePreview");

                        // コピー
                        NativeMethods.BitBlt(hdcDst, 0, 0, rPhysical.Width, rPhysical.Height,
                                            hdcSrc, rPhysical.X, rPhysical.Y, NativeMethods.SRCCOPY);
                    }
                    catch
                    {
                        // 例外時は少し待って次ループ
                        Thread.Sleep(5);
                    }
                    finally
                    {
                        if (hdcDst != IntPtr.Zero)
                        {
                            lock (_bufLock)
                            {
                                // _g が null でない時だけ ReleaseHdc
                                if (_g != null) _g.ReleaseHdc(hdcDst);
                            }
                        }
                        if (hdcSrc != IntPtr.Zero) NativeMethods.ReleaseDC(desktopWnd, hdcSrc);
                    }

                    // フレーム通知（null は送らない）
                    Bitmap toSend = null;
                    lock (_bufLock)
                    {
                        if (_buffer != null)
                            toSend = (Bitmap)_buffer.Clone();
                    }
                    if (toSend != null)
                    {
                        try { FrameArrived?.Invoke(toSend); }
                        finally { toSend.Dispose(); }
                    }

                    if (MaxFps > 0)
                        Thread.Sleep(Math.Max(1, 1000 / MaxFps));
                }
            }
            catch
            {
                // TODO: ログ
            }
        }
        private Rectangle ResolvePhysical()
        {
            if (!CaptureRectPhysical.IsEmpty) return CaptureRectPhysical;
            return DpiUtil.LogicalToPhysical(CaptureRect);
        }

        private void EnsureBuffers(Size size)
        {
            lock (_bufLock)
            {
                if (_buffer != null && _buffer.Size == size && _g != null) return;

                DisposeBuffers_NoLock();
                _buffer = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                _g = Graphics.FromImage(_buffer);
                _g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                _g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                _g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            }
        }

        private void DisposeBuffers_NoLock()
        {
            if (_g != null) { _g.Dispose(); _g = null; }
            if (_buffer != null) { _buffer.Dispose(); _buffer = null; }
        }

        private void DisposeBuffers()
        {
            lock (_bufLock) DisposeBuffers_NoLock();
        }

        public void Dispose()
        {
            Stop();            // ← スレッド停止を先に
            DisposeBuffers();  // ← その後でバッファ破棄
        }

        private static class NativeMethods
        {
            public const int SRCCOPY = 0x00CC0020;

            [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
            [DllImport("user32.dll")] public static extern IntPtr GetWindowDC(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
                                            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
        }

        internal static class DpiUtil
        {
            [DllImport("Shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
            [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
            private const int MDT_EFFECTIVE_DPI = 0;
            private const uint MONITOR_DEFAULTTONEAREST = 2;

            [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

            private static uint GetEffectiveDpiAt(int x, int y)
            {
                var mon = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out dx, out dy) == 0 && dx != 0) return dx;
                return 96;
            }

            public static Rectangle LogicalToPhysical(Rectangle logical)
            {
                var dpi = GetEffectiveDpiAt(logical.Left, logical.Top);
                float s = dpi / 96f;
                return new Rectangle(
                    (int)Math.Round(logical.Left * s),
                    (int)Math.Round(logical.Top * s),
                    (int)Math.Round(logical.Width * s),
                    (int)Math.Round(logical.Height * s)
                );
            }

            public static Rectangle PhysicalToLogical(Rectangle physical)
            {
                var dpi = GetEffectiveDpiAt(physical.Left, physical.Top);
                float s = dpi / 96f;
                return new Rectangle(
                    (int)Math.Round(physical.Left / s),
                    (int)Math.Round(physical.Top / s),
                    (int)Math.Round(physical.Width / s),
                    (int)Math.Round(physical.Height / s)
                );
            }

        }
    }
}

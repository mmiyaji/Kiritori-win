using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kiritori.Views.LiveCapture
{
    internal class GdiCaptureBackend : LiveCaptureBackend, IDisposable
    {
        public event Action<Bitmap> FrameArrived;
        public int MaxFps { get; set; } = 30;

        // ★ 追加：自己キャプチャ除外したいウィンドウのハンドル
        public IntPtr ExcludeWindow { get; set; }

        // ★ CaptureRect は論理px（スクリーン座標）で保持。スレッド安全に読む
        private readonly object _sync = new object();
        private Rectangle _captureRect;
        public Rectangle CaptureRect
        {
            get { lock (_sync) return _captureRect; }
            set { lock (_sync) _captureRect = value; }
        }

        private volatile bool _running;
        private Bitmap _buffer;
        private Graphics _g;

        public void Start()
        {
            if (_running) return;
            _running = true;
            var t = new Thread(CaptureLoop) { IsBackground = true };
            t.Start();
        }

        public void Stop() => _running = false;

        private void CaptureLoop()
        {
            try
            {
                while (_running)
                {
                    Rectangle rLogical;
                    lock (_sync) rLogical = _captureRect;
                    if (rLogical.Width <= 0 || rLogical.Height <= 0)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    // BitBlt は物理pxで行う
                    var rPhysical = DpiUtil.LogicalToPhysical(rLogical);

                    EnsureBuffers(rPhysical.Size);

                    // 画面DC取得
                    IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
                    var hdcSrc = NativeMethods.GetWindowDC(desktopWnd);
                    try
                    {
                        // // ★ ExcludeWindow が重なっていたらスキップ（自己キャプチャ防止）
                        // if (ExcludeWindow != IntPtr.Zero && NativeMethods.GetWindowRect(ExcludeWindow, out var wrc))
                        // {
                        //     var winPhy = Rectangle.FromLTRB(wrc.Left, wrc.Top, wrc.Right, wrc.Bottom);
                        //     if (winPhy.IntersectsWith(rPhysical))
                        //     {
                        //         Thread.Sleep(Math.Max(1, 1000 / Math.Max(1, MaxFps)));
                        //         continue;
                        //     }
                        // }

                        // BitBlt: 画面→バッファ
                        var destHdc = _g.GetHdc();
                        try
                        {
                            NativeMethods.BitBlt(destHdc, 0, 0, rPhysical.Width, rPhysical.Height,
                                                 hdcSrc, rPhysical.X, rPhysical.Y, NativeMethods.SRCCOPY);
                        }
                        finally
                        {
                            _g.ReleaseHdc(destHdc);
                        }
                    }
                    finally
                    {
                        NativeMethods.ReleaseDC(desktopWnd, hdcSrc);
                    }

                    FrameArrived?.Invoke(_buffer);

                    if (MaxFps > 0)
                        Thread.Sleep(Math.Max(1, 1000 / MaxFps));
                }
            }
            catch
            {
                // TODO: ログ
            }
        }

        private void EnsureBuffers(Size size)
        {
            if (_buffer != null && _buffer.Size == size) return;

            DisposeBuffers();
            _buffer = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            _g = Graphics.FromImage(_buffer);
            _g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            _g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            _g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }

        private void DisposeBuffers()
        {
            if (_g != null) { _g.Dispose(); _g = null; }
            if (_buffer != null) { _buffer.Dispose(); _buffer = null; }
        }

        public void Dispose()
        {
            Stop();
            DisposeBuffers();
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

            // ★ 追加：ウィンドウ矩形（物理px）
            [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }
        }

        // ★ 物理/論理変換（左上のモニタ DPI を採用。矩形が複数モニタ跨ぎの場合は分割が理想）
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
                    (int)Math.Round(logical.Top  * s),
                    (int)Math.Round(logical.Width  * s),
                    (int)Math.Round(logical.Height * s)
                );
            }
        }
    }
}

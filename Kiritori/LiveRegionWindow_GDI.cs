using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori
{
    public sealed class LiveRegionWindow_GDI : Form
    {
        // Win32
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
                                  IntPtr hdcSrc, int xSrc, int ySrc, int rop);
        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                      IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, int rop);

        const int SRCCOPY = 0x00CC0020;

        // DPI
        [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
        [DllImport("Shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        const int MDT_EFFECTIVE_DPI = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        // 状態
        private Rectangle _logicalSrc;
        private Rectangle _physSrc;
        private IntPtr _dcScreen = IntPtr.Zero;  // 画面DC
        private IntPtr _dcMem = IntPtr.Zero;     // バックバッファDC
        private IntPtr _bmp = IntPtr.Zero;       // バックバッファBMP
        private IntPtr _bmpPrev = IntPtr.Zero;
        private readonly object _lock = new object();

        // タイマー（UIに依存しない）
        private System.Threading.Timer _timer;
        public int Fps { get; private set; } = 30; // お好みで 20–60

        public LiveRegionWindow_GDI()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            // ちらつき対策（念のため）
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void StartLive(Rectangle logicalSource, Rectangle? viewerBounds = null, int fps = 30)
        {
            Fps = Math.Max(5, Math.Min(120, fps));
            _logicalSrc = logicalSource;
            _physSrc = ClampToVirtual(LogicalToPhysical(logicalSource));

            var vb = viewerBounds ?? new Rectangle(
                Math.Max(SystemInformation.VirtualScreen.Left, _physSrc.Left),
                Math.Max(SystemInformation.VirtualScreen.Top,  _physSrc.Top),
                _physSrc.Width, _physSrc.Height);

            Bounds = vb;

            AllocateResources(_physSrc.Size);

            // Threading.Timer（UIメッセージに依存しない）
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => Tick(), null, 0, 1000 / Fps);

            Show();
        }

        public void StopLive()
        {
            _timer?.Dispose();
            _timer = null;
            ReleaseResources();
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _timer?.Dispose();
            _timer = null;
            ReleaseResources();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // 表示側サイズだけ変わる。バックバッファはソースサイズ固定でOK。
        }

        // ==== コア: 毎フレーム ====
        private void Tick()
        {
            lock (_lock)
            {
                if (_dcScreen == IntPtr.Zero || _dcMem == IntPtr.Zero || _bmp == IntPtr.Zero) return;

                using (var g = Graphics.FromHwnd(this.Handle))
                {
                    var hdcDest = g.GetHdc();
                    var c = this.ClientRectangle;

                    // srcW / srcH の代わりに _physSrc.Width / _physSrc.Height
                    StretchBlt(
                        hdcDest,
                        c.Left, c.Top, c.Width, c.Height,
                        _dcMem,
                        0, 0,
                        _physSrc.Width, _physSrc.Height,
                        SRCCOPY);

                    g.ReleaseHdc(hdcDest);
                }
            }
        }

        private void AllocateResources(Size physSize)
        {
            ReleaseResources();
            _dcScreen = GetDC(IntPtr.Zero);
            _dcMem = CreateCompatibleDC(_dcScreen);
            _bmp = CreateCompatibleBitmap(_dcScreen, physSize.Width, physSize.Height);
            _bmpPrev = SelectObject(_dcMem, _bmp);
        }

        private void ReleaseResources()
        {
            lock (_lock)
            {
                if (_dcMem != IntPtr.Zero)
                {
                    if (_bmpPrev != IntPtr.Zero) SelectObject(_dcMem, _bmpPrev);
                    if (_bmp != IntPtr.Zero) { DeleteObject(_bmp); _bmp = IntPtr.Zero; }
                    DeleteDC(_dcMem); _dcMem = IntPtr.Zero;
                    _bmpPrev = IntPtr.Zero;
                }
                if (_dcScreen != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, _dcScreen);
                    _dcScreen = IntPtr.Zero;
                }
            }
        }

        // ==== 座標ユーティリティ ====
        private Rectangle LogicalToPhysical(Rectangle logical)
        {
            var r = new RECT { Left = logical.Left, Top = logical.Top, Right = logical.Right, Bottom = logical.Bottom };
            IntPtr hmon = MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST);
            if (hmon != IntPtr.Zero && GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0 && dx != 0 && dy != 0)
            {
                double sx = dx / 96.0, sy = dy / 96.0;
                return Rectangle.FromLTRB(
                    (int)Math.Round(logical.Left   * sx),
                    (int)Math.Round(logical.Top    * sy),
                    (int)Math.Round(logical.Right  * sx),
                    (int)Math.Round(logical.Bottom * sy));
            }
            return logical;
        }

        private Rectangle ClampToVirtual(Rectangle phys)
        {
            var v = SystemInformation.VirtualScreen;
            int L = Math.Max(v.Left, phys.Left);
            int T = Math.Max(v.Top,  phys.Top);
            int R = Math.Min(v.Right,  phys.Right);
            int B = Math.Min(v.Bottom, phys.Bottom);
            if (R <= L) R = L + 1;
            if (B <= T) B = T + 1;
            return Rectangle.FromLTRB(L, T, R - L, B - T);
        }
    }
}

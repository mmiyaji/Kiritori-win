using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    public partial class LivePreviewWindow : Form
    {
        private LiveCaptureBackend _backend;
        private Bitmap _latest;

        public Rectangle CaptureRect { get; set; }   // 論理px（スクリーン座標）
        public bool AutoTopMost { get; set; } = true;

        public LivePreviewWindow()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            // 見た目上の白チラ防止（初回のみ透明で開く）
            this.Opacity = 0.0;
            this.BackColor = Color.Black; // 透明が効かない環境でも黒背景に
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryExcludeFromCapture(this.Handle); // 自己キャプチャ除外（対応OSのみ有効）
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 表示前に位置合わせ（論理pxでOK）
            WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: AutoTopMost);

            // ★ 初回だけ同期で1フレーム取って白画面を回避
            TrySyncFirstCaptureIntoLatest(CaptureRect);
            Invalidate(); // 直ちに描画

            // バックエンド開始（ここで初めて生成＆設定→Start）
            var gdi = new GdiCaptureBackend
            {
                MaxFps = 24,
                CaptureRect = this.CaptureRect
            };
            gdi.ExcludeWindow = this.Handle; // 自己キャプチャ避け
            gdi.FrameArrived += OnFrameArrived;
            _backend = gdi;
            _backend.Start();
        }

        private bool _firstFrameShown = false;

        private void OnFrameArrived(Bitmap bmp)
        {
            // 簡易：Clone で差し替え（最適化は後で）
            var old = _latest;
            _latest = (Bitmap)bmp.Clone();
            if (old != null) old.Dispose();

            if (IsHandleCreated) BeginInvoke((Action)(() =>
            {
                Invalidate();
                if (!_firstFrameShown)
                {
                    _firstFrameShown = true;
                    // 初フレームが来たら不透明化（保険）
                    if (this.Opacity < 1.0) this.Opacity = 1.0;
                }
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_latest == null) return;

            var dst = this.ClientRectangle;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_latest, dst);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _backend?.Dispose(); } catch { }
            _backend = null;

            if (_latest != null) { _latest.Dispose(); _latest = null; }
            base.OnFormClosed(e);
        }

        // ---- ここが“初回同期キャプチャ”の要 ----
        private void TrySyncFirstCaptureIntoLatest(Rectangle rLogical)
        {
            if (rLogical.Width <= 0 || rLogical.Height <= 0) return;

            // BitBlt は物理px前提：論理→物理に変換
            var rPhysical = DpiUtil.LogicalToPhysical(rLogical);

            // バッファ確保
            var bmp = new Bitmap(rPhysical.Width, rPhysical.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
                IntPtr hdcSrc = NativeMethods.GetWindowDC(desktopWnd);
                IntPtr hdcDst = g.GetHdc();
                try
                {
                    NativeMethods.BitBlt(hdcDst, 0, 0, rPhysical.Width, rPhysical.Height,
                                        hdcSrc, rPhysical.X, rPhysical.Y, NativeMethods.SRCCOPY);
                }
                finally
                {
                    g.ReleaseHdc(hdcDst);
                    NativeMethods.ReleaseDC(desktopWnd, hdcSrc);
                }
            }

            // 取得できたら差し替え＆可視化
            var old = _latest;
            _latest = bmp;
            if (old != null) old.Dispose();

            // ここで見せてOK
            if (this.Opacity < 1.0) this.Opacity = 1.0;
            _firstFrameShown = true; // 既に初回を表示済み
        }

        // ---- 自己キャプチャ除外 ----
        private static void TryExcludeFromCapture(IntPtr hwnd)
        {
            const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+
            try { SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE); } catch { /* 古いOSは無視 */ }
        }

        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

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

        // 論理⇔物理（左上モニタDPI採用）
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

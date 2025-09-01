using Kiritori.Helpers;
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
        private ContextMenuStrip _ctx;
        private ToolStripMenuItem
            _miOriginal, _miZoomIn, _miZoomOut, _miZoomPct,
            _miOpacity, _miPauseResume, _miRealign, _miTopMost, _miClose,
            _miPref, _miExit;

        private float _zoom = 1.0f;     // 表示倍率（1.0=100%）
        private bool _paused = false;
        public object MainApp { get; set; }
        private TitleIconBadger _iconBadge;

        public LivePreviewWindow()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            BuildContextMenu();
            this.ContextMenuStrip = _ctx;

            this.Opacity = 0.0; // 初回白チラ防止
            this.BackColor = Color.Black;
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

            // 初回だけ同期で1フレーム取って白画面を回避
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

            _iconBadge = new TitleIconBadger(this);
            _iconBadge.SetState(LiveBadgeState.Recording); // ライブ開始 → 録画中バッジ
        }

        private bool _firstFrameShown = false;

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

            try { _iconBadge?.Dispose(); } catch { }
            _iconBadge = null;

            try { _ctx?.Dispose(); } catch { }   // ★ これを追加（ContextMenuStrip を明示破棄）

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
        private void RealignToKiritori()
        {
            // _zoom = 1.0f;
            WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: _miTopMost?.Checked ?? true);
            Invalidate();
        }
        private void BuildContextMenu()
        {
            _ctx = new ContextMenuStrip();

            // --- View 節に近い並び ---
            _miOriginal = new ToolStripMenuItem(SR.T("Menu.OriginalSize", "Original Size")) { /* Ctrl+0 は ProcessCmdKeyで処理 */ };
            _miOriginal.Click += (s, e) => SetZoom(1.0f);

            _miZoomIn = new ToolStripMenuItem(SR.T("Menu.ZoomIn", "Zoom In(+10%)")); _miZoomIn.Click += (s, e) => SetZoom(_zoom + 0.10f);
            _miZoomOut = new ToolStripMenuItem(SR.T("Menu.ZoomOut", "Zoom Out(-10%)")); _miZoomOut.Click += (s, e) => SetZoom(_zoom - 0.10f);

            // Zoom(%) サブメニュー（スクショ準拠：10 / 50 / 100 / 150 / 200 / 500）
            _miZoomPct = new ToolStripMenuItem(SR.T("Menu.Zoom", "Zoom(%)"));
            foreach (var pct in new[] { 10, 50, 100, 150, 200, 500 })
            {
                var mi = new ToolStripMenuItem($"Size {pct}%") { Tag = pct };
                mi.Click += (s, e) =>
                {
                    var p = (int)((ToolStripMenuItem)s).Tag;
                    SetZoom(p / 100f);
                };
                _miZoomPct.DropDownItems.Add(mi);
            }

            // Opacity サブメニュー（よく使う段）
            _miOpacity = new ToolStripMenuItem(SR.T("Menu.Opacity", "Opacity"));
            foreach (var pct in new[] { 100, 90, 80, 70, 60, 50, 30 })
            {
                var mi = new ToolStripMenuItem($"Opacity {pct}%") { Tag = pct };
                mi.Click += (s, e) =>
                {
                    var p = (int)((ToolStripMenuItem)s).Tag;
                    this.Opacity = Math.Max(0.05, Math.Min(1.0, p / 100.0));
                };
                _miOpacity.DropDownItems.Add(mi);
            }

            // 実用操作
            _miPauseResume = new ToolStripMenuItem(SR.T("Menu.Pause", "Pause")); _miPauseResume.Click += (s, e) => TogglePause();
            _miRealign = new ToolStripMenuItem(SR.T("Menu.OriginalLocation", "Move to the initial position")); _miRealign.Click += (s, e) => RealignToKiritori();

            // 固定・終了
            _miTopMost = new ToolStripMenuItem(SR.T("Menu.TopMost", "Keep on top")) { Checked = true, CheckOnClick = true };
            _miTopMost.CheckedChanged += (s, e) => this.TopMost = _miTopMost.Checked;

            _miClose = new ToolStripMenuItem(SR.T("Menu.CloseWindow", "Close Window")); _miClose.Click += (s, e) => this.Close();
            _miPref = new ToolStripMenuItem(SR.T("Menu.Preferences", "Preferences")); _miPref.Click += (s, e) => ShowPreferences();
            _miExit = new ToolStripMenuItem(SR.T("Menu.Exit", "Exit Kiritori")); _miExit.Click += (s, e) => Application.Exit();

            _ctx.Items.AddRange(new ToolStripItem[] {
                _miPauseResume,
                _miClose,
                new ToolStripSeparator(),
                _miOriginal,
                _miZoomIn,
                _miZoomOut,
                _miZoomPct,
                _miOpacity,
                new ToolStripSeparator(),
                _miRealign,
                _miTopMost,
                new ToolStripSeparator(),
                _miPref,
                _miExit
            });

            this.ContextMenuStrip = _ctx;

            // メニューウィンドウもキャプチャ除外
            _ctx.Opened += (s, e) =>
            {
                if (_ctx != null && _ctx.Handle != IntPtr.Zero)
                    TryExcludeFromCapture(_ctx.Handle);
            };

            // ドロップダウン（サブメニュー）にも適用するヘルパ
            void MarkDropDownExclusion(ToolStripDropDownItem item)
            {
                if (item == null) return;
                item.DropDownOpened += (ss, ee) =>
                {
                    var dd = item.DropDown;
                    if (dd != null && dd.Handle != IntPtr.Zero)
                        TryExcludeFromCapture(dd.Handle);
                };
            }

            // サブメニューを持つ項目にフック
            MarkDropDownExclusion(_miZoomPct);
            MarkDropDownExclusion(_miOpacity);
        }
        private void ShowPreferences()
        {
            try
            {
                PrefForm.ShowSingleton((IWin32Window)this.MainApp);
            }
            catch
            { 
                // MainApplicationオーナーで開けない場合は自分をオーナーに
                PrefForm.ShowSingleton(this);
            }
        }
        private void SetZoom(float z)
        {
            Debug.WriteLine($"[LivePreview] SetZoom: {z}");
            z = Math.Max(0.1f, Math.Min(8.0f, z));
            if (Math.Abs(_zoom - z) < 0.0001f) return;
            _zoom = z;

            var newClient = new Size(
                (int)Math.Round(CaptureRect.Width * _zoom),
                (int)Math.Round(CaptureRect.Height * _zoom)
            );

            WindowAligner.ResizeFormClientKeepTopLeft(
                this,
                newClient,
                topMost: _miTopMost?.Checked ?? this.TopMost
            );

            Invalidate();
            Debug.WriteLine($"[LivePreview] ResizeFormClientAt: {newClient}, {this.Size}");
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _miPauseResume.Text = _paused ? SR.T("Menu.Resume", "Resume") : SR.T("Menu.Pause", "Pause");
            _iconBadge?.SetState(_paused ? LiveBadgeState.Paused : LiveBadgeState.Recording);
            // バックエンドは動かしたまま、描画だけ止める（カク付き最小）
            if (!_paused) Invalidate();
        }

        // FrameArrived 側で停止中は無視
        private void OnFrameArrived(Bitmap bmp)
        {
            if (_paused) return;

            var old = _latest;
            _latest = (Bitmap)bmp.Clone();
            if (old != null) old.Dispose();

            if (IsHandleCreated) BeginInvoke((Action)(() =>
            {
                Invalidate();
                if (!_firstFrameShown) { _firstFrameShown = true; if (this.Opacity < 1.0) this.Opacity = 1.0; }
            }));
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

    }
}

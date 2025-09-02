using Kiritori.Helpers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            _miPref, _miExit, _miTitlebar;

        private float _zoom = 1.0f;     // 表示倍率（1.0=100%）
        private bool _paused = false;
        public object MainApp { get; set; }
        private TitleIconBadger _iconBadge;

        // ---- Win32 定義
        const int GWL_STYLE = -16;
        const int WS_CAPTION = 0x00C00000;
        const int WS_THICKFRAME = 0x00040000;
        const int WS_BORDER = 0x00800000;

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_FRAMECHANGED = 0x0020;

        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_LBUTTONDOWN   = 0x0201;
        const int WM_LBUTTONUP     = 0x0202;
        const int WM_NCCALCSIZE    = 0x0083;
        const int WM_NCHITTEST     = 0x0084;

        const int HTCLIENT=1, HTCAPTION=2;
        const int HTLEFT=10, HTRIGHT=11, HTTOP=12, HTTOPLEFT=13, HTTOPRIGHT=14, HTBOTTOM=15, HTBOTTOMLEFT=16, HTBOTTOMRIGHT=17;

        // 影オプション（メニューから切替可能にする想定）
        private bool _shadowWhenTabless = true;

        // ---- HUD（Pause/Resume）描画用（子コントロールは使わない）
        private Timer _fadeTimer;                 // ← これだけにする
        private int _hudAlpha = 0, _hudTargetAlpha = 0; // 0..200
        private Rectangle _hudRect;                     // HUD ボタン矩形
        private bool _hudHot = false, _hudDown = false; // ホバー/押下状態
        // HUD フィードバック用
        private bool _hudCursorIsHand = false;
        private const int HUD_INTERACTABLE_ALPHA = 80; // この濃さ以上でクリック可能とみなす
        private bool IsHudInteractable() => _hudAlpha >= HUD_INTERACTABLE_ALPHA && !_hudRect.IsEmpty;


        private System.Diagnostics.Stopwatch _fpsWatch = new System.Diagnostics.Stopwatch();
        private int _maxFps = 15; // 0 = 無制限
        private ToolStripMenuItem _miFpsRoot;
        private ToolStripMenuItem[] _miFpsItems;
        private readonly int[] _fpsChoices = new[] { 5, 10, 15, 30, 60, 0 }; // 0=無制限


        // タブを隠す直前に測った左右・下の枠厚
        private int _savedNcLeft = 0, _savedNcRight = 0, _savedNcBottom = 0;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
        const uint RDW_INVALIDATE=0x0001, RDW_UPDATENOW=0x0100, RDW_FRAME=0x0400, RDW_ALLCHILDREN=0x0080;

        // 影トグル用
        private bool _shadowTabless = true;          // 既定ON
        private ToolStripMenuItem _miShadow;          // メニュー項目
        private int _origStyle = 0;
        private bool _captionHidden = false;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        public LivePreviewWindow()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            try
            {
                var v = Properties.Settings.Default.LivePreviewMaxFps;
                if (v >= 0) _maxFps = v; else _maxFps = 15;
            }
            catch { _maxFps = 15; }

            BuildOverlay();        // HUD 初期化
            WireHoverHandlers();   // ホバー/フェード

            BuildContextMenu();
            this.ContextMenuStrip = _ctx;

            this.Opacity = 0.0; // 初回白チラ防止
            this.BackColor = Color.Black;

            _fpsWatch.Start();
        }

        // 背景消去しない（_latest を全面に描く & フリッカ抑制）
        protected override void OnPaintBackground(PaintEventArgs e) { /* no-op */ }

        // ===== HUD 初期化（タイマーなど） =====
        private void BuildOverlay()
        {
            CenterOverlay(); // _hudRect を決定

            _fadeTimer = new Timer { Interval = 16 }; // ~60fps
            _fadeTimer.Tick += (s, e) =>
            {
                int cur = _hudAlpha;
                int step = 20; // フェード速度
                if (cur < _hudTargetAlpha) cur = Math.Min(_hudTargetAlpha, cur + step);
                else if (cur > _hudTargetAlpha) cur = Math.Max(_hudTargetAlpha, cur - step);
                if (cur != _hudAlpha)
                {
                    _hudAlpha = cur;
                    // Invalidate(_hudRect); // HUD 部分だけ再描画
                    Invalidate(GetHudInvalidateRect());
                }
                if (cur == _hudTargetAlpha) _fadeTimer.Stop();
            };
        }

        private int DpiScale(int px) => (int)Math.Round(px * this.DeviceDpi / 96.0);
        private void CenterOverlay()
        {
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // 円の外側に少し余白（はみ出し防止）
            int safePad = DpiScale(8);

            // そもそも余白を取るスペースがない場合は HUD 無効
            if (clientW <= safePad * 2 || clientH <= safePad * 2)
            {
                _hudRect = Rectangle.Empty;
                return;
            }

            int desiredD = DpiScale(120);                 // 通常サイズ（YouTube風）
            int maxD     = Math.Min(clientW, clientH) - safePad * 2; // 収まる最大直径
            int minD     = DpiScale(56);                  // これ未満なら表示しない

            int d = Math.Min(desiredD, maxD);
            if (d < minD)
            {
                _hudRect = Rectangle.Empty;               // 小さすぎるので非表示
                return;
            }

            _hudRect = new Rectangle(
                (clientW - d) / 2,
                (clientH - d) / 2,
                d, d
            );
        }

        private void WireHoverHandlers()
        {
            this.MouseEnter += (s, e) => ShowOverlay();
            this.MouseMove  += (s, e) =>
            {
                ShowOverlay();

                var me = (MouseEventArgs)e;
                bool inside = !_hudRect.IsEmpty && _hudRect.Contains(me.Location);
                bool canClick = inside && IsHudInteractable();

                // ホット状態を更新（描画は HUD 領域＋縁を再描画）
                if (inside != _hudHot)
                {
                    _hudHot = inside;
                    Invalidate(GetHudInvalidateRect());
                }

                // カーソル更新
                if (canClick && !_hudCursorIsHand)
                {
                    Cursor = Cursors.Hand;
                    _hudCursorIsHand = true;
                }
                else if (!canClick && _hudCursorIsHand)
                {
                    Cursor = Cursors.Default;
                    _hudCursorIsHand = false;
                }
            };

            this.MouseLeave += (s, e) =>
            {
                HideOverlay();
                if (_hudCursorIsHand)
                {
                    Cursor = Cursors.Default;
                    _hudCursorIsHand = false;
                }
            };
        }

        private void ShowOverlay()
        {
            if (_hudRect.IsEmpty)
            {
                // 収まらない場合は常に非表示
                _hudTargetAlpha = 0;
                if (!_fadeTimer.Enabled && _hudAlpha > 0) _fadeTimer.Start();
                return;
            }

            _hudTargetAlpha = 140;             // お好みで 100〜160
            if (!_fadeTimer.Enabled) _fadeTimer.Start();
        }

        private void HideOverlay()
        {
            _hudHot = _hudDown = false;
            _hudTargetAlpha = 0;
            if (!_fadeTimer.Enabled) _fadeTimer.Start();

            if (_hudCursorIsHand)
            {
                Cursor = Cursors.Default;
                _hudCursorIsHand = false;
            }
        }



        private void FadeOutOverlay()
        {
            _hudTargetAlpha = 0;
            if (!_fadeTimer.Enabled) _fadeTimer.Start();
        }

        private Size GetDesiredClient() =>
            new Size(
                (int)Math.Round(CaptureRect.Width * _zoom),
                (int)Math.Round(CaptureRect.Height * _zoom)
            );

        private void ApplyTablessShadow(bool enable)
        {
            try
            {
                var m = enable
                    ? new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 }
                    : new MARGINS(); // 0 で解除
                DwmExtendFrameIntoClientArea(this.Handle, ref m);
            }
            catch { /* 非対応OSは無視 */ }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryExcludeFromCapture(this.Handle); // 自己キャプチャ除外（対応OSのみ有効）
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: AutoTopMost);
            TrySyncFirstCaptureIntoLatest(CaptureRect);
            Invalidate();

            var gdi = new GdiCaptureBackend
            {
                MaxFps = 0, // ← 無制限にしてUI側の _maxFps で制御する
                CaptureRect = this.CaptureRect
            };
            gdi.ExcludeWindow = this.Handle;
            gdi.FrameArrived += OnFrameArrived;
            _backend = gdi;
            _backend.Start();

            _iconBadge = new TitleIconBadger(this);
            _iconBadge.SetState(LiveBadgeState.Recording);
        }


        private bool _firstFrameShown = false;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            var oldInv = GetHudInvalidateRect();  // 変更前
            CenterOverlay();                      // _hudRect 更新
            var newInv = GetHudInvalidateRect();  // 変更後
            if (!oldInv.IsEmpty) Invalidate(oldInv);
            if (!newInv.IsEmpty) Invalidate(newInv);

            if (_hudAlpha > 0 && _hudRect.IsEmpty) HideOverlay(); // 入らなくなったら隠す
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_latest == null) return;

            var dst = this.ClientRectangle;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_latest, dst);

            DrawHud(e.Graphics);
        }

        private void DrawHud(Graphics g)
        {
            if (_hudAlpha <= 0 || _hudRect.IsEmpty) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingMode = CompositingMode.SourceOver;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            int d = _hudRect.Width;                 // 円の直径
            float a = Math.Min(_hudAlpha, 200);     // 背景の最大濃度

            // --- 背景：黒い円
            using (var bg = new SolidBrush(Color.FromArgb((int)a, 0, 0, 0)))
                g.FillEllipse(bg, _hudRect);

            // ホバー時に薄い内側グロー
            if (_hudHot)
            {
                int shrink = (int)Math.Round(d * 0.06);
                using (var glow = new SolidBrush(Color.FromArgb((int)(a * 0.10f), 255, 255, 255)))
                    g.FillEllipse(glow, Rectangle.Inflate(_hudRect, -shrink, -shrink));
            }

            // 外周白枠（ホバー時は少し明るく・押下時はさらに明るく）
            float penW = Math.Max(1f, d * 0.012f);
            float ringBoost = _hudDown ? 0.70f : (_hudHot ? 0.50f : 0.35f);
            using (var pen = new Pen(Color.FromArgb((int)(a * ringBoost), 255, 255, 255), penW))
            {
                var rStroke = new RectangleF(_hudRect.X, _hudRect.Y, _hudRect.Width, _hudRect.Height);
                rStroke.Inflate(-penW / 2f, -penW / 2f); // 外側にはみ出さないよう内側へ
                g.DrawEllipse(pen, rStroke);
            }

            // --- アイコン領域（ホバー時はわずかに拡大）
            double scale = _hudDown ? 0.98 : (_hudHot ? 1.06 : 1.00); // 押下で微縮小、ホバーで微拡大
            int marginBase = (int)Math.Round(d * 0.22);
            int margin = (int)Math.Round(marginBase / scale); // 拡大に合わせて余白を減らす

            var iconRect = new Rectangle(
                _hudRect.X + margin, _hudRect.Y + margin,
                _hudRect.Width - margin * 2, _hudRect.Height - margin * 2);

            // 前景色の明るさ（ホバー/押下で強調）
            float fgBoost = _hudDown ? 1.00f : (_hudHot ? 0.96f : 0.85f);
            int fgA = (int)(255 * fgBoost);

            if (_paused)
            {
                // ▶（重心が中央に来るよう非対称余白）
                float W = iconRect.Width;
                float H = iconRect.Height;

                float leftPadRatio  = 0.30f;
                float rightPadRatio = Math.Max(0f, 2f * leftPadRatio - 0.5f);

                float L = iconRect.Left  + leftPadRatio  * W;
                float R = iconRect.Right - rightPadRatio * W;

                float vPad = H * 0.08f;
                float top = iconRect.Top + vPad;
                float bottom = iconRect.Bottom - vPad;
                float cy = iconRect.Top + H / 2f;

                using (var path = new GraphicsPath())
                using (var br = new SolidBrush(Color.FromArgb(fgA, 255, 255, 255)))
                {
                    path.AddPolygon(new[]
                    {
                        new PointF(L, top),
                        new PointF(L, bottom),
                        new PointF(R, cy)
                    });
                    g.FillPath(br, path);
                }
            }
            else
            {
                // ‖（丸角バー）
                int barW = (int)Math.Round(d * 0.14);
                int gap  = (int)Math.Round(d * 0.12);
                int barH = (int)Math.Round(d * 0.52);
                int y    = _hudRect.Y + (_hudRect.Height - barH) / 2;
                int xL   = _hudRect.X + (_hudRect.Width - (barW * 2 + gap)) / 2;
                int xR   = xL + barW + gap;
                int r    = Math.Max(2, barW / 2);

                using (var br = new SolidBrush(Color.FromArgb(fgA, 255, 255, 255)))
                using (var left  = RoundedRect(new Rectangle(xL, y, barW, barH), r))
                using (var right = RoundedRect(new Rectangle(xR, y, barW, barH), r))
                {
                    g.FillPath(br, left);
                    g.FillPath(br, right);
                }
            }
        }

        public int MaxFps
        {
            get { return _maxFps; }
            set
            {
                if (value < 0) value = 0;
                _maxFps = value;
                UpdateFpsMenuChecks();

                // 変更直後の体感を良くするためにリスタート
                _fpsWatch.Restart();

                try
                {
                    Properties.Settings.Default.LivePreviewMaxFps = _maxFps;
                    Properties.Settings.Default.Save();
                }
                catch { /* 設定が無い場合は無視 */ }
            }
        }

        private void UpdateFpsMenuChecks()
        {
            if (_miFpsItems == null) return;
            for (int i = 0; i < _miFpsItems.Length; i++)
            {
                var mi = _miFpsItems[i];
                int fps = (int)mi.Tag;
                mi.Checked = (fps == _maxFps);
            }
        }

        private void OnFpsMenuClick(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            if (mi == null) return;
            int fps = (int)mi.Tag;
            MaxFps = fps;
        }


        // 角丸矩形（int版）
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _backend?.Dispose(); } catch { }
            _backend = null;

            if (_latest != null) { _latest.Dispose(); _latest = null; }

            try { _iconBadge?.Dispose(); } catch { }
            _iconBadge = null;

            try { _ctx?.Dispose(); } catch { }   // ContextMenuStrip を明示破棄

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

            // Zoom(%) サブメニュー
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

            // Opacity サブメニュー
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

            // === ここから FPS サブメニュー（EnsureContextMenuWithFps を統合） ===
            _miFpsRoot = new ToolStripMenuItem(SR.T("Menu.MaxFPS", "最大 FPS")) { Tag = "fps-root" };
            _miFpsItems = new ToolStripMenuItem[_fpsChoices.Length];
            for (int i = 0; i < _fpsChoices.Length; i++)
            {
                int fps = _fpsChoices[i];
                string text = (fps == 0) ? SR.T("Menu.FPS.Unlimited", "Unlimited") : (fps.ToString() + " fps");

                var mi = new ToolStripMenuItem(text) { Tag = fps };
                mi.Click += OnFpsMenuClick;

                _miFpsItems[i] = mi;
                _miFpsRoot.DropDownItems.Add(mi);
            }
            // === FPS サブメニューここまで ===

            // 実用操作
            _miPauseResume = new ToolStripMenuItem(SR.T("Menu.Pause", "Pause")); _miPauseResume.Click += (s, e) => TogglePause();
            _miTitlebar = new ToolStripMenuItem(SR.T("Menu.Titlebar", "Show Title bar")); _miTitlebar.Click += (s, e) => ToggleTitlebar();
            _miTitlebar.Checked = true;
            _miRealign = new ToolStripMenuItem(SR.T("Menu.OriginalLocation", "Move to the initial position")); _miRealign.Click += (s, e) => RealignToKiritori();

            // 固定・終了
            _miTopMost = new ToolStripMenuItem(SR.T("Menu.TopMost", "Keep on top")) { Checked = true, CheckOnClick = true };
            _miTopMost.CheckedChanged += (s, e) => this.TopMost = _miTopMost.Checked;

            _miClose = new ToolStripMenuItem(SR.T("Menu.CloseWindow", "Close Window")); _miClose.Click += (s, e) => this.Close();
            _miPref = new ToolStripMenuItem(SR.T("Menu.Preferences", "Preferences")); _miPref.Click += (s, e) => ShowPreferences();
            _miExit = new ToolStripMenuItem(SR.T("Menu.Exit", "Exit Kiritori")); _miExit.Click += (s, e) => Application.Exit();

            _miShadow = new ToolStripMenuItem(SR.T("Menu.DropShadow", "Drop shadow (tabless)"))
            {
                Checked = _shadowTabless,
                CheckOnClick = true
            };
            _miShadow.CheckedChanged += (s, e) =>
            {
                _shadowTabless = _miShadow.Checked;

                // いまタブなし表示中なら即反映
                if (_captionHidden && IsHandleCreated)
                {
                    ApplyTablessShadow(_shadowTabless);
                    // 念のため再計算＆再描画（環境差対策）
                    ForceRefreshNonClient();
                }
            };

            // 並び：Pause/Titlebar/Close | View系(Zoom/Opacity/FPS/Shadow) | Realign/TopMost | Pref/Exit
            _ctx.Items.AddRange(new ToolStripItem[] {
                _miPauseResume,
                _miTitlebar,
                _miFpsRoot,
                new ToolStripSeparator(),
                _miOriginal,
                _miZoomIn,
                _miZoomOut,
                _miZoomPct,
                _miOpacity,
                _miShadow,
                new ToolStripSeparator(),
                _miRealign,
                _miTopMost,
                new ToolStripSeparator(),
                _miPref,
                _miClose,
                _miExit
            });

            this.ContextMenuStrip = _ctx;
            UpdateShadowMenuState();
            UpdateFpsMenuChecks(); // ← 初期チェック反映

            // メニューウィンドウもキャプチャ除外
            _ctx.Opened += (s, e) =>
            {
                if (_ctx != null && _ctx.Handle != IntPtr.Zero)
                {
                    TryExcludeFromCapture(_ctx.Handle);
                }
                UpdateShadowMenuState();
                UpdateFpsMenuChecks(); // 開くたび最新のチェック状態に
            };
            _ctx.Closed += (s, e) =>
            {
                if (_pendingNcRefresh)
                {
                    _pendingNcRefresh = false;
                    var desiredClient = GetDesiredClient();
                    BeginInvoke((Action)(() =>
                    {
                        ForceRefreshNonClient();
                        ResizeToKeepClient(desiredClient);
                    }));
                }
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
            MarkDropDownExclusion(_miFpsRoot);
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

            // 再開時は即描画、ラベルは HUD 内で更新
            if (!_paused) Invalidate();
            Invalidate(GetHudInvalidateRect());
        }

        private void ToggleTitlebar()
        {
            var desiredClient = GetDesiredClient();

            // 切替「前」の枠厚（左・上）を実測
            var oldInsets = GetNcInsets();

            if (_miTitlebar.Checked) HideCaptionBarTemporarily();
            else                   RestoreCaptionBar();
            _miTitlebar.Checked = !_miTitlebar.Checked;

            Action apply = () =>
            {
                // フレーム再計算 → クライアント寸法を維持（位置はまだ触らない）
                ForceRefreshNonClient();
                ResizeToKeepClient(desiredClient);

                // 切替「後」の枠厚（左・上）を実測
                var newInsets = GetNcInsets();

                // 差分だけ位置補正（左/上）
                int dx = oldInsets.Left - newInsets.Left;
                int dy = oldInsets.Top - newInsets.Top;

                if (dx != 0 || dy != 0)
                {
                    SetWindowPos(this.Handle, IntPtr.Zero, this.Left + dx, this.Top + dy,
                        0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
                UpdateShadowMenuState();
            };

            if (_ctx != null && _ctx.Visible)
            {
                _pendingNcRefresh = true;
                _ctx.Closed += (s, e) =>
                {
                    if (_pendingNcRefresh)
                    {
                        _pendingNcRefresh = false;
                        BeginInvoke(apply);
                    }
                };
            }
            else
            {
                BeginInvoke(apply);
            }
        }

        // FrameArrived 側で停止中は無視＋最大FPSでスロットリング
        private void OnFrameArrived(Bitmap bmp)
        {
            if (_paused) return;

            // 最大FPS制御をここで行う（RefreshPreview は未使用なので）
            if (_maxFps > 0)
            {
                double minIntervalMs = 1000.0 / _maxFps;
                if (_fpsWatch.ElapsedMilliseconds < minIntervalMs)
                    return;
                _fpsWatch.Restart();
            }

            var old = _latest;
            _latest = (Bitmap)bmp.Clone();
            if (old != null) old.Dispose();

            if (IsHandleCreated) BeginInvoke((Action)(() =>
            {
                Invalidate();
                if (!_firstFrameShown)
                {
                    _firstFrameShown = true;
                    if (this.Opacity < 1.0) this.Opacity = 1.0;
                }
            }));
        }


        // ---- 自己キャプチャ除外 ----
        private static void TryExcludeFromCapture(IntPtr hwnd)
        {
            const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+
            try { SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE); } catch { /* 古いOSは無視 */ }
        }

        // タイトルバーを隠す
        public void HideCaptionBarTemporarily()
        {
            Debug.WriteLine($"[LivePreview] HideCaptionBarTemporarily");
            if (_captionHidden || this.IsDisposed || !this.IsHandleCreated) return;

            var prevState = this.WindowState;
            if (prevState == FormWindowState.Maximized) this.WindowState = FormWindowState.Normal;

            var h = this.Handle;
            _origStyle = GetWindowLong(h, GWL_STYLE);

            int style = _origStyle & ~WS_CAPTION;
            if ((style & WS_THICKFRAME) == 0 && (style & WS_BORDER) == 0)
                style |= WS_THICKFRAME;

            SetWindowLong(h, GWL_STYLE, style);

            // ① FRAMECHANGED で非クライアント再計算
            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // ② フレーム再描画を強制（線が残る環境向け）
            RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);

            // ③ それでも残る場合の“最終兵器”：1px ナッジ（元サイズに戻す）
            RECT rc;
            if (GetWindowRect(h, out rc))
            {
                int w = rc.Right - rc.Left;
                int hgt = rc.Bottom - rc.Top;
                // +1
                SetWindowPos(this.Handle, IntPtr.Zero, this.Left, this.Top, w + 1, hgt + 1, SWP_NOZORDER | SWP_NOACTIVATE);
                // -1（元に戻す & FRAMECHANGED）
                SetWindowPos(this.Handle, IntPtr.Zero, this.Left, this.Top, w, hgt,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }

            _captionHidden = true;
            ApplyTablessShadow(_shadowTabless);
            UpdateShadowMenuState();

            // 追加：全辺 1px のガラス延長 → DWM の影が出る
            var m = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
            try { DwmExtendFrameIntoClientArea(this.Handle, ref m); } catch { /* 非対応OSは無視 */ }
        }

        public void RestoreCaptionBar()
        {
            if (!_captionHidden || this.IsDisposed || !this.IsHandleCreated) return;
            var h = this.Handle;

            if (_origStyle != 0)
            {
                SetWindowLong(h, GWL_STYLE, _origStyle);
                SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);
            }
            _captionHidden = false;
            ApplyTablessShadow(false);
            UpdateShadowMenuState();

            // 延長を 0 に戻して通常の枠に
            var m = new MARGINS(); // 全て 0
            try { DwmExtendFrameIntoClientArea(this.Handle, ref m); } catch { }
        }

        private void UpdateShadowMenuState()
        {
            if (_miShadow == null) return;
            // タブなし（_captionHidden=true）のときだけ操作可能
            _miShadow.Enabled = _captionHidden;
        }

        private bool _pendingNcRefresh = false;

        private void ForceRefreshNonClient()
        {
            if (!IsHandleCreated || IsDisposed) return;
            var h = this.Handle;

            // 非クライアント再計算
            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // フレーム再描画
            RedrawWindow(h, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);

            // それでも残る環境向けの 1px ナッジ
            RECT rc;
            if (GetWindowRect(h, out rc))
            {
                int w = rc.Right - rc.Left;
                int hgt = rc.Bottom - rc.Top;
                SetWindowPos(h, IntPtr.Zero, this.Left, this.Top, w + 1, hgt + 1,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                SetWindowPos(h, IntPtr.Zero, this.Left, this.Top, w, hgt,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // タブなし＝全域クライアント化
            if (_captionHidden && m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // HUD 上のクリック処理（ドラッグ開始を抑止）
            if (m.Msg == WM_LBUTTONDOWN)
            {
                var pt = this.PointToClient(Cursor.Position);
                if (_hudAlpha > 0 && _hudRect.Contains(pt))
                {
                    _hudDown = true;
                    Invalidate(_hudRect);
                    return; // ドラッグ開始させない
                }

                int grip = Math.Max(8, this.DeviceDpi * 8 / 96);
                int w = this.ClientSize.Width, h = this.ClientSize.Height;
                bool nearEdge = (pt.X <= grip) || (pt.X >= w - grip) || (pt.Y <= grip) || (pt.Y >= h - grip);
                if (!nearEdge)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                    return; // ドラッグ開始
                }
            }
            else if (m.Msg == WM_LBUTTONUP)
            {
                if (_hudDown)
                {
                    _hudDown = false;
                    var pt = this.PointToClient(Cursor.Position);
                    bool inside = _hudRect.Contains(pt);
                    Invalidate(_hudRect);
                    if (inside) { TogglePause(); }
                    return;
                }
            }

            // リサイズ帯のヒットテスト（従来どおり）
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                int res = (int)m.Result;
                if (res != HTCLIENT) return;

                int grip = Math.Max(8, this.DeviceDpi * 8 / 96);
                int x = unchecked((short)((long)m.LParam & 0xFFFF));
                int y = unchecked((short)(((long)m.LParam >> 16) & 0xFFFF));
                var p = PointToClient(new System.Drawing.Point(x, y));
                int w = this.ClientSize.Width, h = this.ClientSize.Height;

                bool left   = p.X <= grip;
                bool right  = p.X >= w - grip;
                bool top    = p.Y <= grip;
                bool bottom = p.Y >= h - grip;

                if (top && left)      { m.Result = (IntPtr)HTTOPLEFT;  return; }
                if (top && right)     { m.Result = (IntPtr)HTTOPRIGHT; return; }
                if (bottom && left)   { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                if (bottom && right)  { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (left)             { m.Result = (IntPtr)HTLEFT;     return; }
                if (right)            { m.Result = (IntPtr)HTRIGHT;    return; }
                if (top)              { m.Result = (IntPtr)HTTOP;      return; }
                if (bottom)           { m.Result = (IntPtr)HTBOTTOM;   return; }

                return; // 中央は HTCLIENT
            }

            base.WndProc(ref m);
        }

        const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle, uint dpi);

        private static bool TryAdjustWindowRectForDpi(ref RECT rc, int style, int exstyle, uint dpi)
        {
            try { return AdjustWindowRectExForDpi(ref rc, style, false, exstyle, dpi); }
            catch { return false; }
        }

        private void ResizeToKeepClient(Size client)
        {
            Debug.WriteLine($"ResizeToKeepClient: {client}");
            if (!IsHandleCreated) return;

            var h = this.Handle;

            // タブバーON/OFF切り替え後の現在スタイル
            int style   = GetWindowLong(h, GWL_STYLE);
            int exstyle = GetWindowLong(h, GWL_EXSTYLE);

            int newW, newH;

            if (_captionHidden)
            {
                // 非クライアント=0のため、外枠＝クライアントでOK
                newW = client.Width;
                newH = client.Height;
            }
            else
            {
                // タブバー表示中は枠ぶんを加算
                var rc = new RECT { Left = 0, Top = 0, Right = client.Width, Bottom = client.Height };
                uint dpi = (uint)this.DeviceDpi;
                bool ok = TryAdjustWindowRectForDpi(ref rc, style, exstyle, dpi);
                if (!ok) AdjustWindowRectEx(ref rc, style, false, exstyle);

                newW = rc.Right - rc.Left;
                newH = rc.Bottom - rc.Top;
                Debug.WriteLine($"  -> new outer size(calc): {newW}x{newH} (dpi={dpi})");
            }

            // 左上固定のまま外枠サイズ適用
            SetWindowPos(h, IntPtr.Zero, this.Left, this.Top, newW, newH,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // 念のためフレーム再描画
            ForceRefreshNonClient();

            Debug.WriteLine($"  -> actual outer size: {this.Size} (client={this.ClientSize})");
        }
        private Rectangle GetHudInvalidateRect()
        {
            if (_hudRect.IsEmpty) return Rectangle.Empty;
            int d = _hudRect.Width;
            float penW = Math.Max(1f, d * 0.012f);         // 外周白枠の太さと同じ
            int pad = (int)Math.Ceiling(penW * 0.5f) + 2;  // その半分 + AA余白
            var r = Rectangle.Inflate(_hudRect, pad, pad);
            r.Intersect(this.ClientRectangle);
            return r;
        }

        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern int  MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref RECT lpPoints, int cPoints);

        private struct NcInsets { public int Left, Top, Right, Bottom; }

        private NcInsets GetNcInsets()
        {
            // ウィンドウ外枠（スクリーン座標）
            RECT wrc; GetWindowRect(this.Handle, out wrc);

            // クライアント矩形（クライアント座標）→スクリーン座標へ変換
            RECT crc; GetClientRect(this.Handle, out crc);
            MapWindowPoints(this.Handle, IntPtr.Zero, ref crc, 2);

            return new NcInsets
            {
                Left   = crc.Left  - wrc.Left,
                Top    = crc.Top   - wrc.Top,
                Right  = wrc.Right - crc.Right,
                Bottom = wrc.Bottom- crc.Bottom
            };
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

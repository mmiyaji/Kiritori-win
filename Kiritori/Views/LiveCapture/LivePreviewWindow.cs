using Kiritori.Helpers;
using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace Kiritori.Views.LiveCapture
{
    public partial class LivePreviewWindow : Form
    {
        private LiveCaptureBackend _backend;
        private Bitmap _latest;
        private readonly object _frameSync = new object();
        public Rectangle CaptureRect { get; set; }   // 論理px（スクリーン座標）
        public bool AutoTopMost { get; set; } = true;
        private ContextMenuStrip _ctx;
        private ToolStripMenuItem
            _miCapture, _miOCR, _miLivePreview,
            _miFileRoot, _miEditRoot, _miViewRoot, _miWindowRoot, _miZoomRoot,
            _miOriginal, _miZoomIn, _miZoomOut, _miZoomPct,
            _miOpacity, _miPauseResume, _miRealign, _miTopMost, _miClose,
            _miPref, _miExit, _miTitlebar, _miShowStats;

        private float _zoom = 1.0f;     // 表示倍率（1.0=100%）
        private bool _paused = false;
        public MainApplication MainApp { get; set; }
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

        // ---- HUD（Pause/Resume）描画用
        private System.Windows.Forms.Timer _fadeTimer;
        private int _hudAlpha = 0, _hudTargetAlpha = 0; // 0..200
        private Rectangle _hudRect;
        private bool _hudHot = false, _hudDown = false;
        private bool _hudCursorIsHand = false;
        private const int HUD_INTERACTABLE_ALPHA = 80;
        private bool IsHudInteractable() => _hudAlpha >= HUD_INTERACTABLE_ALPHA && !_hudRect.IsEmpty;

        // === SnapWindow互換：ホバー強調設定（外観） ===
        private Color _hoverColor = Color.DeepSkyBlue;  // SnapWindow同等の鮮やか系を既定に
        private int   _hoverAlphaPercent = 60;          // 0-100（SnapWindow互換の%表現）
        private int   _hoverThicknessPx  = 3;           // 論理px（DPIで実厚算出）

        // 強調枠のフェード（LivePreviewではフェードを活かす）
        private int _hoverAlpha = 0;                 // 実アルファ（0..200程度）
        private int _hoverTargetAlpha = 0;
        private const int HOVER_ALPHA_ON  = 160;
        private const int HOVER_ALPHA_OFF = 0;

        // private Stopwatch _fpsWatch = new Stopwatch();
        private int _maxFps = 15; // 0 = 無制限
        private ToolStripMenuItem _miFpsRoot;
        private ToolStripMenuItem[] _miFpsItems;
        private readonly int[] _fpsChoices = new[] { 5, 10, 15, 30, 60, 0 }; // 0=無制限

        // 影トグル・タブバー
        private bool _shadowTabless = true;
        private ToolStripMenuItem _miShadow;
        private int _origStyle = 0;
        private bool _captionHidden = false;

        // クリック→ドラッグ判定用
        private bool _maybeDrag = false;
        private System.Drawing.Point _downClient;
        private bool _inSizingLoop = false;
        private bool _aspectLockOnShift = true;
        private float _aspectRatio = 0f;
        private bool _useImageAspectIfAvailable = false;
        private bool _isDraggingWindow = false;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }
        private const int MOVE_STEP = 3;
        private const int SHIFT_MOVE_STEP = MOVE_STEP * 10;

        // プロセス表示
        private bool _showStats = true;
        private int _frameCount = 0;
        private int _fps = 0;
        private System.Threading.Timer _perfTimer;
        private readonly Stopwatch _presentWatch = new Stopwatch(); // スロットリング用
        private readonly Stopwatch _fpsWindowWatch = new Stopwatch(); // 1秒ウィンドウ用
        private TimeSpan _lastCpuTime;
        private double _cpuUsage = 0;
        private long _memUsage = 0;

        // 閉じるボタン
        private Rectangle _closeBtnRect = Rectangle.Empty;
        private bool _hoverClose = false;
        private bool _closeDown = false;
        private bool _windowHot = false; // マウスがウィンドウ上にあるか
        // タイトルバーがある時は OS の×があるので自前は非表示にする
        private bool ShouldShowInlineClose() => _captionHidden && _windowHot;

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        public LivePreviewWindow()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            this.KeyPreview = true;
            LoadHoverAppearanceFromSettings();

            Properties.Settings.Default.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightThickness))
                {
                    LoadHoverAppearanceFromSettings();
                    Invalidate();
                }
            };
            BuildOverlay();
            WireHoverHandlers();

            BuildContextMenu();
            this.ContextMenuStrip = _ctx;

            this.Opacity = 0.0; // 初回白チラ防止
            this.BackColor = Color.Black;
            try
            {
                var v = Properties.Settings.Default.LivePreviewMaxFps;
                _maxFps = (v >= 0) ? v : 15;
            }
            catch { _maxFps = 15; }
            // _fpsWatch.Start();
            _presentWatch.Start();
            _fpsWindowWatch.Start();

            // 1秒ごとにCPU/メモリ更新
            // _perfTimer = new System.Threading.Timer(UpdatePerf, null, 1000, 1000);
            _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

            // EnsureContextMenuWithFps();

        }
        // ==== LivePreview 用デバッグロガー（%TEMP%\Kiritori.LivePreview.log + Debug） ====
        private static class LPLog
        {
            private static readonly string LogPath =
                Path.Combine(Path.GetTempPath(), "Kiritori.LivePreview.log");

            public static void Info(string msg)
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [LP] {msg}";
                System.Diagnostics.Debug.WriteLine(line);
                try { File.AppendAllText(LogPath, line + Environment.NewLine); } catch { /* ignore */ }
            }
        }
        private static string RectStr(Rectangle r) => $"({r.X},{r.Y}) {r.Width}x{r.Height}";
        private static string RectStr(RECT r) => $"({r.Left},{r.Top}) {r.Right - r.Left}x{r.Bottom - r.Top}";
        private static string InsetsStr(NcInsets i) => $"L{i.Left} T{i.Top} R{i.Right} B{i.Bottom}";
        // ===========================================================================
        private void MoveThenResizePhysicalWithLogs(string tag, bool topMost)
        {
            // 位置合わせ（※これが内部で“論理サイズ”に変えるので、この後でサイズを上書きする）
            WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: topMost);
            LPLog.Info($"{tag}: after Move: Client={this.ClientSize.Width}x{this.ClientSize.Height}, Bounds=({this.Left},{this.Top}) {this.Width}x{this.Height}, Insets {InsetsStr(GetNcInsets())}");

            // 物理サイズへ上書き
            ResizeToKeepClient(GetDesiredClientPhysical());
            LPLog.Info($"{tag}: after ResizePhysical: Client={this.ClientSize.Width}x{this.ClientSize.Height}, Bounds=({this.Left},{this.Top}) {this.Width}x{this.Height}, Insets {InsetsStr(GetNcInsets())}");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch ((int)keyData)
            {
                // ==== 矢印移動（Ctrl不要 / Shiftで加速） ====
                case (int)HOTS.MOVE_LEFT: NudgeWindowBy(-MOVE_STEP, 0); return true;
                case (int)HOTS.MOVE_RIGHT: NudgeWindowBy(+MOVE_STEP, 0); return true;
                case (int)HOTS.MOVE_UP: NudgeWindowBy(0, -MOVE_STEP); return true;
                case (int)HOTS.MOVE_DOWN: NudgeWindowBy(0, +MOVE_STEP); return true;

                case (int)HOTS.SHIFT_MOVE_LEFT: NudgeWindowBy(-SHIFT_MOVE_STEP, 0); return true;
                case (int)HOTS.SHIFT_MOVE_RIGHT: NudgeWindowBy(+SHIFT_MOVE_STEP, 0); return true;
                case (int)HOTS.SHIFT_MOVE_UP: NudgeWindowBy(0, -SHIFT_MOVE_STEP); return true;
                case (int)HOTS.SHIFT_MOVE_DOWN: NudgeWindowBy(0, +SHIFT_MOVE_STEP); return true;

                // ==== 効果/フラグ ====
                // Ctrl + A : 最前面固定 ON/OFF（SnapWindowの FLOAT 相当）
                case (int)HOTS.FLOAT:
                    if (_miTopMost != null) _miTopMost.Checked = !_miTopMost.Checked;
                    else this.TopMost = !this.TopMost;
                    return true;

                // Ctrl + D : ドロップシャドウ（タブレス影）ON/OFF
                case (int)HOTS.SHADOW:
                    if (_miShadow != null) _miShadow.Checked = !_miShadow.Checked;
                    else
                    {
                        _shadowTabless = !_shadowTabless;
                        if (_captionHidden && IsHandleCreated) ApplyTablessShadow(_shadowTabless);
                    }
                    return true;

                // ==== ズーム ====
                case (int)HOTS.ZOOM_ORIGIN_MAIN:
                case (int)HOTS.ZOOM_ORIGIN_NUMPAD:
                    SetZoom(1.0f, false, true); return true;

                case (int)HOTS.ZOOM_IN:
                    SetZoom(_zoom + 0.10f, false, true); return true;

                case (int)HOTS.ZOOM_OUT:
                    SetZoom(_zoom - 0.10f, false, true); return true;

                // ==== 位置 ====
                case (int)HOTS.LOCATE_ORIGIN_MAIN: // Ctrl + 9
                    RealignToKiritori(); return true;

                // ==== ウィンドウ制御 ====
                case (int)HOTS.MINIMIZE: // Ctrl + H
                    this.WindowState = FormWindowState.Minimized; return true;
                case (int)HOTS.TITLEBAR: // Ctrl + T
                    ToggleTitlebar(); return true;

                case (int)HOTS.CLOSE:   // Ctrl + W
                case (int)HOTS.ESCAPE:  // Esc
                    this.Close(); return true;
                case (int)HOTS.SPACE:   // Space
                    TogglePause(); return true;
                case (int)HOTS.INFO:    // Ctrl + I
                    _showStats = !_showStats;
                    // 表示ON：タイマー開始（未作成なら作る）
                    if (_showStats)
                    {
                        if (_perfTimer == null)
                        {
                            _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
                            _perfTimer = new System.Threading.Timer(UpdatePerf, null, 1000, 1000);
                        }
                    }
                    else
                    {
                        // 表示OFF：タイマー停止（必要なら軽量化）
                        try { _perfTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
                        // 値は残しておいてOK（次回ON時にすぐ表示される）
                    }

                    Invalidate(); // 画面更新
                    return true;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        private void NudgeWindowBy(int dx, int dy)
        {
            // BeginDragHighlight();       // 動いている間は強調
            // _kbdMoveDimmer.Stop();      // 連打で延長
            this.SetDesktopLocation(this.Location.X + dx, this.Location.Y + dy);
            // _kbdMoveDimmer.Start();     // 入力が止まったら EndDragHighlight()
        }
        private void LoadHoverAppearanceFromSettings()
        {
            try
            {
                _hoverColor = Properties.Settings.Default.HoverHighlightColor;
                _hoverAlphaPercent = Properties.Settings.Default.HoverHighlightAlphaPercent;   // 0–100 (%)
                _hoverThicknessPx = Properties.Settings.Default.HoverHighlightThickness;    // 論理px
                // あり得る不正値を軽く矯正
                if (_hoverAlphaPercent < 0) _hoverAlphaPercent = 0;
                if (_hoverAlphaPercent > 100) _hoverAlphaPercent = 100;
                if (_hoverThicknessPx < 1) _hoverThicknessPx = 1;
            }
            catch
            {
                // フォールバック（設定未定義時）
                _hoverColor = Color.DeepSkyBlue;
                _hoverAlphaPercent = 60;
                _hoverThicknessPx = 3;
            }
        }
        private void BeginDragHighlight()
        {
            _isDraggingWindow = true;
            SetHoverInstant(true);     // 即時ON
        }

        private void EndDragHighlight()
        {
            _isDraggingWindow = false;
            var ptClient = PointToClient(Cursor.Position);
            SetHoverInstant(this.ClientRectangle.Contains(ptClient)); // 中ならON/外ならOFF
        }

        // 背景消去しない（_latest を全面に描く & フリッカ抑制）
        protected override void OnPaintBackground(PaintEventArgs e) { /* no-op */ }

        // ===== HUD/枠 初期化（タイマーなど） =====
        private void BuildOverlay()
        {
            CenterOverlay();

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
            _fadeTimer.Tick += (s, e) =>
            {
                bool anyChanged = false;

                // HUD 補間
                int curHud = _hudAlpha;
                int stepHud = 20;
                if (curHud < _hudTargetAlpha) curHud = Math.Min(_hudTargetAlpha, curHud + stepHud);
                else if (curHud > _hudTargetAlpha) curHud = Math.Max(_hudTargetAlpha, curHud - stepHud);
                if (curHud != _hudAlpha) { _hudAlpha = curHud; anyChanged = true; }

                // 強調枠 補間（SnapWindowは即時だが、LivePreviewはフェードを維持）
                // int curHover = _hoverAlpha;
                // int stepHover = 24;
                // if (curHover < _hoverTargetAlpha) curHover = Math.Min(_hoverTargetAlpha, curHover + stepHover);
                // else if (curHover > _hoverTargetAlpha) curHover = Math.Max(_hoverTargetAlpha, curHover - stepHover);
                // if (curHover != _hoverAlpha) { _hoverAlpha = curHover; anyChanged = true; }
                if (_hoverAlpha != _hoverTargetAlpha) { _hoverAlpha = _hoverTargetAlpha; anyChanged = true; }
                if (anyChanged)
                {
                    var inv = Rectangle.Union(GetHudInvalidateRect(), GetHoverInvalidateRect());
                    if (!inv.IsEmpty) Invalidate(inv);
                }

                if (_hudAlpha == _hudTargetAlpha && _hoverAlpha == _hoverTargetAlpha)
                    _fadeTimer.Stop();
            };
        }

        private int DpiScale(int px) => (int)Math.Round(px * this.DeviceDpi / 96.0);

        private void CenterOverlay()
        {
            if (_inSizingLoop) return;
            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            int safePad = DpiScale(8);
            if (clientW <= safePad * 2 || clientH <= safePad * 2)
            {
                _hudRect = Rectangle.Empty;
                return;
            }

            int desiredD = DpiScale(120);
            int maxD     = Math.Min(clientW, clientH) - safePad * 2;
            int minD     = DpiScale(56);

            int d = Math.Min(desiredD, maxD);
            if (d < minD) { _hudRect = Rectangle.Empty; return; }

            _hudRect = new Rectangle((clientW - d) / 2, (clientH - d) / 2, d, d);
        }

        private void WireHoverHandlers()
        {
            this.MouseEnter += (s, e) =>
            {
                _windowHot = true;
                ShowOverlay(); // ← PAUSE中でも通常どおり点灯
            };

            this.MouseMove += (s, e) =>
            {
                ShowOverlay(); // ← PAUSE中でも通常どおり点灯

                var me = (MouseEventArgs)e;
                bool inside = !_hudRect.IsEmpty && _hudRect.Contains(me.Location);
                bool canClick = inside && IsHudInteractable();

                if (inside != _hudHot)
                {
                    _hudHot = inside;
                    Invalidate(GetHudInvalidateRect());
                }
                UpdateCloseHover(me.Location);

                if (canClick && !_hudCursorIsHand) { Cursor = Cursors.Hand; _hudCursorIsHand = true; }
                else if (!canClick && _hudCursorIsHand) { Cursor = Cursors.Default; _hudCursorIsHand = false; }
            };

            this.MouseLeave += (s, e) =>
            {
                _windowHot = false;
                if (_hoverClose) { _hoverClose = false; Invalidate(GetCloseRect()); }

                _hudHot = _hudDown = false;
                _hoverTargetAlpha = HOVER_ALPHA_OFF;
                if (!_fadeTimer.Enabled) _fadeTimer.Start();
                FadeOutOverlay();

                var inv = Rectangle.Union(GetHudInvalidateRect(), GetHoverInvalidateRect());
                if (!inv.IsEmpty) Invalidate(inv);

                if (_hudCursorIsHand) { Cursor = Cursors.Default; _hudCursorIsHand = false; }
            };
        }

        private void ShowOverlay()
        {
            if (_inSizingLoop) return;

            _hudTargetAlpha = _hudRect.IsEmpty ? 0 : 140;  // ← HUDは従来通りフェード
            SetHoverInstant(true);                         // ← ホバーは即時ON

            if (!_fadeTimer.Enabled) _fadeTimer.Start();   // HUD用
        }

        private void HideOverlay()
        {
            _hudHot = _hudDown = false;
            _hudTargetAlpha = 0;       // HUDはフェードアウト
            SetHoverInstant(false);    // ホバーは即時OFF

            if (!_fadeTimer.Enabled) _fadeTimer.Start();   // HUD用
            if (_hudCursorIsHand) { Cursor = Cursors.Default; _hudCursorIsHand = false; }
        }

        private void FadeOutOverlay()
        {
            _hudTargetAlpha = 0;       // HUDのみフェード
            SetHoverInstant(false);    // ホバーは即時OFF
            if (!_fadeTimer.Enabled) _fadeTimer.Start();
        }
        private Rectangle GetCloseRect()
        {
            // DPI 対応のパディングとサイズ（SnapWindow と同等の見え方）
            int pad = DpiScale(6);
            int sz  = DpiScale(20);
            // クライアント右上に配置（タイトルバー非表示時のみ）
            return new Rectangle(ClientSize.Width - pad - sz, pad, sz, sz);
        }

        private void DrawInlineClose(Graphics g)
        {
            if (!ShouldShowInlineClose()) return;

            _closeBtnRect = GetCloseRect();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 常時背景（薄いグレー透明丸）
            int baseAlpha = 40;  // 非ホバー時の背景透明度（0=なし, 255=不透明）
            int hoverAlpha = 90; // ホバー時の背景透明度

            int alpha = _hoverClose ? hoverAlpha : baseAlpha;
            using (var bg = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
            {
                g.FillEllipse(bg, _closeBtnRect);
            }

            // X の線（ホバー時は太く）
            int inset = Math.Max(2, DpiScale(4));
            var r = Rectangle.Inflate(_closeBtnRect, -inset, -inset);
            float w = _hoverClose
                ? Math.Max(2f, (float)DpiScale(2))
                : Math.Max(1.5f, (float)DpiScale(1)); // ← int キャスト注意
            using (var pen = new Pen(Color.White, w))
            {
                pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
                g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
            }

            // 枠のガイド（うっすら）
            using (var guide = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
            {
                g.DrawEllipse(guide, _closeBtnRect);
            }
        }

        private void UpdateCloseHover(Point clientPt)
        {
            bool old = _hoverClose;
            _hoverClose = ShouldShowInlineClose() && GetCloseRect().Contains(clientPt);

            if (old != _hoverClose)
            {
                Invalidate(GetCloseRect());
                if (_hoverClose) Cursor = Cursors.Hand;
                else if (_hudCursorIsHand) { /* HUDの手カーソルは既存で戻す */ }
                else Cursor = Cursors.Default;
            }
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
                    : new MARGINS();
                DwmExtendFrameIntoClientArea(this.Handle, ref m);
            }
            catch { /* 非対応OSは無視 */ }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_perfTimer == null)
                _perfTimer = new System.Threading.Timer(UpdatePerf, null, 1000, 1000);
            else
                _perfTimer.Change(1000, 1000);
            TryExcludeFromCapture(this.Handle);
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            // 破棄中にタイマーが走らないよう即停止
            try { _perfTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
            base.OnHandleDestroyed(e);
        }


        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: AutoTopMost);
            // ResizeToKeepClient(GetDesiredClient());
            // 先に物理サイズを決めてから、クライアント左上=CaptureRectへ位置合わせ
            // ResizeToKeepClient(GetDesiredClientPhysical());
            // WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: AutoTopMost);
            // MoveThenResizePhysicalWithLogs("OnLoad", AutoTopMost);
            ResizeToKeepClient(GetDesiredClientPhysical());
            AlignClientTopLeftPhysical("OnLoad", AutoTopMost);
            TrySyncFirstCaptureIntoLatest(CaptureRect);
            Invalidate();

            try
            {
                var rPhys = DpiUtil.LogicalToPhysical(CaptureRect);
                LPLog.Info($"OnLoad: DeviceDpi={this.DeviceDpi}, CaptionHidden={_captionHidden}, TopMost={this.TopMost}");
                LPLog.Info($"OnLoad: CaptureRect logical={RectStr(CaptureRect)} physical={RectStr(rPhys)}");
                var ins = GetNcInsets();
                LPLog.Info($"OnLoad: Insets {InsetsStr(ins)}");
                LPLog.Info($"OnLoad: Client={this.ClientSize.Width}x{this.ClientSize.Height}, Bounds={RectStr(new Rectangle(this.Left, this.Top, this.Width, this.Height))}");
            }
            catch { /* no-op */ }
            // ---- 追加ここまで ----

            var gdi = new GdiCaptureBackend
            {
                MaxFps = _maxFps,
                CaptureRect = this.CaptureRect
            };

            gdi.ExcludeWindow = this.Handle;
            gdi.FrameArrived += OnFrameArrived;
            _backend = gdi;
            _backend.Start();

            _iconBadge = new TitleIconBadger(this);
            _iconBadge.SetState(LiveBadgeState.Recording);
        }

        private float GetCurrentAspect()
        {
            try
            {
                if (_useImageAspectIfAvailable && _latest != null && _latest.Width > 0 && _latest.Height > 0)
                    return (float)_latest.Width / _latest.Height;

                var cs = this.ClientSize;
                if (cs.Width > 0 && cs.Height > 0) return (float)cs.Width / cs.Height;
            }
            catch { }
            return 1.0f;
        }

        protected override void OnResizeBegin(EventArgs e)
        {
            base.OnResizeBegin(e);
            _aspectRatio = GetCurrentAspect();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            _aspectRatio = 0f;
        }
        private bool _firstFrameShown = false;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            var oldInv = GetHudInvalidateRect();
            CenterOverlay();
            var newInv = GetHudInvalidateRect();
            if (!oldInv.IsEmpty) Invalidate(oldInv);
            if (!newInv.IsEmpty) Invalidate(newInv);

            if (_hudAlpha > 0 && _hudRect.IsEmpty) HideOverlay();
            if (_hoverAlpha > 0) Invalidate(GetHoverInvalidateRect());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Bitmap frame = null;
            lock (_frameSync)
            {
                if (_latest != null)
                    frame = (Bitmap)_latest.Clone();
            }

            if (frame != null)
            {
                try
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(frame, this.ClientRectangle);
                }
                finally { frame.Dispose(); }
            }

            DrawHud(e.Graphics);
            DrawHoverFrame(e.Graphics);

            if (_showStats)
            {
                var g = e.Graphics;

                // 角丸チップ用パラメータ（DPI対応）
                int padX = DpiScale(6);
                int padY = DpiScale(4);
                int radius = DpiScale(0);
                // 背景は少し透過した黒
                int bgA = 130;

                if (_paused)
                {
                    using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                    using (var fg = new SolidBrush(Color.Yellow))
                    using (var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    using (var bg = new SolidBrush(Color.FromArgb(bgA, 0, 0, 0)))
                    {
                        string msg = "PAUSED";
                        var loc = new Point(DpiScale(10), DpiScale(10));

                        // テキストサイズから背景矩形を計算
                        var sz = g.MeasureString(msg, font);
                        var bgRect = new Rectangle(
                            loc.X - padX, loc.Y - padY,
                            (int)Math.Ceiling(sz.Width) + padX * 2,
                            (int)Math.Ceiling(sz.Height) + padY * 2
                        );

                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var path = RoundedRect(bgRect, radius))
                            g.FillPath(bg, path);

                        // 影 + 本体
                        g.DrawString(msg, font, shadow, loc.X + 2, loc.Y + 2);
                        g.DrawString(msg, font, fg, loc);
                    }
                }
                else
                {
                    string info = string.Format("FPS: {0}  CPU: {1:F1}%  MEM(Commit): {2} MB",
                        _fps, _cpuUsage, _memUsage / 1024 / 1024);

                    using (var font = new Font("Segoe UI", 9))
                    using (var fg = new SolidBrush(Color.White))
                    using (var shadow = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
                    using (var bg = new SolidBrush(Color.FromArgb(bgA, 0, 0, 0)))
                    {
                        var loc = new Point(DpiScale(10), DpiScale(10));

                        // テキストサイズから背景矩形を計算
                        var sz = g.MeasureString(info, font);
                        var bgRect = new Rectangle(
                            loc.X - padX, loc.Y - padY,
                            (int)Math.Ceiling(sz.Width) + padX * 2,
                            (int)Math.Ceiling(sz.Height) + padY * 2
                        );

                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var path = RoundedRect(bgRect, radius))
                            g.FillPath(bg, path);

                        // 影 + 本体
                        g.DrawString(info, font, shadow, loc.X + 1, loc.Y + 1);
                        g.DrawString(info, font, fg, loc);
                    }
                }
            }
            DrawInlineClose(e.Graphics);

        }
        private void SetHoverInstant(bool on)
        {
            _hoverTargetAlpha = on ? HOVER_ALPHA_ON : HOVER_ALPHA_OFF;
            // ← アニメ禁止：ターゲットに即座に合わせる
            int before = _hoverAlpha;
            _hoverAlpha = _hoverTargetAlpha;
            if (before != _hoverAlpha)
                Invalidate(GetHoverInvalidateRect());
        }

        private void DrawHud(Graphics g)
        {
            var st = g.Save();
            try
            {
                if (_inSizingLoop) return;
                if (_hudAlpha <= 0 || _hudRect.IsEmpty) return;

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingMode = CompositingMode.SourceOver;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                int d = _hudRect.Width;
                float a = Math.Min(_hudAlpha, 200);

                using (var bg = new SolidBrush(Color.FromArgb((int)a, 0, 0, 0)))
                    g.FillEllipse(bg, _hudRect);

                if (_hudHot)
                {
                    int shrink = (int)Math.Round(d * 0.06);
                    using (var glow = new SolidBrush(Color.FromArgb((int)(a * 0.10f), 255, 255, 255)))
                        g.FillEllipse(glow, Rectangle.Inflate(_hudRect, -shrink, -shrink));
                }

                float penW = Math.Max(1f, d * 0.012f);
                float ringBoost = _hudDown ? 0.70f : (_hudHot ? 0.50f : 0.35f);
                using (var pen = new Pen(Color.FromArgb((int)(a * ringBoost), 255, 255, 255), penW))
                {
                    var rStroke = new RectangleF(_hudRect.X, _hudRect.Y, _hudRect.Width, _hudRect.Height);
                    rStroke.Inflate(-penW / 2f, -penW / 2f);
                    g.DrawEllipse(pen, rStroke);
                }

                double scale = _hudDown ? 0.98 : (_hudHot ? 1.06 : 1.00);
                int marginBase = (int)Math.Round(d * 0.22);
                int margin = (int)Math.Round(marginBase / scale);

                var iconRect = new Rectangle(
                    _hudRect.X + margin, _hudRect.Y + margin,
                    _hudRect.Width - margin * 2, _hudRect.Height - margin * 2);

                float fgBoost = _hudDown ? 1.00f : (_hudHot ? 0.96f : 0.85f);
                int fgA = (int)(255 * fgBoost);

                if (_paused)
                {
                    float W = iconRect.Width;
                    float H = iconRect.Height;

                    float leftPadRatio = 0.30f;
                    float rightPadRatio = Math.Max(0f, 2f * leftPadRatio - 0.5f);

                    float L = iconRect.Left + leftPadRatio * W;
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
                    int barW = (int)Math.Round(d * 0.14);
                    int gap = (int)Math.Round(d * 0.12);
                    int barH = (int)Math.Round(d * 0.52);
                    int y = _hudRect.Y + (_hudRect.Height - barH) / 2;
                    int xL = _hudRect.X + (_hudRect.Width - (barW * 2 + gap)) / 2;
                    int xR = xL + barW + gap;
                    int r = Math.Max(2, barW / 2);

                    using (var br = new SolidBrush(Color.FromArgb(fgA, 255, 255, 255)))
                    using (var left = RoundedRect(new Rectangle(xL, y, barW, barH), r))
                    using (var right = RoundedRect(new Rectangle(xR, y, barW, barH), r))
                    {
                        g.FillPath(br, left);
                        g.FillPath(br, right);
                    }
                }
            }
            finally { g.Restore(st); }
        }

        // === SnapWindow風 強調枠（色/太さ/αはフィールドから、Insetで内側） ===
        private void DrawHoverFrame(Graphics g)
        {
            var st = g.Save();
            try
            {
                // if (_inSizingLoop) return;
                if (_hoverAlpha <= 0) return;

                var rc = this.ClientRectangle;
                if (rc.Width <= 0 || rc.Height <= 0) return;

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int corner = DpiScale(8);
                int rTL = _captionHidden ? corner : 0;
                int rTR = _captionHidden ? corner : 0;
                int rBR = corner;
                int rBL = corner;
                // 端の切れ防止にわずかに内側へ
                Rectangle inner = Rectangle.Inflate(rc, -1, -1);

                using (var path = RoundedRectEach(inner, rTL, rTR, rBR, rBL))
                using (var pen = MakeHoverPen())
                {
                    // フェード中はアルファを上書き（見た目の滑らかさ用）
                    int fadeA = Math.Min(255, _hoverAlpha);
                    var baseCol = pen.Color;
                    var col = Color.FromArgb(Math.Min(255, Math.Max(baseCol.A, fadeA)), baseCol.R, baseCol.G, baseCol.B);
                    pen.Color = col;

                    g.DrawPath(pen, path);
                }
            }
            finally { g.Restore(st); }
        }

        // SnapWindow互換の太さ（DPI反映）
        private float HoverThicknessDpi()
        {
            float t = _hoverThicknessPx * (this.DeviceDpi / 96f);
            return (t < 1f) ? 1f : t;
        }

        // SnapWindow互換のペン（%→アルファ、Insetで内側描画）
        private Pen MakeHoverPen()
        {
            int a = Math.Max(0, Math.Min(255, (int)Math.Round(_hoverAlphaPercent * 2.55)));
            float w = HoverThicknessDpi();
            Color c = Color.FromArgb(a, _hoverColor);
            var pen = new Pen(c, w) { Alignment = PenAlignment.Center };
            return pen;
        }
        // 共通の角丸矩形
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(0, radius * 2);
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }

            p.AddArc(r.Left,  r.Top,    d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top,    d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.Left,  r.Bottom - d, d, d,  90, 90);
            p.CloseFigure();
            return p;
        }

        // 角丸矩形（四隅を個別半径で指定できる版）
        private static GraphicsPath RoundedRectEach(Rectangle r, int rTL, int rTR, int rBR, int rBL)
        {
            var p = new GraphicsPath();
            int dTL = Math.Max(0, rTL * 2);
            int dTR = Math.Max(0, rTR * 2);
            int dBR = Math.Max(0, rBR * 2);
            int dBL = Math.Max(0, rBL * 2);

            if (dTL > 0) p.AddArc(r.Left, r.Top, dTL, dTL, 180, 90); else p.AddLine(r.Left, r.Top, r.Left, r.Top);
            if (dTR > 0) p.AddArc(r.Right - dTR, r.Top, dTR, dTR, 270, 90); else p.AddLine(r.Right, r.Top, r.Right, r.Top);
            if (dBR > 0) p.AddArc(r.Right - dBR, r.Bottom - dBR, dBR, dBR, 0, 90); else p.AddLine(r.Right, r.Bottom, r.Right, r.Bottom);
            if (dBL > 0) p.AddArc(r.Left, r.Bottom - dBL, dBL, dBL, 90, 90); else p.AddLine(r.Left, r.Bottom, r.Left, r.Bottom);

            p.CloseFigure();
            return p;
        }

        public int MaxFps
        {
            get => _maxFps;
            set
            {
                if (value < 0) value = 0;
                _maxFps = value;
                UpdateFpsMenuChecks();

                // 間引きの基準時計をリセット
                _presentWatch.Reset();
                _presentWatch.Start();

                if (_backend is GdiCaptureBackend gdi)
                    gdi.MaxFps = _maxFps;
            }
        }

        // private void EnsureContextMenuWithFps()
        // {
        //     if (this.ContextMenuStrip == null)
        //         this.ContextMenuStrip = new ContextMenuStrip();

        //     bool needSep = true;
        //     foreach (ToolStripItem it in this.ContextMenuStrip.Items)
        //     {
        //         if (it.Tag as string == "fps-root") { needSep = false; break; }
        //     }
        //     if (!needSep) return;

        //     this.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        //     _miFpsRoot = new ToolStripMenuItem(SR.T("Menu.MaxFPS","最大 FPS"));
        //     _miFpsRoot.Tag = "fps-root";

        //     _miFpsItems = new ToolStripMenuItem[_fpsChoices.Length];
        //     for (int i = 0; i < _fpsChoices.Length; i++)
        //     {
        //         int fps = _fpsChoices[i];
        //         string text = (fps == 0) ? SR.T("Menu.FPS.Unlimited","Unlimited") : (fps + " fps");

        //         var mi = new ToolStripMenuItem(text) { Tag = fps };
        //         mi.Click += OnFpsMenuClick;

        //         _miFpsItems[i] = mi;
        //         _miFpsRoot.DropDownItems.Add(mi);
        //     }

        //     this.ContextMenuStrip.Items.Add(_miFpsRoot);
        //     UpdateFpsMenuChecks();
        // }

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
            if (sender is ToolStripMenuItem mi) MaxFps = (int)mi.Tag;
        }

        private bool IsNearResizeEdge(System.Drawing.Point ptClient)
        {
            int grip = Math.Max(8, this.DeviceDpi * 8 / 96);
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            return (ptClient.X <= grip) || (ptClient.X >= w - grip) ||
                    (ptClient.Y <= grip) || (ptClient.Y >= h - grip);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _perfTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
            try { _perfTimer?.Dispose(); } catch { }
            _perfTimer = null;

            try { _backend?.Dispose(); } catch { }
            _backend = null;

            lock (_frameSync)
            {
                _latest?.Dispose();
                _latest = null;
            }

            try { _iconBadge?.Dispose(); } catch { }
            _iconBadge = null;

            try { _fadeTimer?.Stop(); _fadeTimer?.Dispose(); } catch { }
            _fadeTimer = null;

            try { _ctx?.Dispose(); } catch { }

            base.OnFormClosed(e);
        }

        // 初回同期キャプチャ
        private void TrySyncFirstCaptureIntoLatest(Rectangle rLogical)
        {
            if (rLogical.Width <= 0 || rLogical.Height <= 0) return;

            var rPhysical = DpiUtil.LogicalToPhysical(rLogical);

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

            var old = _latest;
            _latest = bmp;
            if (old != null) old.Dispose();

            if (this.Opacity < 1.0) this.Opacity = 1.0;
            _firstFrameShown = true;
            LPLog.Info($"FirstCapture: logical={RectStr(rLogical)} physical={RectStr(rPhysical)} bmp={_latest?.Width}x{_latest?.Height}");
        }

        // private void RealignToKiritori()
        // {
        //     WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: _miTopMost?.Checked ?? true);
        //     Invalidate();
        // }
        private void RealignToKiritori()
        {
            // ResizeToKeepClient(GetDesiredClient());
            // WindowAligner.MoveFormToMatchClient(this, CaptureRect, topMost: _miTopMost?.Checked ?? true);
            // MoveThenResizePhysicalWithLogs("Realign", _miTopMost?.Checked ?? true);
            ResizeToKeepClient(GetDesiredClientPhysical());
            AlignClientTopLeftPhysical("Realign", _miTopMost?.Checked ?? true);
            Invalidate();
        }
        private void BuildContextMenu()
        {
            _ctx = new ContextMenuStrip();

            _miCapture = new ToolStripMenuItem(SR.T("Menu.Capture", "Capture"));
            _miCapture.Click += (s, e) => startCapture();
            _miCapture.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D5)));
            _miOCR = new ToolStripMenuItem(SR.T("Menu.OCR", "OCR"));
            _miOCR.Click += (s, e) => startOCR();
            _miOCR.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D4)));
            _miLivePreview = new ToolStripMenuItem(SR.T("Menu.LivePreview", "Live Preview"));
            _miLivePreview.Click += (s, e) => startLivePreview();
            _miLivePreview.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D6)));


            // ---------- 既存アイテム初期化 ----------
            _miOriginal = new ToolStripMenuItem(SR.T("Menu.OriginalSize", "Original Size"));
            _miOriginal.Click += (s, e) => SetZoom(1.0f, false, true);
            _miOriginal.ShortcutKeys = (Keys)HOTS.ZOOM_ORIGIN_MAIN;

            _miZoomOut = new ToolStripMenuItem(SR.T("Menu.ZoomOut", "Zoom Out(-10%)"));
            _miZoomOut.Click += (s, e) => SetZoom(_zoom - 0.10f, false, true);
            _miZoomOut.ShortcutKeys = (Keys)HOTS.ZOOM_OUT;
            _miZoomOut.ShortcutKeyDisplayString = "Ctrl+'-'";

            _miZoomIn = new ToolStripMenuItem(SR.T("Menu.ZoomIn", "Zoom In(+10%)"));
            _miZoomIn.Click += (s, e) => SetZoom(_zoom + 0.10f, false, true);
            _miZoomIn.ShortcutKeys = (Keys)HOTS.ZOOM_IN;
            _miZoomIn.ShortcutKeyDisplayString = "Ctrl+'+'";

            _miZoomPct = new ToolStripMenuItem(SR.T("Menu.Zoom", "Zoom(%)"));
            foreach (var pct in new[] { 10, 50, 100, 150, 200, 500 })
            {
                var mi = new ToolStripMenuItem($"Size {pct}%") { Tag = pct };
                mi.Click += (s, e) => SetZoom(((int)((ToolStripMenuItem)s).Tag) / 100f, true, false);
                _miZoomPct.DropDownItems.Add(mi);
            }

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

            _miPauseResume = new ToolStripMenuItem(SR.T("Menu.Pause", "Pause"));
            _miPauseResume.Click += (s, e) => TogglePause();
            _miPauseResume.ShortcutKeyDisplayString = "Space";

            _miTitlebar = new ToolStripMenuItem(SR.T("Menu.Titlebar", "Show Title bar"));
            _miTitlebar.Click += (s, e) => ToggleTitlebar();
            _miTitlebar.Checked = true;
            _miTitlebar.ShortcutKeys = (Keys)HOTS.TITLEBAR;

            _miRealign = new ToolStripMenuItem(SR.T("Menu.OriginalLocation", "Move to the initial position"));
            _miRealign.Click += (s, e) => RealignToKiritori();
            _miRealign.ShortcutKeys = (Keys)HOTS.LOCATE_ORIGIN_MAIN;

            _miTopMost = new ToolStripMenuItem(SR.T("Menu.TopMost", "Keep on top")) { Checked = true, CheckOnClick = true };
            _miTopMost.CheckedChanged += (s, e) => this.TopMost = _miTopMost.Checked;
            _miTopMost.ShortcutKeys = (Keys)HOTS.FLOAT;

            _miClose = new ToolStripMenuItem(SR.T("Menu.CloseWindow", "Close Window"));
            _miClose.Click += (s, e) => this.Close();
            _miClose.ShortcutKeys = (Keys)HOTS.CLOSE;

            _miPref = new ToolStripMenuItem(SR.T("Menu.Preferences", "Preferences"));
            _miPref.Click += (s, e) => ShowPreferences();
            _miPref.ShortcutKeys = (Keys)HOTS.SETTING;
            _miPref.ShortcutKeyDisplayString = "Ctrl+,";

            _miExit = new ToolStripMenuItem(SR.T("Menu.Exit", "Exit Kiritori"));
            _miExit.Click += (s, e) => Application.Exit();

            // DropShadow（タブなしのみ）
            _miShadow = new ToolStripMenuItem(SR.T("Menu.DropShadow", "Drop shadow (tabless)"))
            {
                Checked = _shadowTabless,
                CheckOnClick = true
            };
            _miShadow.CheckedChanged += (s, e) =>
            {
                _shadowTabless = _miShadow.Checked;
                if (_captionHidden && IsHandleCreated)
                {
                    ApplyTablessShadow(_shadowTabless);
                    ForceRefreshNonClient();
                }
            };
            _miShadow.ShortcutKeys = (Keys)HOTS.SHADOW;

            // FPS/Stats
            _miFpsRoot = new ToolStripMenuItem(SR.T("Menu.MaxFPS", "MAX FPS")) { Tag = "fps-root" };
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

            _miShowStats = new ToolStripMenuItem(SR.T("Menu.ShowStats", "Show Stats (FPS/CPU/MEM)"))
            {
                Checked = _showStats,
                CheckOnClick = true
            };
            _miShowStats.CheckedChanged += (s, e) =>
            {
                _showStats = _miShowStats.Checked;
                if (_showStats)
                {
                    if (_perfTimer == null)
                    {
                        _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
                        _perfTimer = new System.Threading.Timer(UpdatePerf, null, 1000, 1000);
                    }
                    else
                    {
                        try { _perfTimer.Change(1000, 1000); } catch { }
                    }
                }
                else
                {
                    try { _perfTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
                }
                Invalidate();
            };
            _miShowStats.ShortcutKeys = (Keys)HOTS.INFO;

            // ---------- サブメニュー（SnapWindow 構成に寄せる） ----------
            var miFile   = new ToolStripMenuItem("File");   // いまは LivePreview 既存機能なし。将来 Save/Copy Frame 等をここに
            miFile.Enabled = false;
            var miEdit   = new ToolStripMenuItem("Edit");   // 予備（将来の編集系コマンド用）
            miEdit.Enabled = false;

            var miView   = new ToolStripMenuItem("View");
            miView.DropDownItems.AddRange(new ToolStripItem[] {
                _miOriginal,
                _miZoomOut,
                _miZoomIn,
                _miZoomPct,
                new ToolStripSeparator(),
                _miOpacity,
                new ToolStripSeparator(),
                _miShowStats,
            });

            var miWindow = new ToolStripMenuItem("Window");
            miWindow.DropDownItems.AddRange(new ToolStripItem[] {
                _miTitlebar,
                _miTopMost,
                _miShadow,
                new ToolStripSeparator(),
                _miRealign,
            });

            // ---------- ルート構成（SnapWindow 風の順序） ----------
            _ctx.Items.AddRange(new ToolStripItem[] {
                // 上段は SnapWindow では 「Image Capture / OCR / Live Preview / Close Window」だが
                // LivePreview ではまず Close のみを同位置に配置（後で統合可能）
                _miClose,
                _miPauseResume,
                _miFpsRoot,
                new ToolStripSeparator(),
                _miCapture,
                _miOCR,
                _miLivePreview,
                new ToolStripSeparator(),
                miFile,
                miEdit,
                miView,
                miWindow,
                new ToolStripSeparator(),
                _miPref,
                _miExit
            });

            this.ContextMenuStrip = _ctx;
            UpdateShadowMenuState();
            UpdateFpsMenuChecks();

            _ctx.Opened += (s, e) =>
            {
                if (_ctx != null && _ctx.Handle != IntPtr.Zero) TryExcludeFromCapture(_ctx.Handle);
                UpdateShadowMenuState();
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

            // --- ドロップダウンはキャプチャ除外（LivePreview への線描画対策）
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
            MarkDropDownExclusion(_miZoomPct);
            MarkDropDownExclusion(_miOpacity);
            MarkDropDownExclusion(_miFpsRoot);
        }

        private void ShowPreferences()
        {
            try { PrefForm.ShowSingleton((IWin32Window)this.MainApp); }
            catch { PrefForm.ShowSingleton(this); }
        }
        private void startCapture()
        {
            try { this.MainApp.openScreen(); }
            catch {  }
        }
        private void startOCR()
        {
            try { this.MainApp.openScreenOCR(); }
            catch {  }
        }
        private void startLivePreview()
        {
            try { this.MainApp.openScreenLive(); }
            catch {  }
        }

        private void SetZoom(float z, bool aspectRatioLocked = false, bool force = false)
        {
            z = Math.Max(0.1f, Math.Min(8.0f, z));
            if (Math.Abs(_zoom - z) < 0.0001f && !force) return;
            _zoom = z;
            Size newClient;
            if (aspectRatioLocked)
            {
                var cs = this.ClientSize;
                int w = cs.Width, h = cs.Height;
                newClient = new Size(
                    (int)Math.Round(w * _zoom),
                    (int)Math.Round(h * _zoom)
                );
            }
            else
            {
                newClient = new Size(
                    (int)Math.Round(CaptureRect.Width * _zoom),
                    (int)Math.Round(CaptureRect.Height * _zoom)
                );
            }

            // WindowAligner.ResizeFormClientKeepTopLeft(
            //     this,
            //     newClient,
            //     topMost: _miTopMost?.Checked ?? this.TopMost
            // );
            // タイトルバー/枠を含めた DPI 正確なサイズに調整（左上は維持）
            ResizeToKeepClient(GetDesiredClientPhysical());
            // TopMost は必要なら維持
            this.TopMost = _miTopMost?.Checked ?? this.TopMost;
            Invalidate();
        }
        private void TogglePause()
        {
            _paused = !_paused;
            _miPauseResume.Text = _paused ? SR.T("Menu.Resume", "Resume") : SR.T("Menu.Pause", "Pause");
            _iconBadge?.SetState(_paused ? LiveBadgeState.Paused : LiveBadgeState.Recording);

            if (!_paused) Invalidate();
            Invalidate(GetHudInvalidateRect());
        }
        private void ToggleTitlebar()
        {
            var desiredClient = GetDesiredClient();
            var oldInsets = GetNcInsets();

            if (_miTitlebar.Checked) HideCaptionBarTemporarily();
            else                     RestoreCaptionBar();
            _miTitlebar.Checked = !_miTitlebar.Checked;

            Action apply = () =>
            {
                ForceRefreshNonClient();
                ResizeToKeepClient(desiredClient);

                var newInsets = GetNcInsets();
                int dx = oldInsets.Left - newInsets.Left;
                int dy = oldInsets.Top  - newInsets.Top;

                if (dx != 0 || dy != 0)
                {
                    SetWindowPos(this.Handle, IntPtr.Zero, this.Left + dx, this.Top + dy,
                        0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
                UpdateShadowMenuState();

                // ---- 追加ログ ----
                LPLog.Info($"ToggleTitlebar: CaptionHidden={_captionHidden}, DeviceDpi={this.DeviceDpi}");
                LPLog.Info($"ToggleTitlebar: Insets(before) {InsetsStr(oldInsets)} -> (after) {InsetsStr(newInsets)}");
                LPLog.Info($"ToggleTitlebar: ClientNow={this.ClientSize.Width}x{this.ClientSize.Height}, BoundsNow={RectStr(new Rectangle(this.Left, this.Top, this.Width, this.Height))}");
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

        // 既存ヘルパの近くに追加
        private Size GetDesiredClientPhysical()
        {
            // CaptureRect は論理px。ソース側モニタDPIで物理pxへ。
            var rPhys = DpiUtil.LogicalToPhysical(CaptureRect);
            int w = (int)Math.Round(rPhys.Width  * _zoom);
            int h = (int)Math.Round(rPhys.Height * _zoom);
            LPLog.Info($"DesiredClient: logical={CaptureRect.Width}x{CaptureRect.Height} -> physical={w}x{h} (zoom={_zoom:F2})");
            return new Size(w, h);
        }
        private System.Drawing.Point GetClientTopLeftOnScreen()
        {
            RECT crc; GetClientRect(this.Handle, out crc);
            MapWindowPoints(this.Handle, IntPtr.Zero, ref crc, 2);
            return new System.Drawing.Point(crc.Left, crc.Top);
        }

        // クライアント左上を「物理pxの CaptureRect.X/Y」に合わせる
        private void AlignClientTopLeftPhysical(string tag, bool topMost)
        {
            var rPhys = DpiUtil.LogicalToPhysical(CaptureRect);
            var ins = GetNcInsets();
            float sDst = this.DeviceDpi / 96f; // このウィンドウが今いるモニタのスケール

            // 目的のクライアント左上（このモニタの“論理px”）= 物理 / sDst
            int clientL = (int)Math.Round(rPhys.X / sDst);
            int clientT = (int)Math.Round(rPhys.Y / sDst);

            int newLeft = clientL - ins.Left;
            int newTop  = clientT - ins.Top;

            SetWindowPos(this.Handle, IntPtr.Zero, newLeft, newTop, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            var cur = GetClientTopLeftOnScreen();
            LPLog.Info($"{tag}: AlignPhysical: wantPhysTL=({rPhys.X},{rPhys.Y}), sDst={sDst:F2}, set LeftTop=({newLeft},{newTop}), " +
                    $"clientTL(after)=({cur.X},{cur.Y})");
        }

        // FrameArrived：一部省略（元実装のまま）
        private void OnFrameArrived(Bitmap bmp)
        {
            if (_paused) return;

            // 1) MaxFPSによる間引き（描画の表示タイミングを決める）
            if (_maxFps > 0)
            {
                double minIntervalMs = 1000.0 / _maxFps;
                if (_presentWatch.ElapsedMilliseconds < minIntervalMs)
                    return; // 表示しない（カウントもしない）
                _presentWatch.Restart();
            }

            // 2) ここに来たら「実際に表示するフレーム」なので FPS カウント
            Interlocked.Increment(ref _frameCount);

            // 3) 1秒ごとに表示FPSを更新（無制限でも動く）
            if (_fpsWindowWatch.ElapsedMilliseconds >= 1000)
            {
                _fps = Interlocked.Exchange(ref _frameCount, 0);
                _fpsWindowWatch.Restart();
            }

            // 4) 以降は元の適用処理
            Bitmap old = null;
            lock (_frameSync)
            {
                old = _latest;
                _latest = (Bitmap)bmp.Clone();
            }
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


        private void UpdatePerf(object state)
        {
            if (!_showStats) return;
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                var proc = Process.GetCurrentProcess();

                // CPU計算
                var newCpuTime = proc.TotalProcessorTime;
                //var elapsed = 1.0; // 秒間隔なので1秒
                _cpuUsage = (newCpuTime - _lastCpuTime).TotalMilliseconds / (Environment.ProcessorCount * 1000.0) * 100.0;
                _lastCpuTime = newCpuTime;

                // メモリ
                _memUsage = proc.WorkingSet64;

                this.BeginInvoke((Action)(() => this.Invalidate()));
            }
            catch (ObjectDisposedException) { /* ignore: 閉じた直後の競合 */ }
            catch (InvalidOperationException) { /* ignore: ハンドル競合 */ }

        }
        // ---- 自己キャプチャ除外 ----
        private static void TryExcludeFromCapture(IntPtr hwnd)
        {
            const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+
            try { SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE); } catch { }
        }

        // タイトルバーを隠す
        public void HideCaptionBarTemporarily()
        {
            if (_captionHidden || this.IsDisposed || !this.IsHandleCreated) return;

            var prevState = this.WindowState;
            if (prevState == FormWindowState.Maximized) this.WindowState = FormWindowState.Normal;

            var h = this.Handle;
            _origStyle = GetWindowLong(h, GWL_STYLE);

            int style = _origStyle & ~WS_CAPTION;
            if ((style & WS_THICKFRAME) == 0 && (style & WS_BORDER) == 0)
                style |= WS_THICKFRAME;

            SetWindowLong(h, GWL_STYLE, style);

            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            RedrawWindow(h, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);

            RECT rc;
            if (GetWindowRect(h, out rc))
            {
                int w = rc.Right - rc.Left;
                int hgt = rc.Bottom - rc.Top;
                SetWindowPos(this.Handle, IntPtr.Zero, this.Left, this.Top, w + 1, hgt + 1, SWP_NOZORDER | SWP_NOACTIVATE);
                SetWindowPos(this.Handle, IntPtr.Zero, this.Left, this.Top, w, hgt,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }

            _captionHidden = true;
            ApplyTablessShadow(_shadowTabless);
            UpdateShadowMenuState();

            var m = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
            try { DwmExtendFrameIntoClientArea(this.Handle, ref m); } catch { }
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
                RedrawWindow(h, IntPtr.Zero, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);
            }
            _captionHidden = false;
            ApplyTablessShadow(false);
            UpdateShadowMenuState();

            var m = new MARGINS(); // 0
            try { DwmExtendFrameIntoClientArea(this.Handle, ref m); } catch { }
        }

        private void UpdateShadowMenuState()
        {
            if (_miShadow == null) return;
            _miShadow.Enabled = _captionHidden;
        }

        private bool _pendingNcRefresh = false;

        private void ForceRefreshNonClient()
        {
            if (!IsHandleCreated || IsDisposed) return;
            var h = this.Handle;

            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            RedrawWindow(h, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME | RDW_ALLCHILDREN);

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
            const int WM_MOUSEMOVE     = 0x0200;
            const int WM_LBUTTONDBLCLK = 0x0203;
            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE  = 0x0232;
            const int WM_SIZING        = 0x0214;
            // const int WM_DPICHANGED    = 0x02E0;

            // if (m.Msg == WM_DPICHANGED)
            // {
            //     try
            //     {
            //         long w = m.WParam.ToInt64();
            //         int newDpiX = (int)(w & 0xFFFF);
            //         int newDpiY = (int)((w >> 16) & 0xFFFF);
            //         var rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            //         LPLog.Info($"WM_DPICHANGED: DeviceDpi {this.DeviceDpi} -> {newDpiX}/{newDpiY}, suggested={RectStr(rc)}");
            //     }
            //     catch { }

            //     try
            //     {
            //         // 位置→サイズ（物理）
            //         // MoveThenResizePhysicalWithLogs("WM_DPICHANGED", _miTopMost?.Checked ?? this.TopMost);
            //         ResizeToKeepClient(GetDesiredClientPhysical());
            //         AlignClientTopLeftPhysical("WM_DPICHANGED", _miTopMost?.Checked ?? this.TopMost);
            //     }
            //     catch (Exception ex)
            //     {
            //         LPLog.Info("WM_DPICHANGED: reapply failed: " + ex.Message);
            //     }
            // }
            if (_captionHidden && m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                var pt = this.PointToClient(Cursor.Position);

                // Close button
                if (ShouldShowInlineClose() && GetCloseRect().Contains(pt))
                {
                    _closeDown = true;
                    Invalidate(GetCloseRect());
                    return;
                }
                // HUD area(pause/resume)
                if (!IsHudInteractable() || !_hudRect.Contains(pt))
                {
                    if (!IsNearResizeEdge(pt))
                    {
                        TogglePause();
                        Invalidate(GetHudInvalidateRect());
                        _maybeDrag = false;
                        return;
                    }
                }
            }

            if (m.Msg == WM_SIZING && _aspectLockOnShift && IsShiftDown())
            {
                var aspect = _aspectRatio > 0f ? _aspectRatio : GetCurrentAspect();
                RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                int edge = m.WParam.ToInt32();
                int minW = Math.Max(1, this.MinimumSize.Width);
                int minH = Math.Max(1, this.MinimumSize.Height);
                ApplyAspectSizing(ref rc, edge, aspect, minW, minH);
                Marshal.StructureToPtr(rc, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_LBUTTONDOWN)
            {
                var pt = this.PointToClient(Cursor.Position);

                if (ShouldShowInlineClose() && GetCloseRect().Contains(pt))
                {
                    _closeDown = true;
                    Invalidate(GetCloseRect());
                    return;
                }
                if (_hudAlpha > 0 && _hudRect.Contains(pt))
                {
                    _hudDown = true;
                    Invalidate(_hudRect);
                    return;
                }

                if (IsNearResizeEdge(pt))
                {
                    base.WndProc(ref m);
                    return;
                }

                _maybeDrag = true;
                _downClient = pt;
                base.WndProc(ref m);
                return;
            }
            else if (m.Msg == WM_MOUSEMOVE)
            {
                UpdateCloseHover(this.PointToClient(Cursor.Position));
                if (_maybeDrag && (Control.MouseButtons & MouseButtons.Left) != 0)
                {
                    var pt = this.PointToClient(Cursor.Position);
                    var drag = SystemInformation.DragSize;
                    if (Math.Abs(pt.X - _downClient.X) >= drag.Width / 2 ||
                        Math.Abs(pt.Y - _downClient.Y) >= drag.Height / 2)
                    {
                        _maybeDrag = false;
                        ReleaseCapture();
                        BeginDragHighlight();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                        return;
                    }
                }
            }
            else if (m.Msg == WM_LBUTTONUP)
            {
                if (_closeDown)
                {
                    _closeDown = false;
                    bool inside = GetCloseRect().Contains(this.PointToClient(Cursor.Position));
                    Invalidate(GetCloseRect());
                    if (inside) { this.Close(); return; }
                }
                if (_hudDown)
                {
                    _hudDown = false;
                    var pt = this.PointToClient(Cursor.Position);
                    bool inside = _hudRect.Contains(pt);
                    Invalidate(_hudRect);
                    if (inside) { TogglePause(); }
                    return;
                }
                _maybeDrag = false;
            }
            if (m.Msg == WM_NCLBUTTONDOWN && m.WParam == (IntPtr)HTCAPTION)
            {
                BeginDragHighlight();
            }
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
                return;
            }

            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _inSizingLoop = true;
                var oldInv = Rectangle.Union(GetHudInvalidateRect(), GetHoverInvalidateRect());
                _hudTargetAlpha = 0;             // HUDはフェードアウト
                SetHoverInstant(_isDraggingWindow); // ホバーはドラッグ中なら即時ON, それ以外OFF
                _hudHot = _hudDown = false;
                if (_hudCursorIsHand) { Cursor = Cursors.Default; _hudCursorIsHand = false; }
                if (!oldInv.IsEmpty) Invalidate(oldInv);
                _hudRect = Rectangle.Empty;
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _inSizingLoop = false;
                CenterOverlay();
                if (_isDraggingWindow) EndDragHighlight(); // 即時復帰
                else ShowOverlay();                        // 通常（HUDはフェード、ホバーは即時ON）
                var inv = Rectangle.Union(GetHudInvalidateRect(), GetHoverInvalidateRect());
                if (!inv.IsEmpty) Invalidate(inv);
            }

            base.WndProc(ref m);
        }

        private static bool IsShiftDown()
        {
            const int VK_SHIFT = 0x10;
            short s = GetKeyState(VK_SHIFT);
            return (s & 0x8000) != 0;
        }

        private static void ApplyAspectSizing(ref RECT rc, int edge, float aspect, int minW, int minH)
        {
            int w = Math.Max(minW, rc.Right - rc.Left);
            int h = Math.Max(minH, rc.Bottom - rc.Top);

            const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4,
                    WMSZ_TOPRIGHT = 5, WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7;
                    // , WMSZ_BOTTOMRIGHT = 8;

            switch (edge)
            {
                case WMSZ_LEFT:
                case WMSZ_RIGHT:
                {
                    h = (int)Math.Round(w / aspect);
                    h = Math.Max(h, minH);
                    if (edge == WMSZ_RIGHT) rc.Bottom = rc.Top + h;
                    else rc.Top = rc.Bottom - h;
                    break;
                }
                case WMSZ_TOP:
                case WMSZ_BOTTOM:
                {
                    w = (int)Math.Round(h * aspect);
                    w = Math.Max(w, minW);
                    if (edge == WMSZ_BOTTOM) rc.Right = rc.Left + w;
                    else rc.Left = rc.Right - w;
                    break;
                }
                default:
                {
                    w = (int)Math.Round(h * aspect);
                    w = Math.Max(w, minW);
                    h = (int)Math.Round(w / aspect);
                    if (edge == WMSZ_TOPLEFT) { rc.Left = rc.Right - w; rc.Top = rc.Bottom - h; }
                    else if (edge == WMSZ_TOPRIGHT) { rc.Right = rc.Left + w; rc.Top = rc.Bottom - h; }
                    else if (edge == WMSZ_BOTTOMLEFT) { rc.Left = rc.Right - w; rc.Bottom = rc.Top + h; }
                    else { rc.Right = rc.Left + w; rc.Bottom = rc.Top + h; }
                    break;
                }
            }
        }

        // ====== 補助（Invalidate領域など） ======
        private Rectangle GetHudInvalidateRect()
        {
            if (_hudRect.IsEmpty) return Rectangle.Empty;
            int d = _hudRect.Width;
            float penW = Math.Max(1f, d * 0.012f);
            int pad = (int)Math.Ceiling(penW * 0.5f) + 2;
            var r = Rectangle.Inflate(_hudRect, pad, pad);
            r.Intersect(this.ClientRectangle);
            return r;
        }

        private Rectangle GetHoverInvalidateRect()
        {
            return (_hoverAlpha > 0) ? this.ClientRectangle : Rectangle.Empty;
        }

        // ===== Win32 =====
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern short GetKeyState(int nVirtKey);
        const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle, uint dpi);

        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern int  MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref RECT lpPoints, int cPoints);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll", SetLastError = true)] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        const uint RDW_INVALIDATE=0x0001, RDW_UPDATENOW=0x0100, RDW_FRAME=0x0400, RDW_ALLCHILDREN=0x0080;

        private struct NcInsets { public int Left, Top, Right, Bottom; }

        private NcInsets GetNcInsets()
        {
            RECT wrc; GetWindowRect(this.Handle, out wrc);
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

        private static bool TryAdjustWindowRectForDpi(ref RECT rc, int style, int exstyle, uint dpi)
        {
            try { return AdjustWindowRectExForDpi(ref rc, style, false, exstyle, dpi); }
            catch { return false; }
        }

        private void ResizeToKeepClient(Size client)
        {
            if (!IsHandleCreated) return;
            var h = this.Handle;

            // ---- 追加ログ：入口 ----
            LPLog.Info($"ResizeToKeepClient: reqClient={client.Width}x{client.Height}, CaptionHidden={_captionHidden}, DeviceDpi={this.DeviceDpi}");

            int style   = GetWindowLong(h, GWL_STYLE);
            int exstyle = GetWindowLong(h, GWL_EXSTYLE);

            int newW, newH;

            if (_captionHidden)
            {
                newW = client.Width;
                newH = client.Height;

                SetWindowPos(h, IntPtr.Zero, this.Left, this.Top,
                            newW, newH,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                ForceRefreshNonClient();

                var insH = GetNcInsets();
                LPLog.Info($"ResizeToKeepClient(hidden): applied outer={newW}x{newH}, Insets {InsetsStr(insH)}");
                if (GetClientRect(h, out RECT crcH))
                {
                    LPLog.Info($"ResizeToKeepClient(hidden): realClient={crcH.Right - crcH.Left}x{crcH.Bottom - crcH.Top}");
                }
                return;
            }

            // ① 現在の実測インセットで一度サイズを当てる
            var ins = GetNcInsets();
            newW = client.Width  + ins.Left + ins.Right;
            newH = client.Height + ins.Top  + ins.Bottom;

            LPLog.Info($"ResizeToKeepClient: current Insets {InsetsStr(ins)} => target outer={newW}x{newH} (style=0x{style:X8}, ex=0x{exstyle:X8})");

            SetWindowPos(h, IntPtr.Zero, this.Left, this.Top, newW, newH,
                        SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // ② 実測クライアントと要求との差を見て微調整
            if (GetClientRect(h, out RECT crc))
            {
                int curW = crc.Right  - crc.Left;
                int curH = crc.Bottom - crc.Top;
                int dW = client.Width  - curW;
                int dH = client.Height - curH;

                LPLog.Info($"ResizeToKeepClient: after1 realClient={curW}x{curH} -> delta dW={dW}, dH={dH}");

                if (dW != 0 || dH != 0)
                {
                    SetWindowPos(h, IntPtr.Zero, this.Left, this.Top,
                                newW + dW, newH + dH,
                                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }
            }

            ForceRefreshNonClient();

            // ③ 最終状態を記録
            var ins2 = GetNcInsets();
            LPLog.Info($"ResizeToKeepClient: final Insets {InsetsStr(ins2)}");
            if (GetClientRect(h, out RECT crc2))
            {
                LPLog.Info($"ResizeToKeepClient: final realClient={crc2.Right - crc2.Left}x{crc2.Bottom - crc2.Top}, Bounds={RectStr(new Rectangle(this.Left, this.Top, this.Width, this.Height))}");
            }
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
    }
}

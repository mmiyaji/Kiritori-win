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
            _miPref, _miExit, _miTabbar;

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
        const int WM_NCCALCSIZE    = 0x0083;
        const int WM_NCHITTEST     = 0x0084;

        const int HTCLIENT=1, HTCAPTION=2;
        const int HTLEFT=10, HTRIGHT=11, HTTOP=12, HTTOPLEFT=13, HTTOPRIGHT=14, HTBOTTOM=15, HTBOTTOMLEFT=16, HTBOTTOMRIGHT=17;

        // 影オプション（メニューから切替可能にする想定）
        private bool _shadowWhenTabless = true;

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

        private int _origStyle = 0;
        private bool _captionHidden = false;
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

        private Size GetDesiredClient() =>
            new Size(
                (int)Math.Round(CaptureRect.Width  * _zoom),
                (int)Math.Round(CaptureRect.Height * _zoom)
            );

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
            _miTabbar = new ToolStripMenuItem(SR.T("Menu.Tabbar", "Show Tabbar")); _miTabbar.Click += (s, e) => ToggleTabbar();
            _miTabbar.Checked = true;
            _miRealign = new ToolStripMenuItem(SR.T("Menu.OriginalLocation", "Move to the initial position")); _miRealign.Click += (s, e) => RealignToKiritori();

            // 固定・終了
            _miTopMost = new ToolStripMenuItem(SR.T("Menu.TopMost", "Keep on top")) { Checked = true, CheckOnClick = true };
            _miTopMost.CheckedChanged += (s, e) => this.TopMost = _miTopMost.Checked;

            _miClose = new ToolStripMenuItem(SR.T("Menu.CloseWindow", "Close Window")); _miClose.Click += (s, e) => this.Close();
            _miPref = new ToolStripMenuItem(SR.T("Menu.Preferences", "Preferences")); _miPref.Click += (s, e) => ShowPreferences();
            _miExit = new ToolStripMenuItem(SR.T("Menu.Exit", "Exit Kiritori")); _miExit.Click += (s, e) => Application.Exit();

            _ctx.Items.AddRange(new ToolStripItem[] {
                _miPauseResume,
                _miTabbar,
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
        private void ToggleTabbar()
        {
            var desiredClient = GetDesiredClient();

            // 切替「前」の枠厚（左・上）を実測
            var oldInsets = GetNcInsets();

            if (_miTabbar.Checked) HideCaptionBarTemporarily();
            else                   RestoreCaptionBar();
            _miTabbar.Checked = !_miTabbar.Checked;

            Action apply = () =>
            {
                // フレーム再計算 → クライアント寸法を維持（位置はまだ触らない）
                ForceRefreshNonClient();
                ResizeToKeepClient(desiredClient);

                // 切替「後」の枠厚（左・上）を実測
                var newInsets = GetNcInsets();

                // 差分だけ位置補正（左/上）
                int dx = oldInsets.Left - newInsets.Left;   // 左枠が薄くなったら +dx で右へ、厚くなったら -dx で左へ
                int dy = oldInsets.Top  - newInsets.Top;    // これまでのdyと同じ理屈

                if (dx != 0 || dy != 0)
                {
                    SetWindowPos(this.Handle, IntPtr.Zero, this.Left + dx, this.Top + dy,
                        0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
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
            // 参照コードの WM_NCCALCSIZE ブロックはそのまま（＝_captionHidden時は全域クライアント化）
            if (_captionHidden && m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // ★ 追加：タブあり/なし関係なく、クライアントの“リサイズ帯以外”を掴んだらドラッグ開始
            if (m.Msg == WM_LBUTTONDOWN)
            {
                int grip = Math.Max(8, this.DeviceDpi * 8 / 96);
                var pt = this.PointToClient(Cursor.Position);
                int w = this.ClientSize.Width, h = this.ClientSize.Height;

                bool nearEdge = (pt.X <= grip) || (pt.X >= w - grip) || (pt.Y <= grip) || (pt.Y >= h - grip);
                if (!nearEdge)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                    return; // ドラッグ開始
                }
                // 端はリサイズに任せる
            }

            // 参照コードの NCHITTEST 補強もそのまま（全域クライアント時＝タブなしで効く）
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

                return; // 中央は HTCLIENT（右クリックメニューは既存どおり）
            }

            base.WndProc(ref m);
        }

        const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle, uint dpi);

        // 既存のヘルパ（あればそのまま）
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

            // 念のためフレーム再描画（あなたの ForceRefreshNonClient でOK）
            ForceRefreshNonClient();

            Debug.WriteLine($"  -> actual outer size: {this.Size} (client={this.ClientSize})");
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

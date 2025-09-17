using Kiritori.Views.LiveCapture;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Logging;
using Kiritori.Views.Capture;
using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
// using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using CommunityToolkit.WinUI.Notifications;
using System.Diagnostics;
using System.Security.Principal;
using Windows.UI.Notifications;
using System.IO;
using System.Threading;
using Kiritori.Services.Ocr;
//using static Kiritori.Helpers;

namespace Kiritori
{
    public partial class ScreenWindow : Form
    {
        // ====== 既存フィールド ======
        private readonly Func<int> getHostDpi;
        int x = 0, y = 0, h = 0, w = 0;
        private Graphics g;
        private Bitmap bmp;
        private Bitmap baseBmp; // キャプチャ原本
        private Boolean isOpen;
        private ArrayList captureArray;
        private Font fnt = new Font("Segoe UI", 10, GraphicsUnit.Point);
        private MainApplication ma;
        private int currentDpi = 96;

        // スナップ関連の既定（Settings が無い場合のフォールバック）
        private int snapGrid = 50;      // グリッド間隔(px)
        private int edgeSnapTol = 6;    // 端スナップの許容距離(px)

        // ====== 設定をキャッシュして描画に使うフィールド ======
        private Color _bgColor;               // CaptureBackgroundColor
        private int _bgAlphaPercent;        // CaptureBackgroundAlphaPercent (0-100)
        private Color _hoverColor;            // HoverHighlightColor
        private int _hoverAlphaPercent;     // HoverHighlightAlphaPercent (0-100)
        private int _hoverThicknessPx;      // HoverHighlightThickness (px)
        private int _lastPaintTick;
        // 任意の拡張（Settings があれば拾う。無ければ既存値を使用）
        private int _snapGridPx;            // SnapGridPx
        private int _edgeSnapTolPx;         // EdgeSnapTolerancePx
        private bool _ocr_mode = false;
        // Live mode
        private bool _live_mode = false;
        // 固定サイズモード
        private bool _fixed_mode = false;
        private Size _fixedSizePx = Size.Empty;
        private int _fixedPresetIndex = -1; // 0-based
        private int _fixed_armTick = 0;
        private static readonly (string Label, int W, int H)[] FIXED_PRESETS = new[]
        {
            ("1) 500×500 (1:1)",    500,  500),
            ("2) 640×360 (16:9)",   640,  360),
            ("3) 800×600 (4:3)",    800,  600),
            ("4) 1024×768 (4:3)",   1024, 768),
            ("5) 1280×720 (HD)",    1280, 720),
            ("6) 1920×1080 (FHD)",  1920,1080),
            ("カスタム",               0,    0), // カスタムサイズ
        };
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        private struct POINT { public int X; public int Y; }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmFlush();
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]  private static extern bool GdiFlush();
        private static void FlushComposition()
        {
            try { DwmFlush(); } catch { }
            try { GdiFlush(); } catch { }
            try { Application.DoEvents(); } catch { }
        }

        // ====== コンストラクタ ======
        public ScreenWindow(MainApplication mainapp, Func<int> getHostDpi = null)
        {
            this.ma = mainapp;
            this.getHostDpi = getHostDpi ?? (Func<int>)(() =>
            {
                try { return GetDpiForWindow(this.Handle); } catch { return 96; }
            });
            captureArray = new ArrayList();
            isOpen = true;

            InitializeComponent();

            this.DoubleBuffered = true;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            // 設定を反映 & 設定変更のライブ反映
            ApplySettingsFromPreferences();
            HookSettingsChanged();

            try { OnHostDpiChanged(this.getHostDpi()); } catch { }
        }

        // ====== 公開API ======
        public Boolean isScreenOpen() { return this.isOpen; }

        // ====== ロード時のイベント ======
        private void Screen_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown += new MouseEventHandler(ScreenWindow_MouseDown);
            pictureBox1.MouseMove += new MouseEventHandler(ScreenWindow_MouseMove);
            pictureBox1.MouseUp += new MouseEventHandler(ScreenWindow_MouseUp);
        }

        // ====== 設定読み込み・監視 ======
        private void ApplySettingsFromPreferences()
        {
            var S = Properties.Settings.Default;

            // --- Background mask ---
            _bgColor = S.CaptureBackgroundColor;
            _bgAlphaPercent = Clamp01to100(S.CaptureBackgroundAlphaPercent);

            // --- Hover highlight ---
            _hoverColor = S.HoverHighlightColor;
            _hoverAlphaPercent = Clamp01to100(S.HoverHighlightAlphaPercent);
            _hoverThicknessPx = Math.Max(1, S.HoverHighlightThickness);

            // --- Snap grid / tolerance（存在すれば上書き） ---
            _snapGridPx = snapGrid;
            _edgeSnapTolPx = edgeSnapTol;
            try { object v = S["SnapGridPx"]; if (v != null) _snapGridPx = Math.Max(5, Convert.ToInt32(v)); } catch { }
            try { object v = S["EdgeSnapTolerancePx"]; if (v != null) _edgeSnapTolPx = Math.Max(1, Convert.ToInt32(v)); } catch { }

            // 既存フィールドへ反映
            snapGrid = _snapGridPx;
            edgeSnapTol = _edgeSnapTolPx;
        }

        private void HookSettingsChanged()
        {
            Properties.Settings.Default.PropertyChanged += (s, e) =>
            {
                // 必要なキーで再読込
                if (e.PropertyName == nameof(Properties.Settings.Default.CaptureBackgroundColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.CaptureBackgroundAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightThickness) ||
                    e.PropertyName == "SnapGridPx" || e.PropertyName == "EdgeSnapTolerancePx" ||
                    e.PropertyName == nameof(Properties.Settings.Default.ScreenGuideEnabled))
                {
                    ApplySettingsFromPreferences();
                    if (bmp != null) pictureBox1.Refresh();
                }
            };
        }

        // ====== 入力状態（修飾キー） ======
        private bool IsAltDown() => (ModifierKeys & Keys.Alt) == Keys.Alt;

        private bool IsGuidesEnabled()
        {
            // Alt で反転（既定は Settings）
            bool baseVal = Properties.Settings.Default.ScreenGuideEnabled;
            if ((ModifierKeys & Keys.Alt) != 0) return !baseVal;
            return baseVal;
        }

        private bool IsSnapEnabled()
        {
            // Ctrl で反転（既定は Settings に SnapToEdgesEnabled があれば使用、無ければ false）
            bool baseVal = false;
            try
            {
                object v = Properties.Settings.Default["SnapToEdgesEnabled"];
                if (v != null) baseVal = Convert.ToBoolean(v);
            }
            catch { }
            if ((ModifierKeys & Keys.Control) != 0) return !baseVal;
            return baseVal;
        }

        private bool IsSquareConstraint() { return (ModifierKeys & Keys.Shift) != 0; }

        // DPI スケールした線幅
        private float HoverThicknessDpi()
        {
            float scaled = _hoverThicknessPx * (currentDpi / 96f);
            if (scaled < 1f) scaled = 1f;
            return scaled;
        }

        // ====== スナップ ======
        private Point SnapPoint(Point p)
        {
            if (bmp == null) return p;

            // 1) グリッドにスナップ（四捨五入）
            int sx = (int)Math.Round(p.X / (double)snapGrid) * snapGrid;
            int sy = (int)Math.Round(p.Y / (double)snapGrid) * snapGrid;
            p = new Point(sx, sy);

            // 2) 画像端にスナップ（許容距離内なら吸着）
            int W = bmp.Width, H = bmp.Height;
            if (Math.Abs(p.X - 0) <= edgeSnapTol) p.X = 0;
            if (Math.Abs(p.Y - 0) <= edgeSnapTol) p.Y = 0;
            if (Math.Abs(p.X - (W - 1)) <= edgeSnapTol) p.X = W - 1;
            if (Math.Abs(p.Y - (H - 1)) <= edgeSnapTol) p.Y = H - 1;

            return p;
        }

        // ====== 画像オープン関連（既存） ======
        public void openImage(String path = null)
        {
            try
            {
                if (path != null)
                {
                    SnapWindow sw = new SnapWindow(this.ma);
                    sw.StartPosition = FormStartPosition.CenterScreen;
                    sw.setImageFromPath(path);
                    sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                    captureArray.Add(sw);
                    sw.Show();
                    return;
                }
                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                openFileDialog1.Title = "Open Image";
                openFileDialog1.Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.ValidateNames = false;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    openFileDialog1.Dispose();

                    SnapWindow sw = new SnapWindow(this.ma);
                    sw.StartPosition = FormStartPosition.CenterScreen;
                    sw.setImageFromPath(openFileDialog1.FileName);
                    sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                    captureArray.Add(sw);
                    sw.Show();
                }
            }
            catch { }
        }

        public SnapWindow getSW(int i)
        {
            return (SnapWindow)captureArray[i];
        }

        public void openImageFromHistory(ToolStripMenuItem item)
        {
            try
            {
                SnapWindow sw = new SnapWindow(this.ma);
                sw.StartPosition = FormStartPosition.CenterScreen;

                var src = (item.Tag as SnapWindow)?.main_image;
                if (src == null) return;
                sw.setImageFromBMP((Bitmap)src.Clone(), LoadMethod.History);
                sw.Text = (item.Tag as SnapWindow).Text;

                sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                captureArray.Add(sw);
                sw.Show();
            }
            catch { }
        }

        // ====== 画面表示 ======
        public void showScreenAll()
        {
            Log.Info("Screen capture started", "Capture");
            this.Opacity = 1.0;
            this.isOpen = true;
            DisposeCaptureSurface();

            this.StartPosition = FormStartPosition.Manual;

            var vs = GetVirtualScreenPhysical();
            x = vs.X; y = vs.Y; w = vs.W; h = vs.H;
            Log.Debug($"Virtual screen (physical): {x},{y} {w}x{h}", "DPI");

            this.SetBounds(x, y, w, h);
            bmp = new Bitmap(w, h);
            using (g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(new Point(x, y), new Point(0, 0), bmp.Size);
            }
            baseBmp = (Bitmap)bmp.Clone();

            using (g = Graphics.FromImage(bmp))
            using (var mask = MakeBackgroundMaskBrush())
                g.FillRectangle(mask, new Rectangle(0, 0, w, h));

            pictureBox1.SetBounds(0, 0, w, h);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = bmp;
            pictureBox1.Refresh();
            this.TopLevel = true;
            this.Show();
        }
        public void showScreenOCR()
        {
            Log.Info("set OCR mode", "Capture");
            this._ocr_mode = true;
            showScreenAll();
        }
        public void showScreenLive()
        {
            Log.Info("set Live mode", "Capture");
            this._live_mode = true;
            showScreenAll();
        }
        // 幅・高さ(px) を指定して固定サイズキャプチャ開始
        public void ShowScreenFixed(int width, int height)
        {
            Log.Info("set Fixed mode", "Capture");
            if (width < 10 || height < 10) return;

            _fixed_mode = true;
            _fixedSizePx = new Size(width, height);
            _fixedPresetIndex = FindNearestPresetIndex(_fixedSizePx);

            // 既存：仮想スクリーン全域のオーバーレイ表示
            showScreenAll();

            // キーを確実に受ける
            try { this.KeyPreview = true; } catch { }
            try { this.Activate(); this.Focus(); this.BringToFront(); } catch { }

            // 起動直後にも一度描画しておく
            RenderFixedOverlay();
        }

        private Point GetCurrentCursorClientPoint()
        {
            POINT p; GetCursorPos(out p);
            // x,y は「仮想スクリーンの左上スクリーン座標」を表す既存フィールドを想定
            return new Point(p.X - x, p.Y - y);
        }

        private void ApplyFixedPreset(int idx, bool swapToPortrait)
        {
            if (idx < 0 || idx >= FIXED_PRESETS.Length) return;
            _fixedPresetIndex = idx;
            var sz = new Size(FIXED_PRESETS[idx].W, FIXED_PRESETS[idx].H);
            if (swapToPortrait) sz = new Size(sz.Height, sz.Width);
            _fixedSizePx = sz;

            Log.Debug($"[Fixed] preset -> #{idx + 1} {_fixedSizePx.Width}x{_fixedSizePx.Height}", "Capture");
            RenderFixedOverlay(); // ← ここがポイント
        }

        private void CyclePreset(int dir)
        {
            if (FIXED_PRESETS.Length == 0) return;
            if (_fixedPresetIndex < 0) _fixedPresetIndex = 0;
            _fixedPresetIndex = (_fixedPresetIndex + dir + FIXED_PRESETS.Length) % FIXED_PRESETS.Length;
            ApplyFixedPreset(_fixedPresetIndex, false);
        }

        private void NudgeSize(int dw, int dh)
        {
            int w = Math.Max(10, _fixedSizePx.Width  + dw);
            int h = Math.Max(10, _fixedSizePx.Height + dh);
            _fixedSizePx = new Size(w, h);
            RenderFixedOverlay(); // ← ここがポイント
        }

        private static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min; if (v > max) return max; return v;
        }

        public void ShowScreenFixedWithPrompt()
        {
            int w, h, presetIdx; bool remember;
            if (!Kiritori.Views.Capture.FixedSizePresetDialog.TryPrompt(this.ma, out w, out h, out presetIdx, out remember))
                return;

            // ダイアログを消した直後の合成を確実に完了させる
            FlushComposition();

            // 既存フローでオーバーレイ表示 → 固定サイズ追随
            ShowScreenFixed(w, h);
        }

        private int FindNearestPresetIndex(Size sz)
        {
            int best = FIXED_PRESETS.Length - 1; // デフォはカスタム
            long bestScore = long.MaxValue;
            for (int i = 0; i < FIXED_PRESETS.Length - 1; i++)
            {
                long dx = FIXED_PRESETS[i].W - sz.Width;
                long dy = FIXED_PRESETS[i].H - sz.Height;
                long score = dx * dx + dy * dy;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
        // 固定モードを開始（ダイアログOK後に呼ばれる）
        private void StartFixedMode(int width, int height)
        {
            _fixed_mode = true;
            _fixedSizePx = new Size(width, height);
            _fixedPresetIndex = FindNearestPresetIndex(_fixedSizePx);

            // 起動直後の描画（マウス未移動でも即反映）
            RenderFixedOverlay();

            // 誤クリック防止の最短アーム時間（100–150ms 推奨）
            _fixed_armTick = Environment.TickCount + 150;
        }

        private Rectangle GetFixedRectCentered(Point center)
        {
            int w = _fixedSizePx.Width, h = _fixedSizePx.Height;
            int x0 = center.X - w / 2, y0 = center.Y - h / 2;
            if (baseBmp != null)
            {
                x0 = Math.Max(0, Math.Min(x0, baseBmp.Width  - w));
                y0 = Math.Max(0, Math.Min(y0, baseBmp.Height - h));
            }
            return new Rectangle(x0, y0, w, h);
        }

        private void RenderFixedOverlay()
        {
            if (!_fixed_mode || baseBmp == null || bmp == null) return;

            Point cur = GetCurrentCursorClientPoint();
            if (IsSnapEnabled()) cur = SnapPoint(cur);

            var rect = GetFixedRectCentered(cur);

            using (var g2 = Graphics.FromImage(bmp))
            {
                g2.DrawImage(baseBmp, Point.Empty);
                using (var mask = MakeBackgroundMaskBrush())
                using (var outside = new Region(new Rectangle(0, 0, bmp.Width, bmp.Height)))
                {
                    outside.Exclude(rect);
                    g2.FillRegion(mask, outside);
                }
                using (var pen = MakeGuidePen())
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g2.DrawRectangle(pen, rect);
                }
                if (IsGuidesEnabled())
                {
                    DrawLabel(g2, rect.Width + " x " + rect.Height, new Point(rect.Left, Math.Max(0, rect.Top - 28)));
                    DrawCrosshair(g2, cur);
                    DrawLabel(g2, (rect.X + x) + ", " + (rect.Y + y), new Point(rect.Left + 8, rect.Top + 8));
                }
            }
            pictureBox1.Invalidate();
            pictureBox1.Update();
        }

        // ====== 選択操作 ======
        private Point startPoint;
        private Point startPointPhys;
        private Point hoverPoint = Point.Empty;
        private Rectangle rc;
        private Boolean isPressed = false;

        private void ScreenWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (_fixed_mode && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (Environment.TickCount < _fixed_armTick) return; // 早押しガード
                Point cur = new Point(e.X, e.Y);
                if (IsSnapEnabled()) cur = SnapPoint(cur);
                var rect = GetFixedRectCentered(cur);
                TryCaptureAndOpen(rect);
                return;
            }
            else if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                startPoint = new Point(e.X, e.Y);
                startPointPhys = new Point(e.X + x, e.Y + y);
                isPressed = true;
                Log.Debug($"MouseDown at {startPoint} (physical: {startPointPhys})", "Capture");
            }
        }

        private void ScreenWindow_MouseMove(object sender, MouseEventArgs e)
        {
            hoverPoint = new Point(e.X, e.Y);
            if (_fixed_mode) { RenderFixedOverlay(); return; }

            if (!isPressed)
            {
                if (IsGuidesEnabled())
                {
                    RedrawHoverOnly();
                }
                return;
            }

            Point cur = new Point(e.X, e.Y);
            if (IsSnapEnabled())
            {
                cur = SnapPoint(cur);
            }

            int dx = cur.X - startPoint.X;
            int dy = cur.Y - startPoint.Y;

            if (!IsSquareConstraint())
            {
                // 通常
                rc.X = Math.Min(startPoint.X, cur.X);
                rc.Y = Math.Min(startPoint.Y, cur.Y);
                rc.Width = Math.Abs(dx);
                rc.Height = Math.Abs(dy);
            }
            else
            {
                // Shift：正方形制約
                // カーソル方向（dx, dy の符号）に応じて起点を動かし、辺は Max(|dx|, |dy|)
                int side = Math.Max(Math.Abs(dx), Math.Abs(dy));

                int x0 = (dx >= 0) ? startPoint.X : startPoint.X - side;
                int y0 = (dy >= 0) ? startPoint.Y : startPoint.Y - side;

                rc.X = x0;
                rc.Y = y0;
                rc.Width = side;
                rc.Height = side;
            }

            using (g = Graphics.FromImage(bmp))
            {
                // 原本から描き直し
                g.DrawImage(baseBmp, Point.Empty);

                // 外側だけに背景マスク
                using (var mask = MakeBackgroundMaskBrush())
                using (var outside = new Region(new Rectangle(0, 0, bmp.Width, bmp.Height)))
                {
                    outside.Exclude(rc);
                    g.FillRegion(mask, outside);
                }

                // 選択枠（設定色＋太さ、破線）
                using (var pen = MakeGuidePen())
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(pen, rc);
                }

                if (IsGuidesEnabled())
                {
                    // 1) 選択サイズ
                    DrawLabel(g, string.Format("{0} x {1}", rc.Width, rc.Height), new Point(e.X + 12, e.Y + 12));

                    // 2) 開始点の十字マーカー
                    DrawCrosshair(g, startPoint);

                    // 3) 開始点の物理座標
                    DrawLabel(g, string.Format("{0}, {1}", startPointPhys.X, startPointPhys.Y),
                            new Point(startPoint.X + 10, startPoint.Y + 10));

                    using (var pen = MakeGuidePen())
                    {
                        g.DrawLine(pen, 0, cur.Y, bmp.Width, cur.Y);
                        g.DrawLine(pen, cur.X, 0, cur.X, bmp.Height);
                    }
                }

                int nowTick = Environment.TickCount;
                if (nowTick - _lastPaintTick >= 15)
                {   // ~66fps
                    pictureBox1.Invalidate();
                    pictureBox1.Update();               // この瞬間だけ同期描画
                    _lastPaintTick = nowTick;
                }
                // pictureBox1.Refresh();
            }
        }

        private void ScreenWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isPressed) return;
            isPressed = false;

            try
            {
                if (rc.Width > 0 && rc.Height > 0 && baseBmp != null)
                {
                    // 1) 安全クロップ（baseBmp の範囲に収める）
                    Rectangle crop = Rectangle.Intersect(rc, new Rectangle(0, 0, baseBmp.Width, baseBmp.Height));
                    Log.Info($"crop={crop}", "Capture");
                    if (crop.Width > 0 && crop.Height > 0)
                    {
                        // 透明化：画面上に残らないように（自己キャプチャ抑止にも有効）
                        this.Opacity = 0.0;

                        if (_ocr_mode)
                        {
                            // CloseScreen() より前にクロップ済み画像を作る
                            Bitmap sub = null;
                            try
                            {
                                sub = baseBmp?.Clone(crop, baseBmp.PixelFormat);
                            }
                            catch
                            {
                                // clone失敗は sub=null のまま後段へ
                            }

                            // もう画面は閉じてOK（baseBmpはこの中で破棄される想定）
                            this.CloseScreen();

                            // UIスレッドでOCRへ。sub の破棄は OCR 側で実施する
                            this.BeginInvoke(new Action(async () =>
                            {
                                try
                                {
                                    if (sub == null)
                                    {
                                        Log.Debug("sub image is null.", "OCR");
                                        NotifyOcrError();
                                        return;
                                    }
                                    await DoOcrFromSubImageAsync(sub); // 別実装に委譲
                                }
                                finally
                                {
                                    _ocr_mode = false;
                                }
                            }));
                            return;
                        }
                        else if (_live_mode)
                        {
                            // ======== Live プレビュー（リアルタイム） =========
                            // crop は baseBmp 内の相対座標なので、必ずキャプチャ原点 (x,y) を加算して
                            // 「スクリーン上の論理座標」に直すこと！
                            // ここで物理pxに変換しないこと（キャプチャ側だけで物理化する設計）

                            // 幅・高さを偶数に丸める
                            // int evenWidth = crop.Width & ~1;
                            // int evenHeight = crop.Height & ~1;
                            // var rPhysical = new Rectangle(
                            //     crop.X + x,
                            //     crop.Y + y,
                            //     evenWidth,
                            //     evenHeight
                            // );
                            // var rScreenLogical = Kiritori.Views.LiveCapture.GdiCaptureBackend.DpiUtil.PhysicalToLogical(rPhysical);
                            // var rPhysical = new Rectangle(crop.X + x, crop.Y + y, crop.Width, crop.Height);
                            // var rScreenLogical = Kiritori.Helpers.DpiUtil.PhysicalToLogical(rPhysical);
                            // var desired = new Point(crop.X + x, crop.Y + y);
                            var srcPhys = new Rectangle(
                                crop.X + x,
                                crop.Y + y,
                                (crop.Width  & ~1),
                                (crop.Height & ~1)
                            );
                            var srcLog = Kiritori.Helpers.DpiUtil.PhysicalToLogical(srcPhys);
                            Log.Debug($"[LivePreview] [Select] rPhys={srcPhys}  rLog={srcLog}", "LivePreview");
                            // var rPhys = new Rectangle(crop.X + x, crop.Y + y, crop.Width, crop.Height);
                            // var rLog  = DpiUtil.PhysicalToLogical(rPhys); // ←重要：論理に戻す
                            // Log.Debug($"[Select] rPhys={rPhys}  rLog={rLog}", "LivePreview");

                            // ここは"論理px"を渡す
                            var liveWin = new LivePreviewWindow {
                                CaptureRect = srcLog,
                                SourceRectPhysical = srcPhys,
                                StartPosition = FormStartPosition.Manual,
                                MainApp = this.ma,
                                AutoTopMost = true,
                            };
                            liveWin.Show();

                            // クライアント左上が rScreenLogical.Left/Top に一致するように、
                            // DPI と非クライアント（枠/タイトルバー）を API で加味して配置。
                            WindowAligner.MoveFormToMatchClient(liveWin, srcLog, topMost: true);

                            // 表示（TopMost は MoveFormToMatchClient 内でも設定している）
                            liveWin.Show();

                            // この選択ウィンドウは役目を終えたので閉じる
                            this.CloseScreen();

                            // Live モードはここで終了
                            _live_mode = false;
                            return;
                        }
                        else
                        {
                            // ======== 通常の静止画スナップ（既存動作） ========
                            // 2) 画面上の配置位置（論理スクリーン座標）。SnapWindow でも (x,y) を足している
                            var desired = new Point(crop.X + x, crop.Y + y);

                            var sw = new SnapWindow(this.ma)
                            {
                                StartPosition = FormStartPosition.Manual
                            };

                            // CloseScreen() より前に baseBmp を使い切る or Clone して渡す
                            sw.CaptureFromBitmap(baseBmp, crop, desired, LoadMethod.Capture);

                            // 4) 原本は不要ならここで閉じる（baseBmp 破棄など）
                            this.CloseScreen();

                            sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                            captureArray.Add(sw);
                            notifyCaptured();
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("[ScreenWindow_MouseUp] " + ex);
            }
            finally
            {
                // フォールバック的に、最後は閉じておく
                this.CloseScreen();
            }
        }


        private static DateTime _lastToastAt = DateTime.MinValue;

        private void notifyCaptured()
        {
            // OCRモード時はキャプチャ完了トーストを出さない
            if (_ocr_mode) return;
            if (!Properties.Settings.Default.ShowNotificationOnCapture) return;
            // 直前の連打を抑止（例: 500ms 以内は捨てる）
            var now = DateTime.Now;
            if ((now - _lastToastAt).TotalMilliseconds < 500) return;
            _lastToastAt = now;

            try
            {
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "open")
                    .AddText("Kiritori")
                    .AddAudio(new Uri("ms-winsoundevent:Notification.IM"))
                    .AddText(SR.T("Toast.Captured", "Captured"));

                if (PackagedHelper.IsPackaged())
                {
                    // パッケージアプリ: そのまま送る
                    builder.Show(t =>
                    {
                        t.Tag = "kiritori-capture";
                        t.Group = "kiritori";
                    });
                }
                else
                {
                    // 非パッケージの場合：ToastNotificationManager（Compatじゃない方）で AUMID を指定
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new ToastNotification(xml) { Tag = "kiritori-capture", Group = "kiritori" };

                    // ここで AUMID（Startメニューのショートカットと一致するID）を指定
                    Log.Debug("Show() called: " + NotificationService.GetAppAumid(), "Toast");
                    ToastNotificationManager.CreateToastNotifier(NotificationService.GetAppAumid()).Show(toast);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Show() failed: " + ex, "Toast");
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                main?.NotifyIcon?.ShowBalloonTip(2500, "Kiritori", "キャプチャを保存しました", ToolTipIcon.None);
            }
        }


        private Point _hoverPoint;
        private bool _showHover = false;

        private void RedrawHoverOnly()
        {
            if (!_showHover || pictureBox1.Image == null) return;

            var old = _hoverPoint;
            _hoverPoint = IsAltDown() ? SnapPoint(hoverPoint) : hoverPoint;

            // 影響範囲だけ再描画（ライン太さ等を考慮して少し広めに）
            int pad = 8;
            var r1 = new Rectangle(0, _hoverPoint.Y - pad, pictureBox1.Width, pad * 2);
            var r2 = new Rectangle(_hoverPoint.X - pad, 0, pad * 2, pictureBox1.Height);
            var r3 = new Rectangle(0, old.Y - pad, pictureBox1.Width, pad * 2);
            var r4 = new Rectangle(old.X - pad, 0, pad * 2, pictureBox1.Height);

            pictureBox1.Invalidate(Rectangle.Union(Rectangle.Union(r1, r2), Rectangle.Union(r3, r4)));
            // 必要なら「全面マスク」を描くために全体 Invalidate:
            // pictureBox1.Invalidate();
        }
        static bool IsElevated()
        {
            var id = WindowsIdentity.GetCurrent();
            try
            {
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            finally
            {
                id?.Dispose();
            }
        }
        private void TryCaptureAndOpen(Rectangle cropLocal)
        {
            try
            {
                if (baseBmp == null || cropLocal.Width <= 0 || cropLocal.Height <= 0)
                    return;

                // 安全クロップ
                var crop = Rectangle.Intersect(cropLocal, new Rectangle(0, 0, baseBmp.Width, baseBmp.Height));
                if (crop.Width <= 0 || crop.Height <= 0) return;

                this.Opacity = 0.0; // 自己写り抑止

                // 既存の通常スナップと同じ：表示位置はスクリーン座標 (x,y) 加算
                var desired = new Point(crop.X + x, crop.Y + y);

                var sw = new SnapWindow(this.ma) { StartPosition = FormStartPosition.Manual };
                sw.CaptureFromBitmap(baseBmp, crop, desired, LoadMethod.Capture); // 既存メソッド
                this.CloseScreen(); // 既存の後始末（baseBmp破棄を含む）

                sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                captureArray.Add(sw);
                notifyCaptured(); // 既存トースト
            }
            catch (Exception ex)
            {
                Log.Debug("[FixedCapture] " + ex, "Capture");
                this.CloseScreen();
            }
            finally
            {
                _fixed_mode = false;
                _fixedSizePx = Size.Empty;
            }
        }


        // ====== 複数ウィンドウ管理（既存） ======
        public void hideWindows()
        {
            foreach (SnapWindow sw in captureArray)
            {
                sw.minimizeWindow();
            }
        }
        public void showWindows()
        {
            foreach (SnapWindow sw in captureArray)
            {
                sw.showWindow();
            }
        }
        public void closeWindows()
        {
            var snapshot = captureArray.Cast<SnapWindow>().ToArray();
            foreach (var sw in snapshot)
            {
                try
                {
                    if (sw != null && !sw.IsDisposed)
                        sw.closeWindow();
                }
                catch { }
            }
        }

        private void CloseScreen()
        {
            _fixed_mode = false;
            _fixedSizePx = Size.Empty;
            this.isOpen = false;
            DisposeCaptureSurface();
            this.Hide();
            try { ma?.ReleaseScreenGate(); } catch { }
        }

        void SW_FormClosing(object sender, FormClosingEventArgs e)
        {
            captureArray.Remove(sender);
        }

        // ====== キー処理 ======
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var keyOnly = keyData & Keys.KeyCode;

            if (_fixed_mode)
            {
                // 1..6 でプリセット（Shiftで縦横入れ替え）
                if (keyOnly >= Keys.D1 && keyOnly <= Keys.D6)
                {
                    int idx = (int)keyOnly - (int)Keys.D1;
                    bool swap = (keyData & Keys.Shift) == Keys.Shift;
                    ApplyFixedPreset(idx, swap);
                    return true;
                }
                // [ / ] or ← / → で前後プリセット
                if (keyOnly == Keys.OemOpenBrackets || keyOnly == Keys.Left)  { CyclePreset(-1); return true; }
                if (keyOnly == Keys.OemCloseBrackets || keyOnly == Keys.Right) { CyclePreset(+1); return true; }

                // R で縦横スワップ
                if (keyOnly == Keys.R)
                {
                    _fixedSizePx = new Size(_fixedSizePx.Height, _fixedSizePx.Width);
                    RenderFixedOverlay();
                    return true;
                }

                // + / - で 10px 微調整
                if (keyOnly == Keys.Oemplus || keyOnly == Keys.Add)       { NudgeSize(+10, +10); return true; }
                if (keyOnly == Keys.OemMinus || keyOnly == Keys.Subtract) { NudgeSize(-10, -10); return true; }
            }
            switch ((int)keyData)
            {
                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    _fixed_mode = false;
                    _fixedSizePx = Size.Empty;
                    _fixedPresetIndex = -1;
                    this.CloseScreen();
                    break;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        // ====== DPI/スクリーン ======
        [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hWnd); // Win10+
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi); // Win10+
        private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

        private struct PhysVS { public int X, Y, W, H; }
        private PhysVS GetVirtualScreenPhysical()
        {
            try
            {
                int dpi = GetDpiForWindow(this.Handle); // 例: 96,120,144...
                int x = GetSystemMetricsForDpi(SM_XVIRTUALSCREEN, (uint)dpi);
                int y = GetSystemMetricsForDpi(SM_YVIRTUALSCREEN, (uint)dpi);
                int cx = GetSystemMetricsForDpi(SM_CXVIRTUALSCREEN, (uint)dpi);
                int cy = GetSystemMetricsForDpi(SM_CYVIRTUALSCREEN, (uint)dpi);
                return new PhysVS { X = x, Y = y, W = cx, H = cy };
            }
            catch
            {
                var vs = SystemInformation.VirtualScreen;
                return new PhysVS { X = vs.X, Y = vs.Y, W = vs.Width, H = vs.Height };
            }
        }

        /// <summary>
        /// DPI 変更時にフォント等を作り直す
        /// </summary>
        public void OnHostDpiChanged(int newDpi)
        {
            if (newDpi <= 0) newDpi = 96;
            if (newDpi == currentDpi) return;
            currentDpi = newDpi;

            fnt?.Dispose();
            int basePt = 10; // 96dpi で 10pt
            float scaledPt = basePt * (currentDpi / 96f);
            fnt = new Font("Segoe UI", scaledPt, GraphicsUnit.Point);
        }

        // ====== 描画ユーティリティ ======
        private void DrawLabel(Graphics g, string text, Point anchor, int pad = 4)
        {
            var sz = g.MeasureString(text, fnt);
            var rect = new RectangleF(anchor.X, anchor.Y, sz.Width + pad * 2, sz.Height + pad * 2);

            using (var bg = new SolidBrush(Color.FromArgb(180, Color.White)))
            using (var pen = new Pen(Color.Black))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, Rectangle.Round(rect));
            }

            g.DrawString(text, fnt, Brushes.Black, rect.X + pad, rect.Y + pad);
        }

        private void DrawCrosshair(Graphics g, Point p)
        {
            var baseColor = GetOppositeColor(_bgColor, _bgAlphaPercent);
            using (var pen = new Pen(baseColor, 1))
            {
                g.DrawLine(pen, p.X - 6, p.Y, p.X + 6, p.Y);
                g.DrawLine(pen, p.X, p.Y - 6, p.X, p.Y + 6);
            }
        }

        private SolidBrush MakeBackgroundMaskBrush()
        {
            int a = (int)Math.Round(_bgAlphaPercent * 2.55); // 0..100 → 0..255
            var c = Color.FromArgb(a, _bgColor);
            return new SolidBrush(c);
        }

        private Pen MakeGuidePen()
        {
            var baseColor = GetOppositeColor(_bgColor, _bgAlphaPercent);
            var c = Color.FromArgb(120, baseColor); // 半透明で薄く
            var pen = new Pen(c, 1f);
            pen.DashStyle = DashStyle.Solid;
            return pen;
        }
        private Color GetOppositeColor(Color bg, int alphaPercent)
        {
            // Transparent のときは黒を返す
            if (alphaPercent == 0)
                return Color.Black;

            // 明るさ判定（輝度で計算）
            double brightness = bg.GetBrightness(); // 0=黒, 1=白
            if (brightness < 0.5)
            {
                // 背景が Dark 系（黒〜グレー） → Light 系に
                return Color.White;
            }
            else
            {
                // 背景が Light 系（白〜グレー） → Dark 系に
                return Color.Black;
            }
        }


        private static int Clamp01to100(int v) { return Math.Max(0, Math.Min(100, v)); }

        // ====== 終了処理 ======
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { ma?.ReleaseScreenGate(); } catch { }
            DisposeCaptureSurface();

            fnt?.Dispose();
            fnt = null;
            if (this.Icon != null)
            {
                this.Icon.Dispose();
                this.Icon = null;
            }

            base.OnFormClosed(e);
        }

        private void DisposeCaptureSurface()
        {
            var pic = pictureBox1 != null ? pictureBox1.Image : null;
            if (pictureBox1 != null) pictureBox1.Image = null;

            if (bmp != null && !ReferenceEquals(bmp, pic)) { bmp.Dispose(); }
            if (baseBmp != null && baseBmp != bmp && baseBmp != pic) { baseBmp.Dispose(); }
            if (pic != null) { pic.Dispose(); }

            bmp = null;
            baseBmp = null;
            g = null;
        }


        // // ====== OCR ユーティリティ ======
        // private async System.Threading.Tasks.Task DoOcrFromCropAsync(Bitmap baseImage, Rectangle crop)
        // {
        //     try
        //     {
        //         using (var sub = baseImage.Clone(crop, baseImage.PixelFormat))
        //         {
        //             // あなたの OCR 実装に合わせて参照名を調整してください
        //             // 例: Kiritori.Services.Ocr.WindowsOcrProvider / IOcrProvider / OcrOptions
        //             var provider = new Kiritori.Services.Ocr.WindowsOcrProvider();
        //             string lang = Properties.Settings.Default["OcrLanguage"] as string;
        //             if (string.IsNullOrWhiteSpace(lang))
        //             {
        //                 string ui = Properties.Settings.Default.UICulture;
        //                 lang = !string.IsNullOrEmpty(ui) ? ui : "ja";
        //             }

        //             var ocrService = new OcrService();
        //             var provider   = ocrService.Get(null);
        //             var opt = new Kiritori.Services.Ocr.OcrOptions
        //             {
        //                 LanguageTag = lang,
        //                 Preprocess = true,
        //                 CopyToClipboard = true
        //             };

        //             var result = await provider.RecognizeAsync(sub, opt);

        //             // OcrResult のプロパティ名に合わせて調整
        //             // Windows.Media.Ocr.OcrResult なら .Text
        //             // 自作 OcrResult でも通常は Text/PlainText いずれか
        //             string text = null;
        //             try
        //             {
        //                 // 最初に Text を試す
        //                 var prop = result.GetType().GetProperty("Text");
        //                 if (prop != null) text = prop.GetValue(result, null) as string;
        //             }
        //             catch { /* ignore */ }

        //             if (string.IsNullOrEmpty(text))
        //             {
        //                 // 次善: PlainText を試す
        //                 try
        //                 {
        //                     var prop = result.GetType().GetProperty("PlainText");
        //                     if (prop != null) text = prop.GetValue(result, null) as string;
        //                 }
        //                 catch { /* ignore */ }
        //             }

        //             if (text == null) text = result != null ? result.ToString() : string.Empty;
        //             if (text == null) text = string.Empty;

        //             // クリップボードへ
        //             TrySetClipboardText(text);

        //             // トースト
        //             NotifyOcr(text);
        //         }
        //     }
        //     catch (System.Exception ex)
        //     {
        //         Log.Trace("[OCR] failed: " + ex, "OCR");
        //         NotifyOcrError();
        //     }
        // }

        /// <summary>
        /// STA 前提の UI スレッドから呼ばれる想定。例外は握りつぶす。
        /// </summary>
        private void TrySetClipboardText(string text)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                    Clipboard.SetText(text);
            }
            catch { /* クリップボード競合などは無視 */ }
        }

        private void NotifyOcr(string text)
        {
            if (!Properties.Settings.Default.ShowNotificationOnOcr) return;

            // 先頭 ~80文字をプレビューに
            string preview = text ?? "";
            if (preview.Length > 80) preview = preview.Substring(0, 80) + "…";

            try
            {
                var builder = new CommunityToolkit.WinUI.Notifications.ToastContentBuilder()
                    .AddArgument("action", "open")
                    .AddText("Kiritori - OCR")
                    .AddAudio(new System.Uri("ms-winsoundevent:Notification.IM"))
                    .AddText(SR.T("Toast.OcrCopied", "OCR text copied to clipboard"))
                    .AddText(preview);

                if (Kiritori.Helpers.PackagedHelper.IsPackaged())
                {
                    builder.Show(t => { t.Tag = "kiritori-ocr"; t.Group = "kiritori"; });
                }
                else
                {
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new Windows.UI.Notifications.ToastNotification(xml) { Tag = "kiritori-ocr", Group = "kiritori" };
                    Windows.UI.Notifications.ToastNotificationManager
                        .CreateToastNotifier(Kiritori.Services.Notifications.NotificationService.GetAppAumid())
                        .Show(toast);
                }
            }
            catch
            {
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                if (main != null && main.NotifyIcon != null)
                    main.NotifyIcon.ShowBalloonTip(1000, "Kiritori - OCR", SR.T("Toast.OcrCopied", "OCR text copied to clipboard"), ToolTipIcon.None);
            }
        }

        private void NotifyOcrError()
        {
            try
            {
                var builder = new CommunityToolkit.WinUI.Notifications.ToastContentBuilder()
                    .AddArgument("action", "open")
                    .AddText("Kiritori - OCR")
                    .AddText(SR.T("Toast.OcrFailed", "OCR failed"));
                if (Kiritori.Helpers.PackagedHelper.IsPackaged())
                {
                    builder.Show(t => { t.Tag = "kiritori-ocr"; t.Group = "kiritori"; });
                }
                else
                {
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new Windows.UI.Notifications.ToastNotification(xml) { Tag = "kiritori-ocr", Group = "kiritori" };
                    Windows.UI.Notifications.ToastNotificationManager
                        .CreateToastNotifier(Kiritori.Services.Notifications.NotificationService.GetAppAumid())
                        .Show(toast);
                }
            }
            catch
            {
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                if (main != null && main.NotifyIcon != null)
                    main.NotifyIcon.ShowBalloonTip(2500, "Kiritori", SR.T("Toast.OcrFailed", "OCR failed"), ToolTipIcon.Error);
            }
        }
        private async System.Threading.Tasks.Task DoOcrFromSubImageAsync(Bitmap sub)
        {
            try
            {
                using (sub) // ここで確実に破棄（Facade内部でクローンする前提）
                {
                    var text = await OcrFacade.RunAsync(
                        sub,
                        copyToClipboard: true,
                        preprocess: true
                    ).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(text))
                    {
                        NotifyOcrError();
                        return;
                    }
                    NotifyOcr(text);
                }
            }
            catch (System.Exception ex)
            {
                Log.Trace("[OCR] failed: " + ex, "OCR");
                NotifyOcrError();
            }
        }
    }
    internal sealed class FixedSizeInputDialog : Form
    {
        private NumericUpDown _w = new NumericUpDown();
        private NumericUpDown _h = new NumericUpDown();
        private Button _ok = new Button();
        private Button _cancel = new Button();

        public int OutW => (int)_w.Value;
        public int OutH => (int)_h.Value;

        public FixedSizeInputDialog()
        {
            this.Text = "Fixed-size Capture";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f, GraphicsUnit.Point);
            this.ClientSize = new Size(260, 110);

            var lblW = new Label { Left = 12, Top = 16, Width = 20, Text = "W" };
            _w.SetBounds(40, 12, 80, 24); _w.Minimum = 10; _w.Maximum = 9999; _w.Value = 640;
            var lblH = new Label { Left = 132, Top = 16, Width = 20, Text = "H" };
            _h.SetBounds(160, 12, 80, 24); _h.Minimum = 10; _h.Maximum = 9999; _h.Value = 360;

            _ok.Text = "OK"; _ok.SetBounds(84, 64, 70, 28); _ok.DialogResult = DialogResult.OK;
            _cancel.Text = "Cancel"; _cancel.SetBounds(164, 64, 76, 28); _cancel.DialogResult = DialogResult.Cancel;

            this.AcceptButton = _ok; this.CancelButton = _cancel;
            this.Controls.AddRange(new Control[] { lblW, _w, lblH, _h, _ok, _cancel });

            _ok.Click += (s, e) =>
            {
                try
                {
                    // フェードが走る前に即不可視化
                    this.Opacity = 0;     // 合成から即外す
                    this.Hide();          // ShowDialog の終了を待たずに画面から消す
                }
                catch { /* ignore */ }

                this.DialogResult = DialogResult.OK; // これで ShowDialog が戻る
            };

            _cancel.Click += (s, e) =>
            {
                try { this.Opacity = 0; this.Hide(); } catch { }
                this.DialogResult = DialogResult.Cancel;
            };
        }

        public static bool TryPrompt(IWin32Window owner, out int w, out int h)
        {
            using (var dlg = new FixedSizeInputDialog())
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    w = dlg.OutW; h = dlg.OutH; return true;
                }
            }
            w = h = 0; return false;
        }
    }
}

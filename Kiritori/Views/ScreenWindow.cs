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
using Kiritori.Views.LiveCapture;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using System.IO;
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
        private Font fnt = new Font("Segoe UI", 10);
        private MainApplication ma;
        private int currentDpi = 96;

        // スナップ関連の既定（Settings が無い場合のフォールバック）
        private int snapGrid = 50;      // グリッド間隔(px)
        private int edgeSnapTol = 6;    // 端スナップの許容距離(px)
        private bool showSnapGuides = true; // Alt中にガイド線を描くか

        private MagnifierLiveWindow _live;
        private LiveRegionWindow_GDI _liveRegion;

        // ====== 設定をキャッシュして描画に使うフィールド ======
        private Color _bgColor;               // CaptureBackgroundColor
        private int _bgAlphaPercent;        // CaptureBackgroundAlphaPercent (0-100)
        private Color _hoverColor;            // HoverHighlightColor
        private int _hoverAlphaPercent;     // HoverHighlightAlphaPercent (0-100)
        private int _hoverThicknessPx;      // HoverHighlightThickness (px)

        // 任意の拡張（Settings があれば拾う。無ければ既存値を使用）
        private int _snapGridPx;            // SnapGridPx
        private int _edgeSnapTolPx;         // EdgeSnapTolerancePx
        private bool _ocr_mode = false;

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
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

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
                    e.PropertyName == nameof(Properties.Settings.Default.isScreenGuide))
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
            bool baseVal = Properties.Settings.Default.isScreenGuide;
            if ((ModifierKeys & Keys.Alt) != 0) return !baseVal;
            return baseVal;
        }

        private bool IsSnapEnabled()
        {
            // Ctrl で反転（既定は Settings に isScreenSnap があれば使用、無ければ false）
            bool baseVal = false;
            try
            {
                object v = Properties.Settings.Default["isScreenSnap"];
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
            this.Opacity = 1.0;
            this.isOpen = true;
            DisposeCaptureSurface();

            this.StartPosition = FormStartPosition.Manual;

            var vs = GetVirtualScreenPhysical();
            x = vs.X; y = vs.Y; w = vs.W; h = vs.H;

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
            this._ocr_mode = true;
            showScreenAll();
        }


        // ====== 選択操作 ======
        private Point startPoint;
        private Point startPointPhys;
        private Point hoverPoint = Point.Empty;
        private bool showHover = true;
        //private Point endPoint;
        private Rectangle rc;
        private Boolean isPressed = false;

        private void ScreenWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                startPoint = new Point(e.X, e.Y);
                startPointPhys = new Point(e.X + x, e.Y + y);
                isPressed = true;
            }
        }

        private void ScreenWindow_MouseMove(object sender, MouseEventArgs e)
        {
            hoverPoint = new Point(e.X, e.Y);

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

            rc.X = Math.Min(startPoint.X, cur.X);
            rc.Y = Math.Min(startPoint.Y, cur.Y);
            rc.Width = Math.Abs(cur.X - startPoint.X);
            rc.Height = Math.Abs(cur.Y - startPoint.Y);

            if (IsSquareConstraint())
            {
                int size = Math.Min(rc.Width, rc.Height);
                rc.Width = rc.Height = size;
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

                pictureBox1.Refresh();
            }
        }

        private void ScreenWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isPressed) return;
            isPressed = false;

            if (rc.Width > 0 && rc.Height > 0 && baseBmp != null)
            {
                // 1) 先に安全クロップ
                Rectangle crop = Rectangle.Intersect(rc, new Rectangle(0, 0, baseBmp.Width, baseBmp.Height));
                if (crop.Width > 0 && crop.Height > 0)
                {
                    this.Opacity = 0.0; // 透明化しておく
                    if (_ocr_mode)
                    {
                        // ★ CloseScreen() より前に「クロップ済み画像」を作る
                        Bitmap sub = null;
                        try
                        {
                            sub = baseBmp?.Clone(crop, baseBmp.PixelFormat);
                        }
                        catch { /* clone失敗は null で後段へ */ }

                        // もう画面は閉じてOK（baseBmp はここで破棄される）
                        this.CloseScreen();

                        // ★ UIスレッド(BeginInvoke)でOCRへ。subの破棄はOCR側で行う
                        this.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                if (sub == null)
                                {
                                    System.Diagnostics.Debug.WriteLine("[OCR] sub image is null.");
                                    NotifyOcrError();
                                    return;
                                }
                                await DoOcrFromSubImageAsync(sub); // ← 新規（下で追加）
                            }
                            finally
                            {
                                _ocr_mode = false;
                            }
                        }));
                        return;
                    }
                    else
                    {
                        // 2) 画面上の配置位置（物理座標）
                        var desired = new Point(crop.X + x, crop.Y + y);

                        // 3) SnapWindow を作って baseBmp から適用
                        var sw = new SnapWindow(this.ma)
                        {
                            StartPosition = FormStartPosition.Manual
                        };

                        // CloseScreen() より前に baseBmp を使い切る or Clone して渡す
                        sw.CaptureFromBitmap(baseBmp, crop, desired, LoadMethod.Capture);

                        // 4) もう原本は不要ならここで閉じる
                        this.CloseScreen();

                        sw.FormClosing += new FormClosingEventHandler(SW_FormClosing);
                        captureArray.Add(sw);
                        notifyCaptured();
                        return;
                    }
                }
            }
            this.CloseScreen();
        }

        private static DateTime _lastToastAt = DateTime.MinValue;

        private void notifyCaptured()
        {
            // OCRモード時はキャプチャ完了トーストを出さない
            if (_ocr_mode) return;
            if (!Properties.Settings.Default.isShowNotify) return;
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

                    // ★ ここで AUMID（Startメニューのショートカットと一致するID）を指定
                    Debug.WriteLine("[Toast] Show() called: " + NotificationService.GetAppAumid());
                    ToastNotificationManager.CreateToastNotifier(NotificationService.GetAppAumid()).Show(toast);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[Toast] Show() failed: " + ex);
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                main?.NotifyIcon?.ShowBalloonTip(2500, "Kiritori", "キャプチャを保存しました", ToolTipIcon.None);
            }
        }


        private void RedrawHoverOnly()
        {
            if (!showHover || bmp == null || baseBmp == null) return;

            var pt = hoverPoint;
            if (IsAltDown())
                pt = SnapPoint(pt);

            using (g = Graphics.FromImage(bmp))
            {
                // 原本から描き直し
                g.DrawImage(baseBmp, Point.Empty);

                // 全体に背景マスク
                using (var mask = MakeBackgroundMaskBrush())
                    g.FillRectangle(mask, new Rectangle(0, 0, bmp.Width, bmp.Height));

                if (showSnapGuides && IsAltDown())
                {
                    using (var pen = MakeGuidePen())
                    {
                        g.DrawLine(pen, 0, pt.Y, bmp.Width, pt.Y);
                        g.DrawLine(pen, pt.X, 0, pt.X, bmp.Height);
                    }
                }
                // 開始候補点に十字
                // DrawCrosshair(g, hoverPoint);

                // 物理座標（仮想スクリーン基準）
                var phys = new Point(hoverPoint.X + x, hoverPoint.Y + y);
                if (showSnapGuides)
                {
                    DrawLabel(g, string.Format("{0}, {1}", phys.X, phys.Y),
                                new Point(hoverPoint.X + 10, hoverPoint.Y + 10));
                }
            }
            pictureBox1.Refresh();
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
            switch ((int)keyData)
            {
                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
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

        // ====== ライブ表示サンプル（既存） ======
        private void StartLiveFromSelection1(Rectangle rect)
        {
            var viewerSize = rect.Size;
            var screenBounds = Screen.PrimaryScreen.WorkingArea;
            var viewerLoc = new Point(
                Math.Max(0, screenBounds.Right - viewerSize.Width - 20),
                Math.Max(0, screenBounds.Bottom - viewerSize.Height - 20)
            );

            _live?.Close();
            _live = new MagnifierLiveWindow();
            _live.StartLive(rect, viewerLoc, viewerSize, scale: 1.0f, clickThrough: false);
        }

        private void StopLive1()
        {
            _live?.Close();
            _live = null;
        }

        private void StartLiveFromSelection(Rectangle selectedLogicalRect)
        {
            _liveRegion?.StopLive();
            _liveRegion = new LiveRegionWindow_GDI();

            Rectangle? viewer = null; // 例: new Rectangle(100, 100, 400, 300);
            _liveRegion.StartLive(selectedLogicalRect, viewer);
        }

        private void StopLive() { if (_liveRegion != null) _liveRegion.StopLive(); }

        // ====== OCR ユーティリティ ======
        private async System.Threading.Tasks.Task DoOcrFromCropAsync(Bitmap baseImage, Rectangle crop)
        {
            try
            {
                using (var sub = baseImage.Clone(crop, baseImage.PixelFormat))
                {
                    // あなたの OCR 実装に合わせて参照名を調整してください
                    // 例: Kiritori.Services.Ocr.WindowsOcrProvider / IOcrProvider / OcrOptions
                    var provider = new Kiritori.Services.Ocr.WindowsOcrProvider();
                    var opt = new Kiritori.Services.Ocr.OcrOptions(); // オプションが不要なら削除OK

                    var result = await provider.RecognizeAsync(sub, opt);

                    // ★ OcrResult のプロパティ名に合わせて調整
                    // Windows.Media.Ocr.OcrResult なら .Text
                    // 自作 OcrResult でも通常は Text/PlainText いずれか
                    string text = null;
                    try
                    {
                        // 最初に Text を試す
                        var prop = result.GetType().GetProperty("Text");
                        if (prop != null) text = prop.GetValue(result, null) as string;
                    }
                    catch { /* ignore */ }

                    if (string.IsNullOrEmpty(text))
                    {
                        // 次善: PlainText を試す
                        try
                        {
                            var prop = result.GetType().GetProperty("PlainText");
                            if (prop != null) text = prop.GetValue(result, null) as string;
                        }
                        catch { /* ignore */ }
                    }

                    if (text == null) text = result != null ? result.ToString() : string.Empty;
                    if (text == null) text = string.Empty;

                    // クリップボードへ
                    TrySetClipboardText(text);

                    // トースト
                    NotifyOcr(text);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[OCR] failed: " + ex);
                NotifyOcrError();
            }
        }

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
            if (!Properties.Settings.Default.isShowNotify) return;

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
        // こちらを新設。受け取った sub はここで using 破棄します
        private async System.Threading.Tasks.Task DoOcrFromSubImageAsync(Bitmap sub)
        {
            try
            {
                using (sub) // ★ ここで確実に破棄
                {
                    // あなたの OCR 実装に合わせて
                    var provider = new Kiritori.Services.Ocr.WindowsOcrProvider();
                    var opt = new Kiritori.Services.Ocr.OcrOptions();

                    var result = await provider.RecognizeAsync(sub, opt);

                    string text = null;
                    try
                    {
                        var prop = result.GetType().GetProperty("Text");
                        if (prop != null) text = prop.GetValue(result, null) as string;
                    }
                    catch { }
                    if (string.IsNullOrEmpty(text))
                    {
                        try
                        {
                            var prop = result.GetType().GetProperty("PlainText");
                            if (prop != null) text = prop.GetValue(result, null) as string;
                        }
                        catch { }
                    }
                    if (text == null) text = result != null ? result.ToString() : string.Empty;

                    TrySetClipboardText(text);
                    NotifyOcr(text);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[OCR] failed: " + ex);
                NotifyOcrError();
            }
        }

    }
}

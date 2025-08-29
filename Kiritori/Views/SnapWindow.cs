using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Services.Ocr;
using Windows.UI.Notifications;

namespace Kiritori
{
    enum HOTS
    {
        MOVE_LEFT   = Keys.Left,
        MOVE_RIGHT  = Keys.Right,
        MOVE_UP     = Keys.Up,
        MOVE_DOWN   = Keys.Down,
        SHIFT_MOVE_LEFT  = Keys.Left  | Keys.Shift,
        SHIFT_MOVE_RIGHT = Keys.Right | Keys.Shift,
        SHIFT_MOVE_UP    = Keys.Up    | Keys.Shift,
        SHIFT_MOVE_DOWN  = Keys.Down  | Keys.Shift,
        FLOAT       = Keys.Control | Keys.A,
        SHADOW      = Keys.Control | Keys.D,
        HOVER      = Keys.Control | Keys.F,
        SAVE        = Keys.Control | Keys.S,
        LOAD        = Keys.Control | Keys.O,
        OPEN        = Keys.Control | Keys.N,
        EDIT_MSPAINT        = Keys.Control | Keys.E,
        ZOOM_ORIGIN_NUMPAD = Keys.Control | Keys.NumPad0,
        ZOOM_ORIGIN_MAIN   = Keys.Control | Keys.D0,
        ZOOM_IN     = Keys.Control | Keys.Oemplus,
        ZOOM_OUT    = Keys.Control | Keys.OemMinus,
        CLOSE       = Keys.Control | Keys.W,
        ESCAPE      = Keys.Escape,
        COPY        = Keys.Control | Keys.C,
        CUT         = Keys.Control | Keys.X,
        OCR         = Keys.Control | Keys.T,
        PRINT       = Keys.Control | Keys.P,
        MINIMIZE    = Keys.Control | Keys.H
    }

    public partial class SnapWindow : Form
    {
        #region ===== フィールド/定数 =====

        public DateTime date;

        private bool isWindowShadow = true;
        private bool isAfloatWindow = true;
        private bool isOverlay = true;

        // マウスドラッグ関連
        private Point mousePoint;
        private bool _isDragging = false;

        // 透明度関連
        private double WindowAlphaPercent;
        private const double DRAG_ALPHA = 0.3;
        private const double MIN_ALPHA = 0.1;

        // ウィンドウ移動
        private const int MOVE_STEP = 3;
        private const int SHIFT_MOVE_STEP = MOVE_STEP * 10;

        // サムネイル
        private const int THUMB_WIDTH = 250;
        // 親参照
        private MainApplication ma;

        // オーバーレイ
        private string _overlayText = null;
        private DateTime _overlayStart;
        private readonly int _overlayDurationMs = 2000; // 表示時間
        private readonly int _overlayFadeMs = 300;      // 終了前フェード
        private readonly Timer _overlayTimer;
        private Font _overlayFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private int _dpi = 96;

        private Image _originalImage;  // 元画像
        private float _scale = 1f;
        private int _zoomStep = 0;            // 0=100%, +1=110%, -1=90% ...
        private const float STEP_LINEAR = 0.10f;  // 10% 刻み
        private const float MIN_SCALE = 0.10f;
        private const float MAX_SCALE = 8.00f;

        private const int MIN_WIDTH = 100; // ホバー時のボタン表示調整用
        private const int MIN_HEIGHT = 50; // ホバー時のボタン表示調整用
        // 右上クローズボタン描画
        public Bitmap main_image;
        public Bitmap thumbnail_image;
        private readonly Dictionary<int, (Bitmap normal, Bitmap hover)> _closeIconCache
            = new Dictionary<int, (Bitmap normal, Bitmap hover)>();
        private bool _hoverWindow = false;
        private bool _hoverClose = false;
        private Rectangle _closeBtnRect = Rectangle.Empty;

        // ドロップシャドウ
        private const int CS_DROPSHADOW = 0x00020000;

        // MSPaint 編集
        private string _paintEditPath;  // 編集ファイルのパス（元ファイル or 一時ファイル）
        public bool SuppressHistory { get; set; } = false;

        private bool isHighlightOnHover = true;

        // ===== 設定反映（ホバー枠） =====
        private Color _hoverColor;            // HoverHighlightColor
        private int _hoverAlphaPercent;     // HoverHighlightAlphaPercent (0-100)
        private int _hoverThicknessPx;      // HoverHighlightThickness (px)
        private PropertyChangedEventHandler _settingsHandler;
        private bool _isApplyingSettings = false;

        // OCR 処理中フラグ
        private bool _ocrBusy = false;

        #endregion

        #region ===== Win32/プロパティ =====

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (this.isWindowShadow)
                {
                    cp.ClassStyle |= CS_DROPSHADOW;
                }
                return cp;
            }
        }

        #endregion

        #region ===== コンストラクタ/ライフサイクル =====

        public SnapWindow(MainApplication mainapp)
        {
            this.ma = mainapp;

            // 設定の初期読み込み（ハンドル未作成でもOKな形に）
            ReadSettingsIntoFieldsWithFallback();

            _overlayTimer = new Timer { Interval = 33 }; // ~30fps
            _overlayTimer.Tick += (s, e) =>
            {
                if (_overlayText == null) { _overlayTimer.Stop(); return; }
                if ((DateTime.Now - _overlayStart).TotalMilliseconds > _overlayDurationMs)
                {
                    _overlayText = null;
                    pictureBox1.Invalidate();
                    _overlayTimer.Stop();
                    return;
                }
                pictureBox1.Invalidate(new Rectangle(
                    pictureBox1.ClientSize.Width - 300,
                    pictureBox1.ClientSize.Height - 120, 300, 120));
            };

            InitializeComponent();
            Localizer.Apply(this);            // 通常コントロール（Tag=loc:...）
            ApplyAllContextMenusLocalization(); // 右クリック等のメニュー（Tag=loc:...）

            // 言語切替イベントで追従
            SR.CultureChanged += () =>
            {
                if (this.IsDisposed) return;
                Localizer.Apply(this);
                ApplyAllContextMenusLocalization();
            };
            // ハンドル作成後に UI へ反映
            ApplyUiFromFields();


            // 設定変更の監視
            HookSettingsChanged();

            this.pictureBox1.Paint += PictureBox1_Paint;
            this.pictureBox1.MouseMove += PictureBox1_MouseMove_Icon;
            this.pictureBox1.MouseClick += PictureBox1_MouseClick_Icon;
            this.pictureBox1.MouseEnter += delegate { _hoverWindow = true; pictureBox1.Invalidate(); };
            this.pictureBox1.MouseLeave += delegate { _hoverWindow = false; _hoverClose = false; pictureBox1.Invalidate(); };

            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
        }

        public Bitmap GetMainImage() => (Bitmap)pictureBox1.Image;
        public string GetImageSourcePath() => pictureBox1.Image?.Tag as string;

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown += new MouseEventHandler(Form1_MouseDown);
            pictureBox1.MouseMove += new MouseEventHandler(Form1_MouseMove);
            pictureBox1.MouseUp += new MouseEventHandler(Form1_MouseUp);
        }

        #endregion

        #region ===== 設定読み込み / 監視 =====

        /// <summary>
        /// Settings を読み取り、内部フィールドに格納（Color/Alpha/Thickness は Empty/0 なら既定値へ）
        /// ハンドル未作成でも安全。
        /// </summary>
        private void ReadSettingsIntoFieldsWithFallback()
        {
            var S = Properties.Settings.Default;

            // 汎用（UIに直接触らない）
            isWindowShadow = S.isWindowShadow;
            isAfloatWindow = S.isAfloatWindow;
            isOverlay = S.isOverlay;
            WindowAlphaPercent = S.WindowAlphaPercent / 100.0;
            isHighlightOnHover = S.isHighlightWindowOnHover;

            // ハイライト系（フォールバック）
            var c = S.HoverHighlightColor;
            int a = S.HoverHighlightAlphaPercent;
            int t = S.HoverHighlightThickness;

            if (c.IsEmpty) c = Color.Red;
            if (a <= 0) a = 60;     // 既定: 60%
            if (t <= 0) t = 2;      // 既定: 2px

            _hoverColor = c;
            _hoverAlphaPercent = Math.Max(0, Math.Min(100, a));
            _hoverThicknessPx = Math.Max(1, t);
        }

        /// <summary>
        /// 内部フィールドの値を UI（TopMost, Opacity など）へ反映
        /// </summary>
        private void ApplyUiFromFields()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                this.TopMost = isAfloatWindow;
                this.Opacity = WindowAlphaPercent;
                // 影変更は必要な時のみ（ここではハンドル再作成を避ける）
            }
            catch { /* 破棄競合などは無視 */ }
        }

        private void SafeApplySettings()
        {
            if (_isApplyingSettings) return;
            _isApplyingSettings = true;
            try
            {
                if (this.IsDisposed || this.Disposing) return;

                // 設定→フィールド（常に実施、フォールバック込み）
                ReadSettingsIntoFieldsWithFallback();

                // フィールド→UI（ハンドルがある時だけ）
                ApplyUiFromFields();

                pictureBox1?.Invalidate();
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private void HookSettingsChanged()
        {
            // 多重購読防止
            if (_settingsHandler != null)
                Properties.Settings.Default.PropertyChanged -= _settingsHandler;

            _settingsHandler = (s, e) =>
            {
                if (this.IsDisposed || this.Disposing) return;

                if (this.InvokeRequired)
                {
                    try { this.BeginInvoke(_settingsHandler, s, e); } catch { }
                    return;
                }

                // 設定変更 → 反映（Settings への書き戻しはしない）
                if (e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightThickness) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isHighlightWindowOnHover) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isWindowShadow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isAfloatWindow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isOverlay) ||
                    e.PropertyName == nameof(Properties.Settings.Default.WindowAlphaPercent) ||
                    string.IsNullOrEmpty(e.PropertyName))
                {
                    SafeApplySettings();
                }
            };

            Properties.Settings.Default.PropertyChanged += _settingsHandler;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // ハンドル作成直後に UI へ反映
            ApplyUiFromFields();
        }

        // DPI に合わせた線幅
        private float HoverThicknessDpi()
        {
            float t = _hoverThicknessPx * (this.DeviceDpi / 96f);
            return (t < 1f) ? 1f : t;
        }

        // ホバー枠のペン（設定色＋アルファ＋太さ）
        private Pen MakeHoverPen()
        {
            int a = Math.Max(0, Math.Min(255, (int)Math.Round(_hoverAlphaPercent * 2.55))); // 0..100 -> 0..255
            float w = HoverThicknessDpi();
            Color c = Color.FromArgb(a, _hoverColor);
            var pen = new Pen(c, w)
            {
                Alignment = PenAlignment.Inset // 外にはみ出さない
            };
            return pen;
        }

        #endregion

        #region ===== キー入力（ホットキー） =====

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch ((int)keyData)
            {
                case (int)HOTS.MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.MOVE_UP:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y - MOVE_STEP);
                    break;
                case (int)HOTS.MOVE_DOWN:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y + MOVE_STEP);
                    break;
                case (int)HOTS.SHIFT_MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - SHIFT_MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.SHIFT_MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + SHIFT_MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.SHIFT_MOVE_UP:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y - SHIFT_MOVE_STEP);
                    break;
                case (int)HOTS.SHIFT_MOVE_DOWN:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y + SHIFT_MOVE_STEP);
                    break;

                case (int)HOTS.SHADOW:
                    ToggleShadow(!this.isWindowShadow);
                    break;
                case (int)HOTS.FLOAT:
                    afloatImage(this);
                    break;
                case (int)HOTS.HOVER:
                    ToggleHoverHighlight(!this.isHighlightOnHover);
                    break;

                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    closeImage(this);
                    break;

                case (int)HOTS.SAVE:
                    saveImage();
                    break;
                case (int)HOTS.LOAD:
                    loadImage();
                    break;
                case (int)HOTS.OPEN:
                    openImage();
                    break;
                case (int)HOTS.EDIT_MSPAINT:
                    editInMSPaint(this);
                    break;

                case (int)HOTS.ZOOM_ORIGIN_NUMPAD:
                case (int)HOTS.ZOOM_ORIGIN_MAIN:
                    zoomOff();
                    break;
                case (int)HOTS.ZOOM_IN:
                    zoomIn();
                    break;
                case (int)HOTS.ZOOM_OUT:
                    zoomOut();
                    break;

                case (int)HOTS.COPY:
                    copyImage(this);
                    break;
                case (int)HOTS.CUT:
                    closeImage(this);
                    break;
                case (int)HOTS.OCR:
                    RunOcrOnCurrentImage();
                    break;
                case (int)HOTS.PRINT:
                    printImage();
                    break;
                case (int)HOTS.MINIMIZE:
                    minimizeWindow();
                    break;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        #endregion

        #region ===== クリックイベントから呼ばれる関数（イベントハンドラ群） =====

        private void captureToolStripMenuItem_Click(object sender, EventArgs e) { this.ma.openScreen(); }
        private void closeESCToolStripMenuItem_Click(object sender, EventArgs e) { this.Close(); }
        private void cutCtrlXToolStripMenuItem_Click(object sender, EventArgs e) { Clipboard.SetImage(this.pictureBox1.Image); this.Close(); }
        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e) { Clipboard.SetImage(this.pictureBox1.Image); ShowOverlay("Copy"); }
        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            ShowOverlay("Keep Afloat: " + this.TopMost);
            // 保存は不要のため Settings へは書かない
            this.isAfloatWindow = this.TopMost;
        }
        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e) { saveImage(); }
        private void openImageToolStripMenuItem_Click(object sender, EventArgs e) { openImage(); }
        private void originalSizeToolStripMenuItem_Click(object sender, EventArgs e) { zoomOff(); }
        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e) { zoomIn(); }
        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e) { zoomOut(); }
        private void size10ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(10); }
        private void size50ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(50); }
        private void size100ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(100); }
        private void size150ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(150); }
        private void size200ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(200); }
        private void size500ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(500); }
        private void dropShadowToolStripMenuItem_Click(object sender, EventArgs e) { ToggleShadow(!this.isWindowShadow); }
        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e) { PrefForm.ShowSingleton(this); }
        private void printToolStripMenuItem_Click(object sender, EventArgs e) { printImage(); }
        private void opacity100toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(1.0); ShowOverlay("Opacity: 100%"); }
        private void opacity90toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.9); ShowOverlay("Opacity: 90%"); }
        private void opacity80toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.8); ShowOverlay("Opacity: 80%"); }
        private void opacity50toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.5); ShowOverlay("Opacity: 50%"); }
        private void opacity30toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.3); ShowOverlay("Opacity: 30%"); }
        private void minimizeToolStripMenuItem_Click(object sender, EventArgs e) { this.minimizeWindow(); }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e) { Application.Exit(); }

        // Paint 編集（MSPaint起動→終了で再読込）
        private void editPaintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (pictureBox1.Image == null)
                {
                    MessageBox.Show(this, "No image to edit.", "Kiritori",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string preferredSrcPath = pictureBox1.Image?.Tag as string;

                if (!string.IsNullOrEmpty(preferredSrcPath) && File.Exists(preferredSrcPath))
                {
                    _paintEditPath = preferredSrcPath;
                }
                else
                {
                    _paintEditPath = Path.Combine(
                        Path.GetTempPath(),
                        $"Kiritori_Edit_{DateTime.Now:yyyyMMdd_HHmmssfff}.png"
                    );
                    using (var bmp = new Bitmap(pictureBox1.Image))
                    {
                        bmp.Save(_paintEditPath, ImageFormat.Png);
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "mspaint.exe",
                    Arguments = $"\"{_paintEditPath}\"",
                    UseShellExecute = true
                };

                var proc = Process.Start(psi);
                if (proc == null) return;

                proc.EnableRaisingEvents = true;
                proc.Exited += (s, ev) =>
                {
                    try
                    {
                        if (File.Exists(_paintEditPath))
                        {
                            using (var fs = new FileStream(_paintEditPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var img = Image.FromStream(fs))
                            {
                                var updated = new Bitmap(img);
                                this.BeginInvoke((Action)(() =>
                                {
                                    var old = pictureBox1.Image;
                                    pictureBox1.Image = updated;
                                    if (string.IsNullOrEmpty(preferredSrcPath))
                                        pictureBox1.Image.Tag = _paintEditPath;
                                    old?.Dispose();
                                }));
                            }
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Failed to load edited image: " + _paintEditPath);
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open in Paint.\r\n" + ex.Message,
                    "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // オーバーレイ/クローズボタン描画・マウス系（イベント）
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (!string.IsNullOrEmpty(_overlayText))
            {
                double elapsed = (DateTime.Now - _overlayStart).TotalMilliseconds;
                int alpha = 200;
                double remain = _overlayDurationMs - elapsed;
                if (remain < _overlayFadeMs)
                {
                    alpha = (int)(alpha * (remain / _overlayFadeMs));
                    if (alpha < 0) alpha = 0;
                }

                int padding = (int)(10 * (_dpi / 96f));
                SizeF ts = g.MeasureString(_overlayText, _overlayFont);
                int w = (int)Math.Ceiling(ts.Width) + padding * 2;
                int h = (int)Math.Ceiling(ts.Height) + padding * 2;

                int margin = (int)(12 * (_dpi / 96f));
                int x = pictureBox1.ClientSize.Width - w - margin;
                int y = pictureBox1.ClientSize.Height - h - margin;

                using (var path = RoundedRect(new Rectangle(x, y, w, h), (int)(8 * (_dpi / 96f))))
                using (var bg = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                using (var pen = new Pen(Color.FromArgb(Math.Max(80, alpha), 255, 255, 255), 1f))
                using (var txt = new SolidBrush(Color.FromArgb(Math.Max(180, alpha), 255, 255, 255)))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    g.DrawString(_overlayText, _overlayFont, txt, x + padding, y + padding);
                }
            }

            // --- マウスホバー中の内枠強調（Settings 反映） ---
            if (isHighlightOnHover && _hoverWindow && _hoverAlphaPercent > 0 && _hoverThicknessPx > 0)
            {
                using (var pen = MakeHoverPen())
                {
                    var r = pictureBox1.ClientRectangle;
                    // int inset = (int)Math.Ceiling(pen.Width / 2f);
                    // if (inset > 0) r = Rectangle.Inflate(r, -inset, -inset);
                    if (r.Width > 0 && r.Height > 0)
                    {
                        pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;
                        g.DrawRectangle(pen, r);
                    }
                }
            }

            if (_hoverWindow &&
                pictureBox1.ClientSize.Width >= MIN_WIDTH &&
                pictureBox1.ClientSize.Height >= MIN_HEIGHT)
            {
                var pair = GetCloseBitmapsForDpi(this.DeviceDpi);
                var img = _hoverClose ? pair.hover : pair.normal;

                float scale = this.DeviceDpi / 96f;
                int marginPx = (int)Math.Round(8 * scale);

                int x = pictureBox1.ClientSize.Width - img.Width - marginPx;
                int y = marginPx;

                _closeBtnRect = new Rectangle(x, y, img.Width, img.Height);

                int pad = (int)Math.Round(1 * scale);
                using (var bg = new SolidBrush(Color.FromArgb(_hoverClose ? 160 : 50, 0, 0, 0)))
                    g.FillEllipse(bg, Rectangle.Inflate(_closeBtnRect, pad, pad));

                g.DrawImage(img, _closeBtnRect);
            }
            else
            {
                _closeBtnRect = Rectangle.Empty;
                _hoverClose = false;
            }
        }

        private void PictureBox1_MouseMove_Icon(object sender, MouseEventArgs e)
        {
            bool now = _closeBtnRect.Contains(e.Location);
            if (now != _hoverClose)
            {
                _hoverClose = now;
                pictureBox1.Invalidate(_closeBtnRect);
            }
        }

        private void PictureBox1_MouseClick_Icon(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _closeBtnRect.Contains(e.Location))
                this.Close();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;
            mousePoint = new Point(e.X, e.Y);
            _isDragging = true;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location))
            {
                this.Cursor = Cursors.Hand;
                return;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }

            if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                this.Left += e.X - mousePoint.X;
                this.Top += e.Y - mousePoint.Y;
                this.Opacity = this.WindowAlphaPercent * DRAG_ALPHA;
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
            this.Opacity = this.WindowAlphaPercent;
        }

        #endregion

        #region ===== 汎用関数（メニュー/キーから呼ばれるロジック） =====

        public void capture(Rectangle rc)
        {
            Debug.WriteLine("Capturing: " + rc);
            Bitmap bmp = new Bitmap(rc.Width, rc.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(new Point(rc.X, rc.Y),
                                new Point(0, 0),
                                new Size(rc.Width, rc.Height),
                                CopyPixelOperation.SourceCopy);
            }
            this.Size = bmp.Size;
            pictureBox1.Size = bmp.Size;
            SetImageAndResetZoom(bmp);
            pictureBox1.Image = bmp;
            date = DateTime.Now;
            this.Text = date.ToString("yyyyMMdd-HHmmss") + ".png";
            this.TopMost = this.isAfloatWindow;
            this.Opacity = this.WindowAlphaPercent;

            this.main_image = bmp;
            this.setThumbnail(bmp);
            if (!SuppressHistory) ma.setHistory(this);
            ShowOverlay("Kiritori");
        }
        public void CaptureFromBitmap(Bitmap source, Rectangle crop, Point desiredScreenPos)
        {
            // 1) 安全にトリミング
            Rectangle safe = Rectangle.Intersect(crop, new Rectangle(0, 0, source.Width, source.Height));
            if (safe.Width <= 0 || safe.Height <= 0) return;

            // 2) Cloneで切り出し（sourceは呼び出し元が管理）
            Bitmap cropped = source.Clone(safe, source.PixelFormat);

            // 3) 共通適用
            ApplyBitmap(cropped);

            // 4) クライアント左上を物理座標 desired に合わせる（枠・DPI差を補正）
            AlignClientTopLeftToScreen(desiredScreenPos);
        }

        private void ApplyBitmap(Bitmap bmp)
        {
            // 既存画像の破棄（リーク防止）
            if (pictureBox1.Image != null && !ReferenceEquals(pictureBox1.Image, bmp))
            {
                try { pictureBox1.Image.Dispose(); } catch { }
            }
            if (main_image != null && !ReferenceEquals(main_image, bmp))
            {
                try { main_image.Dispose(); } catch { }
            }

            // UI適用
            this.Size = bmp.Size;
            pictureBox1.Size = bmp.Size;

            // 既存のズーム管理があるなら使う
            SetImageAndResetZoom(bmp);
            pictureBox1.Image = bmp;

            // メタ・状態
            date = DateTime.Now;
            this.Text = date.ToString("yyyyMMdd-HHmmss") + ".png";
            this.TopMost = this.isAfloatWindow;
            this.Opacity = this.WindowAlphaPercent;

            // 所有を明確に
            this.main_image = bmp;
            this.setThumbnail(bmp);
            if (!SuppressHistory) ma.setHistory(this);
            ShowOverlay("Kiritori");
        }

        /// <summary>
        /// ウィンドウのクライアント左上（0,0）を、指定スクリーン座標に合わせる
        /// （枠/タイトルバー/DPI差によるズレを実測で補正）
        /// </summary>
        public void AlignClientTopLeftToScreen(Point targetScreenPoint)
        {
            if (!this.IsHandleCreated) this.CreateControl();
            if (!this.Visible) this.Show();

            var clientTopLeftOnScreen = this.PointToScreen(Point.Empty);
            var dx = targetScreenPoint.X - clientTopLeftOnScreen.X;
            var dy = targetScreenPoint.Y - clientTopLeftOnScreen.Y;
            if (dx != 0 || dy != 0)
            {
                this.Location = new Point(this.Location.X + dx, this.Location.Y + dy);
            }
        }

        private void setThumbnail(Bitmap bmp)
        {
            this.main_image = bmp;
            if (bmp.Size.Width > THUMB_WIDTH)
            {
                int resizeWidth = THUMB_WIDTH;
                int resizeHeight = (int)(bmp.Height * ((double)resizeWidth / (double)bmp.Width));
                Bitmap resizeBmp = new Bitmap(resizeWidth, resizeHeight);
                Graphics g = Graphics.FromImage(resizeBmp);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, resizeWidth, resizeHeight);
                g.Dispose();
                this.thumbnail_image = resizeBmp;
            }
            else
            {
                this.thumbnail_image = bmp;
            }
        }

        public void saveImage()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = this.Text;
            sfd.Filter = "Image Files(*.png;*.PNG)|*.png;*.PNG|All Files(*.*)|*.*";
            sfd.FilterIndex = 1;
            sfd.Title = "Select a path to save the image";
            sfd.RestoreDirectory = true;
            sfd.OverwritePrompt = true;
            sfd.CheckPathExists = true;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                this.pictureBox1.Image.Save(sfd.FileName);
                ShowOverlay("Saved");
            }
            sfd.Dispose();
        }

        public void loadImage()
        {
            try
            {
                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                openFileDialog1.Title = "Load Image";
                openFileDialog1.Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.ValidateNames = false;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    openFileDialog1.Dispose();

                    Bitmap bmp = new Bitmap(openFileDialog1.FileName);
                    Size bs = bmp.Size;
                    int sw = Screen.GetBounds(this).Width;
                    int sh = Screen.GetBounds(this).Height;
                    if (bs.Height > sh)
                    {
                        bs.Height = sh;
                        bs.Width = (int)((double)bs.Height * ((double)bmp.Width / (double)bmp.Height));
                    }
                    this.Size = bs;
                    pictureBox1.Size = bs;
                    SetImageAndResetZoom(bmp);
                    pictureBox1.Image = bmp;
                    this.Text = openFileDialog1.FileName;
                    this.StartPosition = FormStartPosition.CenterScreen;
                    this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0),
                                            this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                    this.main_image = bmp;
                    this.setThumbnail(bmp);
                    if (!SuppressHistory) ma.setHistory(this);
                    ShowOverlay("Loaded");
                }
                else
                {
                    openFileDialog1.Dispose();
                }
            }
            catch
            {
                this.Close();
            }
        }

        public void openImage() => this.ma.openImage();

        public void setImageFromPath(string fname)
        {
            try
            {
                Bitmap bmp = new Bitmap(fname);
                Size bs = bmp.Size;
                int sw = Screen.GetBounds(this).Width;
                int sh = Screen.GetBounds(this).Height;
                if (bs.Height > sh)
                {
                    bs.Height = sh;
                    bs.Width = (int)((double)bs.Height * ((double)bmp.Width / (double)bmp.Height));
                }
                this.Size = bs;
                pictureBox1.Size = bs;
                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                this.Text = fname;
                this.TopMost = this.isAfloatWindow;
                this.Opacity = this.WindowAlphaPercent;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0),
                                        this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                this.main_image = bmp;
                this.setThumbnail(bmp);
                if (!SuppressHistory) ma.setHistory(this);
                ShowOverlay("Loaded");
            }
            catch
            {
                this.Close();
            }
        }

        public void setImageFromBMP(Bitmap bmp)
        {
            try
            {
                Size bs = bmp.Size;
                int sw = Screen.GetBounds(this).Width;
                int sh = Screen.GetBounds(this).Height;
                if (bs.Height > sh)
                {
                    bs.Height = sh;
                    bs.Width = (int)((double)bs.Height * ((double)bmp.Width / (double)bmp.Height));
                }
                this.Size = bs;
                pictureBox1.Size = bs;
                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                this.TopMost = this.isAfloatWindow;
                this.Opacity = this.WindowAlphaPercent;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0),
                                        this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                this.main_image = bmp;
                this.setThumbnail(bmp);
            }
            catch
            {
                this.Close();
            }
        }

        public void printImage()
        {
            PrintDialog printDialog1 = new PrintDialog();
            printDialog1.PrinterSettings = new System.Drawing.Printing.PrinterSettings();
            if (printDialog1.ShowDialog() == DialogResult.OK)
            {
                System.Drawing.Printing.PrintDocument pd = new System.Drawing.Printing.PrintDocument();
                pd.PrintPage += new System.Drawing.Printing.PrintPageEventHandler(pd_PrintPage);
                pd.Print();
                pd.Dispose();
                ShowOverlay("Printed");
            }
            printDialog1.Dispose();
        }

        private void pd_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            e.Graphics.DrawImage(this.pictureBox1.Image, e.MarginBounds);
            e.HasMorePages = false;
        }

        public void minimizeWindow()
        {
            this.WindowState = FormWindowState.Minimized;
            ShowOverlay("Minimized");
        }
        public void showWindow()
        {
            this.WindowState = FormWindowState.Normal;
            ShowOverlay("Show");
        }
        public void closeWindow()
        {
            ShowOverlay("Close");
            this.Close();
        }

        public void copyImage(object sender) => copyCtrlCToolStripMenuItem_Click(sender, EventArgs.Empty);
        public void closeImage(object sender) => closeESCToolStripMenuItem_Click(sender, EventArgs.Empty);
        public void afloatImage(object sender) => keepAfloatToolStripMenuItem_Click(sender, EventArgs.Empty);

        public void editInMSPaint(object sender)
        {
            editPaintToolStripMenuItem_Click(sender, EventArgs.Empty);
            ShowOverlay("Edit");
        }

        public void setAlpha(double alpha)
        {
            this.Opacity = alpha;
            this.WindowAlphaPercent = alpha;
        }

        public void ShowOverlay(string text)
        {
            if (!this.isOverlay) return;
            const int MIN_W = 100;
            const int MIN_H = 50;
            if (this.ClientSize.Width < MIN_W || this.ClientSize.Height < MIN_H) return;

            _overlayText = text;
            _overlayStart = DateTime.Now;
            _overlayTimer.Start();
            pictureBox1.Invalidate();
        }

        // === ズーム系（ロジック） ===

        public void zoomIn()
        {
            _zoomStep++;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
        }

        public void zoomOut()
        {
            _zoomStep--;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
        }

        public void zoomOff()
        {
            _zoomStep = 0;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom 100%");
        }

        public void ZoomToPercent(int percent)
        {
            _zoomStep = (int)Math.Round((percent - 100) / (STEP_LINEAR * 100f));
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + percent + "%");
        }

        private void UpdateScaleFromStep()
        {
            _scale = 1.0f + (_zoomStep * STEP_LINEAR);
            if (_scale < MIN_SCALE)
            {
                _scale = MIN_SCALE;
                _zoomStep = (int)Math.Round((MIN_SCALE - 1.0f) / STEP_LINEAR);
            }
            else if (_scale > MAX_SCALE)
            {
                _scale = MAX_SCALE;
                _zoomStep = (int)Math.Round((MAX_SCALE - 1.0f) / STEP_LINEAR);
            }
        }

        private void SetImageAndResetZoom(Image img)
        {
            _originalImage?.Dispose();
            _originalImage = (Image)img.Clone();

            _scale = 1f;
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            ApplyZoom(false);
        }

        private void ApplyZoom(bool redrawOnly)
        {
            if (_originalImage == null) return;

            int newW = Math.Max(1, (int)Math.Round(_originalImage.Width * _scale));
            int newH = Math.Max(1, (int)Math.Round(_originalImage.Height * _scale));

            Size oldClient = this.ClientSize;

            Bitmap bmp = new Bitmap(newW, newH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(_originalImage, 0, 0, newW, newH);
            }

            var oldImg = pictureBox1.Image;
            pictureBox1.Image = bmp;
            oldImg?.Dispose();

            pictureBox1.Width = newW;
            pictureBox1.Height = newH;

            this.ClientSize = new Size(newW, newH);

            if (!redrawOnly)
            {
                int dx = (this.ClientSize.Width - oldClient.Width) / 2;
                int dy = (this.ClientSize.Height - oldClient.Height) / 2;
                this.Left -= dx;
                this.Top -= dy;
            }
        }

        public void ToggleShadow(bool enable)
        {
            this.isWindowShadow = enable;
            this.RecreateHandle();
            ShowOverlay(this.isWindowShadow ? "Shadow: ON" : "Shadow: OFF");
        }

        public void ToggleHoverHighlight(bool enable)
        {
            this.isHighlightOnHover = enable;
            ShowOverlay(this.isHighlightOnHover ? "Hover Highlight: ON" : "Hover Highlight: OFF");
        }
        // ヘルパー
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private (Bitmap normal, Bitmap hover) GetCloseBitmapsForDpi(int dpi)
        {
            int key = (dpi <= 0 ? this.DeviceDpi : dpi);
            if (_closeIconCache.TryGetValue(key, out var cached)) return cached;

            float scale = key / 96f;
            int size = (int)Math.Round(20 * scale);

            Bitmap bmpNormal = new Bitmap(Properties.Resources.close, new Size(size, size));
            Bitmap bmpHover = new Bitmap(Properties.Resources.close_bold, new Size(size, size));

            _closeIconCache[key] = (bmpNormal, bmpHover);
            return _closeIconCache[key];
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_settingsHandler != null)
            {
                try { Properties.Settings.Default.PropertyChanged -= _settingsHandler; } catch { }
                _settingsHandler = null;
            }

            if (this.Icon != null) { this.Icon.Dispose(); this.Icon = null; }
            base.OnFormClosed(e);
        }

        private static void ApplyToolStripLocalization(ToolStripItemCollection items)
        {
            if (items == null) return;
            foreach (ToolStripItem it in items)
            {
                if (it.Tag is string tag && tag.StartsWith("loc:", StringComparison.Ordinal))
                {
                    it.Text = SR.T(tag.Substring(4));
                }
                if (it is ToolStripDropDownItem dd)
                {
                    ApplyToolStripLocalization(dd.DropDownItems);
                }
            }
        }
        private void ApplyAllContextMenusLocalization()
        {
            // フォーム自身に ContextMenuStrip がある場合
            if (this.ContextMenuStrip != null)
                ApplyToolStripLocalization(this.ContextMenuStrip.Items);

            // 子コントロールツリーを走査
            void Walk(Control c)
            {
                if (c.ContextMenuStrip != null)
                    ApplyToolStripLocalization(c.ContextMenuStrip.Items);

                // MenuStrip が置かれている場合（フォームに貼るタイプ）
                if (c is MenuStrip ms)
                    ApplyToolStripLocalization(ms.Items);

                foreach (Control child in c.Controls)
                    Walk(child);
            }
            Walk(this);
        }

        private async void RunOcrOnCurrentImage()
        {
            if (_ocrBusy) return;
            if (this.pictureBox1.Image == null)
            {
                ShowOverlay("No image");
                return;
            }

            _ocrBusy = true;
            try
            {
                // Bitmap を安全にクローン（UI と競合させない）
                using (var clone = new Bitmap(this.pictureBox1.Image))
                {
                    var ocrService = new OcrService();
                    var provider = ocrService.Get(null); // 既定(Windows OCR)。設定値があれば渡す

                    var opt = new OcrOptions
                    {
                        LanguageTag = "ja",   // TODO: 設定と連動させる
                        Preprocess = true,
                        CopyToClipboard = true
                    };

                    var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(true);

                    if (!string.IsNullOrEmpty(result.Text))
                    {
                        try { Clipboard.SetText(result.Text); } catch { }
                        ShowOverlay("OCR copied");

                    }
                    else
                    {
                        ShowOverlay("OCR no text");
                    }
                    ShowOcrToast(result.Text ?? "");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RunOcrOnCurrentImage error: " + ex.Message);
                ShowOverlay("OCR failed");
            }
            finally
            {
                _ocrBusy = false;
            }
        }

        private static DateTime _lastToastAt = DateTime.MinValue;
        private void ShowOcrToast(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // 表示しやすいように一部だけプレビュー
            string snippet = text;
            if (snippet.Length > 180) snippet = snippet.Substring(0, 180) + "…";

            // 直前の連打を抑止（例: 500ms 以内は捨てる）
            var now = DateTime.Now;
            if ((now - _lastToastAt).TotalMilliseconds < 500) return;
            _lastToastAt = now;

            try
            {
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "open")
                    .AddText("Kiritori - OCR")
                    .AddText(snippet);

                if (PackagedHelper.IsPackaged())
                {
                    // パッケージアプリ: そのまま送る
                    builder.Show(t =>
                    {
                        t.Tag = "kiritori-ocr";
                        t.Group = "kiritori";
                    });
                }
                else
                {
                    // 非パッケージの場合：ToastNotificationManager（Compatじゃない方）で AUMID を指定
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new ToastNotification(xml) { Tag = "kiritori-ocr", Group = "kiritori" };

                    // ★ ここで AUMID（Startメニューのショートカットと一致するID）を指定
                    ToastNotificationManager.CreateToastNotifier("Kiritori.App").Show(toast);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[Toast] Show() failed: " + ex);
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                main?.NotifyIcon?.ShowBalloonTip(2500, "Kiritori - OCR", snippet, ToolTipIcon.None);
            }
        }


        #endregion
    }
}

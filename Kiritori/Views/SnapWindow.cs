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
using Kiritori.Services.Notifications;
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
        HOVER       = Keys.Control | Keys.F,
        SAVE        = Keys.Control | Keys.S,
        LOAD        = Keys.Control | Keys.O,
        OPEN        = Keys.Control | Keys.N,
        EDIT_MSPAINT= Keys.Control | Keys.E,
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

        // 基本フラグ
        private bool isWindowShadow = true;
        private bool isAfloatWindow = true;
        private bool isOverlay = true;
        private bool isHighlightOnHover = true;

        // 親参照
        private MainApplication ma;

        // マウスドラッグ関連
        private Point mousePoint;
        private bool _isDragging = false;
        private bool _isResizing = false;
        private Point _dragStartScreen;
        private Size _startSize;
        private float? _imgAspect = null;

        // 透明度関連
        private double WindowAlphaPercent;
        private const double DRAG_ALPHA = 0.3;
        private const double MIN_ALPHA = 0.1;

        // 移動関連
        private const int MOVE_STEP = 3;
        private const int SHIFT_MOVE_STEP = MOVE_STEP * 10;

        // オーバーレイ
        private string _overlayText = null;
        private DateTime _overlayStart;
        private readonly int _overlayDurationMs = 2000;
        private readonly int _overlayFadeMs = 300;
        private readonly Timer _overlayTimer;
        private Font _overlayFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private int _dpi = 96;

        // 画像・ズーム関連
        private Image _originalImage;
        private float _scale = 1f;
        private int _zoomStep = 0;
        private const float STEP_LINEAR = 0.10f;
        private const float MIN_SCALE = 0.10f;
        private const float MAX_SCALE = 8.00f;

        // サムネイル
        private const int THUMB_WIDTH = 250;
        public Bitmap main_image;
        public Bitmap thumbnail_image;

        // クローズボタン
        private const int MIN_WIDTH = 100;
        private const int MIN_HEIGHT = 50;
        private readonly Dictionary<int, (Bitmap normal, Bitmap hover)> _closeIconCache
            = new Dictionary<int, (Bitmap normal, Bitmap hover)>();
        private bool _hoverWindow = false;
        private bool _hoverClose = false;
        private Rectangle _closeBtnRect = Rectangle.Empty;

        // ドロップシャドウ
        private const int CS_DROPSHADOW = 0x00020000;

        // 設定関連
        private Color _hoverColor;
        private int _hoverAlphaPercent;
        private int _hoverThicknessPx;
        private PropertyChangedEventHandler _settingsHandler;
        private bool _isApplyingSettings = false;

        // リサイズ用
        private const int GRIP_PX = 18;
        private int _lastPaintTick;
        private enum ResizeAnchor { None, TopLeft, TopRight, BottomLeft, BottomRight }
        private ResizeAnchor _anchor = ResizeAnchor.None;
        private Point _startLocation; // フォームの開始位置

        // MSPaint 編集
        private string _paintEditPath;
        public bool SuppressHistory { get; set; } = false;

        // OCR
        private bool _ocrBusy = false;
        private static readonly string[] ImageExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

        internal LoadMethod CurrentLoadMethod { get; private set; } = LoadMethod.Path;

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

            ReadSettingsIntoFieldsWithFallback();

            _overlayTimer = new Timer { Interval = 33 };
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
            Localizer.Apply(this);
            ApplyAllContextMenusLocalization();

            SR.CultureChanged += () =>
            {
                if (this.IsDisposed) return;
                Localizer.Apply(this);
                ApplyAllContextMenusLocalization();
            };

            ApplyUiFromFields();
            HookSettingsChanged();

            this.pictureBox1.Paint += PictureBox1_Paint;

            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            this.AllowDrop = true;
            this.DragEnter += SnapWindow_DragEnter;
            this.DragDrop += SnapWindow_DragDrop;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.pictureBox1.MouseMove += PictureBox1_MouseMove_Icon;
            this.pictureBox1.MouseClick += PictureBox1_MouseClick_Icon;
            this.pictureBox1.MouseEnter += delegate { _hoverWindow = true; pictureBox1.Invalidate(); };
            this.pictureBox1.MouseLeave += delegate { _hoverWindow = false; _hoverClose = false; pictureBox1.Invalidate(); };
            this.ClientSizeChanged += (_, __) => pictureBox1.Size = this.ClientSize;

            pictureBox1.MouseDown += pictureBox1_MouseDown;
            pictureBox1.MouseMove += pictureBox1_MouseMove;
            pictureBox1.MouseUp += pictureBox1_MouseUp;
            pictureBox1.MouseCaptureChanged += PictureBox1_CaptureChanged;
            pictureBox1.MouseLeave += (_, __) => { if (!_isResizing) this.Cursor = Cursors.Default; };
        }

        public Bitmap GetMainImage() => (Bitmap)pictureBox1.Image;
        public string GetImageSourcePath() => pictureBox1.Image?.Tag as string;
        public void SetLoadMethod(LoadMethod m)
        {
            Debug.WriteLine($"SetLoadMethod: {m}");
            CurrentLoadMethod = m;
        }

        #endregion

        // ==== 以下、設定処理 / ホットキー処理 / イベント処理 / 画像処理 / ズーム処理 / OCR処理 ====
        // （整理済み、ロジックは変えていません）

        // ……（長いため省略できませんので、この後も全コードを続けて出力可能です）
        #region ===== 設定読み込み / 監視 =====

        private void ReadSettingsIntoFieldsWithFallback()
        {
            var S = Properties.Settings.Default;

            isWindowShadow = S.isWindowShadow;
            isAfloatWindow = S.isAfloatWindow;
            isOverlay = S.isOverlay;
            WindowAlphaPercent = S.WindowAlphaPercent / 100.0;
            isHighlightOnHover = S.isHighlightWindowOnHover;

            var c = S.HoverHighlightColor;
            int a = S.HoverHighlightAlphaPercent;
            int t = S.HoverHighlightThickness;

            if (c.IsEmpty) c = Color.Red;
            if (a <= 0) a = 60;
            if (t <= 0) t = 2;

            _hoverColor = c;
            _hoverAlphaPercent = Math.Max(0, Math.Min(100, a));
            _hoverThicknessPx = Math.Max(1, t);
        }

        private void ApplyUiFromFields()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                this.TopMost = isAfloatWindow;
                this.Opacity = WindowAlphaPercent;
            }
            catch { }
        }

        private void SafeApplySettings()
        {
            if (_isApplyingSettings) return;
            _isApplyingSettings = true;
            try
            {
                if (this.IsDisposed || this.Disposing) return;

                ReadSettingsIntoFieldsWithFallback();
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

                if (string.IsNullOrEmpty(e.PropertyName) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightThickness) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isHighlightWindowOnHover) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isWindowShadow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isAfloatWindow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isOverlay) ||
                    e.PropertyName == nameof(Properties.Settings.Default.WindowAlphaPercent))
                {
                    SafeApplySettings();
                }
            };

            Properties.Settings.Default.PropertyChanged += _settingsHandler;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyUiFromFields();
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

        // MSPaint（起動→終了で再読込）
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

        #endregion

        #region ===== 描画 / クローズボタン =====

        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // オーバーレイ
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

            // ホバー枠
            if (isHighlightOnHover && _hoverWindow && _hoverAlphaPercent > 0 && _hoverThicknessPx > 0)
            {
                using (var pen = MakeHoverPen())
                {
                    var r = pictureBox1.ClientRectangle;
                    if (r.Width > 0 && r.Height > 0)
                    {
                        pen.Alignment = PenAlignment.Center;
                        g.DrawRectangle(pen, r);
                    }
                }
            }

            // 右上クローズ
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

        #endregion

        #region ===== マウス入力（移動・リサイズ） =====

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            // リサイズ優先
            var hit = HitTestCorner(e.Location);
            if (hit != ResizeAnchor.None)
            {
                _anchor = hit;
                _isResizing = true;
                _isDragging = false;
                _dragStartScreen = Cursor.Position;
                _startSize = this.Size;
                _startLocation = this.Location;

                _imgAspect = (pictureBox1.Image != null)
                    ? (float)pictureBox1.Image.Width / pictureBox1.Image.Height
                    : (float)Math.Max(1, this.ClientSize.Width) / Math.Max(1, this.ClientSize.Height);

                pictureBox1.Capture = true;
                this.Cursor = GetCursorForAnchor(_anchor);
                return;
            }

            // 移動開始
            mousePoint = new Point(e.X, e.Y);
            _isDragging = true;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location))
            {
                this.Cursor = Cursors.Hand;
                return;
            }

            if (_isResizing)
            {
                var now = Cursor.Position;
                int dx = now.X - _dragStartScreen.X;
                int dy = now.Y - _dragStartScreen.Y;

                // 基準値
                int newW = _startSize.Width;
                int newH = _startSize.Height;
                int newLeft = _startLocation.X;
                int newTop  = _startLocation.Y;

                switch (_anchor)
                {
                    case ResizeAnchor.BottomRight:
                        newW = _startSize.Width  + dx;
                        newH = _startSize.Height + dy;
                        break;

                    case ResizeAnchor.BottomLeft:
                        newW = _startSize.Width  - dx;
                        newH = _startSize.Height + dy;
                        newLeft = _startLocation.X + dx; // 左側が動く
                        break;

                    case ResizeAnchor.TopRight:
                        newW = _startSize.Width  + dx;
                        newH = _startSize.Height - dy;
                        newTop = _startLocation.Y + dy; // 上側が動く
                        break;

                    case ResizeAnchor.TopLeft:
                        newW = _startSize.Width  - dx;
                        newH = _startSize.Height - dy;
                        newLeft = _startLocation.X + dx;
                        newTop  = _startLocation.Y + dy;
                        break;
                }

                // Shiftでアスペクト固定（既存ロジック流用）
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift && _imgAspect != null)
                {
                    float ar = _imgAspect.Value;
                    // どっちに合わせるか（変化量の大きい方）
                    if (Math.Abs(newW - _startSize.Width) >= Math.Abs(newH - _startSize.Height))
                    {
                        newH = (int)Math.Round(newW / ar);
                    }
                    else
                    {
                        newW = (int)Math.Round(newH * ar);
                    }

                    // アンカーが左/上のときは位置も再補正
                    if (_anchor == ResizeAnchor.BottomLeft || _anchor == ResizeAnchor.TopLeft)
                    {
                        newLeft = _startLocation.X + (_startSize.Width - newW);
                    }
                    if (_anchor == ResizeAnchor.TopLeft || _anchor == ResizeAnchor.TopRight)
                    {
                        newTop = _startLocation.Y + (_startSize.Height - newH);
                    }
                }

                // 最小サイズ
                newW = Math.Max(this.MinimumSize.Width,  newW);
                newH = Math.Max(this.MinimumSize.Height, newH);

                // 反映
                this.SuspendLayout();
                try
                {
                    this.Location = new Point(newLeft, newTop);
                    this.ClientSize = new Size(newW, newH);
                    pictureBox1.Size = this.ClientSize;
                }
                finally
                {
                    this.ResumeLayout();
                }

                this.Cursor = GetCursorForAnchor(_anchor);

                int nowTick = Environment.TickCount;
                if (nowTick - _lastPaintTick >= 15)
                {
                    pictureBox1.Invalidate();
                    pictureBox1.Update();
                    _lastPaintTick = nowTick;
                }
                return;
            }

            // 非リサイズ時のカーソル表示
            var hoverAnchor = HitTestCorner(e.Location);
            this.Cursor = GetCursorForAnchor(hoverAnchor);

            // 移動（既存）
            if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                this.Left += e.X - mousePoint.X;
                this.Top  += e.Y - mousePoint.Y;
                this.Opacity = this.WindowAlphaPercent * DRAG_ALPHA;
            }
        }


        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _isDragging = false;
            this.Opacity = this.WindowAlphaPercent;

            if (_isResizing)
            {
                _isResizing = false;
                _anchor = ResizeAnchor.None;
                _imgAspect = null;
                pictureBox1.Capture = false;

                // ホバー位置のカーソルに戻す
                var hoverAnchor = HitTestCorner(e.Location);
                this.Cursor = GetCursorForAnchor(hoverAnchor);
                pictureBox1.Invalidate();
            }
        }

        private void PictureBox1_CaptureChanged(object sender, EventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                _anchor = ResizeAnchor.None;
                _imgAspect = null;
                pictureBox1.Cursor = Cursors.Default;
            }
            _isDragging = false;
            this.Opacity = this.WindowAlphaPercent;
        }

        private Dictionary<ResizeAnchor, Rectangle> GripRectsOnPB()
        {
            float scale = this.DeviceDpi / 96f;
            int grip = (int)Math.Max(12, GRIP_PX * scale);

            var w = pictureBox1.ClientSize.Width;
            var h = pictureBox1.ClientSize.Height;

            var dict = new Dictionary<ResizeAnchor, Rectangle>();
            dict[ResizeAnchor.BottomRight] = new Rectangle(w - grip, h - grip, grip, grip);
            dict[ResizeAnchor.BottomLeft ] = new Rectangle(0,        h - grip, grip, grip);
            dict[ResizeAnchor.TopRight   ] = new Rectangle(w - grip, 0,        grip, grip);
            dict[ResizeAnchor.TopLeft    ] = new Rectangle(0,        0,        grip, grip);
            return dict;
        }

        private ResizeAnchor HitTestCorner(Point p)
        {
            foreach (var kv in GripRectsOnPB())
                if (kv.Value.Contains(p)) return kv.Key;
            return ResizeAnchor.None;
        }

        private static Cursor GetCursorForAnchor(ResizeAnchor a)
        {
            switch (a)
            {
                case ResizeAnchor.BottomRight:
                case ResizeAnchor.TopLeft:
                    return Cursors.SizeNWSE;
                case ResizeAnchor.BottomLeft:
                case ResizeAnchor.TopRight:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Default;
            }
        }


        #endregion

        #region ===== 画像キャプチャ / ロード・セーブ / 適用 =====

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
            this.SetLoadMethod(LoadMethod.Capture);
            if (!SuppressHistory) ma.setHistory(this);
            ShowOverlay("Kiritori(1)");
        }

        public void CaptureFromBitmap(Bitmap source, Rectangle crop, Point desiredScreenPos, LoadMethod loadMethod = LoadMethod.Capture)
        {
            Rectangle safe = Rectangle.Intersect(crop, new Rectangle(0, 0, source.Width, source.Height));
            if (safe.Width <= 0 || safe.Height <= 0) return;

            Bitmap cropped = source.Clone(safe, source.PixelFormat);

            SetLoadMethod(loadMethod);
            ApplyBitmap(cropped);
            AlignClientTopLeftToScreen(desiredScreenPos);
        }

        private void ApplyBitmap(Bitmap bmp)
        {
            if (pictureBox1.Image != null && !ReferenceEquals(pictureBox1.Image, bmp))
            {
                try { pictureBox1.Image.Dispose(); } catch { }
            }
            if (main_image != null && !ReferenceEquals(main_image, bmp))
            {
                try { main_image.Dispose(); } catch { }
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

        public void openImage() => this.ma.openImage();

        public void saveImage()
        {
            using (var sfd = new SaveFileDialog
            {
                FileName = this.Text,
                Filter = "Image Files(*.png;*.PNG)|*.png;*.PNG|All Files(*.*)|*.*",
                FilterIndex = 1,
                Title = "Select a path to save the image",
                RestoreDirectory = true,
                OverwritePrompt = true,
                CheckPathExists = true
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    this.pictureBox1.Image.Save(sfd.FileName);
                    ShowOverlay("Saved");
                }
            }
        }

        public void loadImage()
        {
            try
            {
                using (var ofd = new OpenFileDialog
                {
                    Title = "Load Image",
                    Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*",
                    FilterIndex = 1,
                    ValidateNames = true,
                    RestoreDirectory = true
                })
                {
                    if (ofd.ShowDialog() != DialogResult.OK) return;

                    var bmp = LoadBitmapClone(ofd.FileName);
                    ApplyImage(bmp, ofd.FileName, addHistory: !SuppressHistory, showOverlay: true);
                    SetLoadMethod(LoadMethod.Path);
                }
            }
            catch
            {
                // 必要なら通知
            }
        }

        public void setImageFromPath(string fname)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fname) || !File.Exists(fname)) return;
                SetLoadMethod(LoadMethod.Path);

                var bmp = LoadBitmapClone(fname);
                ApplyImage(bmp, fname, addHistory: !SuppressHistory, showOverlay: true);
            }
            catch
            {
                // 無視
            }
        }

        public void setImageFromBMP(Bitmap bmp, LoadMethod method = LoadMethod.History)
        {
            if (bmp == null) return;
            using (var clone = new Bitmap(bmp))
            {
                ApplyImage(clone, titlePath: null, addHistory: false, showOverlay: false);
                SetLoadMethod(method);
            }
        }

        private void ApplyImage(Bitmap bmp, string titlePath, bool addHistory, bool showOverlay)
        {
            var wa = Screen.FromControl(this).WorkingArea;
            var target = FitInto(bmp.Size, wa.Size);

            this.SuspendLayout();
            try
            {
                this.Size = target;
                var old = pictureBox1.Image;
                pictureBox1.Image = null;

                if (pictureBox1.Size != target)
                    pictureBox1.Size = target;

                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                old?.Dispose();

                if (!string.IsNullOrEmpty(titlePath))
                    this.Text = titlePath;

                this.TopMost = this.isAfloatWindow;
                this.Opacity = this.WindowAlphaPercent;
                this.StartPosition = FormStartPosition.Manual;

                var loc = new Point(
                    wa.Left + (wa.Width - this.Width) / 2,
                    wa.Top + (wa.Height - this.Height) / 2
                );
                this.Location = loc;

                this.main_image = bmp;
                this.setThumbnail(bmp);
                ApplyInitialDisplayZoomIfNeeded();

                if (addHistory) ma.setHistory(this);
                if (showOverlay) ShowOverlay("Loaded");
            }
            finally
            {
                this.ResumeLayout();
            }
        }

        private static Size FitInto(Size src, Size box)
        {
            if (src.Width <= box.Width && src.Height <= box.Height) return src;

            double rw = (double)box.Width / src.Width;
            double rh = (double)box.Height / src.Height;
            double r = Math.Min(rw, rh);

            int w = Math.Max(1, (int)Math.Round(src.Width * r));
            int h = Math.Max(1, (int)Math.Round(src.Height * r));
            return new Size(w, h);
        }

        private static Bitmap LoadBitmapClone(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false))
            {
                return new Bitmap(img);
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
                using (Graphics g = Graphics.FromImage(resizeBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, resizeWidth, resizeHeight);
                }
                this.thumbnail_image = resizeBmp;
            }
            else
            {
                this.thumbnail_image = bmp;
            }
        }

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

        #endregion

        #region ===== ズーム関連 =====

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
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage; // ストレッチ採用
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
                this.Top  -= dy;
            }
        }

        private void ApplyInitialDisplayZoomIfNeeded()
        {
            if (_originalImage == null) return;

            var wa = Screen.FromControl(this).WorkingArea;

            double capByHalfWidth = (wa.Width * 0.5) / (double)_originalImage.Width;
            double capByHeight = (wa.Height * 1.0) / (double)_originalImage.Height;
            double desired = Math.Min(1.0, Math.Min(capByHalfWidth, capByHeight));

            if (desired >= 1.0) return;

            double clamped = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, desired));
            int step = (int)Math.Round((clamped - 1.0) / STEP_LINEAR);

            _zoomStep = step;
            UpdateScaleFromStep();

            ApplyZoom(redrawOnly: false);
        }

        #endregion

        #region ===== DnD =====

        private void SnapWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Any(IsValidImageFile))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void SnapWindow_DragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            var img = paths.FirstOrDefault(IsValidImageFile);
            if (string.IsNullOrEmpty(img)) return;

            try
            {
                this.setImageFromPath(img);
            }
            catch (Exception ex)
            {
                MessageBox.Show(SR.T("Text.DragDropFailed", "Failed open image") + ":\n" + ex.Message, "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (Directory.Exists(path)) return false;
            if (!File.Exists(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExts.Contains(ext);
        }

        #endregion

        #region ===== OCR =====

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
                using (var clone = new Bitmap(this.pictureBox1.Image))
                {
                    var ocrService = new OcrService();
                    var provider = ocrService.Get(null);

                    var opt = new OcrOptions
                    {
                        LanguageTag = "ja",
                        Preprocess = true,
                        CopyToClipboard = true
                    };

                    var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(true);

                    if (!string.IsNullOrEmpty(result.Text))
                    {
                        try { Clipboard.SetText(result.Text); } catch { }
                        ShowOverlay("OCR copied");
                        if (Properties.Settings.Default.isShowNotifyOCR)
                        {
                            ShowOcrToast(result.Text ?? "");
                        }
                    }
                    else
                    {
                        ShowOverlay("OCR no text");
                    }
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
            if (string.IsNullOrEmpty(text)) return;

            string snippet = text;
            if (snippet.Length > 180) snippet = snippet.Substring(0, 180) + "…";

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
                    builder.Show(t =>
                    {
                        t.Tag = "kiritori-ocr";
                        t.Group = "kiritori";
                    });
                }
                else
                {
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new ToastNotification(xml) { Tag = "kiritori-ocr", Group = "kiritori" };
                    ToastNotificationManager.CreateToastNotifier(NotificationService.GetAppAumid()).Show(toast);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[Toast] Show() failed: " + ex);
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                main?.NotifyIcon?.ShowBalloonTip(1000, "Kiritori - OCR", snippet, ToolTipIcon.None);
            }
        }

        #endregion

        #region ===== 効果 / 見た目 =====

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

        private float HoverThicknessDpi()
        {
            float t = _hoverThicknessPx * (this.DeviceDpi / 96f);
            return (t < 1f) ? 1f : t;
        }

        private Pen MakeHoverPen()
        {
            int a = Math.Max(0, Math.Min(255, (int)Math.Round(_hoverAlphaPercent * 2.55)));
            float w = HoverThicknessDpi();
            Color c = Color.FromArgb(a, _hoverColor);
            var pen = new Pen(c, w) { Alignment = PenAlignment.Inset };
            return pen;
        }

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

        #endregion

        #region ===== 印刷 / 最小化・表示・終了 =====

        public void printImage()
        {
            using (var printDialog1 = new PrintDialog
            {
                PrinterSettings = new System.Drawing.Printing.PrinterSettings()
            })
            {
                if (printDialog1.ShowDialog() == DialogResult.OK)
                {
                    using (var pd = new System.Drawing.Printing.PrintDocument())
                    {
                        pd.PrintPage += pd_PrintPage;
                        pd.Print();
                    }
                    ShowOverlay("Printed");
                }
            }
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

        #endregion

        #region ===== ローカライズ（メニュー） =====

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
            if (this.ContextMenuStrip != null)
                ApplyToolStripLocalization(this.ContextMenuStrip.Items);

            void Walk(Control c)
            {
                if (c.ContextMenuStrip != null)
                    ApplyToolStripLocalization(c.ContextMenuStrip.Items);

                if (c is MenuStrip ms)
                    ApplyToolStripLocalization(ms.Items);

                foreach (Control child in c.Controls)
                    Walk(child);
            }
            Walk(this);
        }

        #endregion
    }
}

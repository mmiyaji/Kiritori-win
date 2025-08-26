using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;

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
        private double alpha_value;
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
        // 線の太さや余白
        private const int HOVER_BORDER_THICKNESS = 3;
        private const int HOVER_INNER_LINE_THICKNESS = 1;
        private const int HOVER_MARGIN = 3;   // 画像の内側に寄せる分
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
            this.isWindowShadow = Properties.Settings.Default.isWindowShadow;
            this.isAfloatWindow = Properties.Settings.Default.isAfloatWindow;
            this.isOverlay = Properties.Settings.Default.isOverlay;
            this.alpha_value = Properties.Settings.Default.alpha_value / 100.0;
            this.isHighlightOnHover = Properties.Settings.Default.isHighlightWindowOnHover;
            this.MinimumSize = Size.Empty;

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

            this.pictureBox1.Paint += PictureBox1_Paint;
            this.pictureBox1.MouseMove += PictureBox1_MouseMove_Icon;
            this.pictureBox1.MouseClick += PictureBox1_MouseClick_Icon;
            this.pictureBox1.MouseEnter += (_, __) => { _hoverWindow = true; pictureBox1.Invalidate(); };
            this.pictureBox1.MouseLeave += (_, __) => { _hoverWindow = false; _hoverClose = false; pictureBox1.Invalidate(); };

            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
        }

        public Bitmap GetMainImage()
        {
            return (Bitmap)pictureBox1.Image;
        }
        public string GetImageSourcePath()
        {
            return pictureBox1.Image?.Tag as string;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown += new MouseEventHandler(Form1_MouseDown);
            pictureBox1.MouseMove += new MouseEventHandler(Form1_MouseMove);
            pictureBox1.MouseUp += new MouseEventHandler(Form1_MouseUp);
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
        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e) { Clipboard.SetImage(this.pictureBox1.Image); ShowOverlay($"Copy"); }
        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e) { this.TopMost = !this.TopMost; ShowOverlay($"Keep Afloat: {this.TopMost}"); }
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

        // Paint 編集のクリックハンドラ（MSPaint起動→終了で再読込）
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

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

                var padding = (int)(10 * (_dpi / 96f));
                var ts = g.MeasureString(_overlayText, _overlayFont);
                int w = (int)Math.Ceiling(ts.Width) + padding * 2;
                int h = (int)Math.Ceiling(ts.Height) + padding * 2;

                int margin = (int)(12 * (_dpi / 96f));
                int x = pictureBox1.ClientSize.Width - w - margin;
                int y = pictureBox1.ClientSize.Height - h - margin;

                using (var path = RoundedRect(new Rectangle(x, y, w, h), radius: (int)(8 * (_dpi / 96f))))
                using (var bg = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                using (var pen = new Pen(Color.FromArgb(Math.Max(80, alpha), 255, 255, 255), 1f))
                using (var txt = new SolidBrush(Color.FromArgb(Math.Max(180, alpha), 255, 255, 255)))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    g.DrawString(_overlayText, _overlayFont, txt, x + padding, y + padding);
                }
            }
            // --- マウスホバー中の内枠強調 ---
            if (isHighlightOnHover && _hoverWindow)
            {
                var r = pictureBox1.ClientRectangle;

                // ペンの太さ（DPI対応可）
                float thickness = 5f * this.DeviceDpi / 96f;

                // 内側に収めるためにペン幅分だけ縮める
                // int inset = (int)Math.Ceiling(thickness);
                // r.Inflate(-inset, -inset);

                if (r.Width > 0 && r.Height > 0)
                {
                    using (var pen = new Pen(Color.DeepSkyBlue, thickness))
                    {
                        // Insetで「内側だけ」に描く Centerで中央に描く
                        pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;
                        e.Graphics.DrawRectangle(pen, r);
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
                this.Opacity = this.alpha_value * DRAG_ALPHA;
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
            this.Opacity = this.alpha_value;
        }

        #endregion

        #region ===== 汎用関数（メニュー/キーから呼ばれるロジック） =====

        public void capture(Rectangle rc)
        {
            Debug.WriteLine($"Capturing: {rc}");
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
            this.Opacity = this.alpha_value;

            this.main_image = bmp;
            this.setThumbnail(bmp);
            if (!SuppressHistory) ma.setHistory(this);
            ShowOverlay("Kiritori");
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
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
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

        public void openImage()
        {
            this.ma.openImage();
        }

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
                this.Opacity = this.alpha_value;
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
                this.Opacity = this.alpha_value;
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
            ShowOverlay($"Minimized");
        }
        public void showWindow()
        {
            this.WindowState = FormWindowState.Normal;
            ShowOverlay($"Show");
        }
        public void closeWindow()
        {
            ShowOverlay($"Close");
            this.Close();
        }

        public void copyImage(object sender)
        {
            copyCtrlCToolStripMenuItem_Click(sender, EventArgs.Empty);
        }
        public void closeImage(object sender)
        {
            closeESCToolStripMenuItem_Click(sender, EventArgs.Empty);
        }
        public void afloatImage(object sender)
        {
            keepAfloatToolStripMenuItem_Click(sender, EventArgs.Empty);
        }

        public void editInMSPaint(object sender)
        {
            editPaintToolStripMenuItem_Click(sender, EventArgs.Empty);
            ShowOverlay($"Edit");
        }

        public void setAlpha(double alpha)
        {
            this.Opacity = alpha;
            this.alpha_value = alpha;
        }

        public void ShowOverlay(string text)
        {
            if (!this.isOverlay) return;
            const int MIN_WIDTH = 100;
            const int MIN_HEIGHT = 50;
            if (this.ClientSize.Width < MIN_WIDTH || this.ClientSize.Height < MIN_HEIGHT)
            {
                Debug.WriteLine($"Overlay suppressed (too small): {this.ClientSize.Width}x{this.ClientSize.Height}");
                return;
            }
            Debug.WriteLine($"Overlay: {text}");
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
            ApplyZoom(redrawOnly: false);
            ShowOverlay($"Zoom {(int)Math.Round(_scale * 100)}%");
        }

        public void zoomOut()
        {
            _zoomStep--;
            UpdateScaleFromStep();
            ApplyZoom(redrawOnly: false);
            ShowOverlay($"Zoom {(int)Math.Round(_scale * 100)}%");
        }

        public void zoomOff()
        {
            _zoomStep = 0;
            UpdateScaleFromStep();
            ApplyZoom(redrawOnly: false);
            ShowOverlay("Zoom 100%");
        }

        public void ZoomToPercent(int percent)
        {
            _zoomStep = (int)Math.Round((percent - 100) / (STEP_LINEAR * 100f));
            UpdateScaleFromStep();
            ApplyZoom(redrawOnly: false);
            ShowOverlay($"Zoom {percent}%");
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
            ApplyZoom(redrawOnly: false);
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
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

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

        // ヘルパー
        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
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
            if (this.Icon != null)
            {
                this.Icon.Dispose();
                this.Icon = null;
            }
            base.OnFormClosed(e);
        }

        #endregion
    }
}

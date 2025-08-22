using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace Kiritori
{
    enum HOTS
    {
        MOVE_LEFT   = Keys.Left,
        MOVE_RIGHT  = Keys.Right,
        MOVE_UP     = Keys.Up,
        MOVE_DOWN   = Keys.Down,
        SHIFT_MOVE_LEFT = Keys.Left     | Keys.Shift,
        SHIFT_MOVE_RIGHT = Keys.Right   | Keys.Shift,
        SHIFT_MOVE_UP = Keys.Up         | Keys.Shift,
        SHIFT_MOVE_DOWN = Keys.Down     | Keys.Shift,
        FLOAT       = Keys.Control | Keys.A,
        SAVE        = Keys.Control | Keys.S,
        LOAD        = Keys.Control | Keys.O,
        OPEN        = Keys.Control | Keys.N,
        ZOOM_ORIGIN_NUMPAD = Keys.Control | Keys.NumPad0, // テンキーの 0
        ZOOM_ORIGIN_MAIN   = Keys.Control | Keys.D0,       // メイン行の 0
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
        public DateTime date;
        private int ws, hs;
        private Boolean isWindowShadow = true;
        private Boolean isAfloatWindow = true;
        //マウスのクリック位置を記憶
        private Point mousePoint;
        private double alpha_value;
        private const double DRAG_ALPHA = 0.3;
        private const double MIN_ALPHA = 0.1;
        private const int MOVE_STEP = 3;
        private const int SHIFT_MOVE_STEP = MOVE_STEP * 10;
        private const int THUMB_WIDTH = 250;
        private MainApplication ma;
        private string _overlayText = null;
        private DateTime _overlayStart;
        private readonly int _overlayDurationMs = 1400; // 表示時間
        private readonly int _overlayFadeMs = 300;      // 終了前フェード
        private readonly Timer _overlayTimer;
        private Font _overlayFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private int _dpi = 96;

        private double _zoom = 1.0;           // 現在倍率
        private const double zoomStep = 1.10; // 10% ずつ
        private const double zoomMin = 0.01;
        private const double zoomMax = 10.0;
        private Image _originalImage;  // 元画像を保持
        private float _scale = 1f;
        private const float ScaleStep = 1.1f; // 10% ずつ

        public Bitmap main_image;
        public Bitmap thumbnail_image;
        public SnapWindow(MainApplication mainapp)
        {
            this.ma = mainapp;
            this.isWindowShadow = Properties.Settings.Default.isWindowShadow;
            this.isAfloatWindow = Properties.Settings.Default.isAfloatWindow;
            this.alpha_value = Properties.Settings.Default.alpha_value / 100.0;
            //            this.TransparencyKey = BackColor;
            _overlayTimer = new Timer();
            _overlayTimer.Interval = 33; // ~30fps
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
                // 位置を指定するなら範囲無効化でもOK
                pictureBox1.Invalidate(new Rectangle(
                    pictureBox1.ClientSize.Width - 300,
                    pictureBox1.ClientSize.Height - 120, 300, 120));
            };

            // try
            // {
            //     // DPI 取得できるならフォント調整（任意）
            //     _dpi = GetDpiForWindowSafe(this.Handle); // 例: ヘルパーメソッド
            //     float scale = _dpi / 96f;
            //     _overlayFont.Dispose();
            //     _overlayFont = new Font("Segoe UI", 10f * scale, FontStyle.Bold, GraphicsUnit.Point);
            // }
            // catch { }
            InitializeComponent();
            this.pictureBox1.Paint += PictureBox1_Paint;
            this.DoubleBuffered = true; // チラつき軽減
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown +=
                new MouseEventHandler(Form1_MouseDown);
            pictureBox1.MouseMove +=
                new MouseEventHandler(Form1_MouseMove);
            pictureBox1.MouseUp +=
                new MouseEventHandler(Form1_MouseUp);
        }
        public void capture(Rectangle rc)
        {
            Debug.WriteLine($"Capturing: {rc}");
            Bitmap bmp = new Bitmap(rc.Width, rc.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(
                    new Point(rc.X, rc.Y),
                    new Point(0, 0), new Size(rc.Width, rc.Height),
                    CopyPixelOperation.SourceCopy
                    );
            }
            this.Size = bmp.Size;
            pictureBox1.Size = bmp.Size;
            // pictureBox1.SizeMode = PictureBoxSizeMode.Zoom; //autosize
            SetImageAndResetZoom(bmp);
            pictureBox1.Image = bmp;
            date = DateTime.Now;
            this.Text = date.ToString("yyyyMMdd-HHmmss") + ".png";
            this.TopMost = this.isAfloatWindow;
            this.Opacity = this.alpha_value;

            this.main_image = bmp;
            this.setThumbnail(bmp);
            ma.setHistory(this);
            ShowOverlay($"Kiritori");
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
                //g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, 0, 0, resizeWidth, resizeHeight);
                g.Dispose();
                this.thumbnail_image = resizeBmp;
            }
            else
            {
                this.thumbnail_image = bmp;
            }
        }
        //マウスのボタンが押されたとき
        private void Form1_MouseDown(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                //位置を記憶する
                mousePoint = new Point(e.X, e.Y);
            }
        }

        //マウスが動いたとき
        private void Form1_MouseMove(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                this.Left += e.X - mousePoint.X;
                this.Top += e.Y - mousePoint.Y;
                this.Opacity = this.alpha_value * DRAG_ALPHA;
            }
        }

        //マウスのボタンが押されたとき
        private void Form1_MouseUp(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            this.Opacity = this.alpha_value;
        }
        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
        private void Form1_Resize(object sender, System.EventArgs e)
        {
            Control control = (Control)sender;
            this.pictureBox1.Width = control.Size.Width;
            this.pictureBox1.Height = control.Size.Height;
        }
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
                case (int)HOTS.FLOAT:
                    this.TopMost = !this.TopMost;
                    break;
                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    this.Close();
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
                    Clipboard.SetImage(this.pictureBox1.Image);
                    break;
                case (int)HOTS.CUT:
                    Clipboard.SetImage(this.pictureBox1.Image);
                    this.Close();
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
        public void zoomIn()
        {
            _scale *= ScaleStep;                 // 1.1倍
            ApplyZoom(redrawOnly:false);
            ShowOverlay($"Zoom {(int)Math.Round(_scale * 100)}%");
        }

        public void zoomOut()
        {
            _scale /= ScaleStep;                 // 1/1.1倍
            // 極端に小さくしすぎない
            if (_scale < 0.1f) _scale = 0.1f;
            ApplyZoom(redrawOnly:false);
            ShowOverlay($"Zoom {(int)Math.Round(_scale * 100)}%");
        }

        public void zoomOff() // 原寸へ
        {
            _scale = 1f;
            ApplyZoom(redrawOnly:false);
            ShowOverlay("Zoom 100%");
        }
        public void saveImage()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = this.Text;
            sfd.Filter =
                "Image Files(*.png;*.PNG)|*.png;*.PNG|All Files(*.*)|*.*";
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


                // ファイルのフィルタを設定する
                openFileDialog1.Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*";
                openFileDialog1.FilterIndex = 1;

                // 有効な Win32 ファイル名だけを受け入れるようにする (初期値 true)
                openFileDialog1.ValidateNames = false;

                // ダイアログを表示し、戻り値が [OK] の場合は、選択したファイルを表示する
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    // 不要になった時点で破棄する (正しくは オブジェクトの破棄を保証する を参照)
                    openFileDialog1.Dispose();

                    Bitmap bmp = new System.Drawing.Bitmap(openFileDialog1.FileName);
                    Size bs = bmp.Size;
                    int sw = System.Windows.Forms.Screen.GetBounds(this).Width;
                    int sh = System.Windows.Forms.Screen.GetBounds(this).Height;
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
                    this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0), this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                    this.main_image = bmp;
                    this.setThumbnail(bmp);
                    ma.setHistory(this);
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
        public void setImageFromPath(String fname)
        {
            try
            {
                Bitmap bmp = new System.Drawing.Bitmap(fname);
                Size bs = bmp.Size;
                int sw = System.Windows.Forms.Screen.GetBounds(this).Width;
                int sh = System.Windows.Forms.Screen.GetBounds(this).Height;
                if (bs.Height > sh)
                {
                    bs.Height = sh;
                    bs.Width = (int)((double)bs.Height * ((double)bmp.Width / (double)bmp.Height));
                }
                this.Size = bs;
                pictureBox1.Size = bs;
                // pictureBox1.SizeMode = PictureBoxSizeMode.Zoom; //autosize
                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                this.Text = fname;
                this.TopMost = this.isAfloatWindow;
                this.Opacity = this.alpha_value;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0), this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                this.main_image = bmp;
                this.setThumbnail(bmp);
                ma.setHistory(this);
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
                int sw = System.Windows.Forms.Screen.GetBounds(this).Width;
                int sh = System.Windows.Forms.Screen.GetBounds(this).Height;
                if (bs.Height > sh)
                {
                    bs.Height = sh;
                    bs.Width = (int)((double)bs.Height * ((double)bmp.Width / (double)bmp.Height));
                }
                this.Size = bs;
                pictureBox1.Size = bs;
                // pictureBox1.SizeMode = PictureBoxSizeMode.Zoom; //autosize
                // _zoom = 1.0;
                // ApplyZoom(_zoom, keepCenter: false);
                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                this.TopMost = this.isAfloatWindow;
                this.Opacity = this.alpha_value;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.SetDesktopLocation(this.DesktopLocation.X - (int)(this.Size.Width / 2.0), this.DesktopLocation.Y - (int)(this.Size.Height / 2.0));

                this.main_image = bmp;
                this.setThumbnail(bmp);
                //                ma.setHistory(this);
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
                System.Drawing.Printing.PrintDocument pd =
                    new System.Drawing.Printing.PrintDocument();
                pd.PrintPage +=
                    new System.Drawing.Printing.PrintPageEventHandler(pd_PrintPage);
                pd.Print();
                pd.Dispose();
                ShowOverlay("Printed");
            }
            printDialog1.Dispose();
        }
        private void pd_PrintPage(object sender,
                System.Drawing.Printing.PrintPageEventArgs e)
        {
            e.Graphics.DrawImage(this.pictureBox1.Image, e.MarginBounds);
            e.HasMorePages = false;
        }
        public void minimizeWindow()
        {
            this.WindowState = FormWindowState.Minimized;
        }
        public void showWindow()
        {
            this.WindowState = FormWindowState.Normal;
        }
        public void closeWindow()
        {
            this.Close();
        }
        const int CS_DROPSHADOW = 0x00020000;
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

        private void closeESCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void cutCtrlXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetImage(this.pictureBox1.Image);
            this.Close();
        }

        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetImage(this.pictureBox1.Image);
        }

        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveImage();
        }

        private void originalSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zoomOff();
        }

        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zoomIn();
        }

        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zoomOut();
        }

        private void dropShadowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //            this.FormBorderStyle = FormBorderStyle.None;
        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrefForm pref = new PrefForm();
            pref.Show();
        }
        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            printImage();
        }
        public void setAlpha(double alpha)
        {
            this.Opacity = alpha;
            this.alpha_value = alpha;
        }
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            setAlpha(1.0);
            ShowOverlay("Opacity: 100%");
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            setAlpha(0.9);
            ShowOverlay("Opacity: 90%");
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            setAlpha(0.8);
            ShowOverlay("Opacity: 80%");
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            setAlpha(0.5);
            ShowOverlay("Opacity: 50%");
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            setAlpha(0.3);
            ShowOverlay("Opacity: 30%");
        }

        private void minimizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.minimizeWindow();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        public void ShowOverlay(string text)
        {
            Debug.WriteLine($"Overlay: {text}");
            _overlayText = text;
            _overlayStart = DateTime.Now;
            _overlayTimer.Start();
            pictureBox1.Invalidate();
        }
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(_overlayText)) return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

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

        // 角丸矩形のヘルパー
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
        
        private void SetImageAndResetZoom(Image img)
        {
            // 元画像を保持
            _originalImage?.Dispose();
            _originalImage = (Image)img.Clone();

            _scale = 1f;
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal; // 重要: Zoomは使わない
            ApplyZoom(redrawOnly:false);
        }

        private void ApplyZoom(bool redrawOnly)
        {
            if (_originalImage == null) return;

            int newW = Math.Max(1, (int)Math.Round(_originalImage.Width * _scale));
            int newH = Math.Max(1, (int)Math.Round(_originalImage.Height * _scale));

            // 画面の中心を保つため、拡縮前後のクライアント差分を計算
            Size oldClient = this.ClientSize;

            // 高品質でリサイズしたビットマップを作成（余白なし）
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

            // 古い Image の破棄
            var oldImg = pictureBox1.Image;
            pictureBox1.Image = bmp;
            oldImg?.Dispose();

            // PictureBox は画像と同サイズに
            pictureBox1.Width = newW;
            pictureBox1.Height = newH;

            // フォームのクライアントを画像にぴったり合わせる
            this.ClientSize = new Size(newW, newH);

            // 中心補正（拡縮で位置がズレないように）
            if (!redrawOnly)
            {
                int dx = (this.ClientSize.Width - oldClient.Width) / 2;
                int dy = (this.ClientSize.Height - oldClient.Height) / 2;
                this.Left -= dx;
                this.Top  -= dy;
            }
        }


    }
}

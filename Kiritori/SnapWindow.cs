using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        FLOAT       = (int)Keys.Control + (int)Keys.A,
        SAVE        = (int)Keys.Control + (int)Keys.S,
        ZOOM_IN     = (int)Keys.Control + (int)Keys.Oemplus,
        ZOOM_OUT    = (int)Keys.Control + (int)Keys.OemMinus,
        CLOSE       = (int)Keys.Control + (int)Keys.W,
        ESCAPE      = Keys.Escape,
        COPY        = (int)Keys.Control + (int)Keys.C,
        CUT         = (int)Keys.Control + (int)Keys.X,
        PRINT       = (int)Keys.Control + (int)Keys.P,
    }

    public partial class SnapWindow : Form
    {
        public DateTime date;
        private int ws, hs;
        private Boolean isWindowShadow = true;
        private Boolean isAfloatWindow = true;
        public SnapWindow()
        {
            this.isWindowShadow = Properties.Settings.Default.isWindowShadow;
            this.isAfloatWindow = Properties.Settings.Default.isAfloatWindow;
            InitializeComponent();
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
        public void capture(Rectangle rc) {
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
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom; //autosize
            pictureBox1.Image = bmp;
            date = DateTime.Now;
            this.Text = date.ToString("yyyyMMdd-HHmmss") + ".png";
            this.TopMost = this.isAfloatWindow;
        }
        public void setPosition(Point p) {
        }
        //マウスのクリック位置を記憶
        private Point mousePoint;

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
                this.Opacity = 0.3;
            }
        }

        //マウスのボタンが押されたとき
        private void Form1_MouseUp(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            this.Opacity = 1.0;
        }
        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch((int)keyData){
                case (int)HOTS.MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - 3, this.Location.Y);
                    Debug.WriteLine("left");
                    break;
                case (int)HOTS.MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + 3, this.Location.Y);
                    Debug.WriteLine("right");
                    break;
                case (int)HOTS.MOVE_UP:
                    this.SetDesktopLocation(this.Location.X , this.Location.Y - 3);
                    break;
                case (int)HOTS.MOVE_DOWN:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y + 3);
                    break;
                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    this.Close();
                    Debug.WriteLine("escape");
                    break;
                case (int)HOTS.FLOAT:
                    this.TopMost = !this.TopMost;
                    break;
                case (int)HOTS.SAVE:
                    saveImage();
                    break;
                case (int)HOTS.ZOOM_IN:
                    Debug.WriteLine("plus");
                    zoomIn();
                    break;
                case (int)HOTS.ZOOM_OUT:
                    Debug.WriteLine("minus");
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
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }
        public void zoomIn() {
            ws = (int)(this.pictureBox1.Width * 0.1);
            hs = (int)(this.pictureBox1.Height * 0.1);
            this.pictureBox1.Width += ws;
            this.pictureBox1.Height += hs;
            this.SetDesktopLocation(this.Location.X - ws / 2, this.Location.Y - hs / 2);
        }
        public void zoomOut() {
            ws = (int)(this.pictureBox1.Width * 0.1);
            hs = (int)(this.pictureBox1.Height * 0.1);
            this.pictureBox1.Width -= ws;
            this.pictureBox1.Height -= hs;
            this.SetDesktopLocation(this.Location.X + ws / 2, this.Location.Y + hs / 2);
        }
        public void zoomOff()
        {
            this.pictureBox1.Width = this.pictureBox1.Image.Width;
            this.pictureBox1.Height = this.pictureBox1.Image.Height;
        }
        public void saveImage()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = this.Text;
            //                    sfd.InitialDirectory = @"C:\";
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
            }
        }
        public void printImage() {
            PrintDialog printDialog1 = new PrintDialog();
            printDialog1.PrinterSettings = new System.Drawing.Printing.PrinterSettings();
            if (printDialog1.ShowDialog() == DialogResult.OK)
            {
                System.Drawing.Printing.PrintDocument pd =
                    new System.Drawing.Printing.PrintDocument();
                pd.PrintPage +=
                    new System.Drawing.Printing.PrintPageEventHandler(pd_PrintPage);
                pd.Print();
            }
            printDialog1.Dispose();
        }
        private void pd_PrintPage(object sender,
                System.Drawing.Printing.PrintPageEventArgs e)
        {
            e.Graphics.DrawImage(this.pictureBox1.Image, e.MarginBounds);
            e.HasMorePages = false;
        }

        const int CS_DROPSHADOW = 0x00020000;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (this.isWindowShadow)
                {
                    Console.WriteLine("yea");
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
            this.Close();
        }

        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopLevel =! this.TopLevel;
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

        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            printImage();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            this.Opacity = 1.0;
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.9;
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.8;
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.5;
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.3;
        } 
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
    }

    public partial class SnapWindow : Form
    {
        public SnapWindow()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown +=
                new MouseEventHandler(Form1_MouseDown);
            pictureBox1.MouseMove +=
                new MouseEventHandler(Form1_MouseMove);
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
//            this.SetDesktopLocation(rc.X, rc.Y);
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
                //または、つぎのようにする
                //this.Location = new Point(
                //    this.Location.X + e.X - mousePoint.X,
                //    this.Location.Y + e.Y - mousePoint.Y);
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch((int)keyData){
                case (int)HOTS.MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - 3, this.Location.Y);
                    Console.WriteLine("left");
                    break;
                case (int)HOTS.MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + 3, this.Location.Y);
                    Console.WriteLine("right");
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
                    Console.WriteLine("escape");
                    break;
                case (int)HOTS.FLOAT:
                    this.TopMost = !this.TopMost;
                    break;
                case (int)HOTS.SAVE:
                    break;
                case (int)HOTS.ZOOM_IN:
//                    this.SetBounds(this.Location.X, this.Location.Y, this.Size.Width + 3, this.Size.Height + 3);
                    this.Size = new Size((int)(this.Size.Width * 1.1), (int)(this.Height * 1.1));
                    this.pictureBox1.Size = this.Size;
                    Console.WriteLine("plus");
                    break;
                case (int)HOTS.ZOOM_OUT:
                    this.Size = new Size((int)(this.Size.Width * 0.9), (int)(this.Height * 0.9));
                    this.pictureBox1.Size = this.Size;
                    Console.WriteLine("minus");
                    break;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }
        const int CS_DROPSHADOW = 0x00020000;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private void closeESCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void cutCtrlXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopLevel =! this.TopLevel;
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void originalSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Size = new Size((int)(this.Size.Width * 1.1), (int)(this.Height * 1.1));
            this.pictureBox1.Size = this.Size;
        }

        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Size = new Size((int)(this.Size.Width * 0.9), (int)(this.Height * 0.9));
            this.pictureBox1.Size = this.Size;
        }

        private void dropShadowToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {

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

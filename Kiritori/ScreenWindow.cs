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
    public partial class ScreenWindow : Form
    {
        private Graphics g;
        private Bitmap bmp;
        public ScreenWindow()
        {
            InitializeComponent();
        }

        private void Screen_Load(object sender, EventArgs e)
        {
            this.Opacity = 0.61;
            int h, w;
            //ディスプレイの高さ
            h = System.Windows.Forms.Screen.GetBounds(this).Height;
            //ディスプレイの幅
            w = System.Windows.Forms.Screen.GetBounds(this).Width;
            this.SetBounds(0, 0, w, h);
            bmp = new Bitmap(w, h);
            g = Graphics.FromImage(bmp);
            g.CopyFromScreen(
                new Point(w - bmp.Size.Width, h - bmp.Size.Height),
                new Point(0, 0), bmp.Size
            );
            pictureBox1.SetBounds(0, 0, w, h);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = bmp;
            Console.WriteLine(pictureBox1.Bounds);
            pictureBox1.MouseDown +=
                    new MouseEventHandler(ScreenWindow_MouseDown);
            pictureBox1.MouseMove +=
                    new MouseEventHandler(ScreenWindow_MouseMove);
            pictureBox1.MouseUp +=
                    new MouseEventHandler(ScreenWindow_MouseUp);

        }
        //マウスのクリック位置を記憶
        private Point startPoint;
        private Point movePoint;
        private Point endPoint;
        private Rectangle rc;
        private Boolean isPressed = false;
        //マウスのボタンが押されたとき
        private void ScreenWindow_MouseDown(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                //位置を記憶する
                startPoint = new Point(e.X, e.Y);
                isPressed = true;
            }
        }
        //マウスが動いたとき
        private void ScreenWindow_MouseMove(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
//                movePoint = new Point(e.X, e.Y);
                rc = new Rectangle();
                Pen p = new Pen(Color.Black, 10);
                if (startPoint.X < e.X)
                {
                    rc.X = startPoint.X;
                    rc.Width = e.X - startPoint.X;
                }
                else
                {
                    rc.X = e.X;
                    rc.Width = startPoint.X - e.X;
                }
                if (startPoint.Y < e.Y)
                {
                    rc.Y = startPoint.Y;
                    rc.Height = e.Y - startPoint.Y;
                }
                else
                {
                    rc.Y = e.Y;
                    rc.Height = startPoint.Y - e.Y;
                }
//                g.DrawRectangle(p, rc.X, rc.Y, rc.Width, rc.Height);
                Pen blackPen = new Pen(Color.Black);
                Graphics g = Graphics.FromImage(bmp);

                // 描画する線を点線に設定
                blackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                blackPen.Width = 1;
                // 画面を消去
                g.Clear(SystemColors.Control);

                // 領域を描画
                g.DrawRectangle(blackPen, rc);

                g.Dispose();
//                pictureBox1.Image = bmp;
//                ControlPaint.DrawReversibleFrame(rc,
//                    Color.White, FrameStyle.Dashed);
                pictureBox1.Refresh();
//                pictureBox1.Update();
            }
        }
        //マウスのボタンが離されたとき
        private void ScreenWindow_MouseUp(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if (isPressed)
            {
                endPoint = new Point(e.X, e.Y);
                isPressed = false;
                this.Close();
                Console.WriteLine("s" + startPoint + " e" + endPoint);
//                pictureBox1.Update();
                SnapWindow f3 = new SnapWindow();
                f3.capture(rc);
                f3.Show();
                f3.SetDesktopLocation(rc.X, rc.Y);
            }
        }
    }
}

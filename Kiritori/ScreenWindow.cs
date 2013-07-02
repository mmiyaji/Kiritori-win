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
        public ScreenWindow()
        {
            InitializeComponent();
        }

        private void Screen_Load(object sender, EventArgs e)
        {
            this.Opacity = 0.5;
            int h, w;
            //ディスプレイの高さ
            h = System.Windows.Forms.Screen.GetBounds(this).Height;
            //ディスプレイの幅
            w = System.Windows.Forms.Screen.GetBounds(this).Width;
            this.SetBounds(0, 0, w, h);
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(
                    new Point(w - bmp.Size.Width, h - bmp.Size.Height),
                    new Point(0, 0), bmp.Size
                );
            }
            pictureBox1.SetBounds(0, 0, w, h);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = bmp;
            Console.WriteLine(pictureBox1.Bounds);
        }
    }
}

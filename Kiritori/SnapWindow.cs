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
                this.Opacity = 0.3;
                //または、つぎのようにする
                //this.Location = new Point(
                //    this.Location.X + e.X - mousePoint.X,
                //    this.Location.Y + e.Y - mousePoint.Y);
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
                    //SaveFileDialogクラスのインスタンスを作成
                    SaveFileDialog sfd = new SaveFileDialog();

                    //はじめのファイル名を指定する
                    sfd.FileName = "新しいファイル.png";
                    //はじめに表示されるフォルダを指定する
                    sfd.InitialDirectory = @"C:\";
                    //[ファイルの種類]に表示される選択肢を指定する
//                    sfd.Filter =
//                        "HTMLファイル(*.html;*.htm)|*.html;*.htm|すべてのファイル(*.*)|*.*";
                    //[ファイルの種類]ではじめに
                    //「すべてのファイル」が選択されているようにする
                    sfd.FilterIndex = 2;
                    //タイトルを設定する
                    sfd.Title = "保存先のファイルを選択してください";
                    //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
                    sfd.RestoreDirectory = true;
                    //既に存在するファイル名を指定したとき警告する
                    //デフォルトでTrueなので指定する必要はない
                    sfd.OverwritePrompt = true;
                    //存在しないパスが指定されたとき警告を表示する
                    //デフォルトでTrueなので指定する必要はない
                    sfd.CheckPathExists = true;

                    //ダイアログを表示する
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        //OKボタンがクリックされたとき
                        //選択されたファイル名を表示する
                        this.pictureBox1.Image.Save(sfd.FileName);
                    }
                    break;
                case (int)HOTS.ZOOM_IN:
//                    this.SetBounds(this.Location.X, this.Location.Y, this.Size.Width + 3, this.Size.Height + 3);
                    this.pictureBox1.Size = new Size((int)(this.pictureBox1.Image.Width * 1.1), 
                                                     (int)(this.pictureBox1.Image.Height * 1.1));
                    this.Size = this.pictureBox1.Size;
                    Debug.WriteLine("plus");
                    break;
                case (int)HOTS.ZOOM_OUT:
                    this.Size = new Size((int)(this.Size.Width * 0.9), (int)(this.Height * 0.9));
                    this.pictureBox1.Size = this.Size;
                    Debug.WriteLine("minus");
                    break;
                case (int)HOTS.COPY:
                    Clipboard.SetImage(this.pictureBox1.Image);
                    break;
                case (int)HOTS.CUT:
                    Clipboard.SetImage(this.pictureBox1.Image);
                    this.Close();
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

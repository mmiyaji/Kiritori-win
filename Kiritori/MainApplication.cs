using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
//using System.Windows.Forms.Cursor;

namespace Kiritori
{
    public partial class MainApplication : Form
    {
        private HotKey hotKey;
        private ScreenWindow s;
        public MainApplication()
        {
            InitializeComponent();
            hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            s = new ScreenWindow(this);
        }
        void hotKey_HotKeyPush(object sender, EventArgs e)
        {
            this.openScreen();
        }
        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            try
            {
                this.notifyIcon1.Visible = false;
                hotKey.Dispose();
            }
            catch { }
        }
        private void Form2_Load(object sender, EventArgs e)
        {
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void captureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openScreen();
        }

        public void openScreen() {
            // restrict multiple open
            if (s == null)
            {
                s = new ScreenWindow(this);
                s.showScreenAll();
            }
            else
            {
                s.showScreenAll();
            }
        }
        public void openImage()
        {
            if (s == null)
            {
                s = new ScreenWindow(this);
            }
            s.openImage();
        }
        public void openImageFromHistory(ToolStripMenuItem item)
        {
            if (s == null)
            {
                s = new ScreenWindow(this);
            }
            s.openImageFromHistory(item);
        }
        public void setHistory(SnapWindow sw) {
            ToolStripMenuItem item1 = new ToolStripMenuItem();
            item1.Image = sw.thumbnail_image;
//            item1.Text = sw.Text;
            // ファイル名が一行表示だと長くなるので、てきとーに改行
            item1.Text = substringAtCount(sw.Text, 30);
            item1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            item1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            item1.ImageScaling = ToolStripItemImageScaling.None;
            item1.Tag = sw;
            historyToolStripMenuItem1.DropDownItems.Add(item1);
            item1.Click += new System.EventHandler(this.historyToolStripMenuItem1_item_Click);
        }
        private void hideAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.hideWindows();
        }

        private void showAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.showWindows();
        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrefForm pref = new PrefForm();
            pref.Show();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openImage();
        }

        private void historyToolStripMenuItem1_item_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            this.openImageFromHistory(item);
        }
        
        private String substringAtCount(string source, int count)
        {
            String newStr = "";
            int length = (int)Math.Ceiling((double)source.Length / (double)count);

            for (int i = 0; i < length; i++)
            {
                int start = count * i;
                if (start >= source.Length)
                {
                    break;
                }
                if (i > 0)
                {
                    newStr += Environment.NewLine;
                }
                if (start + count > source.Length)
                {
                    newStr += source.Substring(start);
                }
                else
                {
                    newStr += source.Substring(start, count);
                }
            }
            return newStr;
        }
    }
}

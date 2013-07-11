using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            s = new ScreenWindow();
        }
        void hotKey_HotKeyPush(object sender, EventArgs e)
        {
            this.openScreen();
        }
        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            Console.WriteLine("close");
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
                s = new ScreenWindow();
                s.showScreen();
            }
            else
            {
                s.showScreen();
            }
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
    }
}

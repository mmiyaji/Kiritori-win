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
        }
        void hotKey_HotKeyPush(object sender, EventArgs e)
        {
            if (s == null)
            {
                s = new ScreenWindow();
                s.Show();
            }
            else {
                if(!s.isScreenOpen()){
                    s = new ScreenWindow();
                    s.Show();
                }
            }
        }
        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.notifyIcon1.Visible = false;
            hotKey.Dispose();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            Console.WriteLine("c");
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void captureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ScreenWindow s = new ScreenWindow();
            s.Show();
        }
    }
}

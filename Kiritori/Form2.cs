using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Windows.Forms.Cursor;

namespace Kiritori
{
    public partial class Form2 : Form
    {
        HotKey hotKey;
        public Form2()
        {
            InitializeComponent();
            hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
        }
        void hotKey_HotKeyPush(object sender, EventArgs e)
        {
            SnapWindow f3 = new SnapWindow();
            f3.Show();
            f3.SetDesktopLocation(Cursor.Position.X - 200 + 3, Cursor.Position.Y - 200 + 2);
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            hotKey.Dispose();
        }

    }
}

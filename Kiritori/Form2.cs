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
    public partial class Form2 : Form
    {
        HotKey hotKey;
        Point startX, startY, endX, endY;
        public Form2()
        {
            InitializeComponent();
            hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
//            this.MouseMove += new MouseEventHandler(Form2_MouseMove);
        }
        void hotKey_HotKeyPush(object sender, EventArgs e)
        {
            ScreenWindow s = new ScreenWindow();
            s.Show();
            
 //           Console.WriteLine("cap");
 //           this.Capture = true;
 //           Cursor.Current = Cursors.Cross;
 //           CaptureRegion.SetMouseRButtonDown();
            Console.WriteLine("ture");
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
//            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void captureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ScreenWindow s = new ScreenWindow();
            s.Show();
        }
    }
    public class CaptureRegion
    {
        internal class NativeMethods
        {
            [DllImport("user32.dll")]
            extern public static void
              mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
            [DllImport("user32.dll")]
            extern public static int GetMessageExtraInfo();

            public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
            public const int MOUSEEVENTF_RIGHTUP = 0x0010;
        }

        public static void SetMouseRButtonDown()
        {
            Console.WriteLine("down");
            NativeMethods.mouse_event(
              NativeMethods.MOUSEEVENTF_RIGHTDOWN,
              0, 0, 0, NativeMethods.GetMessageExtraInfo());
        }

        public static void SetMouseRButtonUp()
        {
            Console.WriteLine("up");
            NativeMethods.mouse_event(
              NativeMethods.MOUSEEVENTF_RIGHTUP,
              0, 0, 0, NativeMethods.GetMessageExtraInfo());
            SnapWindow f3 = new SnapWindow();
            f3.Show();
            f3.SetDesktopLocation(Cursor.Position.X - 200 + 3, Cursor.Position.Y - 200 + 2);
        }
    }
}

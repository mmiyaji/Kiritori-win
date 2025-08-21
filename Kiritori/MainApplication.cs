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
        private const int WM_DPICHANGED = 0x02E0;
        private HotKey hotKey;
        private ScreenWindow s;
        public MainApplication()
        {
            InitializeComponent();
            hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            s = new ScreenWindow(this);
            ApplyDpiToUi(GetDpiForWindowSafe(this.Handle));
        }
        
        // --- DPI 変更を受け取り、UI のスケール依存を更新
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_DPICHANGED)
            {
                // wParam の下位ワードが新しい DPI
                int newDpi = (int)((uint)m.WParam & 0xFFFF);
                ApplyDpiToUi(newDpi);
            }
        }

        private void ApplyDpiToUi(int dpi)
        {
            // ToolStrip / Menu の画像スケール
            // 例: 16px 基準で DPI に合わせる
            int px = (int)Math.Round(16 * dpi / 96.0);
            foreach (var ts in this.ComponentsRecursive<ToolStrip>())
            {
                ts.ImageScalingSize = new Size(px, px);
            }
            // NotifyIcon のアイコンそのものは OS がよしなにしてくれますが、
            // 必要に応じて DPI 切替時にアイコンを差し替える処理を追加してもOK。

            // ScreenWindow 側に DPI 変更を知らせたい場合（後述のフック用）
            try { s?.OnHostDpiChanged(dpi); } catch { /* ScreenWindow 未対応でもOK */ }
        }

        // Utility: コントロール木から指定型を列挙
        private IEnumerable<T> ComponentsRecursive<T>() where T : Control
        {
            Queue<Control> q = new Queue<Control>();
            q.Enqueue(this);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c is T t) yield return t;
                foreach (Control child in c.Controls) q.Enqueue(child);
            }
        }

        // Utility: GetDpiForWindow の安全呼び出し
        private int GetDpiForWindowSafe(IntPtr hWnd)
        {
            try { return Native.GetDpiForWindow(hWnd); } catch { return 96; }
        }

        private static class Native
        {
            [DllImport("user32.dll")] public static extern int GetDpiForWindow(IntPtr hWnd);
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
                s = new ScreenWindow(this, () => GetDpiForWindowSafe(this.Handle));
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
        public void setHistory(SnapWindow sw)
        {
            int limit = Properties.Settings.Default.HistoryLimit;
            if (limit == 0) return;

            ToolStripMenuItem item1 = new ToolStripMenuItem();
            item1.Image = sw.thumbnail_image;
            //            item1.Text = sw.Text;
            // ファイル名が一行表示だと長くなるので、てきとーに改行
            item1.Text = substringAtCount(sw.Text, 30);
            item1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            item1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            item1.ImageScaling = ToolStripItemImageScaling.None;
            item1.Tag = sw;
            // historyToolStripMenuItem1.DropDownItems.Add(item1);
            item1.Click += new System.EventHandler(this.historyToolStripMenuItem1_item_Click);
            historyToolStripMenuItem1.DropDownItems.Insert(0, item1);
                        
            // 件数制限を超えていたら古いものを削除
            while (historyToolStripMenuItem1.DropDownItems.Count > limit)
            {
                historyToolStripMenuItem1.DropDownItems.RemoveAt(historyToolStripMenuItem1.DropDownItems.Count - 1);
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

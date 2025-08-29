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
using Kiritori.Properties;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;
using Kiritori.Helpers;
using System.Threading.Tasks;
using System.Threading;

namespace Kiritori
{
    internal sealed class HistoryEntry
    {
        public string Path;
        public Bitmap Thumb;
    }
    public partial class MainApplication : Form
    {
        private const int WM_DPICHANGED = 0x02E0;
        private HotKey hotKey;
        private ScreenWindow s;
        private static readonly string HistoryTempDir = Path.Combine(Path.GetTempPath(), "Kiritori", "History");

        private bool _allowShow = false;          // Visible 抑止用
        private readonly System.Windows.Forms.Timer _bootTimer; // 起動後ワンショット
        public MainApplication()
        {
            InitializeComponent();
            notifyIcon1.Icon = Properties.Resources.AppIcon;
            this.Icon = Properties.Resources.AppIcon;
            hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            s = new ScreenWindow(this);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            ApplyTrayLocalization();
            SR.CultureChanged += () =>
            {
                Localizer.Apply(this);
                ApplyTrayLocalization();
            };

            ApplyDpiToUi(GetDpiForWindowSafe(this.Handle));
            // MaybeShowPreferencesOnStartup();
            _bootTimer = new System.Windows.Forms.Timer { Interval = 10, Enabled = true };
            _bootTimer.Tick += async (_, __) =>
            {
                _bootTimer.Enabled = false;
                await EnsureStartupAsync();
                MaybeShowPreferencesOnStartup();
            };
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

        private void MaybeShowPreferencesOnStartup()
        {
            // 初回だけは強制表示
            if (!Settings.Default.isFirstRunShown)
            {
                Settings.Default.isFirstRunShown = true;
                Settings.Default.Save(); // 記録
                PrefForm.ShowSingleton(this);
                return;
            }
            if (Settings.Default.isOpenMenuOnAppStart)
            {
                PrefForm.ShowSingleton(this);
            }
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
                if (notifyIcon1 == null) return;
                ClearHistoryMenu();
                //try { if (notifyIcon1 != null && notifyIcon1.Visible) notifyIcon1.Visible = false; }
                //catch { /* NullReference/InvalidOperation を握りつぶす */ }
                notifyIcon1.Dispose();
                this.Icon?.Dispose();
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

        public void openScreen()
        {
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

            // 1) 履歴用の実パスを決定（なければ Temp に保存）
            string path = sw.Text; // 既存はタイトル文字列。ファイルパスのこともある
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // 画像ソースの既知パスが付いていればそれを優先（Tagに入れている場合）
                // var tagged = sw.pictureBox1?.Image?.Tag as string;
                var tagged = sw.GetImageSourcePath();
                if (!string.IsNullOrEmpty(tagged) && File.Exists(tagged))
                {
                    path = tagged;
                }
                else
                {
                    // Temp に確実に保存
                    Directory.CreateDirectory(HistoryTempDir);
                    path = Path.Combine(HistoryTempDir, $"{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
                    using (var clone = (Bitmap)sw.main_image.Clone())
                    {
                        clone.Save(path, ImageFormat.Png);
                    }
                }
            }

            // 2) サムネは必ず独立クローン
            Bitmap thumb = (Bitmap)sw.thumbnail_image.Clone();

            var entry = new HistoryEntry { Path = path, Thumb = thumb };

            var item = new ToolStripMenuItem
            {
                Image = thumb,
                Text = substringAtCount(Path.GetFileName(path), 30),
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ImageAlign = ContentAlignment.MiddleLeft,
                ImageScaling = ToolStripItemImageScaling.None,
                Tag = entry
            };
            item.Click += historyToolStripMenuItem1_item_Click;
            historyToolStripMenuItem1.DropDownItems.Insert(0, item);

            while (historyToolStripMenuItem1.DropDownItems.Count > limit)
            {
                int lastIdx = historyToolStripMenuItem1.DropDownItems.Count - 1;
                var last = historyToolStripMenuItem1.DropDownItems[lastIdx] as ToolStripMenuItem;

                // 1) まずコレクションから外す（Countが変わるのはここだけ）
                historyToolStripMenuItem1.DropDownItems.RemoveAt(lastIdx);

                // 2) その後に後始末（ファイル/Bitmap/Item）
                if (last != null)
                {
                    if (last.Tag is HistoryEntry he)
                    {
                        if (IsHistoryTempPath(he.Path)) SafeDelete(he.Path);
                        he.Thumb?.Dispose();
                        last.Tag = null;
                    }
                    last.Image?.Dispose();
                    last.Dispose();
                }
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
        private void closeAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.closeWindows();
        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // PrefForm pref = new PrefForm();
            // pref.Show();
            PrefForm.ShowSingleton(this);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openImage();
        }

        private void historyToolStripMenuItem1_item_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            if (item.Tag is HistoryEntry he && File.Exists(he.Path))
            {
                var sw = new SnapWindow(this)
                {
                    StartPosition = FormStartPosition.CenterScreen,
                    SuppressHistory = true
                };
                sw.setImageFromPath(he.Path);
                sw.Show();
            }
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
        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PrefForm.ShowSingleton(this);
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右クリックは ContextMenuStrip が出るので、通常は何もしない
            }
        }
        private void ClearHistoryMenu()
        {
            foreach (ToolStripItem tsi in historyToolStripMenuItem1.DropDownItems)
            {
                if (tsi is ToolStripMenuItem mi)
                {
                    if (mi.Tag is HistoryEntry he)
                    {
                        if (IsHistoryTempPath(he.Path)) SafeDelete(he.Path);
                        he.Thumb?.Dispose();
                        mi.Tag = null;
                    }
                    mi.Image?.Dispose();
                    mi.Dispose();
                }
            }
            historyToolStripMenuItem1.DropDownItems.Clear();
        }


        private static bool IsHistoryTempPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                string baseDir = Path.GetFullPath(HistoryTempDir)
                                + Path.DirectorySeparatorChar; // 末尾セパレータで誤一致防止
                return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ロック中などは無視（次回のクリーンアップで再挑戦）
            }
        }
        private void ApplyTrayLocalization()
        {
            // メニュー（Tagベースで一括）
            if (contextMenuStrip1 != null)
                ApplyToolStripLocalization(contextMenuStrip1.Items);

            // トレイアイコンのヒント
            if (notifyIcon1 != null)
                notifyIcon1.Text = SR.T("Tray.TrayIcon");  // 文字数は63字以内推奨
        }
        // ToolStrip / ContextMenuStrip 用のローカライズ適用
        private static void ApplyToolStripLocalization(ToolStripItemCollection items)
        {
            foreach (ToolStripItem it in items)
            {
                // 自分自身
                if (it.Tag is string tag && tag.StartsWith("loc:", StringComparison.Ordinal))
                {
                    it.Text = SR.T(tag.Substring(4));
                }

                // サブメニューを再帰
                if (it is ToolStripDropDownItem dd)
                {
                    ApplyToolStripLocalization(dd.DropDownItems);
                }
            }
        }
        public NotifyIcon NotifyIcon
        {
            get { return this.notifyIcon1; }
        }
        // 最初は絶対に Visible にしない（ちらつき・表示を防止）
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(_allowShow && value);
        }

        // 間違って Close されたら Hide に置き換える（本当に終了するときは Application.Exit 呼び出し側で）
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        private async Task EnsureStartupAsync()
        {
            try
            {
                if (Properties.Settings.Default.isStartup)
                {
                    if (Helpers.PackagedHelper.IsPackaged())
                    {
                        // MSIX: 必要なら同意ダイアログ
                        await Startup.StartupManager.EnsureEnabledAsync();
                    }
                    else
                    {
                        // 非MSIX: .lnk 作成は STA が安全
                        await RunStaAsync(() => Startup.StartupManager.EnsureEnabled());
                    }
                }
                else
                {
                    if (Helpers.PackagedHelper.IsPackaged())
                        await Startup.StartupManager.DisableAsync();
                    else
                        await RunStaAsync(() => Startup.StartupManager.SetEnabled(false));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private static Task RunStaAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            var th = new Thread(() => { try { action(); tcs.SetResult(null); } catch (Exception ex) { tcs.SetException(ex); } });
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
            return tcs.Task;
        }

    }
}

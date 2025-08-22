using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Globalization;
using System.Drawing; 
namespace Kiritori
{
    public partial class PrefForm : Form
    {
        public PrefForm()
        {
            InitializeComponent();
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
        }

        private void PrefForm_Load(object sender, EventArgs e)
        {
            // Infoタブのバージョン表示を更新
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;

            var buildDate = File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location);

            this.labelVersion.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Version {0} Build Date: {1:dd MMM, yyyy}",
                ver,
                DateTime.Now
            );
        }

        // 不要（デザイナに紐付けていなければ呼ばれません）
        private void textBox2_TextChanged(object sender, EventArgs e) { }

        private void btnSavestings_Click(object sender, EventArgs e)
        {
            // 設定保存（双方向バインド済みのため Save のみでOK）
            Properties.Settings.Default.Save();
            this.Close();

            // 旧：スタートアップのショートカット作成はMSIX配布では不要
            // SetStartupShortcut(Properties.Settings.Default.isStartup);
        }

        private void btnCancelSettings_Click(object sender, EventArgs e)
        {
            // 変更を破棄して閉じる
            Properties.Settings.Default.Reload();
            this.Close();
        }

        /// <summary>
        /// 旧方式（スタートアップフォルダに LNK 作成）。MSIX/ストア配布なら不要。
        /// 必要ならコメントを外して使ってください。
        /// </summary>
        private static void SetStartupShortcut(bool enable)
        {
            // string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            // string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");
            // if (!enable) { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); return; }
            // string targetPath = Application.ExecutablePath;
            // Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // WScript.Shell
            // object shell = Activator.CreateInstance(t);
            // object shortcut = t.InvokeMember("CreateShortcut",
            //     System.Reflection.BindingFlags.InvokeMethod, null, shell,
            //     new object[] { shortcutPath });
            // try {
            //     t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            //     t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
            //     t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            //     t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            // } finally {
            //     System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            //     System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            // }
        }

        private void btnOpenStartupSettings_Click(object sender, EventArgs e)
        {
            const string uri = "ms-settings:startupapps";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    // 一部環境でのフォールバック
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = uri,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show(
                        "Could not open Windows Settings.\n" +
                        "Please open 'Settings > Apps > Startup' manually and enable Kiritori.",
                        "Startup Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void chkDoNotShowOnStartup_CheckedChanged(object sender, EventArgs e)
        {
            // チェック変更は即時保存
            Properties.Settings.Default.DoNotShowOnStartup = chkDoNotShowOnStartup.Checked;
            Properties.Settings.Default.Save();
        }

        // 旧チェックボックス用ハンドラ（デザイナで使っていなければ実行されません）
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:startupapps",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Please enable Kiritori from 'Settings > Apps > Startup'.");
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            labelLinkWebsite.LinkVisited = true;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://kiritori.ruhenheim.org",
                    UseShellExecute = true
                });
            }
            catch { /* 失敗時は無視 */ }
        }
    }
}

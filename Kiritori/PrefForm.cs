using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Globalization;
using System.Drawing;
using System.ComponentModel;
namespace Kiritori
{
    public partial class PrefForm : Form
    {
        private static PrefForm _instance;
        public PrefForm()
        {
            InitializeComponent();
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.StartPosition = FormStartPosition.CenterScreen;

            if (!IsInDesignMode())
            {
                // 初期配置と、以後の変化を拾う
                this.Load += (_, __) => PositionDescHeader();
                this.tabInfo.Layout += (_, __) => PositionDescHeader();
                this.descCard.LocationChanged += (_, __) => PositionDescHeader();
                this.descCard.SizeChanged += (_, __) => PositionDescHeader();
                this.labelDescHeader.TextChanged += (_, __) => PositionDescHeader();
                var src = Properties.Resources.icon_128x128;
                picAppIcon.Image?.Dispose(); // 先に破棄（初期画像がある場合）
                picAppIcon.Image = ScaleBitmap(src, 120, 120);
            }
        }
        private static bool IsInDesignMode()
        {
            // WinFormsのDesignModeはconstructorでは正しく取れないことがあるため二段構え
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
            var exe = Application.ExecutablePath ?? string.Empty;
            return exe.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase)   // VS
                || exe.EndsWith("Blend.exe", StringComparison.OrdinalIgnoreCase);   // Blend等
        }
        private static Bitmap ScaleBitmap(Image src, int w, int h)
        {
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }

        /// <summary>
        /// 設定ウィンドウを常に1つだけ表示する。
        /// 既に開いていれば前面に出してアクティブ化する。
        /// </summary>
        public static void ShowSingleton(IWin32Window owner = null)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new PrefForm();
                _instance.FormClosed += (s, e) => _instance = null;

                if (owner != null)
                    _instance.Show(owner);
                else
                    _instance.Show();
            }
            else
            {
                // 最小化復帰
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;

                // 所有者を更新（任意）
                if (owner is Form f && _instance.Owner != f)
                    _instance.Owner = f;

                _instance.BringToFront();
                _instance.Activate();
            }
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
        private void btnSavestings_Click(object sender, EventArgs e)
        {
            // 設定保存（双方向バインド済みのため Save のみでOK）
            Properties.Settings.Default.Save();
            // this.Close();
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
        private System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void descCard_Paint(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            var g = e.Graphics;

            // ベース矩形（枠線1pxぶん内側）
            var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            int radius = 8;

            using (var path = RoundRect(rect, radius))
            using (var border = new Pen(Color.FromArgb(210, 215, 220), 1f))
            using (var accent = new SolidBrush(SystemColors.Highlight))
            {
                // --- 1) 角丸カードの内側だけにクリップしてバーを塗る（にじみ防止）
                var state = g.Save();
                g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Replace);

                // バーは枠の内側に 1px 余白を取る（枠のAAと干渉しない）
                const int inset = 1;
                const int barW = 4;
                var bar = new Rectangle(rect.X + inset, rect.Y + inset, barW, rect.Height - inset * 2);

                // --- 2) バー塗りはアンチエイリアスOFFでカリッと
                var oldSmooth = g.SmoothingMode;
                var oldPixel = g.PixelOffsetMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;

                g.FillRectangle(accent, bar);

                // 元に戻す
                g.SmoothingMode = oldSmooth;
                g.PixelOffsetMode = oldPixel;
                g.Restore(state);

                // --- 3) 枠は最後にアンチエイリアスONで重ねる（バーにじみの上書き）
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawPath(border, path);
            }
        }

        // PrefForm クラス内に配置関数を追加
        private void PositionDescHeader()
        {
            if (IsInDesignMode()) return;
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.labelDescHeader == null || this.descCard == null) return;
            if (this.labelDescHeader.Parent == null || this.descCard.Parent == null) return;

            // descCardの左上を、labelの親座標系に変換
            var labelParent = this.labelDescHeader.Parent;
            var cardParent  = this.descCard.Parent;

            Point cardTopLeftInLabelParent;
            if (labelParent == cardParent)
            {
                cardTopLeftInLabelParent = this.descCard.Location;
            }
            else
            {
                var screenPt = cardParent.PointToScreen(this.descCard.Location);
                cardTopLeftInLabelParent = labelParent.PointToClient(screenPt);
            }

            labelParent.SuspendLayout();
            try
            {
                // AutoSizeで高さを確定（再入による無限Layoutを避けたい場合は、必要時のみにする）
                this.labelDescHeader.AutoSize = true;

                int x = cardTopLeftInLabelParent.X + 8;
                int y = cardTopLeftInLabelParent.Y - (this.labelDescHeader.Height / 2);
                if (y < 8) y = 8;

                this.labelDescHeader.Location = new Point(x, y);
                this.labelDescHeader.BringToFront();
            }
            finally
            {
                labelParent.ResumeLayout();
            }
        }

        // KeyDownイベントでキーを取得
        private void textBoxKiritori_KeyDown(object sender, KeyEventArgs e)
        {
            // 修飾キー
            string modifier = "";
            if (e.Control) modifier += "Ctrl + ";
            if (e.Shift) modifier += "Shift + ";
            if (e.Alt) modifier += "Alt + ";

            // 押されたキー
            Keys key = e.KeyCode;

            // 修飾キー単体は無視（Ctrlだけ押した場合とか）
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
                return;

            this.textBoxKiritori.Text = modifier + key.ToString();

            // 他のコントロールにキーイベントが行かないように
            e.SuppressKeyPress = true;
        }

        // 矢印キーやTabなど特殊キーを捕捉するため
        private void textBoxKiritori_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }

    }
}

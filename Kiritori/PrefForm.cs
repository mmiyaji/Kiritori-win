using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class PrefForm : Form
    {
        // =========================================================
        // ================ Fields / Properties ====================
        // =========================================================
        private static PrefForm _instance;
        private bool _initStartupToggle = false;
        private bool _initLang = false;

        // =========================================================
        // ==================== Constructor ========================
        // =========================================================
        public PrefForm()
        {
            _initStartupToggle = true;
            _initLang = true;

            InitializeComponent();
            PopulatePresetCombos();
            WireUpDataBindings();
            SelectPresetComboFromSettings();
            HookRuntimeEvents();

            // デザイナの勝手なバインディングを解除
            var b = this.chkRunAtStartup.DataBindings["Checked"];
            if (b != null) this.chkRunAtStartup.DataBindings.Remove(b);

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.StartPosition = FormStartPosition.CenterScreen;

            if (!IsInDesignMode())
            {
                this.Load += PrefForm_LoadAsync;
                this.Load += (_, __) =>
                {
                    Localizer.Apply(this);
                    ApplyDynamicTexts();
                    LayoutInfoTabResponsive();
                };
                SR.CultureChanged += () =>
                {
                    Localizer.Apply(this);
                    ApplyDynamicTexts();
                    LayoutInfoTabResponsive();
                };
                // 見出し位置調整のためのレイアウトトリガ
                // this.tabInfo.Layout += (_, __) => PositionDescHeader();
                // this.descCard.LocationChanged += (_, __) => PositionDescHeader();
                // this.descCard.SizeChanged += (_, __) => PositionDescHeader();
                // this.labelDescHeader.TextChanged += (_, __) => PositionDescHeader();

                // アプリアイコンのスケーリング
                var src = Properties.Resources.icon_128x128;
                picAppIcon.Image?.Dispose();
                picAppIcon.Image = ScaleBitmap(src, 120, 120);
            }

            // Language コンボの初期化と保存値の復元
            InitLanguageCombo();
        }

        // =========================================================
        // ===================== Public API ========================
        // =========================================================
        /// <summary>
        /// 設定ウィンドウを常に1つだけ表示する。既にあれば前面化。
        /// </summary>
        public static void ShowSingleton(IWin32Window owner = null)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new PrefForm();
                _instance.FormClosed += (s, e) => _instance = null;

                if (owner != null) _instance.Show(owner);
                else _instance.Show();
            }
            else
            {
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;

                if (owner is Form f && _instance.Owner != f)
                    _instance.Owner = f;

                _instance.BringToFront();
                _instance.Activate();
            }
        }

        // =========================================================
        // =============== Form Lifecycle / Loads ==================
        // =========================================================
        private async void PrefForm_LoadAsync(object sender, EventArgs e)
        {
            try
            {
                if (PackagedHelper.IsPackaged())
                {
                    bool enabled = false;
                    try { enabled = await StartupManager.IsEnabledAsync(); }
                    catch { /* 失敗時は false のまま */ }

                    chkRunAtStartup.Checked = enabled;
                    chkRunAtStartup.Enabled = false;

                    toolTip1.SetToolTip(labelStartupInfo, "Managed by Windows Settings > Apps > Startup");
                    labelStartupInfo.Text = "Startup is managed by Windows.";
                }
                else
                {
                    // 非パッケージ：ショートカット有無で判定
                    string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");

                    chkRunAtStartup.Checked = File.Exists(shortcutPath);
                    chkRunAtStartup.Enabled = true;

                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string displayDir = startupDir.Replace(appData, "%APPDATA%");
                    labelStartupInfo.Text = "Shortcut: " + Path.Combine(displayDir, Application.ProductName + ".lnk");
                    toolTip1.SetToolTip(labelStartupInfo, shortcutPath);
                }

                // Info タブのバージョン表示
                UpdateVersionLabel();
            }
            finally
            {
                _initStartupToggle = false;
                chkRunAtStartup.CheckedChanged += ChkRunAtStartup_CheckedChanged;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (this.Icon != null)
            {
                this.Icon.Dispose();
                this.Icon = null;
            }
            base.OnFormClosed(e);
        }

        // =========================================================
        // ================== UI Event Handlers ====================
        // =========================================================
        private void ChkRunAtStartup_CheckedChanged(object sender, EventArgs e)
        {
            if (_initStartupToggle) return; // 初期化中は無視

            try
            {
                if (PackagedHelper.IsPackaged())
                {
                    // ここで StartupManager.Enable/Disable を呼ぶ実装に拡張可
                }
                else
                {
                    SetStartupShortcut(chkRunAtStartup.Checked);
                }

                Properties.Settings.Default.isStartup = chkRunAtStartup.Checked;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                chkRunAtStartup.Checked = false;
                MessageBox.Show(this, SR.F("Text.UnableSetStartup", ex.Message),
                    "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnSavestings_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void btnCancelSettings_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
            this.Close();
        }

        private void btnExitApp_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exit button clicked");
            var result = MessageBox.Show(
                SR.T("Text.ConfirmExit"),
                SR.T("Text.ConfirmExitTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            Debug.WriteLine(result);

            if (result == DialogResult.Yes)
            {
                Properties.Settings.Default.Reload();
                Application.Exit();
            }
        }

        private void btnOpenStartupSettings_Click(object sender, EventArgs e)
        {
            if (PackagedHelper.IsPackaged())
                OpenSettingsStartupApps();
            else
                OpenStartupFolderOrSelectLink();
        }

        private void chkDoNotShowOnStartup_CheckedChanged(object sender, EventArgs e)
        {
            //Properties.Settings.Default.DoNotShowOnStartup = chkDoNotShowOnStartup.Checked;
            Properties.Settings.Default.Save();
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
        private void btnBgCustomColor_Click(object sender, System.EventArgs e)
        {
            using (var cd = new ColorDialog { Color = this.previewBg.RgbColor, FullOpen = true })
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    this.previewBg.RgbColor = cd.Color;   // バインドで Settings にも入る
                    this.previewBg.Invalidate();
                }
            }
        }

        // private void descCard_Paint(object sender, PaintEventArgs e)
        // {
        //     var p = (Panel)sender;
        //     var g = e.Graphics;

        //     var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
        //     int radius = 8;

        //     using (var path = RoundRect(rect, radius))
        //     using (var border = new Pen(Color.FromArgb(210, 215, 220), 1f))
        //     using (var accent = new SolidBrush(SystemColors.Highlight))
        //     {
        //         var state = g.Save();
        //         g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Replace);

        //         const int inset = 1;
        //         const int barW = 4;
        //         var bar = new Rectangle(rect.X + inset, rect.Y + inset, barW, rect.Height - inset * 2);

        //         g.FillRectangle(accent, bar);
        //         g.Restore(state);
        //         g.DrawPath(border, path);
        //     }
        // }

        // Language ComboBox 選択変更
        private void cmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initLang) return;

            var item = cmbLanguage.SelectedItem;
            var valProp = item?.GetType().GetProperty("Value");
            var culture = valProp?.GetValue(item)?.ToString() ?? "en";
            Properties.Settings.Default.UICulture = culture;
            Properties.Settings.Default.Save();

            SR.SetCulture(culture);
        }

        // =========================================================
        // ================= Layout / Painting =====================
        // =========================================================
        /// <summary>
        /// Infoタブのレイアウトをウィンドウ幅に応じて最適化（2カラム⇄1カラム、アイコンサイズ）
        /// </summary>
        private void LayoutInfoTabResponsive()
        {
            if (this.tlpInfoHeader == null || this.picAppIcon == null) return;

            // フォームの表示領域に対する閾値
            int w = this.tabInfo.ClientSize.Width;
            bool narrow = w < 560; // 狭いときは1カラムに折り返す

            // カラム切替
            this.tlpInfoHeader.SuspendLayout();
            if (narrow)
            {
                // 1カラム：アイコン→テキストの縦並び
                this.tlpInfoHeader.ColumnCount = 1;
                this.tlpInfoHeader.ColumnStyles.Clear();
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                // 再配置
                if (this.tlpInfoHeader.GetControlFromPosition(0, 0) != this.picAppIcon)
                {
                    this.tlpInfoHeader.Controls.Clear();
                    this.tlpInfoHeader.Controls.Add(this.picAppIcon, 0, 0);
                    // 右側のパネル（FlowLayoutPanel）は再取得せず既存参照を使う
                    var rightPanel = this.labelAppName.Parent;
                    this.tlpInfoHeader.Controls.Add(rightPanel, 0, 1);
                }
            }
            else
            {
                // 2カラム：左アイコン / 右テキスト
                this.tlpInfoHeader.ColumnCount = 2;
                this.tlpInfoHeader.ColumnStyles.Clear();
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                // 再配置（列0/列1へ）
                var rightPanel = this.labelAppName.Parent;
                if (this.tlpInfoHeader.GetControlFromPosition(0, 0) != this.picAppIcon ||
                    this.tlpInfoHeader.GetControlFromPosition(1, 0) != rightPanel)
                {
                    this.tlpInfoHeader.Controls.Clear();
                    this.tlpInfoHeader.Controls.Add(this.picAppIcon, 0, 0);
                    this.tlpInfoHeader.Controls.Add(rightPanel, 1, 0);
                }
            }
            this.tlpInfoHeader.ResumeLayout(true);

            // アイコンのスケール（小さい幅では96、大きい幅では120）
            int target = narrow ? 96 : 120;
            if (this.picAppIcon.Size.Width != target)
            {
                try
                {
                    var src = global::Kiritori.Properties.Resources.icon_128x128;
                    picAppIcon.Image?.Dispose();
                    picAppIcon.Image = ScaleBitmap(src, target, target);
                    picAppIcon.Size = new Size(target, target);
                }
                catch { /* ignore */ }
            }

            // テキストの最大幅（改行の自然発生を促す）
            var infoRight = this.labelAppName.Parent as FlowLayoutPanel;
            if (infoRight != null)
            {
                int padding = 24;
                int maxw = Math.Max(200, this.tlpInfoHeader.ClientSize.Width - (narrow ? 0 : (picAppIcon.Width + padding)));
                foreach (Control c in infoRight.Controls)
                {
                    c.MaximumSize = new Size(maxw, 0);
                    c.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                }
            }
        }


        // =========================================================
        // =================== Helpers (UI) ========================
        // =========================================================
        private void InitLanguageCombo()
        {
            _initLang = true;

            this.cmbLanguage.DisplayMember = "Text";
            this.cmbLanguage.ValueMember = "Value";
            this.cmbLanguage.Items.Clear();
            this.cmbLanguage.Items.Add(new { Text = "English (en)", Value = "en" });
            this.cmbLanguage.Items.Add(new { Text = "日本語 (ja)", Value = "ja" });

            var saved = Properties.Settings.Default.UICulture;
            if (string.IsNullOrWhiteSpace(saved))
                saved = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            int index = 0;
            for (int i = 0; i < cmbLanguage.Items.Count; i++)
            {
                var item = cmbLanguage.Items[i];
                var valProp = item.GetType().GetProperty("Value");
                var val = valProp?.GetValue(item)?.ToString();
                if (val != null && val.Equals(saved, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            this.cmbLanguage.SelectedIndex = index;

            this.cmbLanguage.SelectedIndexChanged += cmbLanguage_SelectedIndexChanged;
            _initLang = false;
        }

        private void UpdateVersionLabel()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;

            this.labelVersion.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Version {0} Build Date: {1:dd MMM, yyyy}",
                ver,
                DateTime.Now
            );
        }

        private static void OpenSettingsStartupApps()
        {
            const string uri = "ms-settings:startupapps";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            }
            catch
            {
                try
                {
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

        private void OpenStartupFolderOrSelectLink()
        {
            try
            {
                string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string lnk = Path.Combine(startupDir, Application.ProductName + ".lnk");

                if (File.Exists(lnk))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{lnk}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = startupDir,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open Startup folder.\n" + ex.Message,
                    "Startup Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void SetStartupShortcut(bool enable)
        {
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");
            if (!enable) { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); return; }

            string targetPath = Application.ExecutablePath;
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // WScript.Shell
            object shell = Activator.CreateInstance(t);
            object shortcut = t.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            try
            {
                t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        // =========================================================
        // =================== Helpers (General) ===================
        // =========================================================
        private static bool IsInDesignMode()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
            var exe = Application.ExecutablePath ?? string.Empty;
            return exe.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase)
                || exe.EndsWith("Blend.exe", StringComparison.OrdinalIgnoreCase);
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

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
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
        private void ApplyDynamicTexts()
        {
            // タイトル合成（App.Name と PrefForm.Title を resx に用意）
            this.Text = $"{SR.T("App.Name")} - {SR.T("PrefForm.Title")}";

            // MSIX/非MSIXで変わるボタン
            btnOpenStartupSettings.Text = PackagedHelper.IsPackaged()
                ? SR.T("Text.BtnStartupSetting")
                : SR.T("Text.BtnStartupFolder");

            // // 起動管理説明＆ツールチップ（必要に応じてキーを追加）
            if (PackagedHelper.IsPackaged())
            {
                labelStartupInfo.Text = SR.T("Text.StartupManaged"); // 例: "Startup is managed by Windows."
                toolTip1.SetToolTip(labelStartupInfo, SR.T("Text.StartupManagedTip")); // 例: "Settings > Apps > Startup"
            }
            // else
            // {
            //     string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            //     string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");
            //     string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            //     string displayDir = startupDir.Replace(appData, "%APPDATA%");
            //     labelStartupInfo.Text = SR.F("Text.ShortcutPathFmt", Path.Combine(displayDir, Application.ProductName + ".lnk"));
            //     toolTip1.SetToolTip(labelStartupInfo, shortcutPath);
            // }
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            this.labelVersion.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Version {0} Build Date: {1:dd MMM, yyyy}",
                ver,
                DateTime.Now
            );
            this.labelCopyRight.Text = "© 2013–" + DateTime.Now.Year;

            // 最後にレイアウト最適化
            LayoutInfoTabResponsive();
        }
        // =========================================================
        // ================== Nested UI Controls ===================
        // =========================================================
        public class AlphaPreviewPanel : Panel
        {
            private Color _rgbColor = Color.Red;
            private int _alphaPercent = 60;

            [Bindable(true)]
            public Color RgbColor
            {
                get => _rgbColor;
                set { if (_rgbColor != value) { _rgbColor = value; Invalidate(); } }
            }

            [Bindable(true)]
            public int AlphaPercent
            {
                get => _alphaPercent;
                set
                {
                    var v = Math.Max(0, Math.Min(100, value));
                    if (_alphaPercent != v) { _alphaPercent = v; Invalidate(); }
                }
            }

            public AlphaPreviewPanel()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint
                            | ControlStyles.OptimizedDoubleBuffer
                            | ControlStyles.UserPaint, true);
                this.BorderStyle = BorderStyle.FixedSingle;
                this.Height = 24;
                this.Width = 120;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                int sz = 6;
                using (var brushDark = new SolidBrush(Color.LightGray))
                using (var brushLight = new SolidBrush(Color.White))
                {
                    for (int y = 0; y < Height; y += sz)
                        for (int x = 0; x < Width; x += sz)
                            g.FillRectangle((((x / sz) + (y / sz)) % 2 == 0) ? brushDark : brushLight, x, y, sz, sz);
                }

                int alpha = (int)Math.Round(_alphaPercent * 2.55);
                using (var overlay = new SolidBrush(Color.FromArgb(alpha, _rgbColor)))
                {
                    g.FillRectangle(overlay, ClientRectangle);
                }
            }
        }
        public class Separator : Control
        {
            public Separator()
            {
                this.Height = 1;
                this.Dock = DockStyle.Top;
                this.Margin = new Padding(0, 6, 0, 6);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var p = new Pen(SystemColors.ControlLight))
                {
                    e.Graphics.DrawLine(p, 0, this.Height / 2, this.Width, this.Height / 2);
                }
            }
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

    }
}

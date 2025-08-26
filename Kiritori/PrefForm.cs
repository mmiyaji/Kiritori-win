using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
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

        // =========================================================
        // ==================== Constructor ========================
        // =========================================================
        public PrefForm()
        {
            _initStartupToggle = true;
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

                // 見出し位置調整のためのレイアウトトリガ
                this.tabInfo.Layout += (_, __) => PositionDescHeader();
                this.descCard.LocationChanged += (_, __) => PositionDescHeader();
                this.descCard.SizeChanged += (_, __) => PositionDescHeader();
                this.labelDescHeader.TextChanged += (_, __) => PositionDescHeader();

                // アプリアイコンのスケーリング
                var src = Properties.Resources.icon_128x128;
                picAppIcon.Image?.Dispose();
                picAppIcon.Image = ScaleBitmap(src, 120, 120);
            }

            btnOpenStartupSettings.Text = PackagedHelper.IsPackaged()
                ? "Open Startup settings"
                : "Open Startup folder";
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
                MessageBox.Show(this, "Unable to update startup setting.\r\n" + ex.Message,
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
                "Are you sure you want to exit the application?",
                "Confirm Exit",
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
            Properties.Settings.Default.DoNotShowOnStartup = chkDoNotShowOnStartup.Checked;
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

                    // プリセット名は一致しない可能性が高いので「カスタム」扱いにするならこうする：
                    // this.cmbBgPreset.SelectedIndex = -1;
                }
            }
        }

        private void descCard_Paint(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            var g = e.Graphics;

            var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            int radius = 8;

            using (var path = RoundRect(rect, radius))
            using (var border = new Pen(Color.FromArgb(210, 215, 220), 1f))
            using (var accent = new SolidBrush(SystemColors.Highlight))
            {
                // 1) カード内だけにバーを塗る
                var state = g.Save();
                g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Replace);

                const int inset = 1;
                const int barW = 4;
                var bar = new Rectangle(rect.X + inset, rect.Y + inset, barW, rect.Height - inset * 2);

                var oldSmooth = g.SmoothingMode;
                var oldPixel = g.PixelOffsetMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;

                g.FillRectangle(accent, bar);

                g.SmoothingMode = oldSmooth;
                g.PixelOffsetMode = oldPixel;
                g.Restore(state);

                // 3) 枠線
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawPath(border, path);
            }
        }

        // キー入力（ホットキー用テキスト）
        private void textBoxKiritori_KeyDown(object sender, KeyEventArgs e)
        {
            string modifier = "";
            if (e.Control) modifier += "Ctrl + ";
            if (e.Shift)  modifier += "Shift + ";
            if (e.Alt)    modifier += "Alt + ";

            var key = e.KeyCode;

            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
                return;

            this.textBoxKiritori.Text = modifier + key.ToString();

            e.SuppressKeyPress = true;
        }

        private void textBoxKiritori_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }

        // =========================================================
        // ================= Layout / Painting =====================
        // =========================================================
        private void PositionDescHeader()
        {
            if (IsInDesignMode()) return;
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.labelDescHeader == null || this.descCard == null) return;
            if (this.labelDescHeader.Parent == null || this.descCard.Parent == null) return;

            var labelParent = this.labelDescHeader.Parent;
            var cardParent = this.descCard.Parent;

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

        // =========================================================
        // =================== Helpers (UI) ========================
        // =========================================================
        private void UpdateVersionLabel()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;

            // 以前の実装を保持（必要なら buildDate へ変更）
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

        /// <summary>
        /// 非 MSIX の場合のスタートアップショートカット作成/削除
        /// </summary>
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
            return exe.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase)   // VS
                || exe.EndsWith("Blend.exe", StringComparison.OrdinalIgnoreCase);  // Blend
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

        // =========================================================
        // ================== Nested UI Controls ===================
        // =========================================================
        /// <summary>
        /// 透過チェッカー上に合成結果を描く簡易プレビュー
        /// </summary>
        public class AlphaPreviewPanel : Panel
        {
            private Color _rgbColor = Color.Red;
            private int _alphaPercent = 60; // 0-100

            [System.ComponentModel.Bindable(true)]
            public Color RgbColor
            {
                get => _rgbColor;
                set
                {
                    if (_rgbColor == value) return;
                    _rgbColor = value;
                    Invalidate(); // ★ バインド経由でも即再描画
                }
            }

            [System.ComponentModel.Bindable(true)]
            public int AlphaPercent
            {
                get => _alphaPercent;
                set
                {
                    var v = Math.Max(0, Math.Min(100, value));
                    if (_alphaPercent == v) return;
                    _alphaPercent = v;
                    Invalidate(); // ★ バインド経由でも即再描画
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

                // チェッカーボード
                int sz = 6;
                using (var brushDark = new SolidBrush(Color.LightGray))
                using (var brushLight = new SolidBrush(Color.White))
                {
                    for (int y = 0; y < Height; y += sz)
                    {
                        for (int x = 0; x < Width; x += sz)
                        {
                            bool dark = ((x / sz) + (y / sz)) % 2 == 0;
                            g.FillRectangle(dark ? brushDark : brushLight, x, y, sz, sz);
                        }
                    }
                }

                // 合成色
                int alpha = (int)Math.Round(_alphaPercent * 2.55); // 0–100 → 0–255
                using (var overlay = new SolidBrush(Color.FromArgb(alpha, _rgbColor)))
                {
                    g.FillRectangle(overlay, ClientRectangle);
                }
            }
        }

    }
}

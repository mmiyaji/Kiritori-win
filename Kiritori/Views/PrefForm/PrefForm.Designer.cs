using Kiritori.Helpers;
using Kiritori.Views.Controls;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kiritori
{
    partial class PrefForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeLogTab();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        // ==== Presets for Background / Hover ====
        private readonly (string Name, Color Color, int Alpha)[] _bgPresets = new[]
        {
            ("Transparent (0%)", Color.Black, 0),
            ("Dark (30%)",       Color.Black, 30),
            ("Dark (60%)",       Color.Black, 60),
            ("Light (30%)",      Color.White, 30),
            ("Light (60%)",      Color.White, 60),
        };

        private readonly (string Name, Color Color, int Alpha)[] _hoverPresets = new[]
        {
            ("Red",     Color.Red,     100),
            ("Cyan",    Color.Cyan,    100),
            ("Green",   Color.Lime,    100),
            ("Yellow",  Color.Yellow,  100),
            ("Magenta", Color.Magenta, 100),
            ("Blue",    Color.Blue,    100),
            ("Orange",  Color.Orange,  100),
            ("Black",   Color.Black,   100),
            ("White",   Color.White,   100),
        };
        // ロード中ガード
        private bool _loadingUi = false;

        // 末尾に付ける「カスタム」表示名（お好みで変更可）
        private const string CustomPresetName = "(Custom)";


        // ========= Common ==========
        private TabControl tabControl;
        private ToolTip toolTip1;

        // ========= General ==========
        private TabPage tabGeneral;

        // ========= Appearance ==========
        private TabPage tabAppearance;

        // ========= Shortcuts ==========
        private TabPage tabShortcuts;

        // ========= Advanced ==========
        private TabPage tabAdvanced;
        // ========= Logs ==========
        private TabPage tabLogs;
        // ========= Extensions ==========
        private TabPage tabExtensions;

        // ========= Info ==========
        private TabPage tabInfo;
        private TableLayoutPanel tlpInfoRoot;
        private TableLayoutPanel tlpInfoHeader;
        private GroupBox grpOnAppLaunch;
        private PictureBox picAppIcon;
        private Label labelAppName;
        private Label labelVersion;
        private Label labelSign;
        private Label labelCopyRight;
        private LinkLabel labelLinkWebsite;
        private GroupBox grpShortcuts;
        private CheckBox chkOpenMenuOnAppStart;


        // ========= Bottom Buttons ==========
        private TableLayoutPanel bottomBar;
        private FlowLayoutPanel leftButtons;
        private FlowLayoutPanel rightButtons;
        private Button btnCancelSettings;
        private Button btnSaveSettings;
        private Button btnExitAppLeft;
        private Button btnLicenses;

        // ========= Initialize =========
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.toolTip1 = new ToolTip(this.components);

            // ---- Form basics ----
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ClientSize = new Size(640, 460);
            this.MinimumSize = new Size(660, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Kiritori - Preferences";
            try { this.Icon = Properties.Resources.AppIcon; } catch { /* ignore if missing */ }

            // =========================================================
            // ① TabControl（先に作る）
            // =========================================================
            this.tabControl = new TabControl { Dock = DockStyle.Fill };

            this.tabInfo = new TabPage("App Info") { AutoScroll = true, Tag = "loc:Tab.Info" };
            this.tabGeneral = new TabPage("General") { AutoScroll = true, Tag = "loc:Tab.General" };
            this.tabAppearance = new TabPage("Appearance") { AutoScroll = true, Tag = "loc:Tab.Appearance" };
            this.tabShortcuts = new TabPage("Shortcuts") { AutoScroll = true, Tag = "loc:Tab.Shortcut" };
            this.tabAdvanced = new TabPage("Advanced") { AutoScroll = true, Tag = "loc:Tab.Advanced" };
            this.tabLogs = new TabPage("Logs") { AutoScroll = true, Tag = "loc:Tab.Logs" };
            this.tabExtensions = new TabPage("Extensions") { AutoScroll = true, Tag = "loc:Tab.Extensions" };

            if (!Helpers.PackagedHelper.IsPackaged())
            {
                this.tabControl.TabPages.AddRange(new TabPage[]
                {
                    this.tabInfo,
                    this.tabGeneral,
                    this.tabAppearance,
                    this.tabShortcuts,
                    this.tabExtensions,
                    this.tabAdvanced,
                    this.tabLogs,
                });
            }
            else
            {
                this.tabControl.TabPages.AddRange(new TabPage[]
                {
                    this.tabInfo,
                    this.tabGeneral,
                    this.tabAppearance,
                    this.tabShortcuts,
                    this.tabAdvanced,
                    this.tabLogs,
                });
            }

            // =========================================================
            // ② Bottom bar（Exit 左 / Cancel & Save 右）
            // =========================================================
            this.bottomBar = new TableLayoutPanel
            {
                Height = 38,
                Padding = new Padding(6),
                ColumnCount = 3,
                Dock = DockStyle.Top,
                AutoSize = false
            };
            this.bottomBar.Margin = new Padding(0);
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // this.bottomBar.Paint += (s, e) =>
            // {
            //     e.Graphics.DrawLine(SystemPens.ControlLight, 0, 0, this.bottomBar.Width, 0);
            // };
            this.leftButtons = new FlowLayoutPanel
            {
                // FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };

            this.btnExitAppLeft = new Button
            {
                Text = "Exit App",
                AutoSize = true,
                Margin = new Padding(0, 0, 6, 0),
                Tag = "loc:Text.BtnExit"
            };
            this.btnExitAppLeft.Click += new EventHandler(this.btnExitApp_Click);
            this.btnLicenses = new Button
            {
                Text = "Licenses…",
                AutoSize = true,
                Margin = new Padding(0),
                Tag = "loc:Text.LicensesButton"
            };
            this.btnLicenses.Click += (s, e) =>
            {
                using (var dlg = new Kiritori.Views.ThirdPartyDialog())
                    dlg.ShowDialog(this);
            };

            this.leftButtons.Controls.Add(this.btnExitAppLeft);
            this.leftButtons.Controls.Add(this.btnLicenses);

            this.rightButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };
            this.btnCancelSettings = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Margin = new Padding(6, 0, 0, 0),
                Tag = "loc:Text.BtnCancel"
            };
            this.btnCancelSettings.Click += new EventHandler(this.btnCancelSettings_Click);

            this.btnSaveSettings = new Button
            {
                Text = "Save",
                AutoSize = true,
                Margin = new Padding(0),
                Tag = "loc:Text.BtnSave"
            };
            this.btnSaveSettings.Click += new EventHandler(this.btnSaveSettings_Click);

            this.rightButtons.Controls.Add(this.btnCancelSettings);
            this.rightButtons.Controls.Add(this.btnSaveSettings);

            var spacer = new Panel { Dock = DockStyle.Fill };

            this.bottomBar.Controls.Add(this.leftButtons, 0, 0);
            this.bottomBar.Controls.Add(spacer, 1, 0);
            this.bottomBar.Controls.Add(this.rightButtons, 2, 0);

            // =========================================================
            // ③ ルートの shell（フォーム余白はここで持つ）
            // =========================================================
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 0, 12, 8)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // タブ
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // ボタンバー

            shell.Controls.Add(this.tabControl, 0, 0);
            shell.Controls.Add(this.bottomBar, 0, 1);

            // =========================================================
            // Appearance タブ
            // =========================================================
            BuildAppearanceTab();
            // =========================================================
            // General タブ
            // =========================================================
            BuildGeneralTab();
            // =========================================================
            // Advanced タブ
            // =========================================================
            BuildAdvancedTab();
            // =========================================================
            // Extensions タブ
            // =========================================================
            BuildExtensionsTab();
            // =========================================================
            // Info タブ
            // =========================================================
            this.tlpInfoRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12, 0, 12, 12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            this.tlpInfoRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // 0: ヘッダ
            this.tlpInfoRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // 1: ショートカット
            this.tlpInfoRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // 2: 起動時カード

            // --- 上段：名刺レイアウト ---
            this.tlpInfoHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 0)
            };
            this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // アイコン列
            this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 情報列

            // 左：アプリアイコン（初期は120px、幅に応じて後で縮小）
            this.picAppIcon = new PictureBox
            {
                Size = new Size(110, 110),
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
            };
            try { this.picAppIcon.Image = global::Kiritori.Properties.Resources.icon_128x128; } catch { /* ignore */ }

            // 右：テキスト縦積み
            var infoRight = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false
            };

            // 見出し（大きめ太字）
            this.labelAppName = new Label
            {
                Text = "\"Kiritori\" for Windows",
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 5),
                Tag = "loc:App.Name"
            };
            this.labelAppName.Font = new Font(
                this.Font.FontFamily,
                this.Font.Size + 6,
                FontStyle.Bold
            );

            // バージョン（ApplyDynamicTexts で差し替え）
            this.labelVersion = new Label
            {
                Text = "Version - built at (on load)",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };

            // 作者
            this.labelSign = new Label
            {
                Text = "Developed by mmiyaji",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 2)
            };

            // 著作権
            this.labelCopyRight = new Label
            {
                Text = "© 2013–2025",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };

            // リンク
            this.labelLinkWebsite = new LinkLabel
            {
                Text = "Homepage - https://kiritori.ruhenheim.org",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };
            this.labelLinkWebsite.LinkClicked += new LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);

            // 右側へ追加
            infoRight.Controls.Add(this.labelAppName);
            infoRight.Controls.Add(this.labelVersion);
            infoRight.Controls.Add(this.labelSign);
            infoRight.Controls.Add(this.labelCopyRight);
            infoRight.Controls.Add(this.labelLinkWebsite);

            // ヘッダに左右を配置
            this.tlpInfoHeader.Controls.Add(this.picAppIcon, 0, 0);
            this.tlpInfoHeader.Controls.Add(infoRight, 1, 0);

            // --- 下段：ショートカット ---
            this.grpShortcuts = new GroupBox
            {
                Text = "Shortcuts",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Tag = "loc:Text.Shortcuts",
            };

            var tlpShortcutsInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(8),
            };
            tlpShortcutsInfo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlpShortcutsInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + Shift + 5", "Start capture", tagKey: "Text.StartCapture");
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + W, ESC", "Close window", tagKey: "Text.CloseWindow");
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + C", "Copy to clipboard", tagKey: "Text.CopyToClipboard");
            // AddShortcutInfo(tlpShortcutsInfo, "Ctrl + S",         "Save image", tagKey: "Text.SaveImage");
            // AddShortcutInfo(tlpShortcutsInfo, "Ctrl + T",         "Run OCR (copy result / show toast)", tagKey: "Text.RunOCRDesc");
            // AddShortcutInfo(tlpShortcutsInfo, "", "");
            AddShortcutInfo(tlpShortcutsInfo, "", "...and more. See Shortcuts", tagKey: "Text.AndMore");
            this.grpShortcuts.Controls.Clear();
            this.grpShortcuts.Controls.Add(tlpShortcutsInfo);
            RebuildShortcutsInfo();

            // ==== 起動時カード(GroupBox) ====
            this.grpOnAppLaunch = new GroupBox
            {
                Text = "On app launch",
                Tag = "loc:Text.OnAppLaunch",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                // Margin = new Padding(0, 8, 0, 8)
            };

            // 本文レイアウト
            var tlpOnAppLaunch = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8)
            };

            // チェックボックス
            this.chkOpenMenuOnAppStart = new CheckBox
            {
                AutoSize = true,
                Text = "Open this menu on app start",
                Tag = "loc:Text.OpenMenuOnAppStart"
            };

            // 補足ラベル
            var lblDesc = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 4, 0, 0),
                MaximumSize = new Size(1, 0),
                UseMnemonic = false,
                Text = "Automatically shows this settings window when the app starts.\r\nWindows startup (launch at sign-in) is configured in the General tab.",
                Tag = "loc:Text.OpenMenuOnAppStart.Desc"
            };

            tlpOnAppLaunch.Controls.Add(this.chkOpenMenuOnAppStart);
            tlpOnAppLaunch.Controls.Add(lblDesc);
            this.grpOnAppLaunch.Controls.Add(tlpOnAppLaunch);

            // ルートに配置
            this.tlpInfoRoot.Controls.Add(this.tlpInfoHeader, 0, 0); // 上段：名刺ヘッダ
            this.tlpInfoRoot.Controls.Add(this.grpShortcuts, 0, 1); // 中段：ショートカット
            this.tlpInfoRoot.Controls.Add(this.grpOnAppLaunch, 0, 2); // 下段：起動時カード

            // タブに追加
            this.tabInfo.Controls.Clear();
            this.tabInfo.Controls.Add(this.tlpInfoRoot);

            // リサイズでレスポンシブ調整
            tlpOnAppLaunch.SizeChanged += (_, __) =>
            {
                var pad = tlpOnAppLaunch.Padding.Left + tlpOnAppLaunch.Padding.Right;
                lblDesc.MaximumSize = new Size(Math.Max(100, tlpOnAppLaunch.ClientSize.Width - pad), 0);
            };
            this.tabInfo.Resize += (_, __) => LayoutInfoTabResponsive();
            this.Resize += (_, __) => LayoutInfoTabResponsive();
            this.Controls.Add(shell);
        }

        // ========= helpers =========
        private static TableLayoutPanel NewGrid(int rows, int cols)
        {
            var tlp = new TableLayoutPanel
            {
                ColumnCount = cols,
                RowCount = rows,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(2)
            };
            for (int c = 0; c < cols; c++)
                tlp.ColumnStyles.Add(new ColumnStyle(c == 0 ? SizeType.AutoSize : SizeType.Percent, c == 0 ? 0 : 100));
            for (int r = 0; r < rows; r++)
                tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            return tlp;
        }

        // ← 縦積み用 1 列レイアウト（Margin を確実に効かせる）
        private static TableLayoutPanel NewStack()
        {
            var tlp = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 0,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return tlp;
        }

        // AutoSize の GroupBox を簡単に作る
        private static GroupBox NewGroup(string title)
        {
            return new GroupBox
            {
                Text = title,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(10)
            };
        }

        private static Label NewRightLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Right
            };
        }

        private void AddShortcutRow(
            TableLayoutPanel tlp,
            int row,
            string labelText,
            out Label label,
            out TextBox textBox,
            string placeholder,
            int colOffset,
            string tagKey = null)
        {
            label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 3, 6),
                Tag = tagKey != null ? $"loc:{tagKey}" : null
            };
            textBox = new TextBox
            {
                Text = placeholder,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 3, 3, 3),
                Enabled = false,
            };

            tlp.Controls.Add(label, colOffset + 0, row);
            tlp.Controls.Add(textBox, colOffset + 1, row);
        }
        private void AddShortcutInfo(TableLayoutPanel tlp, string keys, string desc, string tagKey = null)
        {
            // var labelFont = new Font(
            //     this.Font.FontFamily,
            //     this.Font.Size - 1
            // );
            if (string.IsNullOrEmpty(keys) && string.IsNullOrEmpty(desc))
            {
                // 空行
                var spacer = new Separator();
                tlp.Controls.Add(spacer);
                tlp.SetColumnSpan(spacer, 2);
                return;
            }
            else if (string.IsNullOrEmpty(keys))
            {
                // キーなし（説明だけ、左寄せ）
                var lblDescOnly = new Label
                {
                    Text = desc,
                    AutoSize = true,
                    Margin = new Padding(0, 2, 0, 2),
                    ForeColor = SystemColors.GrayText,
                    // Font = labelFont,
                    Tag = tagKey != null ? $"loc:{tagKey}" : null
                };
                tlp.Controls.Add(lblDescOnly);
                tlp.SetColumnSpan(lblDescOnly, 2);
                return;
            }
            var lblKey = new Label
            {
                Text = keys,
                AutoSize = true,
                // Font = labelFont,
                Margin = new Padding(0, 2, 20, 2), // 右に余白
            };
            var lblDesc = new Label
            {
                Text = desc,
                AutoSize = true,
                // Font = labelFont,
                Margin = new Padding(0, 2, 0, 2),
                Tag = tagKey != null ? $"loc:{tagKey}" : null
            };
            tlp.Controls.Add(lblKey);
            tlp.Controls.Add(lblDesc);
        }

        private void SelectPresetComboFromSettings()
        {
            var S = Properties.Settings.Default;

            // Hover
            if (cmbHoverPreset != null)
            {
                RemoveCustomIfExists(cmbHoverPreset);
                // int idx = FindHoverPresetIndex(S.HoverHighlightColor, S.HoverHighlightAlphaPercent);
                int idx = FindHoverPresetIndex(S.HoverHighlightColor, 60);
                bool valid = IsAlphaValid(S.HoverHighlightAlphaPercent);
                if (idx >= 0 && valid)
                {
                    cmbHoverPreset.SelectedIndex = idx;
                }
                else
                {
                    AddCustomAndSelect(cmbHoverPreset);
                }
            }

            // Background
            if (cmbBgPreset != null)
            {
                int idx = FindBgPresetIndex(S.CaptureBackgroundColor, S.CaptureBackgroundAlphaPercent);
                bool valid = IsAlphaValid(S.CaptureBackgroundAlphaPercent);
                if (idx >= 0 && valid)
                {
                    cmbBgPreset.SelectedIndex = idx;
                }
                else
                {
                    AddCustomAndSelect(cmbBgPreset);
                }
            }
        }

        private int FindHoverPresetIndex(Color c, int alpha)
        {
            for (int i = 0; i < _hoverPresets.Length; i++)
            {
                // アルファを 0–100 で厳密一致（必要なら±誤差許容に）
                if (_hoverPresets[i].Color.ToArgb() == c.ToArgb())
                //  && _hoverPresets[i].Alpha == alpha)
                    return i;
            }
            return -1;
        }

        private int FindBgPresetIndex(Color c, int alpha)
        {
            for (int i = 0; i < _bgPresets.Length; i++)
            {
                if (_bgPresets[i].Color.ToArgb() == c.ToArgb() &&
                    _bgPresets[i].Alpha == alpha)
                    return i;
            }
            return -1;
        }


        private void WireUpDataBindings()
        {
            var S = Properties.Settings.Default;
            this.trackbarDefaultOpacity.DataBindings.Clear();
            this.labelDefaultOpacityVal.DataBindings.Clear();

            // --- Background preview <-> Settings ---
            // 色
            this.previewBg.DataBindings.Add(
                new Binding("RgbColor", S, nameof(S.CaptureBackgroundColor),
                    true, DataSourceUpdateMode.OnPropertyChanged));
            // アルファ（%）
            this.previewBg.DataBindings.Add(
                new Binding("AlphaPercent", S, nameof(S.CaptureBackgroundAlphaPercent),
                    true, DataSourceUpdateMode.OnPropertyChanged));

            // --- Hover preview <-> Settings ---
            this.previewHover.DataBindings.Add(
                new Binding("RgbColor", S, nameof(S.HoverHighlightColor),
                    true, DataSourceUpdateMode.OnPropertyChanged));
            this.previewHover.DataBindings.Add(
                new Binding("AlphaPercent", S, nameof(S.HoverHighlightAlphaPercent),
                    true, DataSourceUpdateMode.OnPropertyChanged));

            // --- その他（例） ---
            this.chkWindowShadow.DataBindings.Add(
                new Binding("Checked", S, nameof(S.WindowShadowEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkAfloat.DataBindings.Add(
                new Binding("Checked", S, nameof(S.AlwaysOnTop), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkHighlightOnHover.DataBindings.Add(
                new Binding("Checked", S, nameof(S.HoverHighlightEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkShowOverlay.DataBindings.Add(
                new Binding("Checked", S, nameof(S.OverlayEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.numHoverThickness.DataBindings.Add(
                new Binding("Value", S, nameof(S.HoverHighlightThickness), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkScreenGuide.DataBindings.Add(
                new Binding("Checked", S, nameof(S.ScreenGuideEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkTrayNotify.DataBindings.Add(
                new Binding("Checked", S, nameof(S.ShowNotificationOnCapture), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkTrayNotifyOCR.DataBindings.Add(
                new Binding("Checked", S, nameof(S.ShowNotificationOnOcr), true, DataSourceUpdateMode.OnPropertyChanged));

            if (S.WindowOpacityPercent < this.trackbarDefaultOpacity.Minimum)
                S.WindowOpacityPercent = this.trackbarDefaultOpacity.Minimum;
            if (S.WindowOpacityPercent > this.trackbarDefaultOpacity.Maximum)
                S.WindowOpacityPercent = this.trackbarDefaultOpacity.Maximum;

            this.trackbarDefaultOpacity.DataBindings.Add(
                new Binding("Value", S, nameof(S.WindowOpacityPercent),
                    /* formattingEnabled */ true,
                    DataSourceUpdateMode.OnPropertyChanged));

            // 4) ラベルは表示専用（int → "NN%" に変換）
            var lblOpacityBinding = new Binding(
                "Text", S, nameof(S.WindowOpacityPercent),
                /* formattingEnabled */ true,
                DataSourceUpdateMode.Never);
            lblOpacityBinding.Format += (o, e) =>
            {
                // e.Value は int（または boxed int）を想定
                int v;
                if (e.Value is int iv) v = iv;
                else int.TryParse(e.Value?.ToString(), out v);
                e.Value = v + "%";
            };
            this.labelDefaultOpacityVal.DataBindings.Add(lblOpacityBinding);

            // Run at startup は OS 処理が絡むため表示だけ同期＋既存ハンドラで処理
            this.chkRunAtStartup.Checked = S.RunAtStartup;
            this.chkRunAtStartup.CheckedChanged += (s, e) =>
            {
                if (_initStartupToggle) return;
                S.RunAtStartup = chkRunAtStartup.Checked;
            };
            this.chkOpenMenuOnAppStart.DataBindings.Add(
                new Binding("Checked", S, nameof(S.OpenPreferencesOnStartup), true, DataSourceUpdateMode.OnPropertyChanged));
        }
        private void HookRuntimeEvents()
        {
            if (cmbHoverPreset != null)
                cmbHoverPreset.SelectedIndexChanged += CmbHoverPreset_SelectedIndexChanged;

            if (cmbBgPreset != null)
                cmbBgPreset.SelectedIndexChanged += CmbBgPreset_SelectedIndexChanged;
        }
        private void PopulatePresetCombos()
        {
            // Hover
            if (cmbHoverPreset != null)
            {
                cmbHoverPreset.BeginUpdate();
                cmbHoverPreset.Items.Clear();
                foreach (var p in _hoverPresets) cmbHoverPreset.Items.Add(p.Name);
                // cmbHoverPreset.Items.Add(CustomPresetName);
                cmbHoverPreset.EndUpdate();
            }

            // Background
            if (cmbBgPreset != null)
            {
                cmbBgPreset.BeginUpdate();
                cmbBgPreset.Items.Clear();
                foreach (var p in _bgPresets) cmbBgPreset.Items.Add(p.Name);
                // cmbBgPreset.Items.Add(CustomPresetName);
                cmbBgPreset.EndUpdate();
            }
        }

        // ---- 安全なハンドラ（nullガード付き） ----
        private void CmbHoverPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            var S = Properties.Settings.Default;

            // SelectedItem → string（null安全）
            string sel = null;
            if (cmbHoverPreset != null && cmbHoverPreset.SelectedItem != null)
                sel = cmbHoverPreset.SelectedItem as string ?? cmbHoverPreset.SelectedItem.ToString();

            if (string.IsNullOrEmpty(sel)) return;
            if (sel == CustomPresetName) return;
            // _hoverPresets から一致を探す（ValueTuple でも struct/class でも可）
            Color color = Color.Red;

            foreach (var p in _hoverPresets)
            {
                if (p.Name == sel)
                {
                    S.HoverHighlightColor = p.Color;
                    S.HoverHighlightAlphaPercent = p.Alpha;
                    // バインドで previewHover は自動更新。保険で下記入れてもOK:
                    // previewHover.RgbColor = p.Color;
                    // previewHover.AlphaPercent = p.Alpha;
                    break;
                }
            }
        }

        private void CmbBgPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;

            var S = Properties.Settings.Default;
            var sel = (cmbBgPreset?.SelectedItem as string) ?? cmbBgPreset?.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(sel)) return;

            foreach (var p in _bgPresets)
            {
                if (p.Name == sel)
                {
                    S.CaptureBackgroundColor = p.Color;
                    S.CaptureBackgroundAlphaPercent = p.Alpha;

                    // フォールバックの即時反映（バインドが効いていれば不要）
                    // previewBg.RgbColor = p.Color;
                    // previewBg.AlphaPercent = p.Alpha;
                    break;
                }
            }
        }
        // --- ComboBox 用ユーティリティ ---
        private static void AddCustomAndSelect(ComboBox cmb)
        {
            if (cmb == null) return;
            if (!cmb.Items.Cast<object>().Any(x => string.Equals(x?.ToString(), CustomPresetName, StringComparison.Ordinal)))
                cmb.Items.Add(CustomPresetName);
            cmb.SelectedItem = CustomPresetName;
        }

        private static void RemoveCustomIfExists(ComboBox cmb)
        {
            if (cmb == null) return;
            int idx = -1;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (string.Equals(cmb.Items[i]?.ToString(), CustomPresetName, StringComparison.Ordinal))
                {
                    idx = i; break;
                }
            }
            if (idx >= 0) cmb.Items.RemoveAt(idx);
        }

        // 値の妥当性チェック（アルファは 0–100、色は任意）
        private static bool IsAlphaValid(int alpha) => alpha >= 0 && alpha <= 100;

    }
}

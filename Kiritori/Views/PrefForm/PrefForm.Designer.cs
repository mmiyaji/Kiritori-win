using Kiritori.Helpers;
using Kiritori.Views.Controls;
using Kiritori.Services.Ocr;
using Kiritori.Services.History;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kiritori.Services.Logging;
using System.IO;
using System.Globalization;

namespace Kiritori
{
    partial class PrefForm
    {
        private bool _advancedBuilt;
        private bool _extensionsBuilt;
        private bool _shortcutsBuilt;
        private bool _historyBuilt;
        private bool _appearanceBuilt;
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

        // Application Settings
        private GroupBox grpAppSettings;
        private Label labelLanguage;
        private ComboBox cmbLanguage;
        private Label labelOCRLanguage;
        private ComboBox cmbOCRLanguage;
        private Label labelStartup;
        private CheckBox chkRunAtStartup;
        private Button btnOpenStartupSettings;
        // private Label labelStartupInfo;
        private Label labelHistory;
        private NumericUpDown textBoxHistory;

        // Hotkeys
        private GroupBox grpHotkey;
        private Label labelHotkeyCapture;
        private TextBox textBoxKiritori;            // Capture (existing)
        private Label labelHotkeyCaptureOCR;
        private TextBox textBoxHotkeyCaptureOCR;
        private Label labelHotkeyLivePreview;       // Live Preview
        private TextBox textBoxHotkeyLivePreview;
        private Label labelHotkeyCaptureFixed;       // Capture at fixed size
        private TextBox textBoxHotkeyCaptureFixed;
        private Label labelHotkeyCapturePrev;       // Capture at previous region
        private TextBox textBoxCapturePrev;

        // ========= Appearance ==========
        private TabPage tabAppearance;

        // Capture settings（簡素化）
        private GroupBox grpCaptureSettings;
        private CheckBox chkScreenGuide;   // show guide lines
        private CheckBox chkTrayNotify;    // notify on capture
        private CheckBox chkTrayNotifyOCR;    // notify on capture
        // private CheckBox chkPlaySound;     // play sound on capture
        private Label labelBgPreset;
        private ComboBox cmbBgPreset;
        private AlphaPreviewPanel previewBg;

        // Window settings（プリセット＋不透明度統合）
        private GroupBox grpWindowSettings;
        private CheckBox chkWindowShadow;
        private CheckBox chkAfloat;
        private CheckBox chkHighlightOnHover;
        private CheckBox chkShowOverlay;
        private Label labelHoverPreset;
        private ComboBox cmbHoverPreset;
        private Label labelHoverThickness;
        private NumericUpDown numHoverThickness;
        private AlphaPreviewPanel previewHover; // 透過は 100% 固定
        private Label labelDefaultOpacity;
        private TrackBar trackbarDefaultOpacity;
        private Label labelDefaultOpacityVal;
        private GroupBox grpLivePreview;
        private CheckBox chkLiveShowStats;
        private Label lblSaveFolder, lblGifMax;
        private TextBox txtSaveFolder;
        private Button btnBrowseSaveFolder, btnClearSaveFolder;
        private NumericUpDown numGifMax;
        private ToolTip tips;
        private Label lblGifFps, lblGifOptimize, lblGifWidth;
        private NumericUpDown numGifFps;
        private NumericUpDown numGifWidth;
        private CheckBox chkGifOptimize;

        // ========= Shortcuts ==========
        private TabPage tabShortcuts;


        // ========= Advanced ==========
        private TabPage tabAdvanced;
        // ========= Logs ==========
        private TabPage tabLogs;
        // ========= Extensions ==========
        private TabPage tabExtensions;

        // ========= History ==========
        private TabPage tabHistory;

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
            this.tabHistory = new TabPage("History") { AutoScroll = true, Tag = "loc:Tab.History" };

            // if (!Helpers.PackagedHelper.IsPackaged())
            // {
                this.tabControl.TabPages.AddRange(new TabPage[]
                {
                    this.tabInfo,
                    this.tabGeneral,
                    this.tabAppearance,
                    this.tabShortcuts,
                    this.tabExtensions,
                    this.tabAdvanced,
                    this.tabHistory,
                    this.tabLogs,
                });
            // }
            // else
            // {
            //     this.tabControl.TabPages.AddRange(new TabPage[]
            //     {
            //         this.tabInfo,
            //         this.tabGeneral,
            //         this.tabAppearance,
            //         this.tabShortcuts,
            //         this.tabAdvanced,
            //         this.tabHistory,
            //         this.tabLogs,
            //     });
            // }
            // タブ切り替え前に再帰的にレイアウトを停止（子コントロールの OnVisibleChanged 連鎖を防ぐ）
            this.tabControl.Selecting += (s, e) =>
            {
                if (e.TabPage != null) SuspendLayoutDeep(e.TabPage);
            };
            this.tabControl.Selected += (s, e) =>
            {
                // 履歴タブから離れたら idle の生成を止める
                if (e.TabPage != this.tabHistory) StopLazyThumbLoad();

                if (e.TabPage == this.tabAdvanced && !_advancedBuilt) { _advancedBuilt = true; BuildAdvancedTab(); }
                else if (e.TabPage == this.tabExtensions && !_extensionsBuilt) { _extensionsBuilt = true; BuildExtensionsTab(); }
                else if (e.TabPage == this.tabAppearance && !_appearanceBuilt) { _appearanceBuilt = true; BuildAppearanceTab(); }
                else if (e.TabPage == this.tabShortcuts && !_shortcutsBuilt) { _shortcutsBuilt = true; BuildShortcutsTab(); }
                else if (e.TabPage == this.tabHistory)
                {
                    if (!_historyBuilt)
                    {
                        _historyBuilt = true;
                        BuildHistoryTab();
                    }
                    try
                    {
                        var snap = HistoryBridge.GetSnapshot();
                        this.SetupHistoryTabIfNeededAndShow(snap);
                    }
                    catch { /* 取得できなければ無視 */ }
                    StartLazyThumbLoad();
                }
                // 子から順に Resume して最後に TabPage で一括レイアウト実行
                if (e.TabPage != null)
                {
                    ResumeLayoutDeep(e.TabPage);
                    e.TabPage.PerformLayout();
                }
            };
            // フォーム表示時、既に選ばれているタブだけ即構築（安全策）
            this.Shown += (s, e) =>
            {
                var t = this.tabControl.SelectedTab;
                if (t != null) SuspendLayoutDeep(t);
                if (t == this.tabAdvanced && !_advancedBuilt) { _advancedBuilt = true; BuildAdvancedTab(); }
                else if (t == this.tabExtensions && !_extensionsBuilt) { _extensionsBuilt = true; BuildExtensionsTab(); }
                else if (t == this.tabAppearance && !_appearanceBuilt) { _appearanceBuilt = true; BuildAppearanceTab(); }
                else if (t == this.tabShortcuts && !_shortcutsBuilt) { _shortcutsBuilt = true; BuildShortcutsTab(); }
                else if (t == this.tabHistory && !_historyBuilt)
                {
                    _historyBuilt = true;
                    BuildHistoryTab();

                    var snap = HistoryBridge.GetSnapshot();
                    this.SetupHistoryTabIfNeededAndShow(snap);

                    StartLazyThumbLoad();
                }
                // if (t == this.tabInfo       && !_infoBuilt)       { _infoBuilt = true; BuildInfoTab(); }
                if (t != null) { ResumeLayoutDeep(t); t.PerformLayout(); }
            };

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
            // General タブ（縦積みレイアウト）
            // =========================================================
            this.tabGeneral.SuspendLayout();
            var scrollGeneral = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 12),
                Padding = new Padding(12)
            };
            this.tabGeneral.Controls.Add(scrollGeneral);
            var stackGeneral = NewStack();
            stackGeneral.Dock = DockStyle.Top;
            stackGeneral.AutoSize = true;
            stackGeneral.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stackGeneral.SuspendLayout();
            this.tabGeneral.Controls.Add(stackGeneral);

            // Application Settings
            this.grpAppSettings = NewGroup("Application Settings");
            this.grpAppSettings.Tag = "loc:Text.AppSetting";
            var tlpApp = NewGrid(3, 3);
            tlpApp.SuspendLayout();

            this.labelLanguage = NewRightLabel("Language");
            this.labelLanguage.Tag = "loc:Text.Language";
            this.cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            this.cmbLanguage.Items.AddRange(new object[] { "English (en)", "日本語 (ja)" });
            this.cmbLanguage.SelectedIndex = 0;

            this.labelOCRLanguage = NewRightLabel("OCR Language");
            this.labelOCRLanguage.Tag = "loc:Setting.Display.OcrLanguage";
            this.cmbOCRLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill};

            PopulateOcrLanguageCombo();
            RestoreOcrLanguageSelection();
            this.cmbOCRLanguage.SelectedIndexChanged += (s, e) =>
            {
                if (_loadingUi) return;          // 初期化中は無視
                SaveOcrLanguageSelection();
            };

            this.labelStartup = NewRightLabel("Startup");
            this.labelStartup.Tag = "loc:Text.Startup";
            var flowStartup = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            this.chkRunAtStartup = new CheckBox { Text = "Run at startup", AutoSize = true, Enabled = false, Tag = "loc:Text.Runatstartup" };
            this.btnOpenStartupSettings = new Button { Text = "Open Startup", AutoSize = true, Tag = "loc:Text.BtnStartupFolder" };
            this.btnOpenStartupSettings.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.btnOpenStartupSettings.Click += new EventHandler(this.btnOpenStartupSettings_Click);
            // this.labelStartupInfo = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Startup is managed by Windows.", Dock = DockStyle.Fill };
            // this.toolTip1.SetToolTip(this.labelStartupInfo, "Settings > Apps > Startup");
            flowStartup.Controls.Add(this.chkRunAtStartup);
            flowStartup.Controls.Add(this.btnOpenStartupSettings);

            this.labelHistory = NewRightLabel("History limit");
            this.labelHistory.Tag = "loc:Text.HistoryLimit";
            this.textBoxHistory = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Value = 20,
                Anchor = AnchorStyles.Left,
                Dock = DockStyle.Fill
            };
            this.textBoxHistory.AutoSize = true;
            this.textBoxHistory.MaximumSize = new Size(120, 0);

            tlpApp.Controls.Add(this.labelLanguage, 0, 0);
            tlpApp.Controls.Add(this.cmbLanguage, 1, 0);

            tlpApp.Controls.Add(this.labelOCRLanguage, 0, 1);
            tlpApp.Controls.Add(this.cmbOCRLanguage, 1, 1);

            tlpApp.Controls.Add(this.labelStartup, 0, 2);
            var flowStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            flowStack.Controls.Add(flowStartup);
            tlpApp.Controls.Add(flowStack, 1, 2);

            tlpApp.Controls.Add(this.labelHistory, 0, 3);
            tlpApp.Controls.Add(this.textBoxHistory, 1, 3);

            tlpApp.ResumeLayout(false);
            this.grpAppSettings.Controls.Add(tlpApp);

            // Hotkeys（上マージンで間隔）
            this.grpHotkey = NewGroup("Hotkeys");
            this.grpHotkey.Tag = "loc:Text.Hotkeys";
            this.grpHotkey.Margin = new Padding(0, 8, 0, 0);

            var tlpHot = NewGrid(4, 3);
            tlpHot.SuspendLayout();
            this.labelHotkeyCapture = NewRightLabel("Image capture");
            this.labelHotkeyCapture.Tag = "loc:Text.ImageCapture";
            // this.textBoxKiritori = new TextBox
            // {
            //     Enabled = false,
            //     Width = 160,
            //     Text = "Ctrl + Shift + 5",
            //     Dock = DockStyle.Fill
            // };
            this.labelHotkeyCaptureOCR = NewRightLabel("OCR capture");
            this.labelHotkeyCaptureOCR.Tag = "loc:Text.OCRCapture";

            this.labelHotkeyLivePreview = NewRightLabel("Live preview");
            this.labelHotkeyLivePreview.Tag = "loc:Text.LivePreview";

            this.labelHotkeyCaptureFixed = NewRightLabel("Capture at fixed size");
            this.labelHotkeyCaptureFixed.Tag = "loc:Text.CaptureFixed";

            this.textBoxKiritori = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxKiritori).SetFromText(
                Properties.Settings.Default.HotkeyCapture, DEF_HOTKEY_CAP);
            ((HotkeyPicker)this.textBoxKiritori).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.image, (HotkeyPicker)this.textBoxKiritori);
            };

            this.textBoxHotkeyCaptureOCR = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxHotkeyCaptureOCR).SetFromText(
                Properties.Settings.Default.HotkeyOcr, DEF_HOTKEY_OCR);
            ((HotkeyPicker)this.textBoxHotkeyCaptureOCR).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.ocr, (HotkeyPicker)this.textBoxHotkeyCaptureOCR);
            };

            this.textBoxHotkeyLivePreview = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxHotkeyLivePreview).SetFromText(
                Properties.Settings.Default.HotkeyLive, DEF_HOTKEY_LIVE);
            ((HotkeyPicker)this.textBoxHotkeyLivePreview).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.live, (HotkeyPicker)this.textBoxHotkeyLivePreview);
            };

            this.textBoxHotkeyCaptureFixed = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxHotkeyCaptureFixed).SetFromText(
                Properties.Settings.Default.HotkeyCaptureFixed, DEF_HOTKEY_FIXED);
            ((HotkeyPicker)this.textBoxHotkeyCaptureFixed).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.fix, (HotkeyPicker)this.textBoxHotkeyCaptureFixed);
            };

            var btnResetCap = new Button { Text = "Reset", AutoSize = true };
            btnResetCap.Tag = "loc:Text.ResetDefault";
            btnResetCap.Click += (s, e) => ResetCaptureHotkeyToDefault();

            var btnResetOcr = new Button { Text = "Reset", AutoSize = true };
            btnResetOcr.Tag = "loc:Text.ResetDefault";
            btnResetOcr.Click += (s, e) => ResetOcrHotkeyToDefault();

            var btnResetLive = new Button { Text = "Reset", AutoSize = true };
            btnResetLive.Tag = "loc:Text.ResetDefault";
            btnResetLive.Click += (s, e) => ResetLiveHotkeyToDefault();

            var btnResetFixed = new Button { Text = "Reset", AutoSize = true };
            btnResetFixed.Tag = "loc:Text.ResetDefault";
            btnResetFixed.Click += (s, e) => ResetFixedHotkeyToDefault();

            var btnExecCap = new Button { Text = "Exec", AutoSize = true };
            btnExecCap.Tag = "loc:Text.ExecDefault";
            btnExecCap.Click += (s, e) => ExecCaptureHotkeyToDefault();

            var btnExecOcr = new Button { Text = "Exec", AutoSize = true };
            btnExecOcr.Tag = "loc:Text.ExecDefault";
            btnExecOcr.Click += (s, e) => ExecOcrHotkeyToDefault();

            var btnExecLive = new Button { Text = "Exec", AutoSize = true };
            btnExecLive.Tag = "loc:Text.ExecDefault";
            btnExecLive.Click += (s, e) => ExecLiveHotkeyToDefault();

            var btnExecFixed = new Button { Text = "Exec", AutoSize = true };
            btnExecFixed.Tag = "loc:Text.ExecDefault";
            btnExecFixed.Click += (s, e) => ExecFixedHotkeyToDefault();

            this.labelHotkeyCapturePrev = NewRightLabel("Capture at previous region");
            this.labelHotkeyCapturePrev.Tag = "loc:Text.PreviousCapture";
            this.textBoxCapturePrev = new TextBox { Enabled = false, Width = 160, Text = "(disabled)" };

            tlpHot.Controls.Add(this.labelHotkeyCapture, 0, 0);
            tlpHot.Controls.Add(this.textBoxKiritori, 1, 0);
            tlpHot.Controls.Add(PackButtons(btnResetCap, btnExecCap), 3, 0);

            tlpHot.Controls.Add(this.labelHotkeyCaptureOCR, 0, 1);
            tlpHot.Controls.Add(this.textBoxHotkeyCaptureOCR, 1, 1);
            tlpHot.Controls.Add(PackButtons(btnResetOcr, btnExecOcr), 3, 1);

            tlpHot.Controls.Add(this.labelHotkeyLivePreview, 0, 2);
            tlpHot.Controls.Add(this.textBoxHotkeyLivePreview, 1, 2);
            tlpHot.Controls.Add(PackButtons(btnResetLive, btnExecLive), 3, 2);

            tlpHot.Controls.Add(this.labelHotkeyCaptureFixed, 0, 3);
            tlpHot.Controls.Add(this.textBoxHotkeyCaptureFixed, 1, 3);
            tlpHot.Controls.Add(PackButtons(btnResetFixed, btnExecFixed), 3, 3);

            tlpHot.ResumeLayout(false);
            this.grpHotkey.Controls.Add(tlpHot);

            // stack へ追加
            stackGeneral.Controls.Add(this.grpAppSettings, 0, 0);
            stackGeneral.Controls.Add(this.grpHotkey, 0, 1);
            stackGeneral.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);

            // =========================================================
            // Appearance タブ（ラジーロード：選択時に BuildAppearanceTab() で構築）
            // =========================================================

            // =========================================================
            // Shortcuts タブ（縦積み）
            // =========================================================

            // =========================================================
            // Advanced タブ（プレースホルダ）
            // =========================================================
            // BuildAdvancedTab();
            // =========================================================
            // Extensions タブ（プレースホルダ）
            // =========================================================
            // BuildExtensionsTab();
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
                FontStyle.Bold,
                GraphicsUnit.Point
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
        FlowLayoutPanel PackButtons(Button reset, Button exec)
        {
            // 余白を詰める設定
            reset.Margin = new Padding(0, 0, 6, 0);
            exec.Margin  = new Padding(0);

            return new FlowLayoutPanel {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Controls = { reset, exec }
            };
        }
        private void BuildAppearanceTab()
        {
            this.tabAppearance.SuspendLayout();

            var scrollLivePreview = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 12),
                Padding = new Padding(12)
            };
            this.tabAppearance.Controls.Add(scrollLivePreview);

            var stackAppearance = NewStack();
            stackAppearance.Dock = DockStyle.Top;
            stackAppearance.AutoSize = true;
            stackAppearance.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stackAppearance.SuspendLayout();
            this.tabAppearance.Controls.Add(stackAppearance);

            // Capture settings
            this.grpCaptureSettings = NewGroup("Capture Settings");
            this.grpCaptureSettings.Tag = "loc:Text.CaptureSetting";
            var tlpCap = NewGrid(2, 2);
            tlpCap.SuspendLayout();

            this.chkScreenGuide = new CheckBox { Text = "Show guide lines", Checked = true, AutoSize = true, Tag = "loc:Text.ShowGuide" };
            this.chkTrayNotify = new CheckBox { Text = "Notify in tray on capture", AutoSize = true, Tag = "loc:Text.NotifyTray" };
            this.chkTrayNotifyOCR = new CheckBox { Text = "Notify in tray on OCR capture", AutoSize = true, Tag = "loc:Text.NotifyTrayOCR" };

            var flowToggles = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
            };
            flowToggles.Controls.Add(this.chkScreenGuide);
            flowToggles.Controls.Add(this.chkTrayNotify);
            flowToggles.Controls.Add(this.chkTrayNotifyOCR);

            tlpCap.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Tag = "loc:Text.Option" }, 0, 0);
            tlpCap.Controls.Add(flowToggles, 1, 0);

            this.labelBgPreset = NewRightLabel("Background");
            this.labelBgPreset.Tag = "loc:Text.Background";
            this.cmbBgPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            this.cmbBgPreset.Items.AddRange(new object[] {
                "Transparent (0%)",
                "Dark (30%)",
                "Dark (60%)",
                "Light (30%)",
                "Light (60%)"
            });
            this.cmbBgPreset.SelectedIndex = 0;

            this.previewBg = new AlphaPreviewPanel { Height = 20, Width = 50, Anchor = AnchorStyles.Left, RgbColor = Color.Black, AlphaPercent = 0 };

            var flowPreset = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowPreset.Controls.Add(this.cmbBgPreset);
            flowPreset.Controls.Add(this.previewBg);

            tlpCap.Controls.Add(this.labelBgPreset, 0, 1);
            tlpCap.Controls.Add(flowPreset, 1, 1);
            tlpCap.ResumeLayout(false);

            this.grpCaptureSettings.Controls.Add(tlpCap);

            this.cmbBgPreset.SelectedIndexChanged += (s, e) =>
            {
                var S = Properties.Settings.Default;
                var sel = this.cmbBgPreset.SelectedItem?.ToString() ?? "";
                foreach (var p in _bgPresets)
                {
                    if (p.Name == sel)
                    {
                        S.CaptureBackgroundColor = p.Color;
                        S.CaptureBackgroundAlphaPercent = p.Alpha;
                        break;
                    }
                }
            };

            // Window settings
            this.grpWindowSettings = NewGroup("Window Settings");
            this.grpWindowSettings.Tag = "loc:Text.WindowSetting";
            this.grpWindowSettings.Margin = new Padding(0, 8, 0, 0);

            var tlpWin = NewGrid(2, 2);
            tlpWin.SuspendLayout();

            var flowWinToggles = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
            };
            this.chkWindowShadow = new CheckBox { Text = "Window shadow", Checked = true, AutoSize = true, Tag = "loc:Text.DropShadow" };
            this.chkAfloat = new CheckBox { Text = "Always on top", Checked = true, AutoSize = true, Tag = "loc:Text.AlwaysOnTop" };
            this.chkHighlightOnHover = new CheckBox { Text = "Highlight on hover", Checked = true, AutoSize = true, Tag = "loc:Text.HighlightOnHover" };
            this.chkShowOverlay = new CheckBox { Text = "Show overlay", Checked = true, AutoSize = true, Tag = "loc:Text.ShowOverlay" };
            flowWinToggles.Controls.Add(this.chkWindowShadow);
            flowWinToggles.Controls.Add(this.chkAfloat);
            flowWinToggles.Controls.Add(this.chkHighlightOnHover);
            flowWinToggles.Controls.Add(this.chkShowOverlay);

            tlpWin.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Tag = "loc:Text.Option" }, 0, 0);
            tlpWin.Controls.Add(flowWinToggles, 1, 0);

            this.labelHoverPreset = NewRightLabel("Highlight color");
            this.labelHoverPreset.Tag = "loc:Text.HighlightColor";
            this.cmbHoverPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            this.cmbHoverPreset.Items.AddRange(new object[] {
                "Red", "Cyan", "Green", "Yellow", "Magenta", "Blue", "Orange", "Black", "White"
            });
            this.cmbHoverPreset.SelectedItem = "Cyan";

            this.previewHover = new AlphaPreviewPanel { Height = 20, Width = 50, Anchor = AnchorStyles.Left, RgbColor = Color.Cyan, AlphaPercent = 60 };

            var flowHover = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowHover.Controls.Add(this.cmbHoverPreset);
            flowHover.Controls.Add(this.previewHover);

            tlpWin.Controls.Add(this.labelHoverPreset, 0, 1);
            tlpWin.Controls.Add(flowHover, 1, 1);

            this.labelHoverThickness = NewRightLabel("Highlight thickness (px)");
            this.labelHoverThickness.Tag = "loc:Text.HighlightThickness";
            this.numHoverThickness = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Anchor = AnchorStyles.Left,
                Dock = DockStyle.Fill,
            };
            this.numHoverThickness.AutoSize = true;
            this.numHoverThickness.MaximumSize = new Size(120, 0);
            tlpWin.Controls.Add(this.labelHoverThickness, 0, 2);
            tlpWin.Controls.Add(this.numHoverThickness, 1, 2);

            this.labelDefaultOpacity = NewRightLabel("Default opacity");
            this.labelDefaultOpacity.Tag = "loc:Text.WindowOpacity";
            this.trackbarDefaultOpacity = new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                TickFrequency = 10,
                Value = 100,
                Width = 240,
                Anchor = AnchorStyles.Left
            };
            this.trackbarDefaultOpacity.AutoSize = true;
            this.labelDefaultOpacityVal = new Label { AutoSize = true, Text = "100%", Anchor = AnchorStyles.Left };

            var flowOpacity = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowOpacity.Controls.Add(this.trackbarDefaultOpacity);
            flowOpacity.Controls.Add(this.labelDefaultOpacityVal);

            tlpWin.Controls.Add(this.labelDefaultOpacity, 0, 3);
            tlpWin.Controls.Add(flowOpacity, 1, 3);
            tlpWin.ResumeLayout(false);

            this.grpWindowSettings.Controls.Add(tlpWin);

            this.cmbHoverPreset.SelectedIndexChanged += (s, e) =>
            {
                Color c = Color.Cyan;
                switch ((this.cmbHoverPreset.SelectedItem ?? "").ToString())
                {
                    case "Red": c = Color.Red; break;
                    case "Cyan": c = Color.Cyan; break;
                    case "Green": c = Color.Lime; break;
                    case "Yellow": c = Color.Yellow; break;
                    case "Magenta": c = Color.Magenta; break;
                    case "Blue": c = Color.Blue; break;
                    case "Orange": c = Color.Orange; break;
                    case "Black": c = Color.Black; break;
                    case "White": c = Color.White; break;
                }
                this.previewHover.RgbColor = c;
                this.previewHover.AlphaPercent = 100;
                this.previewHover.Invalidate();
            };

            this.trackbarDefaultOpacity.Scroll += (s, e) =>
            {
                this.labelDefaultOpacityVal.Text = this.trackbarDefaultOpacity.Value + "%";
            };

            // Live Preview settings
            this.grpLivePreview = NewGroup("Live Preview Settings");
            this.grpLivePreview.Tag = "loc:Text.LivePreviewSetting";
            this.grpLivePreview.Margin = new Padding(0, 8, 0, 0);

            var tlpLive = NewGrid(2, 2);
            tlpLive.SuspendLayout();

            var flowLiveToggles = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
            };

            this.chkLiveShowStats = new CheckBox {
                Text = "Show stats (FPS/CPU/MEM)",
                AutoSize = true,
                Tag = "loc:Text.ShowStats",
                Checked = Properties.Settings.Default.LivePreviewShowStats
            };
            this.chkLiveShowStats.CheckedChanged += (s, e) =>
            {
                Properties.Settings.Default.LivePreviewShowStats = this.chkLiveShowStats.Checked;
                try { Properties.Settings.Default.Save(); } catch { }
            };
            flowLiveToggles.Controls.Add(this.chkLiveShowStats);

            tips = new ToolTip();

            lblSaveFolder = NewRightLabel("Live Preview Save Folder");
            lblSaveFolder.Tag = "loc:Setting.Display.LivePreviewSaveFolder";

            txtSaveFolder = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            btnBrowseSaveFolder = new Button { Tag = "loc:Button.Browse", AutoSize = true };
            btnClearSaveFolder  = new Button { Tag = "loc:Button.Clear",  AutoSize = true };

            var flowFolder = new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight
            };
            flowFolder.Controls.Add(txtSaveFolder);
            flowFolder.Controls.Add(btnBrowseSaveFolder);
            flowFolder.Controls.Add(btnClearSaveFolder);

            lblGifMax = NewRightLabel("GIF Max Duration (sec)");
            lblGifMax.Tag = "loc:Setting.Display.GifMaxDurationSec";

            numGifMax = new NumericUpDown {
                Minimum = 0,
                Maximum = 3600,
                Increment = 1,
                Anchor = AnchorStyles.Left,
                Dock = DockStyle.Fill,
            };
            this.numGifMax.AutoSize = true;
            this.numGifMax.MaximumSize = new Size(120, 0);
            txtSaveFolder.DataBindings.Add(
                new Binding("Text",
                    Properties.Settings.Default,
                    nameof(Properties.Settings.Default.LivePreviewSaveFolder),
                    true, DataSourceUpdateMode.OnPropertyChanged));

            btnBrowseSaveFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog {
                    Description = SR.T("Dialog.SaveFolder.Description",
                        "Choose a folder for Live Preview recordings"),
                    ShowNewFolderButton = true
                })
                {
                    var cur1 = Properties.Settings.Default.LivePreviewSaveFolder;
                    if (!string.IsNullOrWhiteSpace(cur1) && Directory.Exists(cur1))
                        fbd.SelectedPath = cur1;

                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        Properties.Settings.Default.LivePreviewSaveFolder = fbd.SelectedPath;
                        try { Properties.Settings.Default.Save(); } catch { }
                    }
                }
            };

            btnClearSaveFolder.Click += (s, e) =>
            {
                Properties.Settings.Default.LivePreviewSaveFolder = string.Empty;
                try { Properties.Settings.Default.Save(); } catch { }
            };

            var bMax = new Binding(
                "Value",
                Properties.Settings.Default,
                nameof(Properties.Settings.Default.GifMaxDurationSec),
                formattingEnabled: true,
                DataSourceUpdateMode.OnPropertyChanged);
            bMax.Format += (s, e) =>
            {
                e.Value = Convert.ToDecimal(CoerceInt(e.Value, 0, 3600, 0), CultureInfo.InvariantCulture);
            };
            bMax.Parse += (s, e) =>
            {
                if (e.Value is decimal dec)
                    e.Value = (int)dec;
            };

            var bFps = new Binding(
                "Value",
                Properties.Settings.Default,
                nameof(Properties.Settings.Default.GifMaxFps),
                true,
                DataSourceUpdateMode.OnPropertyChanged);
            bFps.Format += (s, e) =>
            {
                e.Value = Convert.ToDecimal(CoerceInt(e.Value, 1, 30, 10), CultureInfo.InvariantCulture);
            };
            bFps.Parse += (s, e) =>
            {
                if (e.Value is decimal dec)
                    e.Value = (int)dec;
            };

            tips.SetToolTip(numGifMax, SR.T("Tip.GifMax", "0 = Unlimited; otherwise acts as a cap"));
            var cur = Properties.Settings.Default.LivePreviewSaveFolder;
            string unset = SR.T("Tip.SaveFolder.Unset", "Unspecified (uses the default location)");
            tips.SetToolTip(txtSaveFolder, string.IsNullOrWhiteSpace(cur) ? unset : cur);

            lblGifFps = NewRightLabel("GIF max FPS");
            lblGifFps.Tag = "loc:Setting.Display.GifMaxFps";
            numGifFps = new NumericUpDown {
                Minimum = 1,
                Maximum = 30,
                Increment = 1,
                Anchor = AnchorStyles.Left,
                Dock = DockStyle.Fill,
            };
            this.numGifFps.AutoSize = true;
            this.numGifFps.MaximumSize = new Size(120, 0);

            lblGifOptimize = NewRightLabel("Optimize for size");
            lblGifOptimize.Tag = "loc:Setting.Display.GifOptimize";
            chkGifOptimize = new CheckBox
            {
                Text = "Downscale / palette reduction",
                AutoSize = true,
                Tag = "loc:Setting.Display.GifOptimizeTip"
            };

            lblGifWidth = NewRightLabel("GIF max Width");
            lblGifWidth.Tag = "loc:Setting.Display.GifMaxWidth";
            numGifWidth = new NumericUpDown {
                Minimum = 1,
                Maximum = 1920,
                Increment = 1,
                Anchor = AnchorStyles.Left,
                Dock = DockStyle.Fill,
            };
            this.numGifWidth.AutoSize = true;
            this.numGifWidth.MaximumSize = new Size(120, 0);

            chkGifOptimize.DataBindings.Add(new Binding(
                "Checked", Properties.Settings.Default, nameof(Properties.Settings.Default.GifOptimize),
                true, DataSourceUpdateMode.OnPropertyChanged));

            int row = tlpLive.RowCount;
            tlpLive.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLive.Controls.Add(lblSaveFolder, 0, row);
            tlpLive.Controls.Add(flowFolder,   1, row);
            tlpLive.RowCount++;

            row = tlpLive.RowCount;
            tlpLive.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLive.Controls.Add(lblGifFps, 0, row);
            tlpLive.Controls.Add(numGifFps, 1, row);
            tlpLive.RowCount++;

            row = tlpLive.RowCount;
            tlpLive.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLive.Controls.Add(lblGifOptimize, 0, row);
            tlpLive.Controls.Add(chkGifOptimize, 1, row);
            tlpLive.RowCount++;

            row = tlpLive.RowCount;
            tlpLive.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLive.Controls.Add(lblGifWidth, 0, row);
            tlpLive.Controls.Add(numGifWidth, 1, row);
            tlpLive.RowCount++;

            row = tlpLive.RowCount;
            tlpLive.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLive.Controls.Add(lblGifMax, 0, row);
            tlpLive.Controls.Add(numGifMax, 1, row);
            tlpLive.RowCount++;

            tlpLive.Controls.Add(new Label {
                Text = "Options",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Right,
                Tag = "loc:Text.Option"
            }, 0, 0);
            tlpLive.Controls.Add(flowLiveToggles, 1, 0);
            tlpLive.ResumeLayout(false);

            this.grpLivePreview.Controls.Add(tlpLive);

            stackAppearance.Controls.Add(this.grpCaptureSettings, 0, 0);
            stackAppearance.Controls.Add(this.grpWindowSettings, 0, 1);
            stackAppearance.Controls.Add(this.grpLivePreview, 0, 2);
            stackAppearance.ResumeLayout(false);

            this.tabAppearance.ResumeLayout(false);

            // ラジーロード後に配線・設定反映
            var appS = Properties.Settings.Default;
            BindIntNumeric(numHoverThickness, appS, nameof(appS.HoverHighlightThickness), 1, 10, 2);
            BindIntNumeric(numGifMax,         appS, nameof(appS.GifMaxDurationSec),       0, 3600, 0);
            BindIntNumeric(numGifFps,         appS, nameof(appS.GifMaxFps),               1, 30, 10);
            BindIntNumeric(numGifWidth,       appS, nameof(appS.GifMaxWidth),             1, 1920, 960);
            PopulatePresetCombos();
            WireUpAppearanceBindings();
            SelectPresetComboFromSettings();
            HookRuntimeEvents();
            // フォーム全体のレイアウトを一括停止してから Localizer を適用（各 Text 変更ごとのレイアウトパスを防ぐ）
            this.SuspendLayout();
            Localizer.Apply(this);
            this.ResumeLayout(false);
        }

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

        // 子コントロールも含めて再帰的にレイアウトを停止する
        // （AutoSize ネスト構造での OnVisibleChanged 連鎖レイアウトを防ぐ）
        private static void SuspendLayoutDeep(Control c)
        {
            c.SuspendLayout();
            foreach (Control child in c.Controls)
                SuspendLayoutDeep(child);
        }

        // 子から順に Resume し、最後に親で ResumeLayout(true) して一括レイアウト実行
        private static void ResumeLayoutDeep(Control c)
        {
            foreach (Control child in c.Controls)
                ResumeLayoutDeep(child);
            c.ResumeLayout(false);
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

            // --- Appearance controls（ラジーロード済みの場合のみ配線）---
            WireUpAppearanceBindings();

            // --- 履歴数 ---
            var b = new Binding("Value", S, nameof(S.HistoryLimit),
                true, DataSourceUpdateMode.OnPropertyChanged);
            b.Format += (s, e) =>
            {
                if (e.DesiredType == typeof(decimal) && e.Value is int i)
                    e.Value = (decimal)i;
            };
            b.Parse += (s, e) =>
            {
                if (e.Value is decimal d)
                    e.Value = Decimal.ToInt32(d);
            };
            Log.Debug(string.Format("Binding history limit {0}", S.HistoryLimit), "Settings");

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

        private void WireUpAppearanceBindings()
        {
            if (this.trackbarDefaultOpacity == null) return;

            var S = Properties.Settings.Default;
            this.trackbarDefaultOpacity.DataBindings.Clear();
            this.labelDefaultOpacityVal.DataBindings.Clear();

            this.previewBg.DataBindings.Add(
                new Binding("RgbColor", S, nameof(S.CaptureBackgroundColor),
                    true, DataSourceUpdateMode.OnPropertyChanged));
            this.previewBg.DataBindings.Add(
                new Binding("AlphaPercent", S, nameof(S.CaptureBackgroundAlphaPercent),
                    true, DataSourceUpdateMode.OnPropertyChanged));

            this.previewHover.DataBindings.Add(
                new Binding("RgbColor", S, nameof(S.HoverHighlightColor),
                    true, DataSourceUpdateMode.OnPropertyChanged));
            this.previewHover.DataBindings.Add(
                new Binding("AlphaPercent", S, nameof(S.HoverHighlightAlphaPercent),
                    true, DataSourceUpdateMode.OnPropertyChanged));

            this.chkWindowShadow.DataBindings.Add(
                new Binding("Checked", S, nameof(S.WindowShadowEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkAfloat.DataBindings.Add(
                new Binding("Checked", S, nameof(S.AlwaysOnTop), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkHighlightOnHover.DataBindings.Add(
                new Binding("Checked", S, nameof(S.HoverHighlightEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkShowOverlay.DataBindings.Add(
                new Binding("Checked", S, nameof(S.OverlayEnabled), true, DataSourceUpdateMode.OnPropertyChanged));
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
                    true, DataSourceUpdateMode.OnPropertyChanged));

            var lblOpacityBinding = new Binding(
                "Text", S, nameof(S.WindowOpacityPercent),
                true, DataSourceUpdateMode.Never);
            lblOpacityBinding.Format += (o, e) =>
            {
                int v;
                if (e.Value is int iv) v = iv;
                else int.TryParse(e.Value?.ToString(), out v);
                e.Value = v + "%";
            };
            this.labelDefaultOpacityVal.DataBindings.Add(lblOpacityBinding);
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
        /// <summary>インストール済み OCR 言語を列挙してコンボに詰める（先頭に "Auto" を入れる）</summary>
        private void PopulateOcrLanguageCombo()
        {
            this.cmbOCRLanguage.Items.Clear();
            // 先頭は自動選択
            this.cmbOCRLanguage.Items.Add(new ComboItem("Auto (installed best)", "auto"));

            var tags = WindowsOcrHelper.GetAvailableLanguageTags();
            foreach (var tag in tags)
            {
                this.cmbOCRLanguage.Items.Add(new ComboItem(WindowsOcrHelper.ToDisplay(tag), tag));
            }

            if (this.cmbOCRLanguage.Items.Count == 0)
            {
                // OCR自体が使えないケース（Windows OCR API 未サポート等）
                this.cmbOCRLanguage.Items.Add(new ComboItem("(no OCR languages found)", "auto"));
            }
            this.cmbOCRLanguage.SelectedIndex = 0;
        }

        /// <summary>保存済みの OCRLanguage を UI に反映（無ければ "auto"）。</summary>
        private void RestoreOcrLanguageSelection()
        {
            var saved = Properties.Settings.Default.OcrLanguage;
            if (string.IsNullOrEmpty(saved)) saved = "auto";

            for (int i = 0; i < this.cmbOCRLanguage.Items.Count; i++)
            {
                var item = this.cmbOCRLanguage.Items[i] as ComboItem;
                if (item != null && string.Equals(item.Value, saved, StringComparison.OrdinalIgnoreCase))
                {
                    this.cmbOCRLanguage.SelectedIndex = i;
                    return;
                }
            }
            // マッチなし → auto
            this.cmbOCRLanguage.SelectedIndex = 0;
        }

        /// <summary>UI の選択を Settings に保存</summary>
        private void SaveOcrLanguageSelection()
        {
            var item = this.cmbOCRLanguage.SelectedItem as ComboItem;
            var val = (item != null) ? item.Value : "auto";
            if (string.IsNullOrEmpty(val)) val = "auto";

            if (!string.Equals(Properties.Settings.Default.OcrLanguage, val, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.OcrLanguage = val;
                Properties.Settings.Default.Save();
                // ログ出力など
                // AppLog.Info($"[OCR] Preferred language set: {val}");
            }
        }

        // フォーマット例外/NRE対策のユーティリティ（以前の提案と同じ）
        private int CoerceInt(object raw, int min, int max, int fallback)
        {
            try
            {
                if (raw == null) return fallback;
                if (raw is int i) return Math.Min(max, Math.Max(min, i));
                if (raw is decimal d) return Math.Min(max, Math.Max(min, (int)d));
                if (raw is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return Math.Min(max, Math.Max(min, v));
                return Math.Min(max, Math.Max(min, Convert.ToInt32(raw, CultureInfo.InvariantCulture)));
            }
            catch { return fallback; }
        }
        private void SanitizeNumericSettings()
        {
            var S = Properties.Settings.Default;
            S.HistoryLimit            = CoerceInt(S["HistoryLimit"],            0, 100, 20);
            S.HoverHighlightThickness = CoerceInt(S["HoverHighlightThickness"], 1, 10,  2);
            S.GifMaxDurationSec       = CoerceInt(S["GifMaxDurationSec"],       0, 3600, 0);
            S.GifMaxFps               = CoerceInt(S["GifMaxFps"],               1, 30,  10);
            S.GifMaxWidth             = CoerceInt(S["GifMaxWidth"],             1, 1920, 960);
            try { S.Save(); } catch { }
        }
        private bool CoerceBool(object raw, bool fallback)
        {
            try
            {
                if (raw == null) return fallback;
                if (raw is bool b) return b;
                if (raw is string s && bool.TryParse(s, out var bb)) return bb;
                return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch { return fallback; }
        }
        private void BindIntNumeric(NumericUpDown ctrl, object settings, string propName,
                            int min, int max, int fallback,
                            DataSourceUpdateMode mode = DataSourceUpdateMode.OnPropertyChanged)
        {
            if (ctrl == null) return;
            ctrl.Minimum = min; ctrl.Maximum = max;

            var b = new Binding("Value", settings, propName, true, mode);
            b.Format += (s, e) =>
            {
                e.Value = Convert.ToDecimal(CoerceInt(e.Value, min, max, fallback), CultureInfo.InvariantCulture);
            };
            b.Parse += (s, e) =>
            {
                if (e.Value is decimal d) e.Value = Decimal.ToInt32(d);
                else                      e.Value = CoerceInt(e.Value, min, max, fallback);
            };

            ctrl.DataBindings.Clear(); // 多重バインド防止
            ctrl.DataBindings.Add(b);
        }

        private sealed class ComboItem
        {
            public string Text;
            public string Value;
            public ComboItem(string text, string value) { Text = text; Value = value; }
            public override string ToString() { return Text; }
        }

    }
}

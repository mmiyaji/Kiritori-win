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
            if (disposing && (components != null)) components.Dispose();
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
        private Label labelStartup;
        private CheckBox chkRunAtStartup;
        private Button btnOpenStartupSettings;
        private Label labelStartupInfo;
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

        // ========= Shortcuts ==========
        private TabPage tabShortcuts;

        private GroupBox grpShortcutsWindowOps;
        private TableLayoutPanel tlpShortcutsWin;
        private Label labelClose; private TextBox textBoxClose;
        private Label labelMinimize; private TextBox textBoxMinimize;
        private Label labelAfloat; private TextBox textBoxAfloat;
        private Label labelDropShadow; private TextBox textBoxDropShadow;
        private Label labelHoverHighlight; private TextBox textBoxHoverHighlight;
        private Label labelMove; private TextBox textBoxMove;

        private GroupBox grpShortcutsCaptureOps;
        private TableLayoutPanel tlpShortcutsCap;
        private Label labelOCR; private TextBox textBoxOCR;
        private Label labelCopy; private TextBox textBoxCopy;
        private Label labelSave; private TextBox textBoxSave;
        private Label labelPrint; private TextBox textBoxPrint;
        private Label labelZoomIn; private TextBox textBoxZoomIn;
        private Label labelZoomOut; private TextBox textBoxZoomOut;
        private Label labelZoomOff; private TextBox textBoxZoomOff;
        private Label labelScreenGuide; private TextBox textBoxScreenGuide;
        private Label labelScreenSquare; private TextBox textBoxScreenSquare;
        private Label labelScreenSnap; private TextBox textBoxScreenSnap;

        // ========= Advanced ==========
        private TabPage tabAdvanced;

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
        private FlowLayoutPanel rightButtons;
        private Button btnCancelSettings;
        private Button btnSaveSettings;
        private Button btnExitAppLeft;

        // ========= Initialize =========
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.toolTip1 = new ToolTip(this.components);

            // ---- Form basics ----
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
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
            this.tabGeneral = new TabPage("General") { AutoScroll = false, Tag = "loc:Tab.General" };
            this.tabAppearance = new TabPage("Appearance") { AutoScroll = false, Tag = "loc:Tab.Appearance" };
            this.tabShortcuts = new TabPage("Shortcuts") { AutoScroll = false, Tag = "loc:Tab.Shortcut" };
            this.tabAdvanced = new TabPage("Advanced") { AutoScroll = false, Tag = "loc:Tab.Advanced" };

            this.tabControl.TabPages.AddRange(new TabPage[]
            {
                this.tabInfo,
                this.tabGeneral,
                this.tabAppearance,
                this.tabShortcuts,
                // this.tabAdvanced,
            });

            // =========================================================
            // ② Bottom bar（Exit 左 / Cancel & Save 右）
            // =========================================================
            this.bottomBar = new TableLayoutPanel
            {
                Height = 40,
                Padding = new Padding(8, 6, 8, 6),
                ColumnCount = 3,
                Dock = DockStyle.Fill,
                AutoSize = false
            };
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.bottomBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(SystemPens.ControlLight, 0, 0, this.bottomBar.Width, 0);
            };

            this.btnExitAppLeft = new Button
            {
                Text = "Exit App",
                AutoSize = true,
                Margin = new Padding(0, 6, 6, 6),
                Tag = "loc:Text.BtnExit"
            };
            this.btnExitAppLeft.Click += new EventHandler(this.btnExitApp_Click);

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
                Margin = new Padding(6),
                Tag = "loc:Text.BtnCancel"
            };
            this.btnCancelSettings.Click += new EventHandler(this.btnCancelSettings_Click);

            this.btnSaveSettings = new Button
            {
                Text = "OK",
                AutoSize = true,
                Margin = new Padding(6, 6, 0, 6),
                Tag = "loc:Text.BtnSave"
            };
            this.btnSaveSettings.Click += new EventHandler(this.btnSaveSettings_Click);

            this.rightButtons.Controls.Add(this.btnCancelSettings);
            this.rightButtons.Controls.Add(this.btnSaveSettings);

            var spacer = new Panel { Dock = DockStyle.Fill };

            this.bottomBar.Controls.Add(this.btnExitAppLeft, 0, 0);
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
                Padding = new Padding(12, 0, 12, 5)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // タブ
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // ボタンバー

            shell.Controls.Add(this.tabControl, 0, 0);
            shell.Controls.Add(this.bottomBar, 0, 1);

            // =========================================================
            // General タブ（縦積みレイアウト）
            // =========================================================
            var stackGeneral = NewStack();
            this.tabGeneral.Controls.Add(stackGeneral);

            // Application Settings
            this.grpAppSettings = NewGroup("Application Settings");
            this.grpAppSettings.Tag = "loc:Text.AppSetting";
            var tlpApp = NewGrid(3, 2);

            this.labelLanguage = NewRightLabel("Language");
            this.labelLanguage.Tag = "loc:Text.Language";
            this.cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            this.cmbLanguage.Items.AddRange(new object[] { "English (en)", "日本語 (ja)" });
            this.cmbLanguage.SelectedIndex = 0;

            this.labelStartup = NewRightLabel("Startup");
            this.labelStartup.Tag = "loc:Text.Startup";
            var flowStartup = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            this.chkRunAtStartup = new CheckBox { Text = "Run at startup", AutoSize = true, Enabled = false, Tag = "loc:Text.Runatstartup" };
            this.btnOpenStartupSettings = new Button { Text = "Open Startup settings", AutoSize = true, Tag = "loc:Text.BtnStartupFolder" };
            this.btnOpenStartupSettings.Click += new EventHandler(this.btnOpenStartupSettings_Click);
            this.labelStartupInfo = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Startup is managed by Windows.", Dock = DockStyle.Fill };
            this.toolTip1.SetToolTip(this.labelStartupInfo, "Settings > Apps > Startup");
            flowStartup.Controls.Add(this.chkRunAtStartup);
            flowStartup.Controls.Add(this.btnOpenStartupSettings);

            this.labelHistory = NewRightLabel("History limit");
            this.labelHistory.Tag = "loc:Text.HistoryLimit";
            this.textBoxHistory = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 20, Width = 80, Anchor = AnchorStyles.Left };

            tlpApp.Controls.Add(this.labelLanguage, 0, 0);
            tlpApp.Controls.Add(this.cmbLanguage, 1, 0);
            tlpApp.Controls.Add(this.labelStartup, 0, 1);

            var flowStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            flowStack.Controls.Add(flowStartup);
            flowStack.Controls.Add(this.labelStartupInfo);

            tlpApp.Controls.Add(flowStack, 1, 1);
            tlpApp.Controls.Add(this.labelHistory, 0, 2);
            tlpApp.Controls.Add(this.textBoxHistory, 1, 2);

            this.grpAppSettings.Controls.Add(tlpApp);

            // Hotkeys（上マージンで間隔）
            this.grpHotkey = NewGroup("Hotkeys");
            this.grpHotkey.Tag = "loc:Text.Hotkeys";
            this.grpHotkey.Margin = new Padding(0, 8, 0, 0);

            var tlpHot = NewGrid(3, 4);
            this.labelHotkeyCapture = NewRightLabel("Image capture");
            this.labelHotkeyCapture.Tag = "loc:Text.ImageCapture";
            this.textBoxKiritori = new TextBox { Enabled = false, Width = 160, Text = "Ctrl + Shift + 5" };
            this.labelHotkeyCaptureOCR = NewRightLabel("OCR capture");
            this.labelHotkeyCaptureOCR.Tag = "loc:Text.OCRCapture";

            this.labelHotkeyLivePreview = NewRightLabel("Live preview");
            this.labelHotkeyLivePreview.Tag = "loc:Text.LivePreview";

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

            var btnResetCap = new Button { Text = "Reset", AutoSize = true };
            btnResetCap.Tag = "loc:Text.ResetDefault";
            btnResetCap.Click += (s, e) => ResetCaptureHotkeyToDefault();

            var btnResetOcr = new Button { Text = "Reset", AutoSize = true };
            btnResetOcr.Tag = "loc:Text.ResetDefault";
            btnResetOcr.Click += (s, e) => ResetOcrHotkeyToDefault();

            var btnResetLive = new Button { Text = "Reset", AutoSize = true };
            btnResetLive.Tag = "loc:Text.ResetDefault";
            btnResetLive.Click += (s, e) => ResetLiveHotkeyToDefault();

            this.labelHotkeyCapturePrev = NewRightLabel("Capture at previous region");
            this.labelHotkeyCapturePrev.Tag = "loc:Text.PreviousCapture";
            this.textBoxCapturePrev = new TextBox { Enabled = false, Width = 160, Text = "(disabled)" };

            tlpHot.Controls.Add(this.labelHotkeyCapture, 0, 0);
            tlpHot.Controls.Add(this.textBoxKiritori, 1, 0);
            tlpHot.Controls.Add(btnResetCap, 2, 0);
            tlpHot.Controls.Add(this.labelHotkeyCaptureOCR, 0, 1);
            tlpHot.Controls.Add(this.textBoxHotkeyCaptureOCR, 1, 1);
            tlpHot.Controls.Add(btnResetOcr, 2, 1);
            tlpHot.Controls.Add(this.labelHotkeyLivePreview, 0, 2);
            tlpHot.Controls.Add(this.textBoxHotkeyLivePreview, 1, 2);
            tlpHot.Controls.Add(btnResetLive, 2, 2);
            tlpHot.Controls.Add(this.labelHotkeyCapturePrev, 0, 3);
            tlpHot.Controls.Add(this.textBoxCapturePrev, 1, 3);

            this.grpHotkey.Controls.Add(tlpHot);

            // stack へ追加
            stackGeneral.Controls.Add(this.grpAppSettings, 0, 0);
            stackGeneral.Controls.Add(this.grpHotkey, 0, 1);

            // =========================================================
            // Appearance タブ（1画面に収まるシンプル構成）
            // =========================================================
            var stackAppearance = NewStack();
            this.tabAppearance.Controls.Add(stackAppearance);

            // Capture settings（プリセット＋右プレビュー）
            this.grpCaptureSettings = NewGroup("Capture Settings");
            this.grpCaptureSettings.Tag = "loc:Text.CaptureSetting";
            var tlpCap = NewGrid(2, 2);

            this.chkScreenGuide = new CheckBox { Text = "Show guide lines", Checked = true, AutoSize = true, Tag = "loc:Text.ShowGuide" };
            this.chkTrayNotify = new CheckBox { Text = "Notify in tray on capture", AutoSize = true, Tag = "loc:Text.NotifyTray" };
            this.chkTrayNotifyOCR = new CheckBox { Text = "Notify in tray on OCR capture", AutoSize = true, Tag = "loc:Text.NotifyTrayOCR" };
            // this.chkPlaySound = new CheckBox { Text = "Play sound on capture", AutoSize = true, Enabled = false, Tag = "loc:Text.PlaySound" };

            var flowToggles = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
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

            this.grpCaptureSettings.Controls.Add(tlpCap);

            // 背景プリセットのイベント
            // Background preset -> Settings & Preview
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
                        // バインド済みなのでプレビュー側は自動追従
                        break;
                    }
                }
            };


            // Window settings（プリセット＋太さ＋不透明度）
            this.grpWindowSettings = NewGroup("Window Settings");
            this.grpWindowSettings.Tag = "loc:Text.WindowSetting";
            this.grpWindowSettings.Margin = new Padding(0, 8, 0, 0);

            var tlpWin = NewGrid(4, 2);

            var flowWinToggles = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
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

            // 色プリセット＋右プレビュー（透過 100% 固定）
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

            // 太さ（残す）
            this.labelHoverThickness = NewRightLabel("Highlight thickness (px)");
            this.labelHoverThickness.Tag = "loc:Text.HighlightThickness";
            this.numHoverThickness = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 2, Width = 60, Anchor = AnchorStyles.Left };
            tlpWin.Controls.Add(this.labelHoverThickness, 0, 2);
            tlpWin.Controls.Add(this.numHoverThickness, 1, 2);

            // Default Window Opacity（ここに統合）
            this.labelDefaultOpacity = NewRightLabel("Default opacity");
            this.labelDefaultOpacity.Tag = "loc:Text.WindowOpacity";
            this.trackbarDefaultOpacity = new TrackBar { Minimum = 10, Maximum = 100, TickFrequency = 10, Value = 100, Width = 240, Anchor = AnchorStyles.Left };
            this.labelDefaultOpacityVal = new Label { AutoSize = true, Text = "100%", Anchor = AnchorStyles.Left };

            var flowOpacity = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowOpacity.Controls.Add(this.trackbarDefaultOpacity);
            flowOpacity.Controls.Add(this.labelDefaultOpacityVal);

            tlpWin.Controls.Add(this.labelDefaultOpacity, 0, 3);
            tlpWin.Controls.Add(flowOpacity, 1, 3);

            this.grpWindowSettings.Controls.Add(tlpWin);

            // イベント：色プリセット変更
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
                this.previewHover.AlphaPercent = 100; // 透過なし
                this.previewHover.Invalidate();
            };

            // イベント：不透明度表示
            this.trackbarDefaultOpacity.Scroll += (s, e) =>
            {
                this.labelDefaultOpacityVal.Text = this.trackbarDefaultOpacity.Value + "%";
            };

            // stack へ追加（2グループのみ）
            stackAppearance.Controls.Add(this.grpCaptureSettings, 0, 0);
            stackAppearance.Controls.Add(this.grpWindowSettings, 0, 1);

            // =========================================================
            // Shortcuts タブ（縦積み）
            // =========================================================
            var stackShort = NewStack();
            this.tabShortcuts.Controls.Add(stackShort);

            this.grpShortcutsWindowOps = NewGroup("Window operations");
            this.grpShortcutsWindowOps.Tag = "loc:Text.WindowOperation";

            // 4行 × 5列 (左2列 + 仕切り + 右2列)
            this.tlpShortcutsWin = new TableLayoutPanel();
            this.tlpShortcutsWin.ColumnCount = 5;
            this.tlpShortcutsWin.RowCount = 7;
            this.tlpShortcutsWin.Dock = DockStyle.Fill;
            this.tlpShortcutsWin.AutoSize = true;
            this.tlpShortcutsWin.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // 列スタイル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 左ラベル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f)); // 左テキスト
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12f));
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 右ラベル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f)); // 右テキスト


            // セパレータ本体
            var sepPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Width = 1
            };
            sepPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(sepPanel.BackColor);
                using (var p = new Pen(Color.LightGray))
                {
                    int x = sepPanel.Width / 2;  // 真ん中に線を描画
                    e.Graphics.DrawLine(p, x, 0, x, sepPanel.Height);
                }
            };

            // 追加して全行に跨らせる
            this.tlpShortcutsWin.Controls.Add(sepPanel, 2, 0);
            this.tlpShortcutsWin.SetRowSpan(sepPanel, this.tlpShortcutsWin.RowCount);
            // 左段
            AddShortcutRow(this.tlpShortcutsWin, 0, "Close", out this.labelClose, out this.textBoxClose, "Ctrl + w, ESC", colOffset: 0, tagKey: "Text.Close");
            AddShortcutRow(this.tlpShortcutsWin, 1, "Minimize", out this.labelMinimize, out this.textBoxMinimize, "Ctrl + h", colOffset: 0, tagKey: "Text.Minimize");
            AddShortcutRow(this.tlpShortcutsWin, 2, "Always on top", out this.labelAfloat, out this.textBoxAfloat, "Ctrl + a", colOffset: 0, tagKey: "Text.AlwaysOnTop");
            AddShortcutRow(this.tlpShortcutsWin, 3, "Drop shadow", out this.labelDropShadow, out this.textBoxDropShadow, "Ctrl + d", colOffset: 0, tagKey: "Text.DropShadow");
            AddShortcutRow(this.tlpShortcutsWin, 4, "Hover highlight", out this.labelHoverHighlight, out this.textBoxHoverHighlight, "Ctrl + f", colOffset: 0, tagKey: "Text.HighlightOnHover");
            AddShortcutRow(this.tlpShortcutsWin, 5, "Move", out this.labelMove, out this.textBoxMove, "up/down/left/right", colOffset: 0, tagKey: "Text.Move");

            // 右段 (colOffset = 3 → ラベルが列3, TextBoxが列4)
            AddShortcutRow(this.tlpShortcutsWin, 0, "Run OCR", out this.labelOCR, out this.textBoxOCR, "Ctrl + t", colOffset: 3, tagKey: "Text.RunOCR");
            AddShortcutRow(this.tlpShortcutsWin, 1, "Copy", out this.labelCopy, out this.textBoxCopy, "Ctrl + c", colOffset: 3, tagKey: "Text.Copy");
            AddShortcutRow(this.tlpShortcutsWin, 2, "Save", out this.labelSave, out this.textBoxSave, "Ctrl + s", colOffset: 3, tagKey: "Text.Save");
            AddShortcutRow(this.tlpShortcutsWin, 3, "Print", out this.labelPrint, out this.textBoxPrint, "Ctrl + p", colOffset: 3, tagKey: "Text.Print");
            AddShortcutRow(this.tlpShortcutsWin, 4, "Zoom in", out this.labelZoomIn, out this.textBoxZoomIn, "Ctrl + +", colOffset: 3, tagKey: "Text.ZoomIn");
            AddShortcutRow(this.tlpShortcutsWin, 5, "Zoom out", out this.labelZoomOut, out this.textBoxZoomOut, "Ctrl + -", colOffset: 3, tagKey: "Text.ZoomOut");
            AddShortcutRow(this.tlpShortcutsWin, 6, "Zoom reset", out this.labelZoomOff, out this.textBoxZoomOff, "Ctrl + 0", colOffset: 3, tagKey: "Text.ZoomReset");

            this.grpShortcutsWindowOps.Controls.Add(this.tlpShortcutsWin);

            this.grpShortcutsCaptureOps = NewGroup("Capture operations");
            this.grpShortcutsCaptureOps.Tag = "loc:Text.CaptureOptionSetting";
            this.grpShortcutsCaptureOps.Margin = new Padding(0, 8, 0, 0);

            // 5行×4列 (左2列 + 右2列)
            this.tlpShortcutsCap = NewGrid(2, 3);

            // 左段
            AddShortcutRow(this.tlpShortcutsCap, 0, "Toggle guide lines", out this.labelScreenGuide, out this.textBoxScreenGuide, "Alt", colOffset: 0, tagKey: "Text.ToggleGuide" );
            AddShortcutRow(this.tlpShortcutsCap, 1, "Square crop", out this.labelScreenSquare, out this.textBoxScreenSquare, "Shift", colOffset: 0, tagKey: "Text.SquareCrop");
            AddShortcutRow(this.tlpShortcutsCap, 2, "Snap", out this.labelScreenSnap, out this.textBoxScreenSnap, "Ctrl", colOffset: 0, tagKey: "Text.Snap");

            this.grpShortcutsCaptureOps.Controls.Add(this.tlpShortcutsCap);

            stackShort.Controls.Add(this.grpShortcutsWindowOps, 0, 0);
            stackShort.Controls.Add(this.grpShortcutsCaptureOps, 0, 1);

            // =========================================================
            // Advanced タブ（プレースホルダ）
            // =========================================================
            var lblAdv = new Label { Text = "Advanced settings will appear here.", AutoSize = true, Padding = new Padding(12) };
            this.tabAdvanced.Controls.Add(lblAdv);

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
                Size = new Size(128, 128),
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
                Margin = new Padding(0, 15, 0, 10),
                Tag = "loc:App.Name"
            };
            this.labelAppName.Font = new Font(
                this.Font.FontFamily,
                this.Font.Size + 7,
                FontStyle.Bold
            );

            // バージョン（ApplyDynamicTexts で差し替え）
            this.labelVersion = new Label
            {
                Text = "Version - built at (on load)",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
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
                Margin = new Padding(0, 0, 0, 10)
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
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + W, ESC",    "Close window", tagKey: "Text.CloseWindow");
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + C",         "Copy to clipboard", tagKey: "Text.CopyToClipboard");
            // AddShortcutInfo(tlpShortcutsInfo, "Ctrl + S",         "Save image", tagKey: "Text.SaveImage");
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + T",         "Run OCR (copy result / show toast)", tagKey: "Text.RunOCRDesc");
            AddShortcutInfo(tlpShortcutsInfo, "", "");
            AddShortcutInfo(tlpShortcutsInfo, "", "...and more. See Shortcuts", tagKey: "Text.AndMore");
            this.grpShortcuts.Controls.Clear();
            this.grpShortcuts.Controls.Add(tlpShortcutsInfo);
            RebuildShortcutsInfo();

            // ==== 起動時カード(GroupBox) ====
            this.grpOnAppLaunch = new GroupBox
            {
                Text = "On app launch",
                Tag  = "loc:Text.OnAppLaunch",
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
                Tag  = "loc:Text.OpenMenuOnAppStart"
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

            // 追加
            tlpOnAppLaunch.Controls.Add(this.chkOpenMenuOnAppStart);
            tlpOnAppLaunch.Controls.Add(lblDesc);
            this.grpOnAppLaunch.Controls.Add(tlpOnAppLaunch);

            // ルートに配置
            this.tlpInfoRoot.Controls.Add(this.tlpInfoHeader,  0, 0); // 上段：名刺ヘッダ
            this.tlpInfoRoot.Controls.Add(this.grpShortcuts,   0, 1); // 中段：ショートカット
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
                new Binding("Checked", S, nameof(S.isWindowShadow), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkAfloat.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isAfloatWindow), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkHighlightOnHover.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isHighlightWindowOnHover), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkShowOverlay.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isOverlay), true, DataSourceUpdateMode.OnPropertyChanged));
            this.numHoverThickness.DataBindings.Add(
                new Binding("Value", S, nameof(S.HoverHighlightThickness), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkScreenGuide.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isScreenGuide), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkTrayNotify.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isShowNotify), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkTrayNotifyOCR.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isShowNotifyOCR), true, DataSourceUpdateMode.OnPropertyChanged));

            if (S.WindowAlphaPercent < this.trackbarDefaultOpacity.Minimum)
                S.WindowAlphaPercent = this.trackbarDefaultOpacity.Minimum;
            if (S.WindowAlphaPercent > this.trackbarDefaultOpacity.Maximum)
                S.WindowAlphaPercent = this.trackbarDefaultOpacity.Maximum;

            this.trackbarDefaultOpacity.DataBindings.Add(
                new Binding("Value", S, nameof(S.WindowAlphaPercent),
                    /* formattingEnabled */ true,
                    DataSourceUpdateMode.OnPropertyChanged));

            // 4) ラベルは表示専用（int → "NN%" に変換）
            var lblOpacityBinding = new Binding(
                "Text", S, nameof(S.WindowAlphaPercent),
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
            this.chkRunAtStartup.Checked = S.isStartup;
            this.chkRunAtStartup.CheckedChanged += (s, e) =>
            {
                if (_initStartupToggle) return;
                S.isStartup = chkRunAtStartup.Checked;
            };
            this.chkOpenMenuOnAppStart.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isOpenMenuOnAppStart), true, DataSourceUpdateMode.OnPropertyChanged));
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
            // 既にあるなら追加しない
            if (!cmb.Items.Cast<object>().Any(x => string.Equals(x?.ToString(), CustomPresetName, StringComparison.Ordinal)))
                cmb.Items.Add(CustomPresetName);

            // 選択（発火を避けたい場合は呼び出し側で _loadingUi true に）
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

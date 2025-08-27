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
        private Label labelHotkeyCapturePrev;       // Capture at previous region
        private TextBox textBoxCapturePrev;
        private Label labelHotkeyVideo;             // Video capture (disabled)
        private TextBox textBoxVideo;

        // ========= Appearance ==========
        private TabPage tabAppearance;

        // Capture settings（簡素化）
        private GroupBox grpCaptureSettings;
        private CheckBox chkScreenGuide;   // show guide lines
        private CheckBox chkTrayNotify;    // notify on capture
        private CheckBox chkPlaySound;     // play sound on capture
        private Label labelBgPreset;
        private ComboBox cmbBgPreset;
        private AlphaPreviewPanel previewBg;

        // Window settings（プリセット＋不透明度統合）
        private GroupBox grpWindowSettings;
        private CheckBox chkWindowShadow;
        private CheckBox chkAfloat;
        private CheckBox chkHighlightOnHover;
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
        private Label labelMove; private TextBox textBoxMove;

        private GroupBox grpShortcutsCaptureOps;
        private TableLayoutPanel tlpShortcutsCap;
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
        private PictureBox picAppIcon;
        private Label labelAppName;
        private Label labelSign;
        private Label labelVersion;
        private LinkLabel labelLinkWebsite;

        // Description (re-layout)
        private Panel descCard;
        private Label labelDescHeader;
        private Label labelDescription;
        private CheckBox chkDoNotShowOnStartup;
        private Label labelTrayNote;

        // ========= Bottom Buttons ==========
        private TableLayoutPanel bottomBar;
        private FlowLayoutPanel rightButtons;
        private Button btnCancelSettings;
        private Button btnSavestings;
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

            this.tabGeneral = new TabPage("General") { AutoScroll = false };
            this.tabAppearance = new TabPage("Appearance") { AutoScroll = false };
            this.tabShortcuts = new TabPage("Shortcuts") { AutoScroll = false };
            this.tabAdvanced = new TabPage("Advanced") { AutoScroll = false };
            this.tabInfo = new TabPage("Info") { AutoScroll = true };

            this.tabControl.TabPages.AddRange(new TabPage[]
            {
                this.tabGeneral, this.tabAppearance, this.tabShortcuts, this.tabAdvanced, this.tabInfo
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
                Margin = new Padding(0, 6, 6, 6)
            };
            this.btnExitAppLeft.Click += new EventHandler(this.btnExitApp_Click);

            this.rightButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            this.btnCancelSettings = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Margin = new Padding(6)
            };
            this.btnCancelSettings.Click += new EventHandler(this.btnCancelSettings_Click);

            this.btnSavestings = new Button
            {
                Text = "Save",
                AutoSize = true,
                Margin = new Padding(6, 6, 0, 6)
            };
            this.btnSavestings.Click += new EventHandler(this.btnSavestings_Click);

            this.rightButtons.Controls.Add(this.btnCancelSettings);
            this.rightButtons.Controls.Add(this.btnSavestings);

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
            var tlpApp = NewGrid(3, 2);

            this.labelLanguage = NewRightLabel("Language");
            this.cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            this.cmbLanguage.Items.AddRange(new object[] { "English (en)", "日本語 (ja)" });
            this.cmbLanguage.SelectedIndex = 0;

            this.labelStartup = NewRightLabel("Startup");
            var flowStartup = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            this.chkRunAtStartup = new CheckBox { Text = "Run at startup", AutoSize = true, Enabled = false };
            this.btnOpenStartupSettings = new Button { Text = "Open Startup settings", AutoSize = true };
            this.btnOpenStartupSettings.Click += new EventHandler(this.btnOpenStartupSettings_Click);
            this.labelStartupInfo = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Startup is managed by Windows.", Dock = DockStyle.Fill };
            this.toolTip1.SetToolTip(this.labelStartupInfo, "Settings > Apps > Startup");
            flowStartup.Controls.Add(this.chkRunAtStartup);
            flowStartup.Controls.Add(this.btnOpenStartupSettings);

            this.labelHistory = NewRightLabel("History limit");
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
            this.grpHotkey.Margin = new Padding(0, 8, 0, 0);

            var tlpHot = NewGrid(3, 2);
            this.labelHotkeyCapture = NewRightLabel("Image capture");
            this.textBoxKiritori = new TextBox { Enabled = false, Width = 160, Text = "Ctrl + Shift + 5" };
            this.textBoxKiritori.KeyDown += new KeyEventHandler(this.textBoxKiritori_KeyDown);
            this.textBoxKiritori.PreviewKeyDown += new PreviewKeyDownEventHandler(this.textBoxKiritori_PreviewKeyDown);

            this.labelHotkeyCapturePrev = NewRightLabel("Capture at previous region");
            this.textBoxCapturePrev = new TextBox { Enabled = false, Width = 160, Text = "Ctrl + Shift + 4" };

            this.labelHotkeyVideo = NewRightLabel("Video capture");
            this.textBoxVideo = new TextBox { Enabled = false, Width = 160, Text = "(disabled)" };

            tlpHot.Controls.Add(this.labelHotkeyCapture, 0, 0);
            tlpHot.Controls.Add(this.textBoxKiritori, 1, 0);
            tlpHot.Controls.Add(this.labelHotkeyCapturePrev, 0, 1);
            tlpHot.Controls.Add(this.textBoxCapturePrev, 1, 1);
            tlpHot.Controls.Add(this.labelHotkeyVideo, 0, 2);
            tlpHot.Controls.Add(this.textBoxVideo, 1, 2);

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
            var tlpCap = NewGrid(2, 2);

            this.chkScreenGuide = new CheckBox { Text = "Show guide lines", Checked = true, AutoSize = true };
            this.chkTrayNotify = new CheckBox { Text = "Notify in tray on capture", AutoSize = true, Enabled = false };
            this.chkPlaySound = new CheckBox { Text = "Play sound on capture", AutoSize = true, Enabled = false };

            var flowToggles = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowToggles.Controls.Add(this.chkScreenGuide);
            flowToggles.Controls.Add(this.chkTrayNotify);
            flowToggles.Controls.Add(this.chkPlaySound);

            tlpCap.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right }, 0, 0);
            tlpCap.Controls.Add(flowToggles, 1, 0);

            this.labelBgPreset = NewRightLabel("Background");
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
            this.grpWindowSettings.Margin = new Padding(0, 8, 0, 0);

            var tlpWin = NewGrid(4, 2);

            var flowWinToggles = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            this.chkWindowShadow = new CheckBox { Text = "Window shadow", Checked = true, AutoSize = true };
            this.chkAfloat = new CheckBox { Text = "Always on top", Checked = true, AutoSize = true };
            this.chkHighlightOnHover = new CheckBox { Text = "Highlight on hover", Checked = true, AutoSize = true };
            flowWinToggles.Controls.Add(this.chkWindowShadow);
            flowWinToggles.Controls.Add(this.chkAfloat);
            flowWinToggles.Controls.Add(this.chkHighlightOnHover);

            tlpWin.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right }, 0, 0);
            tlpWin.Controls.Add(flowWinToggles, 1, 0);

            // 色プリセット＋右プレビュー（透過 100% 固定）
            this.labelHoverPreset = NewRightLabel("Highlight color");
            this.cmbHoverPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            this.cmbHoverPreset.Items.AddRange(new object[] {
                "Red", "Cyan", "Green", "Yellow", "Magenta", "Blue", "Orange", "Black", "White"
            });
            this.cmbHoverPreset.SelectedItem = "Red";

            this.previewHover = new AlphaPreviewPanel { Height = 20, Width = 50, Anchor = AnchorStyles.Left, RgbColor = Color.Red, AlphaPercent = 100 };

            var flowHover = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowHover.Controls.Add(this.cmbHoverPreset);
            flowHover.Controls.Add(this.previewHover);

            tlpWin.Controls.Add(this.labelHoverPreset, 0, 1);
            tlpWin.Controls.Add(flowHover, 1, 1);

            // 太さ（残す）
            this.labelHoverThickness = NewRightLabel("Highlight thickness (px)");
            this.numHoverThickness = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 2, Width = 60, Anchor = AnchorStyles.Left };
            tlpWin.Controls.Add(this.labelHoverThickness, 0, 2);
            tlpWin.Controls.Add(this.numHoverThickness, 1, 2);

            // Default Window Opacity（ここに統合）
            this.labelDefaultOpacity = NewRightLabel("Default opacity");
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
                Color c = Color.Red;
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

            // 4行 × 5列 (左2列 + 仕切り + 右2列)
            this.tlpShortcutsWin = new TableLayoutPanel();
            this.tlpShortcutsWin.ColumnCount = 5;
            this.tlpShortcutsWin.RowCount = 6;
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
            AddShortcutRow(this.tlpShortcutsWin, 0, "Close",        out this.labelClose,      out this.textBoxClose,      "Ctrl + w, ESC", colOffset: 0);
            AddShortcutRow(this.tlpShortcutsWin, 1, "Minimize",     out this.labelMinimize,   out this.textBoxMinimize,   "Ctrl + h",      colOffset: 0);
            AddShortcutRow(this.tlpShortcutsWin, 2, "Always on top",out this.labelAfloat,     out this.textBoxAfloat,     "Ctrl + a",      colOffset: 0);
            AddShortcutRow(this.tlpShortcutsWin, 3, "Drop shadow",  out this.labelDropShadow, out this.textBoxDropShadow, "Ctrl + d",      colOffset: 0);
            AddShortcutRow(this.tlpShortcutsWin, 4, "Move",         out this.labelMove,       out this.textBoxMove,       "up/down/left/right",colOffset: 0);

            // 右段 (colOffset = 3 → ラベルが列3, TextBoxが列4)
            AddShortcutRow(this.tlpShortcutsWin, 0, "Copy",             out this.labelCopy,        out this.textBoxCopy,        "Ctrl + c", colOffset: 3);
            AddShortcutRow(this.tlpShortcutsWin, 1, "Save",             out this.labelSave,        out this.textBoxSave,        "Ctrl + s", colOffset: 3);
            AddShortcutRow(this.tlpShortcutsWin, 2, "Print",            out this.labelPrint,       out this.textBoxPrint,       "Ctrl + p", colOffset: 3);
            AddShortcutRow(this.tlpShortcutsWin, 3, "Zoom in",          out this.labelZoomIn,      out this.textBoxZoomIn,      "Ctrl + +", colOffset: 3);
            AddShortcutRow(this.tlpShortcutsWin, 4, "Zoom out",         out this.labelZoomOut,     out this.textBoxZoomOut,     "Ctrl + -", colOffset: 3);
            AddShortcutRow(this.tlpShortcutsWin, 5, "Zoom reset",       out this.labelZoomOff,     out this.textBoxZoomOff,     "Ctrl + 0", colOffset: 3);

            this.grpShortcutsWindowOps.Controls.Add(this.tlpShortcutsWin);

            this.grpShortcutsCaptureOps = NewGroup("Capture operations");
            this.grpShortcutsCaptureOps.Margin = new Padding(0, 8, 0, 0);

            // 5行×4列 (左2列 + 右2列)
            this.tlpShortcutsCap = NewGrid(2, 3);

            // 左段
            AddShortcutRow(this.tlpShortcutsCap, 0, "Toggle guide lines",out this.labelScreenGuide,out this.textBoxScreenGuide, "capture & alt", colOffset: 0);
            AddShortcutRow(this.tlpShortcutsCap, 1, "Square crop",      out this.labelScreenSquare,out this.textBoxScreenSquare,"capture & shift",colOffset: 0);
            AddShortcutRow(this.tlpShortcutsCap, 2, "Snap",             out this.labelScreenSnap,  out this.textBoxScreenSnap,  "capture & ctrl", colOffset: 0);

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
            var tlpInfo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            this.picAppIcon = new PictureBox { Size = new Size(120, 120), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Top };
            try { this.picAppIcon.Image = global::Kiritori.Properties.Resources.icon_128x128; } catch { }
            var infoRight = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            this.labelAppName = new Label { Text = "\"Kiritori\" for Windows", AutoSize = true, Margin = new Padding(0, 0, 0, 15) };
            this.labelAppName.Font = new Font(
                this.labelAppName.Font.FontFamily,
                this.labelAppName.Font.Size + 7,
                FontStyle.Bold
            );
            this.labelVersion = new Label { Text = "Version - built at (on load)", AutoSize = true, Margin = new Padding(10, 0, 0, 10) };
            this.labelSign = new Label { Text = "Developed by mmiyaji", AutoSize = true, Margin = new Padding(10, 0, 0, 10) };
            this.labelLinkWebsite = new LinkLabel { Text = "HomePage - https://kiritori.ruhenheim.org", AutoSize = true, Margin = new Padding(10, 0, 0, 10) };
            this.labelLinkWebsite.LinkClicked += new LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);

            infoRight.Controls.Add(this.labelAppName);
            infoRight.Controls.Add(this.labelVersion);
            infoRight.Controls.Add(this.labelSign);
            infoRight.Controls.Add(this.labelLinkWebsite);

            tlpInfo.Controls.Add(this.picAppIcon, 0, 0);
            tlpInfo.Controls.Add(infoRight, 1, 0);

            var infoBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            this.descCard = new Panel { BackColor = Color.FromArgb(248, 248, 248), Padding = new Padding(14, 12, 14, 12), Dock = DockStyle.Top, Height = 70 };
            this.descCard.Paint += new PaintEventHandler(this.descCard_Paint);
            this.labelDescHeader = new Label { Text = "What Kiritori does", AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.labelDescription = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(600, 0),
                Text = "• Capture any screen region instantly.\n• Shows as a borderless, always-on-top window.\n• Move, zoom, copy, or save the cutout."
            };
            var cardStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            cardStack.Controls.Add(this.labelDescHeader);
            cardStack.Controls.Add(this.labelDescription);
            this.descCard.Controls.Add(cardStack);

            var infoMinor = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
            this.chkDoNotShowOnStartup = new CheckBox { Text = "Don’t show this screen at startup", AutoSize = true };
            this.chkDoNotShowOnStartup.CheckedChanged += new EventHandler(this.chkDoNotShowOnStartup_CheckedChanged);
            this.labelTrayNote = new Label
            {
                Text = "Tip: Right-click the tray icon for menu.  Hotkey: Ctrl + Shift + 5",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 20)
            };
            infoMinor.Controls.Add(this.chkDoNotShowOnStartup);
            infoMinor.Controls.Add(this.labelTrayNote);

            infoBottom.Controls.Add(this.descCard);
            infoBottom.Controls.Add(infoMinor);

            tlpInfo.SetColumnSpan(infoBottom, 2);
            tlpInfo.Controls.Add(infoBottom, 0, 1);

            this.tabInfo.Controls.Add(tlpInfo);

            // ---- add to Form（※必ず最後に shell を追加）----
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
            int colOffset)
        {
            label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 3, 6)
            };
            textBox = new TextBox
            {
                Text = placeholder,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 3, 3, 3)
            };

            tlp.Controls.Add(label, colOffset + 0, row);
            tlp.Controls.Add(textBox, colOffset + 1, row);
        }

        private void SelectPresetComboFromSettings()
        {
            var S = Properties.Settings.Default;

            // Hover
            if (cmbHoverPreset != null)
            {
                RemoveCustomIfExists(cmbHoverPreset);
                int idx = FindHoverPresetIndex(S.HoverHighlightColor, S.HoverHighlightAlphaPercent);
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
                if (_hoverPresets[i].Color.ToArgb() == c.ToArgb() &&
                    _hoverPresets[i].Alpha == alpha)
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
            this.numHoverThickness.DataBindings.Add(
                new Binding("Value", S, nameof(S.HoverHighlightThickness), true, DataSourceUpdateMode.OnPropertyChanged));
            this.chkScreenGuide.DataBindings.Add(
                new Binding("Checked", S, nameof(S.isScreenGuide), true, DataSourceUpdateMode.OnPropertyChanged));
            this.trackbarDefaultOpacity.DataBindings.Add(
                new Binding("Value", S, nameof(S.alpha_value), true, DataSourceUpdateMode.OnPropertyChanged));

            var lblOpacityBinding = new Binding("Text", S, nameof(S.alpha_value), true, DataSourceUpdateMode.Never);
            lblOpacityBinding.Format += (o, e) => e.Value = $"{e.Value}%";
            this.labelDefaultOpacityVal.DataBindings.Add(lblOpacityBinding);

            // Run at startup は OS 処理が絡むため表示だけ同期＋既存ハンドラで処理
            this.chkRunAtStartup.Checked = S.isStartup;
            this.chkRunAtStartup.CheckedChanged += (s, e) =>
            {
                if (_initStartupToggle) return;
                S.isStartup = chkRunAtStartup.Checked;
            };
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

using System.Drawing;
using System.Windows.Forms;

namespace Kiritori
{
    partial class PrefForm
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PrefForm));
            this.tabInfo = new System.Windows.Forms.TabPage();
            this.labelTrayNote = new System.Windows.Forms.Label();
            // this.labelDescription = new System.Windows.Forms.Label();
            this.chkDoNotShowOnStartup = new System.Windows.Forms.CheckBox();
            this.chkRunAtStartup = new System.Windows.Forms.CheckBox();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelSign = new System.Windows.Forms.Label();
            this.labelAppName = new System.Windows.Forms.Label();
            this.labelLinkWebsite = new System.Windows.Forms.LinkLabel();
            this.picAppIcon = new System.Windows.Forms.PictureBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.labelStartupInfo = new System.Windows.Forms.Label();
            this.btnOpenStartupSettings = new System.Windows.Forms.Button();
            this.labelHistory = new System.Windows.Forms.Label();
            this.textBoxHistory = new System.Windows.Forms.NumericUpDown();
            this.labelStartup = new System.Windows.Forms.Label();
            this.labelAppearance = new System.Windows.Forms.Label();
            this.textBoxKiritori = new System.Windows.Forms.TextBox();
            this.labelKiritori = new System.Windows.Forms.Label();
            this.labelOpacity1 = new System.Windows.Forms.Label();
            this.labelOpacity2 = new System.Windows.Forms.Label();
            this.labelOpacityDefault = new System.Windows.Forms.Label();
            this.btnSavestings = new System.Windows.Forms.Button();
            this.btnCancelSettings = new System.Windows.Forms.Button();
            this.chkAfloat = new System.Windows.Forms.CheckBox();
            this.chkOverlay = new System.Windows.Forms.CheckBox();
            this.chkWindowShadow = new System.Windows.Forms.CheckBox();
            this.trackbarOpacty = new System.Windows.Forms.TrackBar();
            this.tabPageShortcuts = new System.Windows.Forms.TabPage();
            this.textBoxMoveRight = new System.Windows.Forms.TextBox();
            this.labelMoveRight = new System.Windows.Forms.Label();
            this.textBoxMoveLeft = new System.Windows.Forms.TextBox();
            this.labelMoveLeft = new System.Windows.Forms.Label();
            this.textBoxMoveDown = new System.Windows.Forms.TextBox();
            this.labelMoveDown = new System.Windows.Forms.Label();
            this.textBoxMoveUp = new System.Windows.Forms.TextBox();
            this.labelMoveUp = new System.Windows.Forms.Label();
            this.textBoxMinimize = new System.Windows.Forms.TextBox();
            this.labelMinimize = new System.Windows.Forms.Label();
            this.textBoxPrint = new System.Windows.Forms.TextBox();
            this.labelPrint = new System.Windows.Forms.Label();
            this.textBoxZoomOff = new System.Windows.Forms.TextBox();
            this.labelZoomOff = new System.Windows.Forms.Label();
            this.textBoxZoomOut = new System.Windows.Forms.TextBox();
            this.labelZoomOut = new System.Windows.Forms.Label();
            this.textBoxZoomIn = new System.Windows.Forms.TextBox();
            this.labelZoomIn = new System.Windows.Forms.Label();
            this.textBoxSave = new System.Windows.Forms.TextBox();
            this.labelSave = new System.Windows.Forms.Label();
            this.textBoxCopy = new System.Windows.Forms.TextBox();
            this.labelCopy = new System.Windows.Forms.Label();
            this.textbCut = new System.Windows.Forms.TextBox();
            this.labelCut = new System.Windows.Forms.Label();
            this.textBoxClose = new System.Windows.Forms.TextBox();
            this.labelClose = new System.Windows.Forms.Label();
            this.textBoxAfloat = new System.Windows.Forms.TextBox();
            this.labelAfloat = new System.Windows.Forms.Label();
            this.textBoxDropShadow = new System.Windows.Forms.TextBox();
            this.labelDropShadow = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip();
            this.tabInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).BeginInit();
            this.tabControl.SuspendLayout();
            this.tabPageBasic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textBoxHistory)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackbarOpacty)).BeginInit();
            this.tabPageShortcuts.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabInfo);
            this.tabControl.Controls.Add(this.tabPageBasic);
            this.tabControl.Controls.Add(this.tabPageShortcuts);
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(434, 278);
            this.tabControl.TabIndex = 0;
            // 
            // tabInfo
            // 
            this.tabInfo.Controls.Add(this.labelTrayNote);
            this.tabInfo.Controls.Add(this.chkDoNotShowOnStartup);
            this.tabInfo.Controls.Add(this.labelVersion);
            this.tabInfo.Controls.Add(this.labelSign);
            this.tabInfo.Controls.Add(this.labelAppName);
            this.tabInfo.Controls.Add(this.labelLinkWebsite);
            this.tabInfo.Controls.Add(this.picAppIcon);
            this.tabInfo.Location = new System.Drawing.Point(4, 22);
            this.tabInfo.Name = "tabInfo";
            this.tabInfo.Padding = new System.Windows.Forms.Padding(3);
            this.tabInfo.Size = new System.Drawing.Size(426, 252);
            this.tabInfo.TabIndex = 5;
            this.tabInfo.Text = "Info";
            this.tabInfo.UseVisualStyleBackColor = true;

            // descCard はそのまま
            this.descCard = new System.Windows.Forms.Panel();
            this.descCard.Location = new System.Drawing.Point(20, 140);
            this.descCard.Size = new System.Drawing.Size(390, 60);
            this.descCard.Padding = new System.Windows.Forms.Padding(14, 12, 14, 12);
            this.descCard.Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            this.descCard.BackColor = Color.FromArgb(248, 248, 248);
            this.descCard.Paint += new System.Windows.Forms.PaintEventHandler(this.descCard_Paint);
            this.tabInfo.Controls.Add(this.descCard);

            // 本文ラベル（カードの中）
            this.labelDescription = new System.Windows.Forms.Label();
            this.labelDescription.AutoSize = true;
            this.labelDescription.MaximumSize = new System.Drawing.Size(360, 0);
            this.labelDescription.Text =
            "• Capture any screen region instantly.\n" +
            "• Shows as a borderless, always-on-top window.\n" +
            "• Move, zoom, copy, or save the cutout.";
            this.labelDescription.Dock = DockStyle.Top;
            this.descCard.Controls.Add(this.labelDescription);

            // ←ここがポイント：ヘッダーは tabInfo の子にする（descCard ではない）
            this.labelDescHeader = new System.Windows.Forms.Label();
            this.labelDescHeader.AutoSize = true;
            this.labelDescHeader.Font = new Font(this.Font, FontStyle.Bold);
            this.labelDescHeader.Text = "What Kiritori does";

            // バッジっぽく見せるための余白と背景
            this.labelDescHeader.Padding = new Padding(8, 2, 8, 2);
            // カードの縁を“消す”ためにタブ背景色で塗る
            this.labelDescHeader.BackColor = this.tabInfo.BackColor;

            this.tabInfo.Controls.Add(this.labelDescHeader);
            // 前面に
            this.labelDescHeader.BringToFront();

            // レイアウト時にカード位置から算出して“半分かぶせる”
            // this.tabInfo.Layout += (s, e) => PositionDescHeader();

            // 
            // labelTrayNote
            // 
            this.labelTrayNote.AutoSize = true;
            this.labelTrayNote.ForeColor = System.Drawing.SystemColors.GrayText;
            // this.labelTrayNote.Location = new System.Drawing.Point(24, 160);
            this.labelTrayNote.Location = new System.Drawing.Point(25, 230);
            this.labelTrayNote.Name = "labelTrayNote";
            this.labelTrayNote.Size = new System.Drawing.Size(360, 24);
            this.labelTrayNote.TabIndex = 7;
            this.labelTrayNote.Text = "Tip: Right-click the tray icon for menu.  Hotkey: Ctrl + Shift + 5";
            // 
            // chkDoNotShowOnStartup
            // 
            this.chkDoNotShowOnStartup.AutoSize = true;
            // this.chkDoNotShowOnStartup.Location = new System.Drawing.Point(25, 200);
            this.chkDoNotShowOnStartup.Location = new System.Drawing.Point(25, 210);
            this.chkDoNotShowOnStartup.Name = "chkDoNotShowOnStartup";
            this.chkDoNotShowOnStartup.Size = new System.Drawing.Size(195, 16);
            this.chkDoNotShowOnStartup.TabIndex = 6;
            this.chkDoNotShowOnStartup.Text = "Don’t show this screen at startup";
            this.chkDoNotShowOnStartup.UseVisualStyleBackColor = true;
            this.chkDoNotShowOnStartup.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "DoNotShowOnStartup", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.chkDoNotShowOnStartup.Checked = global::Kiritori.Properties.Settings.Default.DoNotShowOnStartup;
            this.chkDoNotShowOnStartup.CheckedChanged += new System.EventHandler(this.chkDoNotShowOnStartup_CheckedChanged);
            // 
            // picAppIcon
            // 
            this.picAppIcon.Image = global::Kiritori.Properties.Resources.icon_128x128;
            this.picAppIcon.Location = new System.Drawing.Point(20, 10);
            this.picAppIcon.Size = new System.Drawing.Size(120, 120); // 表示サイズだけ決める
            this.picAppIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picAppIcon.TabIndex = 0;
            this.picAppIcon.TabStop = false;
            // 
            // labelAppName
            // 
            this.labelAppName.AutoSize = true;
            //this.labelAppName.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.labelAppName.Font = new Font(
                this.labelAppName.Font.FontFamily,           // 既存フォントの名前を保持
                this.labelAppName.Font.Size + 7,             // サイズを拡大
                FontStyle.Bold                               // 太字
            );
            this.labelAppName.Font = new Font(this.labelAppName.Font, FontStyle.Bold);
            this.labelAppName.Location = new System.Drawing.Point(155, 13);
            this.labelAppName.Name = "labelAppName";
            this.labelAppName.Size = new System.Drawing.Size(156, 16);
            this.labelAppName.TabIndex = 2;
            this.labelAppName.Text = "\"Kiritori\" for windows";
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(170, 50);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(195, 12);
            this.labelVersion.TabIndex = 4;
            this.labelVersion.Text = "Version - last updated at (on load)";
            // 
            // labelSign
            // 
            this.labelSign.AutoSize = true;
            this.labelSign.Location = new System.Drawing.Point(170, 75);
            this.labelSign.Name = "labelSign";
            this.labelSign.Size = new System.Drawing.Size(216, 12);
            this.labelSign.TabIndex = 3;
            this.labelSign.Text = "Developed by mmiyaji";
            // 
            // labelLinkWebsite
            // 
            this.labelLinkWebsite.AutoSize = true;
            this.labelLinkWebsite.Location = new System.Drawing.Point(170, 100);
            this.labelLinkWebsite.Name = "labelLinkWebsite";
            this.labelLinkWebsite.Size = new System.Drawing.Size(260, 12);
            this.labelLinkWebsite.TabIndex = 1;
            this.labelLinkWebsite.TabStop = true;
            this.labelLinkWebsite.Text = "HomePage - https://kiritori.ruhenheim.org";
            this.labelLinkWebsite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // // labelDescription
            // this.labelDescription = new System.Windows.Forms.Label();
            // this.labelDescription.AutoSize = true;
            // this.labelDescription.Location = new System.Drawing.Point(25, 155);
            // this.labelDescription.MaximumSize = new System.Drawing.Size(360, 0); // 幅で折返し
            // this.labelDescription.Name = "labelDescription";
            // this.labelDescription.TabIndex = 8;
            // this.labelDescription.Text =
            //     "\'Kiritori\' is a lightweight screen capture tool. " +
            //     "Select any region of the screen and it instantly appears as a " +
            //     "borderless, always-on-top window that you can move, zoom, copy, or save.";
            // this.tabInfo.Controls.Add(this.labelDescription);

            // 
            // tabPageBasic
            // 
            this.tabPageBasic.Controls.Add(this.labelStartupInfo);
            this.tabPageBasic.Controls.Add(this.btnOpenStartupSettings);
            this.tabPageBasic.Controls.Add(this.chkRunAtStartup);
            this.tabPageBasic.Controls.Add(this.labelHistory);
            this.tabPageBasic.Controls.Add(this.textBoxHistory);
            this.tabPageBasic.Controls.Add(this.labelStartup);
            this.tabPageBasic.Controls.Add(this.labelAppearance);
            this.tabPageBasic.Controls.Add(this.textBoxKiritori);
            this.tabPageBasic.Controls.Add(this.labelKiritori);
            this.tabPageBasic.Controls.Add(this.labelOpacity1);
            this.tabPageBasic.Controls.Add(this.labelOpacity2);
            this.tabPageBasic.Controls.Add(this.labelOpacityDefault);
            this.tabPageBasic.Controls.Add(this.btnSavestings);
            this.tabPageBasic.Controls.Add(this.btnCancelSettings);
            this.tabPageBasic.Controls.Add(this.chkAfloat);
            this.tabPageBasic.Controls.Add(this.chkOverlay);
            this.tabPageBasic.Controls.Add(this.chkWindowShadow);
            this.tabPageBasic.Controls.Add(this.trackbarOpacty);
            this.tabPageBasic.Location = new System.Drawing.Point(4, 22);
            this.tabPageBasic.Name = "tabPageBasic";
            this.tabPageBasic.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageBasic.Size = new System.Drawing.Size(426, 247);
            this.tabPageBasic.TabIndex = 6;
            this.tabPageBasic.Text = "Basic";
            this.tabPageBasic.UseVisualStyleBackColor = true;
            // 
            // labelStartupInfo
            // 
            this.labelStartupInfo.AutoSize = false;
            this.labelStartupInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelStartupInfo.Location = new System.Drawing.Point(20, 216);
            this.labelStartupInfo.Name = "labelStartupInfo";
            // this.labelStartupInfo.Size = new System.Drawing.Size(50, 30);
            this.labelStartupInfo.Width = 200;
            this.labelStartupInfo.TabIndex = 15;
            this.labelStartupInfo.AutoEllipsis = true;
            // this.labelStartupInfo.Font = new Font(
            //     this.labelStartupInfo.Font.FontFamily,
            //     this.labelStartupInfo.Font.Size - 1
            // );
            this.labelStartupInfo.Text = "From Settings > Apps > Startup,\r\nyou can enable or disable Kiritori.";
            this.toolTip1.SetToolTip(labelStartupInfo, this.labelStartupInfo.Text);
            // 
            // btnOpenStartupSettings
            // 
            this.btnOpenStartupSettings.Location = new System.Drawing.Point(34, 180);
            this.btnOpenStartupSettings.Name = "btnOpenStartupSettings";
            this.btnOpenStartupSettings.Size = new System.Drawing.Size(150, 25);
            this.btnOpenStartupSettings.TabIndex = 14;
            this.btnOpenStartupSettings.Text = "Open Startup settings";
            this.btnOpenStartupSettings.UseVisualStyleBackColor = true;
            this.btnOpenStartupSettings.Click += new System.EventHandler(this.btnOpenStartupSettings_Click);
            // 
            // chkRunAtStartup
            // 
            this.chkRunAtStartup.AutoSize = true;
            this.chkRunAtStartup.Location = new System.Drawing.Point(35, 156);
            this.chkRunAtStartup.Name = "chkRunAtStartup";
            this.chkRunAtStartup.Size = new System.Drawing.Size(195, 16);
            this.chkRunAtStartup.TabIndex = 6;
            this.chkRunAtStartup.Text = "Run at startup";
            this.chkRunAtStartup.UseVisualStyleBackColor = true;
            this.chkRunAtStartup.Enabled = false;
            // this.chkRunAtStartup.DataBindings.Add(
            //     new Binding("Checked",
            //         global::Kiritori.Properties.Settings.Default,
            //         "isStartup",
            //         true,
            //         System.Windows.Forms.DataSourceUpdateMode.Never));
            // this.chkRunAtStartup.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isStartup", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            // this.chkRunAtStartup.Checked = global::Kiritori.Properties.Settings.Default.isStartup;
            // this.chkRunAtStartup.CheckedChanged += new System.EventHandler(this.chkRunAtStartup_CheckedChangedAsync);

            // 
            // labelHistory
            // 
            this.labelHistory.AutoSize = true;
            this.labelHistory.Location = new System.Drawing.Point(24, 77);
            this.labelHistory.Name = "labelHistory";
            this.labelHistory.Size = new System.Drawing.Size(41, 12);
            this.labelHistory.TabIndex = 13;
            this.labelHistory.Text = "History";
            // 
            // textBoxHistory
            // 
            this.textBoxHistory.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Kiritori.Properties.Settings.Default, "HistoryLimit", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxHistory.Enabled = true;
            this.textBoxHistory.Increment = new decimal(new int[] { 1, 0, 0, 0 });
            this.textBoxHistory.Location = new System.Drawing.Point(35, 97);
            this.textBoxHistory.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.textBoxHistory.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            this.textBoxHistory.Name = "textBoxHistory";
            this.textBoxHistory.Size = new System.Drawing.Size(100, 19);
            this.textBoxHistory.TabIndex = 16;
            this.textBoxHistory.Value = global::Kiritori.Properties.Settings.Default.HistoryLimit;
            // 
            // labelStartup
            // 
            this.labelStartup.AutoSize = true;
            this.labelStartup.Location = new System.Drawing.Point(24, 133);
            this.labelStartup.Name = "labelStartup";
            this.labelStartup.Size = new System.Drawing.Size(42, 12);
            this.labelStartup.TabIndex = 13;
            this.labelStartup.Text = "Startup";
            // 
            // labelAppearance
            // 
            this.labelAppearance.AutoSize = true;
            this.labelAppearance.Location = new System.Drawing.Point(211, 21);
            this.labelAppearance.Name = "labelAppearance";
            this.labelAppearance.Size = new System.Drawing.Size(65, 12);
            this.labelAppearance.TabIndex = 12;
            this.labelAppearance.Text = "Appearance";
            // 
            // textBoxKiritori
            // 
            this.textBoxKiritori.Enabled = false;
            // this.textBoxKiritori.ReadOnly = true;
            this.textBoxKiritori.Location = new System.Drawing.Point(35, 48);
            this.textBoxKiritori.Name = "textBoxKiritori";
            this.textBoxKiritori.Size = new System.Drawing.Size(100, 19);
            this.textBoxKiritori.TabIndex = 11;
            this.textBoxKiritori.Text = "Ctrl + Shift + 5";
            this.textBoxKiritori.KeyDown += textBoxKiritori_KeyDown;
            this.textBoxKiritori.PreviewKeyDown += textBoxKiritori_PreviewKeyDown;
            // 
            // labelKiritori
            // 
            this.labelKiritori.AutoSize = true;
            this.labelKiritori.Location = new System.Drawing.Point(24, 21);
            this.labelKiritori.Name = "labelKiritori";
            this.labelKiritori.Size = new System.Drawing.Size(83, 12);
            this.labelKiritori.TabIndex = 10;
            this.labelKiritori.Text = "Capture hotkey";
            // 
            // labelOpacity1
            // 
            this.labelOpacity1.AutoSize = true;
            this.labelOpacity1.Location = new System.Drawing.Point(221, 164);
            this.labelOpacity1.Name = "labelOpacity1";
            this.labelOpacity1.Size = new System.Drawing.Size(23, 12);
            this.labelOpacity1.TabIndex = 7;
            this.labelOpacity1.Text = "10%";
            // 
            // labelOpacity2
            // 
            this.labelOpacity2.AutoSize = true;
            this.labelOpacity2.Location = new System.Drawing.Point(373, 164);
            this.labelOpacity2.Name = "labelOpacity2";
            this.labelOpacity2.Size = new System.Drawing.Size(29, 12);
            this.labelOpacity2.TabIndex = 6;
            this.labelOpacity2.Text = "100%";
            // 
            // labelOpacityDefault
            // 
            this.labelOpacityDefault.AutoSize = true;
            this.labelOpacityDefault.Location = new System.Drawing.Point(224, 127);
            this.labelOpacityDefault.Name = "labelOpacityDefault";
            this.labelOpacityDefault.Size = new System.Drawing.Size(127, 12);
            this.labelOpacityDefault.TabIndex = 5;
            this.labelOpacityDefault.Text = "Default Window Opacity";
            // 
            // btnSavestings
            // 
            this.btnSavestings.Location = new System.Drawing.Point(329, 193);
            this.btnSavestings.Name = "btnSavestings";
            this.btnSavestings.Size = new System.Drawing.Size(75, 23);
            this.btnSavestings.TabIndex = 4;
            this.btnSavestings.Text = "Save";
            this.btnSavestings.UseVisualStyleBackColor = true;
            this.btnSavestings.Click += new System.EventHandler(this.btnSavestings_Click);
            // 
            // btnCancelSettings
            // 
            this.btnCancelSettings.Location = new System.Drawing.Point(235, 193);
            this.btnCancelSettings.Name = "btnCancelSettings";
            this.btnCancelSettings.Size = new System.Drawing.Size(75, 23);
            this.btnCancelSettings.TabIndex = 3;
            this.btnCancelSettings.Text = "Close";
            this.btnCancelSettings.UseVisualStyleBackColor = true;
            this.btnCancelSettings.Click += new System.EventHandler(this.btnCancelSettings_Click);
            // 
            // chkAfloat
            // 
            this.chkAfloat.AutoSize = true;
            this.chkAfloat.Checked = global::Kiritori.Properties.Settings.Default.isAfloatWindow;
            this.chkAfloat.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAfloat.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isAfloatWindow", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.chkAfloat.Location = new System.Drawing.Point(226, 70);
            this.chkAfloat.Name = "chkAfloat";
            this.chkAfloat.Size = new System.Drawing.Size(100, 16);
            this.chkAfloat.TabIndex = 8;
            this.chkAfloat.Text = "always in front";
            this.chkAfloat.UseVisualStyleBackColor = true;
            // 
            // chkOverlay
            // 
            this.chkOverlay.AutoSize = true;
            this.chkOverlay.Checked = global::Kiritori.Properties.Settings.Default.isOverlay;
            this.chkOverlay.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkOverlay.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isOverlay", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.chkOverlay.Location = new System.Drawing.Point(226, 95);
            this.chkOverlay.Name = "chkOverlay";
            this.chkOverlay.Size = new System.Drawing.Size(100, 16);
            this.chkOverlay.TabIndex = 2;
            this.chkOverlay.Text = "Text Overlay";
            this.chkOverlay.UseVisualStyleBackColor = true;
            // 
            // chkWindowShadow
            // 
            this.chkWindowShadow.AutoSize = true;
            this.chkWindowShadow.Checked = global::Kiritori.Properties.Settings.Default.isWindowShadow;
            this.chkWindowShadow.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkWindowShadow.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isWindowShadow", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.chkWindowShadow.Location = new System.Drawing.Point(226, 45);
            this.chkWindowShadow.Name = "chkWindowShadow";
            this.chkWindowShadow.Size = new System.Drawing.Size(103, 16);
            this.chkWindowShadow.TabIndex = 1;
            this.chkWindowShadow.Text = "window shadow";
            this.chkWindowShadow.UseVisualStyleBackColor = true;
            // 
            // trackbarOpacty
            // 
            this.trackbarOpacty.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.trackbarOpacty.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Kiritori.Properties.Settings.Default, "alpha_value", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.trackbarOpacty.LargeChange = 10;
            this.trackbarOpacty.Location = new System.Drawing.Point(223, 142);
            this.trackbarOpacty.Maximum = 100;
            this.trackbarOpacty.Minimum = 10;
            this.trackbarOpacty.Name = "trackbarOpacty";
            this.trackbarOpacty.Size = new System.Drawing.Size(186, 45);
            this.trackbarOpacty.SmallChange = 5;
            this.trackbarOpacty.TabIndex = 0;
            this.trackbarOpacty.TickFrequency = 10;
            this.trackbarOpacty.Value = global::Kiritori.Properties.Settings.Default.alpha_value;
            // 
            // tabPageShortcuts
            // 
            this.tabPageShortcuts.Controls.Add(this.textBoxMoveRight);
            this.tabPageShortcuts.Controls.Add(this.labelMoveRight);
            this.tabPageShortcuts.Controls.Add(this.textBoxMoveLeft);
            this.tabPageShortcuts.Controls.Add(this.labelMoveLeft);
            this.tabPageShortcuts.Controls.Add(this.textBoxMoveDown);
            this.tabPageShortcuts.Controls.Add(this.labelMoveDown);
            this.tabPageShortcuts.Controls.Add(this.textBoxMoveUp);
            this.tabPageShortcuts.Controls.Add(this.labelMoveUp);
            this.tabPageShortcuts.Controls.Add(this.textBoxMinimize);
            this.tabPageShortcuts.Controls.Add(this.labelMinimize);
            this.tabPageShortcuts.Controls.Add(this.textBoxPrint);
            this.tabPageShortcuts.Controls.Add(this.labelPrint);
            this.tabPageShortcuts.Controls.Add(this.labelZoomOff);
            this.tabPageShortcuts.Controls.Add(this.textBoxZoomOff);
            this.tabPageShortcuts.Controls.Add(this.textBoxZoomOut);
            this.tabPageShortcuts.Controls.Add(this.labelZoomOut);
            this.tabPageShortcuts.Controls.Add(this.textBoxZoomIn);
            this.tabPageShortcuts.Controls.Add(this.labelZoomIn);
            this.tabPageShortcuts.Controls.Add(this.textBoxSave);
            this.tabPageShortcuts.Controls.Add(this.labelSave);
            this.tabPageShortcuts.Controls.Add(this.textBoxCopy);
            this.tabPageShortcuts.Controls.Add(this.labelCopy);
            this.tabPageShortcuts.Controls.Add(this.textbCut);
            this.tabPageShortcuts.Controls.Add(this.labelCut);
            this.tabPageShortcuts.Controls.Add(this.textBoxClose);
            this.tabPageShortcuts.Controls.Add(this.labelClose);
            this.tabPageShortcuts.Controls.Add(this.textBoxAfloat);
            this.tabPageShortcuts.Controls.Add(this.labelAfloat);
            this.tabPageShortcuts.Controls.Add(this.textBoxDropShadow);
            this.tabPageShortcuts.Controls.Add(this.labelDropShadow);
            this.tabPageShortcuts.Location = new System.Drawing.Point(4, 22);
            this.tabPageShortcuts.Name = "tabPageShortcuts";
            this.tabPageShortcuts.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageShortcuts.Size = new System.Drawing.Size(426, 247);
            this.tabPageShortcuts.TabIndex = 7;
            this.tabPageShortcuts.Text = "Shortcuts";
            this.tabPageShortcuts.UseVisualStyleBackColor = true;
            // 
            // textBoxMoveRight
            // 
            this.textBoxMoveRight.Enabled = false;
            this.textBoxMoveRight.Location = new System.Drawing.Point(304, 120);
            this.textBoxMoveRight.Name = "textBoxMoveRight";
            this.textBoxMoveRight.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveRight.TabIndex = 25;
            this.textBoxMoveRight.Text = "right";
            // 
            // labelMoveRight
            // 
            this.labelMoveRight.AutoSize = true;
            this.labelMoveRight.Location = new System.Drawing.Point(230, 120);
            this.labelMoveRight.Name = "labelMoveRight";
            this.labelMoveRight.Size = new System.Drawing.Size(59, 12);
            this.labelMoveRight.TabIndex = 24;
            this.labelMoveRight.Text = "move right";
            // 
            // textBoxMoveLeft
            // 
            this.textBoxMoveLeft.Enabled = false;
            this.textBoxMoveLeft.Location = new System.Drawing.Point(304, 95);
            this.textBoxMoveLeft.Name = "textBoxMoveLeft";
            this.textBoxMoveLeft.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveLeft.TabIndex = 23;
            this.textBoxMoveLeft.Text = "left";
            // 
            // labelMoveLeft
            // 
            this.labelMoveLeft.AutoSize = true;
            this.labelMoveLeft.Location = new System.Drawing.Point(230, 95);
            this.labelMoveLeft.Name = "labelMoveLeft";
            this.labelMoveLeft.Size = new System.Drawing.Size(53, 12);
            this.labelMoveLeft.TabIndex = 22;
            this.labelMoveLeft.Text = "move left";
            // 
            // textBoxMoveDown
            // 
            this.textBoxMoveDown.Enabled = false;
            this.textBoxMoveDown.Location = new System.Drawing.Point(304, 70);
            this.textBoxMoveDown.Name = "textBoxMoveDown";
            this.textBoxMoveDown.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveDown.TabIndex = 21;
            this.textBoxMoveDown.Text = "down";
            // 
            // labelMoveDown
            // 
            this.labelMoveDown.AutoSize = true;
            this.labelMoveDown.Location = new System.Drawing.Point(230, 70);
            this.labelMoveDown.Name = "labelMoveDown";
            this.labelMoveDown.Size = new System.Drawing.Size(62, 12);
            this.labelMoveDown.TabIndex = 20;
            this.labelMoveDown.Text = "move down";
            // 
            // textBoxMoveUp
            // 
            this.textBoxMoveUp.Enabled = false;
            this.textBoxMoveUp.Location = new System.Drawing.Point(304, 45);
            this.textBoxMoveUp.Name = "textBoxMoveUp";
            this.textBoxMoveUp.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveUp.TabIndex = 19;
            this.textBoxMoveUp.Text = "up";
            // 
            // labelMoveUp
            // 
            this.labelMoveUp.AutoSize = true;
            this.labelMoveUp.Location = new System.Drawing.Point(230, 45);
            this.labelMoveUp.Name = "labelMoveUp";
            this.labelMoveUp.Size = new System.Drawing.Size(48, 12);
            this.labelMoveUp.TabIndex = 18;
            this.labelMoveUp.Text = "move up";
            // 
            // textBoxMinimize
            // 
            this.textBoxMinimize.Enabled = false;
            this.textBoxMinimize.Location = new System.Drawing.Point(304, 20);
            this.textBoxMinimize.Name = "textBoxMinimize";
            this.textBoxMinimize.Size = new System.Drawing.Size(100, 19);
            this.textBoxMinimize.TabIndex = 17;
            this.textBoxMinimize.Text = "Ctrl + h";
            // 
            // labelMinimize
            // 
            this.labelMinimize.AutoSize = true;
            this.labelMinimize.Location = new System.Drawing.Point(230, 20);
            this.labelMinimize.Name = "labelMinimize";
            this.labelMinimize.Size = new System.Drawing.Size(49, 12);
            this.labelMinimize.TabIndex = 16;
            this.labelMinimize.Text = "minimize";
            // 
            // textBoxZoomOff
            // 
            this.textBoxZoomOff.Enabled = false;
            this.textBoxZoomOff.Location = new System.Drawing.Point(98, 170);
            this.textBoxZoomOff.Name = "textBoxZoomOff";
            this.textBoxZoomOff.Size = new System.Drawing.Size(100, 19);
            this.textBoxZoomOff.TabIndex = 26;
            this.textBoxZoomOff.Text = "Ctrl + 0";
            // 
            // textBoxZoomOut
            // 
            this.textBoxZoomOut.Enabled = false;
            this.textBoxZoomOut.Location = new System.Drawing.Point(98, 145);
            this.textBoxZoomOut.Name = "textBoxZoomOut";
            this.textBoxZoomOut.Size = new System.Drawing.Size(100, 19);
            this.textBoxZoomOut.TabIndex = 13;
            this.textBoxZoomOut.Text = "Ctrl + -";
            // 
            // labelZoomOff
            // 
            this.labelZoomOff.AutoSize = true;
            this.labelZoomOff.Location = new System.Drawing.Point(24, 170);
            this.labelZoomOff.Name = "labelZoomOff";
            this.labelZoomOff.Size = new System.Drawing.Size(51, 12);
            this.labelZoomOff.TabIndex = 27;
            this.labelZoomOff.Text = "zoom off";
            // 
            // labelZoomOut
            // 
            this.labelZoomOut.AutoSize = true;
            this.labelZoomOut.Location = new System.Drawing.Point(24, 145);
            this.labelZoomOut.Name = "labelZoomOut";
            this.labelZoomOut.Size = new System.Drawing.Size(51, 12);
            this.labelZoomOut.TabIndex = 12;
            this.labelZoomOut.Text = "zoom out";
            // 
            // textBoxZoomIn
            // 
            this.textBoxZoomIn.Enabled = false;
            this.textBoxZoomIn.Location = new System.Drawing.Point(98, 120);
            this.textBoxZoomIn.Name = "textBoxZoomIn";
            this.textBoxZoomIn.Size = new System.Drawing.Size(100, 19);
            this.textBoxZoomIn.TabIndex = 11;
            this.textBoxZoomIn.Text = "Ctrl + +";
            // 
            // labelZoomIn
            // 
            this.labelZoomIn.AutoSize = true;
            this.labelZoomIn.Location = new System.Drawing.Point(24, 120);
            this.labelZoomIn.Name = "labelZoomIn";
            this.labelZoomIn.Size = new System.Drawing.Size(44, 12);
            this.labelZoomIn.TabIndex = 10;
            this.labelZoomIn.Text = "zoom in";
            // 
            // textBoxSave
            // 
            this.textBoxSave.Enabled = false;
            this.textBoxSave.Location = new System.Drawing.Point(98, 95);
            this.textBoxSave.Name = "textBoxSave";
            this.textBoxSave.Size = new System.Drawing.Size(100, 19);
            this.textBoxSave.TabIndex = 9;
            this.textBoxSave.Text = "Ctrl + s";
            // 
            // labelSave
            // 
            this.labelSave.AutoSize = true;
            this.labelSave.Location = new System.Drawing.Point(24, 95);
            this.labelSave.Name = "labelSave";
            this.labelSave.Size = new System.Drawing.Size(29, 12);
            this.labelSave.TabIndex = 8;
            this.labelSave.Text = "save";
            // 
            // textBoxCopy
            // 
            this.textBoxCopy.Enabled = false;
            this.textBoxCopy.Location = new System.Drawing.Point(98, 70);
            this.textBoxCopy.Name = "textBoxCopy";
            this.textBoxCopy.Size = new System.Drawing.Size(100, 19);
            this.textBoxCopy.TabIndex = 7;
            this.textBoxCopy.Text = "Ctrl + c";
            // 
            // labelCopy
            // 
            this.labelCopy.AutoSize = true;
            this.labelCopy.Location = new System.Drawing.Point(24, 70);
            this.labelCopy.Name = "labelCopy";
            this.labelCopy.Size = new System.Drawing.Size(29, 12);
            this.labelCopy.TabIndex = 6;
            this.labelCopy.Text = "copy";
            // 
            // textbCut
            // 
            this.textbCut.Enabled = false;
            this.textbCut.Location = new System.Drawing.Point(98, 45);
            this.textbCut.Name = "textbCut";
            this.textbCut.Size = new System.Drawing.Size(100, 19);
            this.textbCut.TabIndex = 5;
            this.textbCut.Text = "Ctrl + x";
            // 
            // labelCut
            // 
            this.labelCut.AutoSize = true;
            this.labelCut.Location = new System.Drawing.Point(24, 45);
            this.labelCut.Name = "labelCut";
            this.labelCut.Size = new System.Drawing.Size(21, 12);
            this.labelCut.TabIndex = 4;
            this.labelCut.Text = "cut";
            // 
            // textBoxClose
            // 
            this.textBoxClose.Enabled = false;
            this.textBoxClose.Location = new System.Drawing.Point(98, 20);
            this.textBoxClose.Name = "textBoxClose";
            this.textBoxClose.Size = new System.Drawing.Size(100, 19);
            this.textBoxClose.TabIndex = 3;
            this.textBoxClose.Text = "Ctrl + w, ESC";
            // 
            // labelClose
            // 
            this.labelClose.AutoSize = true;
            this.labelClose.Location = new System.Drawing.Point(24, 20);
            this.labelClose.Name = "labelClose";
            this.labelClose.Size = new System.Drawing.Size(32, 12);
            this.labelClose.TabIndex = 2;
            this.labelClose.Text = "close";
            // 
            // textBoxAfloat
            // 
            this.textBoxAfloat.Enabled = false;
            this.textBoxAfloat.Location = new System.Drawing.Point(98, 195);
            this.textBoxAfloat.Name = "textBoxAfloat";
            this.textBoxAfloat.Size = new System.Drawing.Size(100, 19);
            this.textBoxAfloat.TabIndex = 1;
            this.textBoxAfloat.Text = "Ctrl + a";
            // 
            // labelAfloat
            // 
            this.labelAfloat.AutoSize = true;
            this.labelAfloat.Location = new System.Drawing.Point(22, 195);
            this.labelAfloat.Name = "labelAfloat";
            this.labelAfloat.Size = new System.Drawing.Size(69, 12);
            this.labelAfloat.TabIndex = 0;
            this.labelAfloat.Text = "afloat";
            // 
            // textBoxDropShadow
            // 
            this.textBoxDropShadow.Enabled = false;
            this.textBoxDropShadow.Location = new System.Drawing.Point(98, 220);
            this.textBoxDropShadow.Name = "textBoxDropShadow";
            this.textBoxDropShadow.Size = new System.Drawing.Size(100, 19);
            this.textBoxDropShadow.TabIndex = 1;
            this.textBoxDropShadow.Text = "Ctrl + d";
            // 
            // labelDropShadow
            // 
            this.labelDropShadow.AutoSize = true;
            this.labelDropShadow.Location = new System.Drawing.Point(22, 220);
            this.labelDropShadow.Name = "labelDropShadow";
            this.labelDropShadow.Size = new System.Drawing.Size(69, 12);
            this.labelDropShadow.TabIndex = 0;
            this.labelDropShadow.Text = "drop shadow";
            // 
            // textBoxPrint
            // 
            this.textBoxPrint.Enabled = false;
            this.textBoxPrint.Location = new System.Drawing.Point(304, 145);
            this.textBoxPrint.Name = "textBoxPrint";
            this.textBoxPrint.Size = new System.Drawing.Size(100, 19);
            this.textBoxPrint.TabIndex = 15;
            this.textBoxPrint.Text = "Ctrl + p";
            // 
            // labelPrint
            // 
            this.labelPrint.AutoSize = true;
            this.labelPrint.Location = new System.Drawing.Point(230, 145);
            this.labelPrint.Name = "labelPrint";
            this.labelPrint.Size = new System.Drawing.Size(28, 12);
            this.labelPrint.TabIndex = 14;
            this.labelPrint.Text = "print";
            // 
            // PrefForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(434, 262);
            this.Controls.Add(this.tabControl);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(450, 320);
            this.Name = "PrefForm";
            this.Text = "Kiritori - Main / Preferences";
            this.Load += new System.EventHandler(this.PrefForm_Load);
            this.tabInfo.ResumeLayout(false);
            this.tabInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.tabPageBasic.ResumeLayout(false);
            this.tabPageBasic.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textBoxHistory)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackbarOpacty)).EndInit();
            this.tabPageShortcuts.ResumeLayout(false);
            this.tabPageShortcuts.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabPage tabInfo;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelSign;
        private System.Windows.Forms.Label labelAppName;
        private System.Windows.Forms.LinkLabel labelLinkWebsite;
        private System.Windows.Forms.PictureBox picAppIcon;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageBasic;
        private System.Windows.Forms.CheckBox chkAfloat;
        private System.Windows.Forms.CheckBox chkOverlay;
        private System.Windows.Forms.CheckBox chkWindowShadow;
        private System.Windows.Forms.TrackBar trackbarOpacty;
        private System.Windows.Forms.Button btnSavestings;
        private System.Windows.Forms.Label labelOpacity1;
        private System.Windows.Forms.Label labelOpacity2;
        private System.Windows.Forms.Label labelOpacityDefault;
        private System.Windows.Forms.Label labelKiritori;
        private System.Windows.Forms.TextBox textBoxKiritori;
        private System.Windows.Forms.Button btnCancelSettings;
        private System.Windows.Forms.TabPage tabPageShortcuts;
        private System.Windows.Forms.TextBox textBoxAfloat;
        private System.Windows.Forms.Label labelAfloat;
        private System.Windows.Forms.TextBox textBoxDropShadow;
        private System.Windows.Forms.Label labelDropShadow;
        private System.Windows.Forms.TextBox textBoxClose;
        private System.Windows.Forms.Label labelClose;
        private System.Windows.Forms.TextBox textBoxZoomOut;
        private System.Windows.Forms.Label labelZoomOff;
        private System.Windows.Forms.TextBox textBoxZoomOff;
        private System.Windows.Forms.Label labelZoomOut;
        private System.Windows.Forms.TextBox textBoxZoomIn;
        private System.Windows.Forms.Label labelZoomIn;
        private System.Windows.Forms.TextBox textBoxSave;
        private System.Windows.Forms.Label labelSave;
        private System.Windows.Forms.TextBox textBoxCopy;
        private System.Windows.Forms.Label labelCopy;
        private System.Windows.Forms.TextBox textbCut;
        private System.Windows.Forms.Label labelCut;
        private System.Windows.Forms.TextBox textBoxPrint;
        private System.Windows.Forms.Label labelPrint;
        private System.Windows.Forms.TextBox textBoxMinimize;
        private System.Windows.Forms.Label labelMinimize;
        private System.Windows.Forms.TextBox textBoxMoveRight;
        private System.Windows.Forms.Label labelMoveRight;
        private System.Windows.Forms.TextBox textBoxMoveLeft;
        private System.Windows.Forms.Label labelMoveLeft;
        private System.Windows.Forms.TextBox textBoxMoveDown;
        private System.Windows.Forms.Label labelMoveDown;
        private System.Windows.Forms.TextBox textBoxMoveUp;
        private System.Windows.Forms.Label labelMoveUp;
        private System.Windows.Forms.Label labelStartup;
        private System.Windows.Forms.Label labelAppearance;
        private System.Windows.Forms.Label labelStartupInfo;
        private System.Windows.Forms.Label labelHistory;
        private System.Windows.Forms.NumericUpDown textBoxHistory;
        private System.Windows.Forms.Button btnOpenStartupSettings;
        private System.Windows.Forms.CheckBox chkDoNotShowOnStartup;
        private System.Windows.Forms.CheckBox chkRunAtStartup;
        private System.Windows.Forms.Label labelTrayNote;
        private System.Windows.Forms.Panel descCard;
        private System.Windows.Forms.Label labelDescHeader;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}

namespace Kiritori
{
    partial class PrefForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PrefForm));
            this.tabInfo = new System.Windows.Forms.TabPage();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelSign = new System.Windows.Forms.Label();
            this.labelAppName = new System.Windows.Forms.Label();
            this.labelLinkWebsite = new System.Windows.Forms.LinkLabel();
            this.picAppIcon = new System.Windows.Forms.PictureBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.labelStartupInfo = new System.Windows.Forms.Label();
            // this.checkBox3 = new System.Windows.Forms.CheckBox();
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
            this.textBoxZoomOut = new System.Windows.Forms.TextBox();
            this.labelZoomOut = new System.Windows.Forms.Label();
            this.textBoxZoomIn = new System.Windows.Forms.TextBox();
            this.labelZoomIn = new System.Windows.Forms.Label();
            this.textBoxSave= new System.Windows.Forms.TextBox();
            this.labelSave = new System.Windows.Forms.Label();
            this.textBoxCopy = new System.Windows.Forms.TextBox();
            this.labelCopy = new System.Windows.Forms.Label();
            this.textbCut = new System.Windows.Forms.TextBox();
            this.labelCut = new System.Windows.Forms.Label();
            this.textBoxClose = new System.Windows.Forms.TextBox();
            this.labelClose = new System.Windows.Forms.Label();
            this.textBoxAfloat = new System.Windows.Forms.TextBox();
            this.labelAfloat = new System.Windows.Forms.Label();
            this.labelHistory = new System.Windows.Forms.Label();
            this.textBoxHistory = new System.Windows.Forms.NumericUpDown();
            this.btnOpenStartupSettings = new System.Windows.Forms.Button();
            this.chkDoNotShowOnStartup = new System.Windows.Forms.CheckBox();
            this.labelTrayNote = new System.Windows.Forms.Label();

            this.tabInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).BeginInit();
            this.tabControl.SuspendLayout();
            this.tabPageBasic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackbarOpacty)).BeginInit();
            this.tabPageShortcuts.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabInfo
            // 
            this.tabInfo.Controls.Add(this.labelVersion);
            this.tabInfo.Controls.Add(this.labelSign);
            this.tabInfo.Controls.Add(this.labelAppName);
            this.tabInfo.Controls.Add(this.labelLinkWebsite);
            this.tabInfo.Controls.Add(this.picAppIcon);
            this.tabInfo.Location = new System.Drawing.Point(4, 22);
            this.tabInfo.Name = "tabInfo";
            this.tabInfo.Padding = new System.Windows.Forms.Padding(3);
            this.tabInfo.Size = new System.Drawing.Size(426, 237);
            this.tabInfo.TabIndex = 5;
            this.tabInfo.Text = "Info";
            this.tabInfo.UseVisualStyleBackColor = true;
            // 
            // labelVersion
            // 
            //自分自身のAssemblyを取得
            System.Reflection.Assembly asm =
                System.Reflection.Assembly.GetExecutingAssembly();
            //バージョンの取得
            System.Version ver = asm.GetName().Version;
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(170, 60);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(225, 12);
            this.labelVersion.TabIndex = 4;
            this.labelVersion.Text = "Version " + ver + " last updated at 21 Aug, 2025";
            // 
            // labelSign
            // 
            this.labelSign.AutoSize = true;
            this.labelSign.Location = new System.Drawing.Point(170, 87);
            this.labelSign.Name = "labelSign";
            this.labelSign.Size = new System.Drawing.Size(216, 12);
            this.labelSign.TabIndex = 3;
            this.labelSign.Text = "Developed by Masahiro MIYAJI (mmiyaji)";
            // 
            // labelAppName
            // 
            this.labelAppName.AutoSize = true;
            this.labelAppName.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.labelAppName.Location = new System.Drawing.Point(170, 23);
            this.labelAppName.Name = "labelAppName";
            this.labelAppName.Size = new System.Drawing.Size(156, 16);
            this.labelAppName.TabIndex = 2;
            this.labelAppName.Text = "Kiritori for windows";
            // 
            // labelLinkWebsite
            // 
            this.labelLinkWebsite.AutoSize = true;
            this.labelLinkWebsite.Location = new System.Drawing.Point(170, 119);
            this.labelLinkWebsite.Name = "labelLinkWebsite";
            this.labelLinkWebsite.Size = new System.Drawing.Size(238, 12);
            this.labelLinkWebsite.TabIndex = 1;
            this.labelLinkWebsite.TabStop = true;
            this.labelLinkWebsite.Text = "Go to App page - https://kiritori.ruhenheim.org";
            this.labelLinkWebsite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);

            // chkDoNotShowOnStartup
            this.chkDoNotShowOnStartup.AutoSize = true;
            this.chkDoNotShowOnStartup.Location = new System.Drawing.Point(25, 200);
            this.chkDoNotShowOnStartup.Name = "chkDoNotShowOnStartup";
            this.chkDoNotShowOnStartup.Size = new System.Drawing.Size(195, 16);
            this.chkDoNotShowOnStartup.TabIndex = 17;
            this.chkDoNotShowOnStartup.Text = "Don’t show this screen at startup";
            this.chkDoNotShowOnStartup.UseVisualStyleBackColor = true;
            // Settings と双方向バインド（既定値 True）
            this.chkDoNotShowOnStartup.DataBindings.Add(
                new System.Windows.Forms.Binding(
                    "Checked",
                    global::Kiritori.Properties.Settings.Default,
                    "DoNotShowOnStartup",
                    true,
                    System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));

            this.chkDoNotShowOnStartup.CheckedChanged += new System.EventHandler(this.chkDoNotShowOnStartup_CheckedChanged);
            // General タブに追加
            this.tabInfo.Controls.Add(this.chkDoNotShowOnStartup);

            this.labelTrayNote.AutoSize = true;
            this.labelTrayNote.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelTrayNote.Location = new System.Drawing.Point(24, 160); // 位置は調整
            this.labelTrayNote.Name = "labelTrayNote";
            this.labelTrayNote.Size = new System.Drawing.Size(360, 24);
            this.labelTrayNote.TabIndex = 18;
            this.labelTrayNote.Text = "'Kiritori' runs in the system tray.\r\nRight-click the tray icon to open or exit.";

            this.tabInfo.Controls.Add(this.labelTrayNote);
            // 
            // picAppIcon
            // 
            this.picAppIcon.Image = global::Kiritori.Properties.Resources.icon_128x128;
            this.picAppIcon.InitialImage = ((System.Drawing.Image)(resources.GetObject("picAppIcon.InitialImage")));
            this.picAppIcon.Location = new System.Drawing.Point(25, 23);
            this.picAppIcon.Name = "picAppIcon";
            this.picAppIcon.Size = new System.Drawing.Size(128, 128);
            this.picAppIcon.TabIndex = 0;
            this.picAppIcon.TabStop = false;
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
            this.tabControl.Size = new System.Drawing.Size(434, 263);
            this.tabControl.TabIndex = 0;
            // 
            // tabPageBasic
            // 
            this.tabPageBasic.Controls.Add(this.labelStartupInfo);
            // this.tabPageBasic.Controls.Add(this.checkBox3);
            //this.tabPageBasic.Controls.Add(this.btnOpenStartupSettings);
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
            this.tabPageBasic.Controls.Add(this.chkWindowShadow);
            this.tabPageBasic.Controls.Add(this.trackbarOpacty);
            this.tabPageBasic.Location = new System.Drawing.Point(4, 22);
            this.tabPageBasic.Name = "tabPageBasic";
            this.tabPageBasic.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageBasic.Size = new System.Drawing.Size(426, 237);
            this.tabPageBasic.TabIndex = 6;
            this.tabPageBasic.Text = "Basic";
            this.tabPageBasic.UseVisualStyleBackColor = true;

            // 
            // labalStartupInfo
            // 
            // this.labalStartupInfo.AutoSize = true;
            // this.labalStartupInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            // this.labalStartupInfo.Location = new System.Drawing.Point(32, 200);
            // this.labalStartupInfo.Name = "labalStartupInfo";
            // this.labalStartupInfo.Size = new System.Drawing.Size(175, 12);
            // this.labalStartupInfo.TabIndex = 15;
            // this.labalStartupInfo.Text = "Create shortcut on Startup folder";
            this.labelStartupInfo.AutoSize = true;
            this.labelStartupInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelStartupInfo.Location = new System.Drawing.Point(32, 196);
            this.labelStartupInfo.Name = "labalStartupInfo";
            this.labelStartupInfo.Size = new System.Drawing.Size(320, 30);
            this.labelStartupInfo.TabIndex = 15;
            this.labelStartupInfo.Text = "From Settings > Apps > Startup,\n"
                                + "you can enable or disable Kiritori.";
            // checkBox3
            // 
            // this.checkBox3.AutoSize = true;
            // this.checkBox3.Checked = global::Kiritori.Properties.Settings.Default.isStartup;
            // this.checkBox3.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isStartup", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            // this.checkBox3.Location = new System.Drawing.Point(34, 180);
            // this.checkBox3.Name = "checkBox3";
            // this.checkBox3.Size = new System.Drawing.Size(140, 16);
            // this.checkBox3.TabIndex = 14;
            // this.checkBox3.Text = "Launch Kiritori at login";
            // this.checkBox3.UseVisualStyleBackColor = true;
            // this.checkBox3.CheckedChanged += new System.EventHandler(this.checkBox3_CheckedChanged);
            // btnOpenStartupSettings
            this.btnOpenStartupSettings.Location = new System.Drawing.Point(34, 156);
            this.btnOpenStartupSettings.Name = "btnOpenStartupSettings";
            this.btnOpenStartupSettings.Size = new System.Drawing.Size(150, 25);
            this.btnOpenStartupSettings.TabIndex = 14;
            this.btnOpenStartupSettings.Text = "Open Startup settings";
            this.btnOpenStartupSettings.UseVisualStyleBackColor = true;
            this.btnOpenStartupSettings.Click += new System.EventHandler(this.btnOpenStartupSettings_Click);
            this.tabPageBasic.Controls.Add(this.btnOpenStartupSettings);
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
            // labelStartup
            // 
            this.labelHistory.AutoSize = true;
            this.labelHistory.Location = new System.Drawing.Point(24, 77);
            this.labelHistory.Name = "labelHistory";
            this.labelHistory.Size = new System.Drawing.Size(42, 12);
            this.labelHistory.TabIndex = 13;
            this.labelHistory.Text = "History";

            this.textBoxHistory.Enabled = true;
            this.textBoxHistory.Location = new System.Drawing.Point(30, 97);
            this.textBoxHistory.Name = "textBoxHistory";
            this.textBoxHistory.Size = new System.Drawing.Size(100, 19);
            this.textBoxHistory.TabIndex = 16;
            this.textBoxHistory.Minimum = 0;
            this.textBoxHistory.Maximum = 100;
            this.textBoxHistory.Increment = 1;
            this.textBoxHistory.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Kiritori.Properties.Settings.Default, "HistoryLimit", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBoxHistory.Value = global::Kiritori.Properties.Settings.Default.HistoryLimit;

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
            this.textBoxKiritori.Location = new System.Drawing.Point(35, 48);
            this.textBoxKiritori.Name = "textBoxKiritori";
            this.textBoxKiritori.Size = new System.Drawing.Size(100, 19);
            this.textBoxKiritori.TabIndex = 11;
            this.textBoxKiritori.Text = "Ctrl + Shift + 5";
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
            this.labelOpacity1.Location = new System.Drawing.Point(221, 154);
            this.labelOpacity1.Name = "labelOpacity1";
            this.labelOpacity1.Size = new System.Drawing.Size(23, 12);
            this.labelOpacity1.TabIndex = 7;
            this.labelOpacity1.Text = "10%";
            // 
            // labelOpacity2
            // 
            this.labelOpacity2.AutoSize = true;
            this.labelOpacity2.Location = new System.Drawing.Point(373, 154);
            this.labelOpacity2.Name = "labelOpacity2";
            this.labelOpacity2.Size = new System.Drawing.Size(29, 12);
            this.labelOpacity2.TabIndex = 6;
            this.labelOpacity2.Text = "100%";
            // 
            // labelOpacityDefault
            // 
            this.labelOpacityDefault.AutoSize = true;
            this.labelOpacityDefault.Location = new System.Drawing.Point(224, 107);
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
            this.btnCancelSettings.Text = "Cancel";
            this.btnCancelSettings.UseVisualStyleBackColor = true;
            this.btnCancelSettings.Click += new System.EventHandler(this.button1_Click);
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
            this.chkAfloat.TabIndex = 2;
            this.chkAfloat.Text = "always in front";
            this.chkAfloat.UseVisualStyleBackColor = true;
            // 
            // chkWindowShadow
            // 
            this.chkWindowShadow.AutoSize = true;
            this.chkWindowShadow.Checked = global::Kiritori.Properties.Settings.Default.isWindowShadow;
            this.chkWindowShadow.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkWindowShadow.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Kiritori.Properties.Settings.Default, "isWindowShadow", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.chkWindowShadow.Location = new System.Drawing.Point(226, 48);
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
            this.trackbarOpacty.Location = new System.Drawing.Point(223, 122);
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
            this.tabPageShortcuts.Location = new System.Drawing.Point(4, 22);
            this.tabPageShortcuts.Name = "tabPageShortcuts";
            this.tabPageShortcuts.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageShortcuts.Size = new System.Drawing.Size(426, 237);
            this.tabPageShortcuts.TabIndex = 7;
            this.tabPageShortcuts.Text = "Shortcuts";
            this.tabPageShortcuts.UseVisualStyleBackColor = true;
            // 
            // textBoxMoveRight
            // 
            this.textBoxMoveRight.Enabled = false;
            this.textBoxMoveRight.Location = new System.Drawing.Point(304, 120);
            this.textBoxMoveRight.Name = "textBox13";
            this.textBoxMoveRight.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveRight.TabIndex = 25;
            this.textBoxMoveRight.Text = "right";
            // 
            // labelMoveRight
            // 
            this.labelMoveRight.AutoSize = true;
            this.labelMoveRight.Location = new System.Drawing.Point(230, 120);
            this.labelMoveRight.Name = "label19";
            this.labelMoveRight.Size = new System.Drawing.Size(59, 12);
            this.labelMoveRight.TabIndex = 24;
            this.labelMoveRight.Text = "move right";
            // 
            // textBoxMoveLeft
            // 
            this.textBoxMoveLeft.Enabled = false;
            this.textBoxMoveLeft.Location = new System.Drawing.Point(304, 95);
            this.textBoxMoveLeft.Name = "textBox14";
            this.textBoxMoveLeft.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveLeft.TabIndex = 23;
            this.textBoxMoveLeft.Text = "left";
            // 
            // labelMoveLeft
            // 
            this.labelMoveLeft.AutoSize = true;
            this.labelMoveLeft.Location = new System.Drawing.Point(230, 95);
            this.labelMoveLeft.Name = "label20";
            this.labelMoveLeft.Size = new System.Drawing.Size(53, 12);
            this.labelMoveLeft.TabIndex = 22;
            this.labelMoveLeft.Text = "move left";
            // 
            // textBoxMoveDown
            // 
            this.textBoxMoveDown.Enabled = false;
            this.textBoxMoveDown.Location = new System.Drawing.Point(304, 70);
            this.textBoxMoveDown.Name = "textBox12";
            this.textBoxMoveDown.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveDown.TabIndex = 21;
            this.textBoxMoveDown.Text = "down";
            // 
            // labelMoveDown
            // 
            this.labelMoveDown.AutoSize = true;
            this.labelMoveDown.Location = new System.Drawing.Point(230, 70);
            this.labelMoveDown.Name = "label18";
            this.labelMoveDown.Size = new System.Drawing.Size(62, 12);
            this.labelMoveDown.TabIndex = 20;
            this.labelMoveDown.Text = "move down";
            // 
            // textBoxMoveUp
            // 
            this.textBoxMoveUp.Enabled = false;
            this.textBoxMoveUp.Location = new System.Drawing.Point(304, 45);
            this.textBoxMoveUp.Name = "textBox11";
            this.textBoxMoveUp.Size = new System.Drawing.Size(100, 19);
            this.textBoxMoveUp.TabIndex = 19;
            this.textBoxMoveUp.Text = "up";
            // 
            // labelMoveUp
            // 
            this.labelMoveUp.AutoSize = true;
            this.labelMoveUp.Location = new System.Drawing.Point(230, 45);
            this.labelMoveUp.Name = "label17";
            this.labelMoveUp.Size = new System.Drawing.Size(48, 12);
            this.labelMoveUp.TabIndex = 18;
            this.labelMoveUp.Text = "move up";
            // 
            // textBoxMinimize
            // 
            this.textBoxMinimize.Enabled = false;
            this.textBoxMinimize.Location = new System.Drawing.Point(304, 20);
            this.textBoxMinimize.Name = "textBox10";
            this.textBoxMinimize.Size = new System.Drawing.Size(100, 19);
            this.textBoxMinimize.TabIndex = 17;
            this.textBoxMinimize.Text = "Ctrl + h";
            // 
            // labelMinimize
            // 
            this.labelMinimize.AutoSize = true;
            this.labelMinimize.Location = new System.Drawing.Point(230, 20);
            this.labelMinimize.Name = "label16";
            this.labelMinimize.Size = new System.Drawing.Size(49, 12);
            this.labelMinimize.TabIndex = 16;
            this.labelMinimize.Text = "minimize";
            // 
            // textBoxPrint
            // 
            this.textBoxPrint.Enabled = false;
            this.textBoxPrint.Location = new System.Drawing.Point(98, 195);
            this.textBoxPrint.Name = "textBox9";
            this.textBoxPrint.Size = new System.Drawing.Size(100, 19);
            this.textBoxPrint.TabIndex = 15;
            this.textBoxPrint.Text = "Ctrl + p";
            // 
            // labelPrint
            // 
            this.labelPrint.AutoSize = true;
            this.labelPrint.Location = new System.Drawing.Point(22, 196);
            this.labelPrint.Name = "label15";
            this.labelPrint.Size = new System.Drawing.Size(28, 12);
            this.labelPrint.TabIndex = 14;
            this.labelPrint.Text = "print";
            // 
            // textBoxZoomOut
            // 
            this.textBoxZoomOut.Enabled = false;
            this.textBoxZoomOut.Location = new System.Drawing.Point(98, 145);
            this.textBoxZoomOut.Name = "textBox8";
            this.textBoxZoomOut.Size = new System.Drawing.Size(100, 19);
            this.textBoxZoomOut.TabIndex = 13;
            this.textBoxZoomOut.Text = "Ctrl + -";
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
            this.textBoxZoomIn.Name = "textBox7";
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
            // textBoxZoomo
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
            this.textBoxAfloat.Location = new System.Drawing.Point(98, 170);
            this.textBoxAfloat.Name = "textBoxAfloat";
            this.textBoxAfloat.Size = new System.Drawing.Size(100, 19);
            this.textBoxAfloat.TabIndex = 1;
            this.textBoxAfloat.Text = "Ctrl + a";
            // 
            // labelAfloat
            // 
            this.labelAfloat.AutoSize = true;
            this.labelAfloat.Location = new System.Drawing.Point(22, 171);
            this.labelAfloat.Name = "labelAfloat";
            this.labelAfloat.Size = new System.Drawing.Size(69, 12);
            this.labelAfloat.TabIndex = 0;
            this.labelAfloat.Text = "toggle afloat";
            // 
            // PrefForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(434, 262);
            this.Controls.Add(this.tabControl);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(450, 300);
            this.Name = "PrefForm";
            this.Text = "Kiritori - Main / Preferences";
            this.Load += new System.EventHandler(this.PrefForm_Load);
            this.tabInfo.ResumeLayout(false);
            this.tabInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.tabPageBasic.ResumeLayout(false);
            this.tabPageBasic.PerformLayout();
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
        private System.Windows.Forms.TextBox textBoxClose;
        private System.Windows.Forms.Label labelClose;
        private System.Windows.Forms.TextBox textBoxZoomOut;
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
        private System.Windows.Forms.Label labelTrayNote;
    }
}
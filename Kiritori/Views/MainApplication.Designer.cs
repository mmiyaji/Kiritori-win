namespace Kiritori
{
    partial class MainApplication
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainApplication));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.captureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureOCRToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.livePreviewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hideAllWindowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showAllWindowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeAllWindowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.preferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.historyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trayContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.trayContextMenuStrip;
            this.notifyIcon1.MouseClick += NotifyIcon1_MouseClick;
            // this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "Kiritori — minimized to tray (click to open)";
            this.notifyIcon1.Tag = "loc:Tray.TrayIcon";
            this.notifyIcon1.Visible = true;
            // 
            // trayContextMenuStrip
            // 
            this.trayContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.captureToolStripMenuItem,
            this.captureOCRToolStripMenuItem,
            this.livePreviewToolStripMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.openToolStripMenuItem,
            this.clipboardToolStripMenuItem,
            this.historyToolStripMenuItem,
            this.hideAllWindowsToolStripMenuItem,
            this.showAllWindowsToolStripMenuItem,
            this.closeAllWindowsToolStripMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.preferencesToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.trayContextMenuStrip.Name = "trayContextMenuStrip";
            this.trayContextMenuStrip.Size = new System.Drawing.Size(203, 180);
            // 
            // captureToolStripMenuItem
            // 
            this.captureToolStripMenuItem.Name = "captureToolStripMenuItem";
            this.captureToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.D5)));
            this.captureToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.captureToolStripMenuItem.Text = "Image Capture";
            this.captureToolStripMenuItem.Tag = "loc:Text.ImageCapture";
            this.captureToolStripMenuItem.Click += new System.EventHandler(this.captureToolStripMenuItem_Click);
            // 
            // captureOCRToolStripMenuItem
            // 
            this.captureOCRToolStripMenuItem.Name = "captureOCRToolStripMenuItem";
            this.captureOCRToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.D5)));
            this.captureOCRToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.captureOCRToolStripMenuItem.Text = "OCR Capture";
            this.captureOCRToolStripMenuItem.Tag = "loc:Text.OCRCapture";
            this.captureOCRToolStripMenuItem.Click += new System.EventHandler(this.captureOCRToolStripMenuItem_Click);
            // 
            // livePreviewToolStripMenuItem
            // 
            this.livePreviewToolStripMenuItem.Name = "livePreviewToolStripMenuItem";
            this.livePreviewToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.D6)));
            this.livePreviewToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.livePreviewToolStripMenuItem.Text = "Live Preview";
            this.livePreviewToolStripMenuItem.Tag = "loc:Text.LivePreview";
            this.livePreviewToolStripMenuItem.Click += new System.EventHandler(this.livePreviewToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.openToolStripMenuItem.Text = "Open Image File";
            this.openToolStripMenuItem.Tag = "loc:Text.OpenImageFile";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.clipboardToolStripMenuItem.Text = "Open From Clipboard";
            this.clipboardToolStripMenuItem.Tag = "loc:Menu.OpenClipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // hideAllWindowsToolStripMenuItem
            // 
            this.hideAllWindowsToolStripMenuItem.Name = "hideAllWindowsToolStripMenuItem";
            this.hideAllWindowsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.hideAllWindowsToolStripMenuItem.Text = "Hide All Windows";
            this.hideAllWindowsToolStripMenuItem.Tag = "loc:Text.HideAllWindows";
            this.hideAllWindowsToolStripMenuItem.Click += new System.EventHandler(this.hideAllWindowsToolStripMenuItem_Click);
            // 
            // showAllWindowsToolStripMenuItem
            // 
            this.showAllWindowsToolStripMenuItem.Name = "showAllWindowsToolStripMenuItem";
            this.showAllWindowsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.showAllWindowsToolStripMenuItem.Text = "Show All Windows";
            this.showAllWindowsToolStripMenuItem.Tag = "loc:Text.ShowAllWindows";
            this.showAllWindowsToolStripMenuItem.Click += new System.EventHandler(this.showAllWindowsToolStripMenuItem_Click);
            // 
            // closeAllWindowsToolStripMenuItem
            // 
            this.closeAllWindowsToolStripMenuItem.Name = "closeAllWindowsToolStripMenuItem";
            this.closeAllWindowsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.closeAllWindowsToolStripMenuItem.Text = "Close All Windows";
            this.closeAllWindowsToolStripMenuItem.Tag = "loc:Text.CloseAllWindows";
            this.closeAllWindowsToolStripMenuItem.Click += new System.EventHandler(this.closeAllWindowsToolStripMenuItem_Click);
            // 
            // preferencesToolStripMenuItem
            // 
            this.preferencesToolStripMenuItem.Name = "preferencesToolStripMenuItem";
            this.preferencesToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.preferencesToolStripMenuItem.Text = "Main / Preferences";
            this.preferencesToolStripMenuItem.Tag = "loc:Text.MainPreferences";
            this.preferencesToolStripMenuItem.Click += new System.EventHandler(this.preferencesToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Tag = "loc:Text.BtnExit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // historyToolStripMenuItem
            // 
            this.historyToolStripMenuItem.Name = "historyToolStripMenuItem";
            this.historyToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.historyToolStripMenuItem.Text = "History";
            this.historyToolStripMenuItem.Tag = "loc:Text.History";
            // 
            // MainApplication
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Name = "MainApplication";
            this.Text = "Kiritori - Main";
            this.trayContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip trayContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem captureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem captureOCRToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem livePreviewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem hideAllWindowsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showAllWindowsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeAllWindowsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem preferencesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem historyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        
    }
}
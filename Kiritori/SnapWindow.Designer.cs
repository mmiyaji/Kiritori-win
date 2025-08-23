namespace Kiritori
{
    partial class SnapWindow
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SnapWindow));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.closeESCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.cutCtrlXToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyCtrlXToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.saveImageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.originalSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zoomInToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zoomOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.keepAfloatToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dropShadowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.minimizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.opacityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.preferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.printToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            this.Resize += new System.EventHandler(this.Form1_Resize);
            // 
            // pictureBox1
            // 
            this.pictureBox1.ContextMenuStrip = this.contextMenuStrip1;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(276, 230);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.closeESCToolStripMenuItem,
            this.toolStripSeparator1,
            this.cutCtrlXToolStripMenuItem,
            this.copyCtrlXToolStripMenuItem,
            this.toolStripSeparator2,
            this.saveImageToolStripMenuItem,
            this.toolStripSeparator3,
            this.originalSizeToolStripMenuItem,
            this.zoomInToolStripMenuItem,
            this.zoomOutToolStripMenuItem,
            this.toolStripSeparator4,
            this.keepAfloatToolStripMenuItem,
            this.dropShadowToolStripMenuItem,
            this.minimizeToolStripMenuItem,
            this.opacityToolStripMenuItem,
            this.toolStripSeparator5,
            this.preferencesToolStripMenuItem,
            this.printToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(232, 392);
            // 
            // closeESCToolStripMenuItem
            // 
            this.closeESCToolStripMenuItem.Name = "closeESCToolStripMenuItem";
            this.closeESCToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.closeESCToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.closeESCToolStripMenuItem.Text = "Close Window";
            this.closeESCToolStripMenuItem.Click += new System.EventHandler(this.closeESCToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(228, 6);
            // 
            // cutCtrlXToolStripMenuItem
            // 
            this.cutCtrlXToolStripMenuItem.Name = "cutCtrlXToolStripMenuItem";
            this.cutCtrlXToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutCtrlXToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.cutCtrlXToolStripMenuItem.Text = "Cut";
            this.cutCtrlXToolStripMenuItem.Click += new System.EventHandler(this.cutCtrlXToolStripMenuItem_Click);
            // 
            // copyCtrlXToolStripMenuItem
            // 
            this.copyCtrlXToolStripMenuItem.Name = "copyCtrlXToolStripMenuItem";
            this.copyCtrlXToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyCtrlXToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.copyCtrlXToolStripMenuItem.Text = "Copy";
            this.copyCtrlXToolStripMenuItem.Click += new System.EventHandler(this.copyCtrlCToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(228, 6);
            // 
            // saveImageToolStripMenuItem
            // 
            this.saveImageToolStripMenuItem.Name = "saveImageToolStripMenuItem";
            this.saveImageToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveImageToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.saveImageToolStripMenuItem.Text = "Save Image";
            this.saveImageToolStripMenuItem.Click += new System.EventHandler(this.saveImageToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(228, 6);
            // 
            // originalSizeToolStripMenuItem
            // 
            this.originalSizeToolStripMenuItem.Name = "originalSizeToolStripMenuItem";
            this.originalSizeToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.originalSizeToolStripMenuItem.Text = "Original Size";
            this.originalSizeToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.NumPad0)));
            this.originalSizeToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+0";
            this.originalSizeToolStripMenuItem.Click += new System.EventHandler(this.originalSizeToolStripMenuItem_Click);
            // Zoom In
            this.zoomInToolStripMenuItem.Name = "zoomInToolStripMenuItem";
            this.zoomInToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.zoomInToolStripMenuItem.Text = "Zoom In";
            this.zoomInToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Oemplus)));
            this.zoomInToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+'+'";
            this.zoomInToolStripMenuItem.Click += new System.EventHandler(this.zoomInToolStripMenuItem_Click);

            // Zoom Out
            this.zoomOutToolStripMenuItem.Name = "zoomOutToolStripMenuItem";
            this.zoomOutToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.zoomOutToolStripMenuItem.Text = "Zoom Out";
            this.zoomOutToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.OemMinus)));
            this.zoomOutToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+'-'";
            this.zoomOutToolStripMenuItem.Click += new System.EventHandler(this.zoomOutToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(228, 6);
            // 
            // keepAfloatToolStripMenuItem
            // 
            this.keepAfloatToolStripMenuItem.Name = "keepAfloatToolStripMenuItem";
            this.keepAfloatToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this.keepAfloatToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.keepAfloatToolStripMenuItem.Text = "Toggle Keep Afloat";
            this.keepAfloatToolStripMenuItem.Click += new System.EventHandler(this.keepAfloatToolStripMenuItem_Click);
            // 
            // dropShadowToolStripMenuItem
            // 
            this.dropShadowToolStripMenuItem.Name = "dropShadowToolStripMenuItem";
            this.dropShadowToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.dropShadowToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.dropShadowToolStripMenuItem.Text = "Toggle Drop Shadow";
            this.dropShadowToolStripMenuItem.Click += new System.EventHandler(this.dropShadowToolStripMenuItem_Click);
            // 
            // minimizeToolStripMenuItem
            // 
            this.minimizeToolStripMenuItem.Name = "minimizeToolStripMenuItem";
            this.minimizeToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.H)));
            this.minimizeToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.minimizeToolStripMenuItem.Text = "Minimize";
            this.minimizeToolStripMenuItem.Click += new System.EventHandler(this.minimizeToolStripMenuItem_Click);
            // 
            // opacityToolStripMenuItem
            // 
            this.opacityToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2,
            this.toolStripMenuItem3,
            this.toolStripMenuItem4,
            this.toolStripMenuItem5,
            this.toolStripMenuItem6});
            this.opacityToolStripMenuItem.Name = "opacityToolStripMenuItem";
            this.opacityToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.opacityToolStripMenuItem.Text = "Opacity";
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(107, 22);
            this.toolStripMenuItem2.Text = "100%";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.toolStripMenuItem2_Click);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.Size = new System.Drawing.Size(107, 22);
            this.toolStripMenuItem3.Text = "90%";
            this.toolStripMenuItem3.Click += new System.EventHandler(this.toolStripMenuItem3_Click);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(107, 22);
            this.toolStripMenuItem4.Text = "80%";
            this.toolStripMenuItem4.Click += new System.EventHandler(this.toolStripMenuItem4_Click);
            // 
            // toolStripMenuItem5
            // 
            this.toolStripMenuItem5.Name = "toolStripMenuItem5";
            this.toolStripMenuItem5.Size = new System.Drawing.Size(107, 22);
            this.toolStripMenuItem5.Text = "50%";
            this.toolStripMenuItem5.Click += new System.EventHandler(this.toolStripMenuItem5_Click);
            // 
            // toolStripMenuItem6
            // 
            this.toolStripMenuItem6.Name = "toolStripMenuItem6";
            this.toolStripMenuItem6.Size = new System.Drawing.Size(107, 22);
            this.toolStripMenuItem6.Text = "30%";
            this.toolStripMenuItem6.Click += new System.EventHandler(this.toolStripMenuItem6_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(228, 6);
            // 
            // preferencesToolStripMenuItem
            // 
            this.preferencesToolStripMenuItem.Name = "preferencesToolStripMenuItem";
            this.preferencesToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.preferencesToolStripMenuItem.Text = "Preferences";
            this.preferencesToolStripMenuItem.Click += new System.EventHandler(this.preferencesToolStripMenuItem_Click);
            // 
            // printToolStripMenuItem
            // 
            this.printToolStripMenuItem.Name = "printToolStripMenuItem";
            this.printToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
            this.printToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.printToolStripMenuItem.Text = "Print";
            this.printToolStripMenuItem.Click += new System.EventHandler(this.printToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.exitToolStripMenuItem.Text = "Exit Kiritori";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // SnapWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.ControlBox = false;
            this.Controls.Add(this.pictureBox1);
            this.Cursor = System.Windows.Forms.Cursors.Hand;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SnapWindow";
            this.ShowInTaskbar = false;
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem closeESCToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem cutCtrlXToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyCtrlXToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem saveImageToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem originalSizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem zoomInToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem zoomOutToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem keepAfloatToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem dropShadowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem opacityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem6;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem preferencesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem printToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem minimizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    }
}


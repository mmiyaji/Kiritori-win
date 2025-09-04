using System.Windows.Forms;
namespace Kiritori
{
    partial class SnapWindow
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private PictureBox pictureBox1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem fileParentMenu;
        private ToolStripMenuItem editParentMenu;
        private ToolStripMenuItem viewParentMenu;
        private ToolStripMenuItem windowParentMenu;
        private ToolStripMenuItem zoomParentMenu;
        private ToolStripMenuItem captureToolStripMenuItem;
        private ToolStripMenuItem captureOCRToolStripMenuItem;
        private ToolStripMenuItem livePreviewToolStripMenuItem;
        
        private ToolStripMenuItem closeESCToolStripMenuItem;
        private ToolStripMenuItem cutCtrlXToolStripMenuItem;
        private ToolStripMenuItem copyCtrlCToolStripMenuItem;
        private ToolStripMenuItem ocrCtrlTToolStripMenuItem;
        private ToolStripMenuItem saveImageToolStripMenuItem;
        private ToolStripMenuItem openImageToolStripMenuItem;
        private ToolStripMenuItem editPaintToolStripMenuItem;
        private ToolStripMenuItem originalLocationToolStripMenuItem;
        private ToolStripMenuItem originalSizeToolStripMenuItem;
        private ToolStripMenuItem size10ToolStripMenuItem;
        private ToolStripMenuItem size50ToolStripMenuItem;
        private ToolStripMenuItem size100ToolStripMenuItem;
        private ToolStripMenuItem size150ToolStripMenuItem;
        private ToolStripMenuItem size200ToolStripMenuItem;
        private ToolStripMenuItem size500ToolStripMenuItem;
        private ToolStripMenuItem zoomInToolStripMenuItem;
        private ToolStripMenuItem zoomOutToolStripMenuItem;
        private ToolStripMenuItem keepAfloatToolStripMenuItem;
        private ToolStripMenuItem dropShadowToolStripMenuItem;
        private ToolStripMenuItem opacityParentMenu;
        private ToolStripMenuItem opacity100toolStripMenuItem;
        private ToolStripMenuItem opacity90toolStripMenuItem;
        private ToolStripMenuItem opacity80toolStripMenuItem;
        private ToolStripMenuItem opacity50toolStripMenuItem;
        private ToolStripMenuItem opacity30toolStripMenuItem;
        private ToolStripMenuItem preferencesToolStripMenuItem;
        private ToolStripMenuItem printToolStripMenuItem;
        private ToolStripMenuItem minimizeToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;

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
            this.pictureBox1 = new PictureBox();
            this.contextMenuStrip1 = new ContextMenuStrip(this.components);
            this.fileParentMenu = new ToolStripMenuItem();
            this.editParentMenu = new ToolStripMenuItem();
            this.viewParentMenu = new ToolStripMenuItem();
            this.zoomParentMenu = new ToolStripMenuItem();
            this.windowParentMenu = new ToolStripMenuItem();
            this.captureToolStripMenuItem = new ToolStripMenuItem();
            this.captureOCRToolStripMenuItem = new ToolStripMenuItem();
            this.livePreviewToolStripMenuItem = new ToolStripMenuItem();
            this.closeESCToolStripMenuItem = new ToolStripMenuItem();
            this.cutCtrlXToolStripMenuItem = new ToolStripMenuItem();
            this.copyCtrlCToolStripMenuItem = new ToolStripMenuItem();
            this.ocrCtrlTToolStripMenuItem = new ToolStripMenuItem();
            this.saveImageToolStripMenuItem = new ToolStripMenuItem();
            this.openImageToolStripMenuItem = new ToolStripMenuItem();
            this.editPaintToolStripMenuItem = new ToolStripMenuItem();
            this.originalLocationToolStripMenuItem = new ToolStripMenuItem();
            this.originalSizeToolStripMenuItem = new ToolStripMenuItem();
            this.size10ToolStripMenuItem = new ToolStripMenuItem();
            this.size50ToolStripMenuItem = new ToolStripMenuItem();
            this.size100ToolStripMenuItem = new ToolStripMenuItem();
            this.size150ToolStripMenuItem = new ToolStripMenuItem();
            this.size200ToolStripMenuItem = new ToolStripMenuItem();
            this.size500ToolStripMenuItem = new ToolStripMenuItem();
            this.zoomInToolStripMenuItem = new ToolStripMenuItem();
            this.zoomOutToolStripMenuItem = new ToolStripMenuItem();
            this.keepAfloatToolStripMenuItem = new ToolStripMenuItem();
            this.dropShadowToolStripMenuItem = new ToolStripMenuItem();
            this.minimizeToolStripMenuItem = new ToolStripMenuItem();
            this.opacityParentMenu = new ToolStripMenuItem();
            this.opacity100toolStripMenuItem = new ToolStripMenuItem();
            this.opacity90toolStripMenuItem = new ToolStripMenuItem();
            this.opacity80toolStripMenuItem = new ToolStripMenuItem();
            this.opacity50toolStripMenuItem = new ToolStripMenuItem();
            this.opacity30toolStripMenuItem = new ToolStripMenuItem();
            this.preferencesToolStripMenuItem = new ToolStripMenuItem();
            this.printToolStripMenuItem = new ToolStripMenuItem();
            this.exitToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.ContextMenuStrip = this.contextMenuStrip1;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Margin = new Padding(0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(276, 230);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new ToolStripItem[] {
                this.closeESCToolStripMenuItem,
                new ToolStripSeparator(),
                this.captureToolStripMenuItem,
                this.captureOCRToolStripMenuItem,
                this.livePreviewToolStripMenuItem,
                new ToolStripSeparator(),
                this.fileParentMenu,
                this.editParentMenu,
                this.viewParentMenu,
                this.windowParentMenu,
                new ToolStripSeparator(),
                this.preferencesToolStripMenuItem,
                this.exitToolStripMenuItem,
            });
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(232, 392);
            // 
            // fileParentMenu
            // 
            this.fileParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.saveImageToolStripMenuItem,
                this.openImageToolStripMenuItem,
                new ToolStripSeparator(),
                this.printToolStripMenuItem,
            });
            this.fileParentMenu.Name = "fileParentMenu";
            this.fileParentMenu.Size = new System.Drawing.Size(231, 22);
            this.fileParentMenu.Text = "File";
            this.fileParentMenu.Tag = "loc:Menu.File";
            // 
            // editParentMenu
            // 
            this.editParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.cutCtrlXToolStripMenuItem,
                this.copyCtrlCToolStripMenuItem,
                this.ocrCtrlTToolStripMenuItem,
                this.editPaintToolStripMenuItem,
            });
            this.editParentMenu.Name = "editParentMenu";
            this.editParentMenu.Size = new System.Drawing.Size(231, 22);
            this.editParentMenu.Text = "Edit";
            this.editParentMenu.Tag = "loc:Menu.Edit";
            // 
            // viewParentMenu
            // 
            this.viewParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.originalSizeToolStripMenuItem,
                this.zoomOutToolStripMenuItem,
                this.zoomInToolStripMenuItem,
                this.zoomParentMenu,
                new ToolStripSeparator(),
                this.opacityParentMenu,
            });
            this.viewParentMenu.Name = "viewParentMenu";
            this.viewParentMenu.Size = new System.Drawing.Size(231, 22);
            this.viewParentMenu.Text = "View";
            this.viewParentMenu.Tag = "loc:Menu.View";
            // 
            // windowParentMenu
            // 
            this.windowParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.keepAfloatToolStripMenuItem,
                this.dropShadowToolStripMenuItem,
                new ToolStripSeparator(),
                this.minimizeToolStripMenuItem,
                this.originalLocationToolStripMenuItem,
                // this.closeESCToolStripMenuItem,
            });
            this.windowParentMenu.Name = "windowParentMenu";
            this.windowParentMenu.Size = new System.Drawing.Size(231, 22);
            this.windowParentMenu.Text = "Window";
            this.windowParentMenu.Tag = "loc:Menu.Window";
            // 
            // zoomParentMenu
            // 
            this.zoomParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.size10ToolStripMenuItem,
                this.size50ToolStripMenuItem,
                this.size100ToolStripMenuItem,
                this.size150ToolStripMenuItem,
                this.size200ToolStripMenuItem,
                this.size500ToolStripMenuItem,
            });
            this.zoomParentMenu.Name = "zoomParentMenu";
            this.zoomParentMenu.Size = new System.Drawing.Size(231, 22);
            this.zoomParentMenu.Text = "Zoom(%)";
            this.zoomParentMenu.Tag = "loc:Menu.Zoom";
            // 
            // captureToolStripMenuItem
            // 
            this.captureToolStripMenuItem.Name = "captureToolStripMenuItem";
            this.captureToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D5)));
            this.captureToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.captureToolStripMenuItem.Text = "Capture";
            this.captureToolStripMenuItem.Tag = "loc:Menu.Capture";
            this.captureToolStripMenuItem.Click += new System.EventHandler(this.captureToolStripMenuItem_Click);
            // 
            // captureOCRToolStripMenuItem
            // 
            this.captureOCRToolStripMenuItem.Name = "captureOCRToolStripMenuItem";
            this.captureOCRToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D4)));
            this.captureOCRToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.captureOCRToolStripMenuItem.Text = "OCR(text recognition)";
            this.captureOCRToolStripMenuItem.Tag = "loc:Menu.OCR";
            this.captureOCRToolStripMenuItem.Click += new System.EventHandler(this.captureOCRToolStripMenuItem_Click);
            // 
            // livePreviewToolStripMenuItem
            // 
            this.livePreviewToolStripMenuItem.Name = "livePreviewToolStripMenuItem";
            this.livePreviewToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D6)));
            this.livePreviewToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.livePreviewToolStripMenuItem.Text = "Live Preview";
            this.livePreviewToolStripMenuItem.Tag = "loc:Menu.LivePreview";
            this.livePreviewToolStripMenuItem.Click += new System.EventHandler(this.livePreviewToolStripMenuItem_Click);
            // 
            // closeESCToolStripMenuItem
            // 
            this.closeESCToolStripMenuItem.Name = "closeESCToolStripMenuItem";
            this.closeESCToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.W)));
            this.closeESCToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.closeESCToolStripMenuItem.Text = "Close Window";
            this.closeESCToolStripMenuItem.Tag = "loc:Menu.CloseWindow";
            this.closeESCToolStripMenuItem.Click += new System.EventHandler(this.closeESCToolStripMenuItem_Click);
            // 
            // cutCtrlXToolStripMenuItem
            // 
            this.cutCtrlXToolStripMenuItem.Name = "cutCtrlXToolStripMenuItem";
            this.cutCtrlXToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.X)));
            this.cutCtrlXToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.cutCtrlXToolStripMenuItem.Text = "Cut";
            this.cutCtrlXToolStripMenuItem.Tag = "loc:Menu.Cut";
            this.cutCtrlXToolStripMenuItem.Click += new System.EventHandler(this.cutCtrlXToolStripMenuItem_Click);
            // 
            // copyCtrlCToolStripMenuItem
            // 
            this.copyCtrlCToolStripMenuItem.Name = "copyCtrlCToolStripMenuItem";
            this.copyCtrlCToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.C)));
            this.copyCtrlCToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.copyCtrlCToolStripMenuItem.Text = "Copy";
            this.copyCtrlCToolStripMenuItem.Tag = "loc:Menu.Copy";
            this.copyCtrlCToolStripMenuItem.Click += new System.EventHandler(this.copyCtrlCToolStripMenuItem_Click);
            // 
            // ocrCtrlTToolStripMenuItem
            // 
            this.ocrCtrlTToolStripMenuItem.Name = "ocrCtrlTToolStripMenuItem";
            this.ocrCtrlTToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.T)));
            this.ocrCtrlTToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.ocrCtrlTToolStripMenuItem.Text = "OCR";
            this.ocrCtrlTToolStripMenuItem.Tag = "loc:Menu.OCR";
            this.ocrCtrlTToolStripMenuItem.Click += new System.EventHandler(this.ocrCtrlTToolStripMenuItem_Click);
            // 
            // editPaintToolStripMenuItem
            // 
            this.editPaintToolStripMenuItem.Name = "editPaintToolStripMenuItem";
            this.editPaintToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.E)));
            this.editPaintToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.editPaintToolStripMenuItem.Text = "Edit Paint";
            this.editPaintToolStripMenuItem.Tag = "loc:Menu.EditPaint";
            this.editPaintToolStripMenuItem.Click += new System.EventHandler(this.editPaintToolStripMenuItem_Click);
            // 
            // saveImageToolStripMenuItem
            // 
            this.saveImageToolStripMenuItem.Name = "saveImageToolStripMenuItem";
            this.saveImageToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.S)));
            this.saveImageToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.saveImageToolStripMenuItem.Text = "Save Image";
            this.saveImageToolStripMenuItem.Tag = "loc:Menu.SaveImage";
            this.saveImageToolStripMenuItem.Click += new System.EventHandler(this.saveImageToolStripMenuItem_Click);
            // 
            // openImageToolStripMenuItem
            // 
            this.openImageToolStripMenuItem.Name = "openImageToolStripMenuItem";
            this.openImageToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.O)));
            this.openImageToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.openImageToolStripMenuItem.Text = "Open Image";
            this.openImageToolStripMenuItem.Tag = "loc:Menu.OpenImage";
            this.openImageToolStripMenuItem.Click += new System.EventHandler(this.openImageToolStripMenuItem_Click);
            // 
            // originalLocationToolStripMenuItem
            // 
            this.originalLocationToolStripMenuItem.Name = "originalLocationToolStripMenuItem";
            this.originalLocationToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.originalLocationToolStripMenuItem.Text = "Original Location";
            this.originalLocationToolStripMenuItem.Tag = "loc:Menu.OriginalLocation";
            this.originalLocationToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.NumPad9)));
            this.originalLocationToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+9";
            this.originalLocationToolStripMenuItem.Click += new System.EventHandler(this.originalLocationToolStripMenuItem_Click);
            // 
            // originalSizeToolStripMenuItem
            // 
            this.originalSizeToolStripMenuItem.Name = "originalSizeToolStripMenuItem";
            this.originalSizeToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.originalSizeToolStripMenuItem.Text = "Original Size";
            this.originalSizeToolStripMenuItem.Tag = "loc:Menu.OriginalSize";
            this.originalSizeToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.NumPad0)));
            this.originalSizeToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+0";
            this.originalSizeToolStripMenuItem.Click += new System.EventHandler(this.originalSizeToolStripMenuItem_Click);
            // Zoom In
            this.zoomInToolStripMenuItem.Name = "zoomInToolStripMenuItem";
            this.zoomInToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.zoomInToolStripMenuItem.Text = "Zoom In(+10%)";
            this.zoomInToolStripMenuItem.Tag = "loc:Menu.ZoomIn";
            this.zoomInToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.Oemplus)));
            this.zoomInToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+'+'";
            this.zoomInToolStripMenuItem.Click += new System.EventHandler(this.zoomInToolStripMenuItem_Click);

            // Zoom Out
            this.zoomOutToolStripMenuItem.Name = "zoomOutToolStripMenuItem";
            this.zoomOutToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.zoomOutToolStripMenuItem.Text = "Zoom Out(-10%)";
            this.zoomOutToolStripMenuItem.Tag = "loc:Menu.ZoomOut";
            this.zoomOutToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.OemMinus)));
            this.zoomOutToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+'-'";
            this.zoomOutToolStripMenuItem.Click += new System.EventHandler(this.zoomOutToolStripMenuItem_Click);
            // 
            // size10ToolStripMenuItem
            // 
            this.size10ToolStripMenuItem.Name = "size10ToolStripMenuItem";
            this.size10ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size10ToolStripMenuItem.Text = "Size 10%";
            this.size10ToolStripMenuItem.Tag = "loc:Menu.Size10";
            this.size10ToolStripMenuItem.Click += new System.EventHandler(this.size10ToolStripMenuItem_Click);
            // 
            // size50ToolStripMenuItem
            // 
            this.size50ToolStripMenuItem.Name = "size50ToolStripMenuItem";
            this.size50ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size50ToolStripMenuItem.Text = "Size 50%";
            this.size50ToolStripMenuItem.Tag = "loc:Menu.Size50";
            this.size50ToolStripMenuItem.Click += new System.EventHandler(this.size50ToolStripMenuItem_Click);
            // 
            // size100ToolStripMenuItem
            // 
            this.size100ToolStripMenuItem.Name = "size100ToolStripMenuItem";
            this.size100ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size100ToolStripMenuItem.Text = "Size 100%";
            this.size100ToolStripMenuItem.Tag = "loc:Menu.Size100";
            this.size100ToolStripMenuItem.Click += new System.EventHandler(this.size100ToolStripMenuItem_Click);
            // 
            // size150ToolStripMenuItem
            // 
            this.size150ToolStripMenuItem.Name = "size150ToolStripMenuItem";
            this.size150ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size150ToolStripMenuItem.Text = "Size 150%";
            this.size150ToolStripMenuItem.Tag = "loc:Menu.Size150";
            this.size150ToolStripMenuItem.Click += new System.EventHandler(this.size150ToolStripMenuItem_Click);
            // 
            // size200ToolStripMenuItem
            // 
            this.size200ToolStripMenuItem.Name = "size200ToolStripMenuItem";
            this.size200ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size200ToolStripMenuItem.Text = "Size 200%";
            this.size200ToolStripMenuItem.Tag = "loc:Menu.Size200";
            this.size200ToolStripMenuItem.Click += new System.EventHandler(this.size200ToolStripMenuItem_Click);
            // 
            // size500ToolStripMenuItem
            // 
            this.size500ToolStripMenuItem.Name = "size500ToolStripMenuItem";
            this.size500ToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.size500ToolStripMenuItem.Text = "Size 500%";
            this.size500ToolStripMenuItem.Tag = "loc:Menu.Size500";
            this.size500ToolStripMenuItem.Click += new System.EventHandler(this.size500ToolStripMenuItem_Click);
            // 
            // keepAfloatToolStripMenuItem
            // 
            this.keepAfloatToolStripMenuItem.Name = "keepAfloatToolStripMenuItem";
            this.keepAfloatToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.A)));
            this.keepAfloatToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.keepAfloatToolStripMenuItem.Text = "Keep on top";
            this.keepAfloatToolStripMenuItem.Tag = "loc:Menu.AlwaysOnTop";
            this.keepAfloatToolStripMenuItem.Click += new System.EventHandler(this.keepAfloatToolStripMenuItem_Click);
            // 
            // dropShadowToolStripMenuItem
            // 
            this.dropShadowToolStripMenuItem.Name = "dropShadowToolStripMenuItem";
            this.dropShadowToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.D)));
            this.dropShadowToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.dropShadowToolStripMenuItem.Text = "Drop Shadow";
            this.dropShadowToolStripMenuItem.Tag = "loc:Menu.DropShadow";
            this.dropShadowToolStripMenuItem.Click += new System.EventHandler(this.dropShadowToolStripMenuItem_Click);
            // 
            // minimizeToolStripMenuItem
            // 
            this.minimizeToolStripMenuItem.Name = "minimizeToolStripMenuItem";
            this.minimizeToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.H)));
            this.minimizeToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.minimizeToolStripMenuItem.Text = "Minimize";
            this.minimizeToolStripMenuItem.Tag = "loc:Menu.Minimize";
            this.minimizeToolStripMenuItem.Click += new System.EventHandler(this.minimizeToolStripMenuItem_Click);
            // 
            // opacityParentMenu
            // 
            this.opacityParentMenu.DropDownItems.AddRange(new ToolStripItem[] {
                this.opacity100toolStripMenuItem,
                this.opacity90toolStripMenuItem,
                this.opacity80toolStripMenuItem,
                this.opacity50toolStripMenuItem,
                this.opacity30toolStripMenuItem,
            });
            this.opacityParentMenu.Name = "opacityParentMenu";
            this.opacityParentMenu.Size = new System.Drawing.Size(231, 22);
            this.opacityParentMenu.Text = "Opacity";
            this.opacityParentMenu.Tag = "loc:Menu.Opacity";
            // 
            // opacity100toolStripMenuItem
            // 
            this.opacity100toolStripMenuItem.Name = "opacity100toolStripMenuItem";
            this.opacity100toolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.opacity100toolStripMenuItem.Text = "100%";
            this.opacity100toolStripMenuItem.Click += new System.EventHandler(this.opacity100toolStripMenuItem_Click);
            // 
            // opacity90toolStripMenuItem
            // 
            this.opacity90toolStripMenuItem.Name = "opacity90toolStripMenuItem";
            this.opacity90toolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.opacity90toolStripMenuItem.Text = "90%";
            this.opacity90toolStripMenuItem.Click += new System.EventHandler(this.opacity90toolStripMenuItem_Click);
            // 
            // opacity80toolStripMenuItem
            // 
            this.opacity80toolStripMenuItem.Name = "opacity80toolStripMenuItem";
            this.opacity80toolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.opacity80toolStripMenuItem.Text = "80%";
            this.opacity80toolStripMenuItem.Click += new System.EventHandler(this.opacity80toolStripMenuItem_Click);
            // 
            // opacity50toolStripMenuItem
            // 
            this.opacity50toolStripMenuItem.Name = "opacity50toolStripMenuItem";
            this.opacity50toolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.opacity50toolStripMenuItem.Text = "50%";
            this.opacity50toolStripMenuItem.Click += new System.EventHandler(this.opacity50toolStripMenuItem_Click);
            // 
            // opacity30toolStripMenuItem
            // 
            this.opacity30toolStripMenuItem.Name = "opacity30toolStripMenuItem";
            this.opacity30toolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.opacity30toolStripMenuItem.Text = "30%";
            this.opacity30toolStripMenuItem.Click += new System.EventHandler(this.opacity30toolStripMenuItem_Click);
            // 
            // preferencesToolStripMenuItem
            // 
            this.preferencesToolStripMenuItem.Name = "preferencesToolStripMenuItem";
            this.preferencesToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.preferencesToolStripMenuItem.Text = "Preferences";
            this.preferencesToolStripMenuItem.Tag = "loc:Menu.Preferences";
            this.preferencesToolStripMenuItem.Click += new System.EventHandler(this.preferencesToolStripMenuItem_Click);
            this.preferencesToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.Oemcomma)));
            this.preferencesToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+,";
            // 
            // printToolStripMenuItem
            // 
            this.printToolStripMenuItem.Name = "printToolStripMenuItem";
            this.printToolStripMenuItem.ShortcutKeys = ((Keys)((Keys.Control | Keys.P)));
            this.printToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.printToolStripMenuItem.Text = "Print";
            this.printToolStripMenuItem.Tag = "loc:Menu.Print";
            this.printToolStripMenuItem.Click += new System.EventHandler(this.printToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.exitToolStripMenuItem.Text = "Exit Kiritori";
            this.exitToolStripMenuItem.Tag = "loc:Menu.Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // SnapWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.ControlBox = false;
            this.Controls.Add(this.pictureBox1);
            this.Cursor = Cursors.Hand;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SnapWindow";
            this.ShowInTaskbar = false;
            this.Text = "Kiritori - Snap";
            this.Load += new System.EventHandler(this.SnapWindow_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}


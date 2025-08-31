using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Views.LiveCapture
{
    partial class LivePreviewWindow
    {
        /// <summary>
        /// デザイナー変数
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中リソースの破棄
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // LivePreviewWindow
            // 
            this.ClientSize = new System.Drawing.Size(640, 360);
            this.Name = "LivePreviewWindow";
            this.Text = "Kiritori - Live Preview";
            this.Icon = Properties.Resources.AppIcon;
            this.ResumeLayout(false);
        }
        #endregion
    }
}

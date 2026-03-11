using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== ローカライズ（メニュー） =====
        private static void ApplyToolStripLocalization(ToolStripItemCollection items)
        {
            if (items == null) return;
            foreach (ToolStripItem it in items)
            {
                if (it.Tag is string tag && tag.StartsWith("loc:", StringComparison.Ordinal))
                {
                    it.Text = SR.T(tag.Substring(4));
                }
                if (it is ToolStripDropDownItem dd)
                {
                    ApplyToolStripLocalization(dd.DropDownItems);
                }
            }
        }

        private void ApplyAllContextMenusLocalization()
        {
            if (this.ContextMenuStrip != null)
                ApplyToolStripLocalization(this.ContextMenuStrip.Items);

            void Walk(Control c)
            {
                if (c.ContextMenuStrip != null)
                    ApplyToolStripLocalization(c.ContextMenuStrip.Items);

                if (c is MenuStrip ms)
                    ApplyToolStripLocalization(ms.Items);

                foreach (Control child in c.Controls)
                    Walk(child);
            }
            Walk(this);
        }

        #endregion

    }
}
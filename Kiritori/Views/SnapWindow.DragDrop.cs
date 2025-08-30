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
        #region ===== DnD =====
        private void SnapWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Any(IsValidImageFile))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void SnapWindow_DragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            var img = paths.FirstOrDefault(IsValidImageFile);
            if (string.IsNullOrEmpty(img)) return;

            try
            {
                this.setImageFromPath(img);
            }
            catch (Exception ex)
            {
                MessageBox.Show(SR.T("Text.DragDropFailed", "Failed open image") + ":\n" + ex.Message, "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (Directory.Exists(path)) return false;
            if (!File.Exists(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExts.Contains(ext);
        }

        #endregion

    }
}
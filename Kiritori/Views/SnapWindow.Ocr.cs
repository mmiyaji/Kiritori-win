using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Kiritori.Services.Logging;
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
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== OCR =====
        private async void RunOcrOnCurrentImage()
        {
            if (_ocrBusy) return;
            var src = _originalImage ?? pictureBox1?.Image as Bitmap;
            if (src == null)
            {
                ShowOverlay("No image");
                return;
            }

            _ocrBusy = true;
            try
            {
                using (var clone = new Bitmap(src))
                {
                    var ocrService = new OcrService();
                    var provider = ocrService.Get(null);

                    var opt = new OcrOptions
                    {
                        LanguageTag = "ja",
                        Preprocess = true,
                        CopyToClipboard = true
                    };

                    var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(true);

                    if (!string.IsNullOrEmpty(result.Text))
                    {
                        try { Clipboard.SetText(result.Text); } catch { }
                        ShowOverlay("OCR copied");
                        if (Properties.Settings.Default.isShowNotifyOCR)
                        {
                            ShowOcrToast(result.Text ?? "");
                        }
                    }
                    else
                    {
                        ShowOverlay("OCR no text");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("RunOcrOnCurrentImage error: " + ex.Message, "SnapWindow");
                ShowOverlay("OCR failed");
            }
            finally
            {
                _ocrBusy = false;
            }
        }

        private static DateTime _lastToastAt = DateTime.MinValue;
        private void ShowOcrToast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            string snippet = text;
            if (snippet.Length > 180) snippet = snippet.Substring(0, 180) + "…";

            var now = DateTime.Now;
            if ((now - _lastToastAt).TotalMilliseconds < 500) return;
            _lastToastAt = now;

            try
            {
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "open")
                    .AddText("Kiritori - OCR")
                    .AddText(snippet);

                if (PackagedHelper.IsPackaged())
                {
                    builder.Show(t =>
                    {
                        t.Tag = "kiritori-ocr";
                        t.Group = "kiritori";
                    });
                }
                else
                {
                    var xml = builder.GetToastContent().GetXml();
                    var toast = new ToastNotification(xml) { Tag = "kiritori-ocr", Group = "kiritori" };
                    ToastNotificationManager.CreateToastNotifier(NotificationService.GetAppAumid()).Show(toast);
                }
            }
            catch (Exception ex)
            {
                Log.Trace("Show() failed: " + ex, "Toast");
                var main = Application.OpenForms["MainApplication"] as Kiritori.MainApplication;
                main?.NotifyIcon?.ShowBalloonTip(1000, "Kiritori - OCR", snippet, ToolTipIcon.None);
            }
        }

        #endregion

    }
}
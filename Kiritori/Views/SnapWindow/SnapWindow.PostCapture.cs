using Kiritori.Helpers;
using Kiritori.Services.Logging;
using Kiritori.Services.Ocr;
using System;
using System.IO;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private bool _postCaptureActionScheduled = false;

        private CapturePostActionPreset GetCapturePostActionPreset()
        {
            var raw = Properties.Settings.Default.CapturePostActionPreset;
            CapturePostActionPreset preset;
            if (Enum.TryParse(raw, true, out preset))
                return preset;
            return CapturePostActionPreset.None;
        }

        internal void SchedulePostCaptureActionIfNeeded()
        {
            if (_postCaptureActionScheduled) return;
            if (CurrentLoadMethod != LoadMethod.Capture) return;

            var preset = GetCapturePostActionPreset();
            if (preset == CapturePostActionPreset.None) return;

            _postCaptureActionScheduled = true;

            if (IsHandleCreated && Visible)
            {
                BeginInvoke((Action)(() => _ = ExecutePostCaptureActionAsync(preset)));
                return;
            }

            EventHandler shown = null;
            shown = (s, e) =>
            {
                this.Shown -= shown;
                if (IsDisposed) return;
                BeginInvoke((Action)(() => _ = ExecutePostCaptureActionAsync(preset)));
            };
            this.Shown += shown;
        }

        private async System.Threading.Tasks.Task ExecutePostCaptureActionAsync(CapturePostActionPreset preset)
        {
            if (IsDisposed) return;

            switch (preset)
            {
                case CapturePostActionPreset.CopyImage:
                    CopyCurrentImageToClipboard(showOverlay: true);
                    break;
                case CapturePostActionPreset.RunOcr:
                    await RunPostCaptureOcrAsync(closeAfterSuccess: false);
                    break;
                case CapturePostActionPreset.CopyImageAndClose:
                    if (CopyCurrentImageToClipboard(showOverlay: true) && !IsDisposed)
                        Close();
                    break;
                case CapturePostActionPreset.RunOcrAndClose:
                    await RunPostCaptureOcrAsync(closeAfterSuccess: true);
                    break;
                case CapturePostActionPreset.SaveImage:
                    SavePostCaptureImage(showOverlay: true);
                    break;
                case CapturePostActionPreset.SaveImageAndClose:
                    if (SavePostCaptureImage(showOverlay: true) && !IsDisposed)
                        Close();
                    break;
                case CapturePostActionPreset.SaveImageAndCopy:
                    if (SavePostCaptureImage(showOverlay: true))
                        CopyCurrentImageToClipboard(showOverlay: true);
                    break;
                case CapturePostActionPreset.SaveImageAndRunOcr:
                    if (SavePostCaptureImage(showOverlay: true))
                        await RunPostCaptureOcrAsync(closeAfterSuccess: false);
                    break;
            }
        }

        private bool SavePostCaptureImage(bool showOverlay)
        {
            using (var imageToSave = GetCurrentBitmapClone())
            {
                if (imageToSave == null) return false;

                try
                {
                    var path = ResolvePostCaptureSavePath();
                    imageToSave.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    if (showOverlay) ShowOverlay("SAVED");
                    Log.Info("Post-capture image saved: " + path, "SnapWindow");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug("Post-capture save failed: " + ex.Message, "SnapWindow");
                    if (showOverlay) ShowOverlay("SAVE FAILED");
                    return false;
                }
            }
        }

        private string ResolvePostCaptureSavePath()
        {
            var folder = Properties.Settings.Default.CapturePostActionSaveFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                folder = string.IsNullOrWhiteSpace(pictures)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kiritori")
                    : Path.Combine(pictures, "Kiritori");
            }

            Directory.CreateDirectory(folder);

            var baseName = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(folder, baseName + ".png");
            int suffix = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, baseName + "-" + suffix.ToString("00") + ".png");
                suffix++;
            }

            return path;
        }

        internal bool CopyCurrentImageToClipboard(bool showOverlay)
        {
            using (var copy = GetCurrentBitmapClone())
            {
                if (copy == null) return false;

                try
                {
                    Clipboard.SetImage(copy);
                    if (showOverlay) ShowOverlay("COPIED");
                    Log.Info("Annotated image copied to clipboard", "SnapWindow");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug("Clipboard copy failed: " + ex.Message, "SnapWindow");
                    return false;
                }
            }
        }

        internal bool CopyOriginalImageToClipboard(bool showOverlay)
        {
            var source = main_image ?? _originalImage as System.Drawing.Bitmap;
            if (source == null) return false;

            using (var copy = new System.Drawing.Bitmap(source))
            {
                try
                {
                    Clipboard.SetImage(copy);
                    if (showOverlay) ShowOverlay("ORIGINAL COPIED");
                    Log.Info("Original image copied to clipboard", "SnapWindow");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug("Original clipboard copy failed: " + ex.Message, "SnapWindow");
                    return false;
                }
            }
        }

        private async System.Threading.Tasks.Task<bool> RunPostCaptureOcrAsync(bool closeAfterSuccess)
        {
            if (_ocrBusy) return false;

            using (var ocrCopy = GetCurrentBitmapClone())
            {
                if (ocrCopy == null)
                {
                    ShowOverlay("NO IMAGE FOR OCR");
                    return false;
                }

                _ocrBusy = true;
                try
                {
                    var text = await OcrFacade.RunAsync(
                        ocrCopy,
                        copyToClipboard: true,
                        preprocess: true);

                    if (!string.IsNullOrEmpty(text))
                    {
                        ShowOverlay("OCR RESULT COPIED");
                        if (Properties.Settings.Default.ShowNotificationOnOcr)
                            ShowOcrToast(text);
                        if (closeAfterSuccess && !IsDisposed)
                            Close();
                        return true;
                    }

                    ShowOverlay("OCR NOT DETECTED");
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Debug("RunPostCaptureOcrAsync error: " + ex.Message, "SnapWindow");
                    ShowOverlay("OCR FAILED");
                    return false;
                }
                finally
                {
                    _ocrBusy = false;
                }
            }
        }
    }
}

using Kiritori.Helpers;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class PrefForm
    {
        private sealed class CapturePostActionOption
        {
            public CapturePostActionPreset Preset { get; set; }
            public string Label { get; set; }
            public string Summary { get; set; }
            public override string ToString() { return Label; }
        }

        private static CapturePostActionOption PostActionOption(
            CapturePostActionPreset preset,
            string labelKey,
            string labelFallback,
            string summaryKey,
            string summaryFallback)
        {
            return new CapturePostActionOption
            {
                Preset = preset,
                Label = SR.T(labelKey, labelFallback),
                Summary = SR.T(summaryKey, summaryFallback)
            };
        }

        private static readonly CapturePostActionOption[] _capturePostActionOptions =
        {
            PostActionOption(CapturePostActionPreset.None, "PostCapture.Option.None.Label", "Do nothing", "PostCapture.Option.None.Summary", "Open the capture window normally after each new capture."),
            PostActionOption(CapturePostActionPreset.CopyImage, "PostCapture.Option.CopyImage.Label", "Copy image", "PostCapture.Option.CopyImage.Summary", "Copy the captured image to the clipboard as soon as the SnapWindow appears."),
            PostActionOption(CapturePostActionPreset.RunOcr, "PostCapture.Option.RunOcr.Label", "Run OCR", "PostCapture.Option.RunOcr.Summary", "Run OCR after capture and copy the recognized text to the clipboard."),
            PostActionOption(CapturePostActionPreset.CopyImageAndClose, "PostCapture.Option.CopyImageAndClose.Label", "Copy image and close", "PostCapture.Option.CopyImageAndClose.Summary", "Copy the image, then close the SnapWindow automatically."),
            PostActionOption(CapturePostActionPreset.RunOcrAndClose, "PostCapture.Option.RunOcrAndClose.Label", "Run OCR and close", "PostCapture.Option.RunOcrAndClose.Summary", "Run OCR, copy the text, then close the SnapWindow automatically."),
            PostActionOption(CapturePostActionPreset.SaveImage, "PostCapture.Option.SaveImage.Label", "Save image", "PostCapture.Option.SaveImage.Summary", "Save the captured image automatically."),
            PostActionOption(CapturePostActionPreset.SaveImageAndClose, "PostCapture.Option.SaveImageAndClose.Label", "Save image and close", "PostCapture.Option.SaveImageAndClose.Summary", "Save the image, then close the SnapWindow automatically."),
            PostActionOption(CapturePostActionPreset.SaveImageAndCopy, "PostCapture.Option.SaveImageAndCopy.Label", "Save image and copy", "PostCapture.Option.SaveImageAndCopy.Summary", "Save the image, then copy it to the clipboard."),
            PostActionOption(CapturePostActionPreset.SaveImageAndRunOcr, "PostCapture.Option.SaveImageAndRunOcr.Label", "Save image and run OCR", "PostCapture.Option.SaveImageAndRunOcr.Summary", "Save the image, then run OCR and copy the recognized text."),
        };

        private GroupBox grpPostCaptureActions;
        private Label labelPostCapturePreset;
        private ComboBox cmbPostCapturePreset;
        private Label labelPostCaptureSaveFolder;
        private TextBox txtPostCaptureSaveFolder;
        private Button btnPostCaptureBrowseFolder;
        private Button btnPostCaptureClearFolder;
        private Label labelPostCaptureHelp;

        private void EnsureCapturePostActionUi()
        {
            if (grpPostCaptureActions != null) return;
            if (grpAppSettings == null) return;

            grpPostCaptureActions = NewGroup(SR.T("PostCapture.Group", "After capture"));
            grpPostCaptureActions.Margin = new Padding(0, 8, 0, 0);

            var grid = NewGrid(3, 2);
            labelPostCapturePreset = NewRightLabel(SR.T("PostCapture.Preset", "Preset"));
            cmbPostCapturePreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill
            };
            labelPostCaptureSaveFolder = NewRightLabel(SR.T("PostCapture.SaveFolder", "Save folder"));
            txtPostCaptureSaveFolder = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Width = 300
            };
            btnPostCaptureBrowseFolder = new Button
            {
                Text = SR.T("Button.Browse", "Browse"),
                AutoSize = true,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnPostCaptureClearFolder = new Button
            {
                Text = SR.T("Button.Clear", "Clear"),
                AutoSize = true,
                Margin = new Padding(4, 0, 0, 0)
            };
            labelPostCaptureHelp = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                MaximumSize = new Size(420, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 2, 0, 0)
            };
            var folderFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            folderFlow.Controls.Add(txtPostCaptureSaveFolder);
            folderFlow.Controls.Add(btnPostCaptureBrowseFolder);
            folderFlow.Controls.Add(btnPostCaptureClearFolder);

            grid.Controls.Add(labelPostCapturePreset, 0, 0);
            grid.Controls.Add(cmbPostCapturePreset, 1, 0);
            grid.Controls.Add(labelPostCaptureSaveFolder, 0, 1);
            grid.Controls.Add(folderFlow, 1, 1);
            grid.Controls.Add(labelPostCaptureHelp, 1, 2);
            grpPostCaptureActions.Controls.Add(grid);

            var stack = grpAppSettings.Parent as TableLayoutPanel;
            if (stack != null)
            {
                stack.SuspendLayout();
                stack.Controls.Add(grpPostCaptureActions, 0, 1);
                if (grpHotkey != null)
                    stack.SetRow(grpHotkey, 2);
                stack.ResumeLayout(true);
            }
            else
            {
                tabGeneral.Controls.Add(grpPostCaptureActions);
                grpPostCaptureActions.Dock = DockStyle.Top;
                grpPostCaptureActions.BringToFront();
            }

            PopulateCapturePostActionPresetCombo();
            cmbPostCapturePreset.SelectedIndexChanged += (s, e) =>
            {
                if (_loadingUi) return;
                var option = cmbPostCapturePreset.SelectedItem as CapturePostActionOption;
                if (option == null) return;
                Properties.Settings.Default.CapturePostActionPreset = option.Preset.ToString();
                UpdateCapturePostActionSummary(option);
            };

            btnPostCaptureBrowseFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = SR.T("Dialog.SaveFolder.Description", "Select a folder to save captures.");
                    var current = Properties.Settings.Default.CapturePostActionSaveFolder;
                    if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                        fbd.SelectedPath = current;

                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        Properties.Settings.Default.CapturePostActionSaveFolder = fbd.SelectedPath;
                        UpdateCapturePostActionSaveFolderText();
                    }
                }
            };

            btnPostCaptureClearFolder.Click += (s, e) =>
            {
                Properties.Settings.Default.CapturePostActionSaveFolder = string.Empty;
                UpdateCapturePostActionSaveFolderText();
            };

            UpdateCapturePostActionSaveFolderText();
        }

        private void PopulateCapturePostActionPresetCombo()
        {
            if (cmbPostCapturePreset == null) return;

            cmbPostCapturePreset.BeginUpdate();
            cmbPostCapturePreset.Items.Clear();
            foreach (var option in _capturePostActionOptions)
                cmbPostCapturePreset.Items.Add(option);
            cmbPostCapturePreset.EndUpdate();
        }

        private void RestoreCapturePostActionPresetSelection()
        {
            if (cmbPostCapturePreset == null) return;

            var raw = Properties.Settings.Default.CapturePostActionPreset;
            CapturePostActionPreset preset;
            if (!Enum.TryParse(raw, true, out preset))
                preset = CapturePostActionPreset.None;

            var option = _capturePostActionOptions.FirstOrDefault(x => x.Preset == preset) ?? _capturePostActionOptions[0];
            cmbPostCapturePreset.SelectedItem = option;
            UpdateCapturePostActionSummary(option);
            UpdateCapturePostActionSaveFolderText();
        }

        private void UpdateCapturePostActionSummary(CapturePostActionOption option)
        {
            if (labelPostCaptureHelp == null) return;

            labelPostCaptureHelp.Text = option != null
                ? option.Summary + Environment.NewLine + SR.T("PostCapture.ScopeHelp", "Applies to new captures only. Opened files, clipboard images, and history reopens are not affected.")
                : string.Empty;
        }

        private void UpdateCapturePostActionSaveFolderText()
        {
            if (txtPostCaptureSaveFolder == null) return;

            var folder = Properties.Settings.Default.CapturePostActionSaveFolder;
            txtPostCaptureSaveFolder.Text = string.IsNullOrWhiteSpace(folder)
                ? SR.T("PostCapture.DefaultSaveFolder", "Pictures\\Kiritori")
                : folder;
        }
    }
}

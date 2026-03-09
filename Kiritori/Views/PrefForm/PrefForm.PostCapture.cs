using Kiritori.Helpers;
using System;
using System.Drawing;
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

        private static readonly CapturePostActionOption[] _capturePostActionOptions =
        {
            new CapturePostActionOption { Preset = CapturePostActionPreset.None, Label = "Do nothing", Summary = "Open the capture window normally after each new capture." },
            new CapturePostActionOption { Preset = CapturePostActionPreset.CopyImage, Label = "Copy image", Summary = "Copy the captured image to the clipboard as soon as the SnapWindow appears." },
            new CapturePostActionOption { Preset = CapturePostActionPreset.RunOcr, Label = "Run OCR", Summary = "Run OCR after capture and copy the recognized text to the clipboard." },
            new CapturePostActionOption { Preset = CapturePostActionPreset.CopyImageAndClose, Label = "Copy image and close", Summary = "Copy the image, then close the SnapWindow automatically." },
            new CapturePostActionOption { Preset = CapturePostActionPreset.RunOcrAndClose, Label = "Run OCR and close", Summary = "Run OCR, copy the text, then close the SnapWindow automatically." },
        };

        private GroupBox grpPostCaptureActions;
        private Label labelPostCapturePreset;
        private ComboBox cmbPostCapturePreset;
        private Label labelPostCaptureHelp;

        private void EnsureCapturePostActionUi()
        {
            if (grpPostCaptureActions != null) return;
            if (grpAppSettings == null) return;

            grpPostCaptureActions = NewGroup("After capture");
            grpPostCaptureActions.Margin = new Padding(0, 8, 0, 0);

            var grid = NewGrid(2, 2);
            labelPostCapturePreset = NewRightLabel("Preset");
            cmbPostCapturePreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill
            };
            labelPostCaptureHelp = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                MaximumSize = new Size(420, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 2, 0, 0)
            };

            grid.Controls.Add(labelPostCapturePreset, 0, 0);
            grid.Controls.Add(cmbPostCapturePreset, 1, 0);
            grid.Controls.Add(labelPostCaptureHelp, 1, 1);
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
        }

        private void UpdateCapturePostActionSummary(CapturePostActionOption option)
        {
            if (labelPostCaptureHelp == null) return;

            labelPostCaptureHelp.Text = option != null
                ? option.Summary + Environment.NewLine + "Applies to new captures only. Opened files, clipboard images, and history reopens are not affected."
                : string.Empty;
        }
    }
}

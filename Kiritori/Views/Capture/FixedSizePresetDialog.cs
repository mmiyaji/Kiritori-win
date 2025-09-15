using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kiritori.Views.Capture
{
    internal sealed class FixedSizePresetDialog : Form
    {
        private ComboBox _cmbPreset;
        private NumericUpDown _numW, _numH;
        private CheckBox _chkRemember;
        private Button _ok, _cancel;

        // PowerToys Image Resizer 風のプリセット例（自由に編集OK）
        private static readonly (string Label, int W, int H)[] _presets = new[]
        {
            ("1) 640×360 (16:9)",   640,  360),
            ("2) 800×600 (4:3)",    800,  600),
            ("3) 1024×768 (4:3)",   1024, 768),
            ("4) 1280×720 (HD)",    1280, 720),
            ("5) 1920×1080 (FHD)",  1920,1080),
            ("カスタム",               0,    0), // カスタムサイズ
        };

        public int OutW => (int)_numW.Value;
        public int OutH => (int)_numH.Value;
        public int SelectedPresetIndex => _cmbPreset.SelectedIndex;
        public bool Remember => _chkRemember.Checked;

        public FixedSizePresetDialog()
        {
            Text = "Fixed-size Capture";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen; // 画面中央
            ClientSize = new Size(360, 170);

            var lblPreset = new Label { Left = 12, Top = 14, Width = 60, Text = "プリセット" };
            _cmbPreset = new ComboBox { Left = 80, Top = 10, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var p in _presets) _cmbPreset.Items.Add(p.Label);
            _cmbPreset.SelectedIndexChanged += (s, e) => {
                int i = _cmbPreset.SelectedIndex;
                if (i >= 0 && i < _presets.Length - 1)
                { // カスタム以外
                    _numW.Value = Clamp(_presets[i].W, 10, 10000);
                    _numH.Value = Clamp(_presets[i].H, 10, 10000);
                }
            };

            var lblW = new Label { Left = 12, Top = 56, Width = 20, Text = "W" };
            _numW = new NumericUpDown { Left = 40, Top = 52, Width = 90, Minimum = 10, Maximum = 10000, Increment = 10, Value = 640 };
            var lblH = new Label { Left = 150, Top = 56, Width = 20, Text = "H" };
            _numH = new NumericUpDown { Left = 178, Top = 52, Width = 90, Minimum = 10, Maximum = 10000, Increment = 10, Value = 360 };

            _chkRemember = new CheckBox { Left = 12, Top = 88, Width = 200, Text = "このサイズを記憶する" };

            _ok = new Button { Left = 186, Top = 120, Width = 75, Text = "OK" };
            _cancel = new Button { Left = 267, Top = 120, Width = 75, Text = "Cancel" };
            _ok.Click += (s, e) => { SafeHide(); DialogResult = DialogResult.OK; };
            _cancel.Click += (s, e) => { SafeHide(); DialogResult = DialogResult.Cancel; };

            Controls.AddRange(new Control[] { lblPreset, _cmbPreset, lblW, _numW, lblH, _numH, _chkRemember, _ok, _cancel });
            AcceptButton = _ok; CancelButton = _cancel;

            // 前回値の復元
            int dw = 640, dh = 360, pi = _presets.Length - 1;
            try { dw = (int)Properties.Settings.Default["FixedCropWidth"]; } catch { }
            try { dh = (int)Properties.Settings.Default["FixedCropHeight"]; } catch { }
            try { pi = (int)Properties.Settings.Default["FixedCropPreset"]; } catch { }

            _numW.Value = Clamp(dw, 10, 10000);
            _numH.Value = Clamp(dh, 10, 10000);
            _cmbPreset.SelectedIndex = (pi >= 0 && pi < _presets.Length) ? pi : FindNearestPresetIndex((int)_numW.Value, (int)_numH.Value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // カーソル下のモニタ中央に出したい場合は下記に置換：
            // var scr = Screen.FromPoint(Cursor.Position).Bounds;
            // Location = new Point(scr.Left + (scr.Width-Width)/2, scr.Top + (scr.Height-Height)/2);
            try { _numW.Select(0, _numW.Text.Length); _numW.Focus(); } catch { }
        }

        private static decimal Clamp(int v, int min, int max) { if (v < min) return min; if (v > max) return max; return v; }

        private static int FindNearestPresetIndex(int w, int h)
        {
            int best = _presets.Length - 1; long bestScore = long.MaxValue;
            for (int i = 0; i < _presets.Length - 1; i++)
            { long dx = _presets[i].W - w, dy = _presets[i].H - h, sc = dx * dx + dy * dy; if (sc < bestScore) { bestScore = sc; best = i; } }
            return best;
        }

        private void SafeHide() { try { Opacity = 0; Hide(); } catch { } }

        public static bool TryPrompt(IWin32Window owner, out int w, out int h, out int presetIndex, out bool remember)
        {
            using (var dlg = new FixedSizePresetDialog())
            {
                var r = (owner != null) ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                if (r == DialogResult.OK)
                {
                    w = dlg.OutW; h = dlg.OutH; presetIndex = dlg.SelectedPresetIndex; remember = dlg.Remember;
                    // 記憶
                    if (remember)
                    {
                        try
                        {
                            Properties.Settings.Default["FixedCropWidth"] = w;
                            Properties.Settings.Default["FixedCropHeight"] = h;
                            Properties.Settings.Default["FixedCropPreset"] = presetIndex;
                            Properties.Settings.Default.Save();
                        }
                        catch { }
                    }
                    return true;
                }
            }
            w = h = presetIndex = 0; remember = false; return false;
        }
    }
}

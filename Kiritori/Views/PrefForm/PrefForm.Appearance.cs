using Kiritori.Helpers;
using Kiritori.Services.Logging;
using Kiritori.Views.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Globalization;

namespace Kiritori
{
    public partial class PrefForm
    {
        // Capture settings（簡素化）
        private GroupBox grpCaptureSettings;
        private CheckBox chkScreenGuide;   // show guide lines
        private CheckBox chkTrayNotify;    // notify on capture
        private CheckBox chkTrayNotifyOCR;    // notify on capture
        // private CheckBox chkPlaySound;     // play sound on capture
        private Label labelBgPreset;
        private ComboBox cmbBgPreset;
        private AlphaPreviewPanel previewBg;

        // Window settings（プリセット＋不透明度統合）
        private GroupBox grpWindowSettings;
        private CheckBox chkWindowShadow;
        private CheckBox chkAfloat;
        private CheckBox chkHighlightOnHover;
        private CheckBox chkShowOverlay;
        private Label labelHoverPreset;
        private ComboBox cmbHoverPreset;
        private Label labelHoverThickness;
        private NumericUpDown numHoverThickness;
        private AlphaPreviewPanel previewHover; // 透過は 100% 固定
        private Label labelDefaultOpacity;
        private TrackBar trackbarDefaultOpacity;
        private Label labelDefaultOpacityVal;

        private void BuildAppearanceTab()
        {

            var stackAppearance = NewStack();
            this.tabAppearance.Controls.Add(stackAppearance);

            // Capture settings（プリセット＋右プレビュー）
            this.grpCaptureSettings = NewGroup("Capture Settings");
            this.grpCaptureSettings.Tag = "loc:Text.CaptureSetting";
            var tlpCap = NewGrid(2, 2);

            this.chkScreenGuide = new CheckBox { Text = "Show guide lines", Checked = true, AutoSize = true, Tag = "loc:Text.ShowGuide" };
            this.chkTrayNotify = new CheckBox { Text = "Notify in tray on capture", AutoSize = true, Tag = "loc:Text.NotifyTray" };
            this.chkTrayNotifyOCR = new CheckBox { Text = "Notify in tray on OCR capture", AutoSize = true, Tag = "loc:Text.NotifyTrayOCR" };
            // this.chkPlaySound = new CheckBox { Text = "Play sound on capture", AutoSize = true, Enabled = false, Tag = "loc:Text.PlaySound" };

            var flowToggles = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
            };
            flowToggles.Controls.Add(this.chkScreenGuide);
            flowToggles.Controls.Add(this.chkTrayNotify);
            flowToggles.Controls.Add(this.chkTrayNotifyOCR);

            tlpCap.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Tag = "loc:Text.Option" }, 0, 0);
            tlpCap.Controls.Add(flowToggles, 1, 0);

            this.labelBgPreset = NewRightLabel("Background");
            this.labelBgPreset.Tag = "loc:Text.Background";
            this.cmbBgPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            this.cmbBgPreset.Items.AddRange(new object[] {
                "Transparent (0%)",
                "Dark (30%)",
                "Dark (60%)",
                "Light (30%)",
                "Light (60%)"
            });
            this.cmbBgPreset.SelectedIndex = 0;

            this.previewBg = new AlphaPreviewPanel { Height = 20, Width = 50, Anchor = AnchorStyles.Left, RgbColor = Color.Black, AlphaPercent = 0 };

            var flowPreset = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowPreset.Controls.Add(this.cmbBgPreset);
            flowPreset.Controls.Add(this.previewBg);

            tlpCap.Controls.Add(this.labelBgPreset, 0, 1);
            tlpCap.Controls.Add(flowPreset, 1, 1);

            this.grpCaptureSettings.Controls.Add(tlpCap);

            // 背景プリセットのイベント
            // Background preset -> Settings & Preview
            this.cmbBgPreset.SelectedIndexChanged += (s, e) =>
            {
                var S = Properties.Settings.Default;
                var sel = this.cmbBgPreset.SelectedItem?.ToString() ?? "";
                foreach (var p in _bgPresets)
                {
                    if (p.Name == sel)
                    {
                        S.CaptureBackgroundColor = p.Color;
                        S.CaptureBackgroundAlphaPercent = p.Alpha;
                        // バインド済みなのでプレビュー側は自動追従
                        break;
                    }
                }
            };


            // Window settings（プリセット＋太さ＋不透明度）
            this.grpWindowSettings = NewGroup("Window Settings");
            this.grpWindowSettings.Tag = "loc:Text.WindowSetting";
            this.grpWindowSettings.Margin = new Padding(0, 8, 0, 0);

            var tlpWin = NewGrid(2, 2);

            var flowWinToggles = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
            };
            this.chkWindowShadow = new CheckBox { Text = "Window shadow", Checked = true, AutoSize = true, Tag = "loc:Text.DropShadow" };
            this.chkAfloat = new CheckBox { Text = "Always on top", Checked = true, AutoSize = true, Tag = "loc:Text.AlwaysOnTop" };
            this.chkHighlightOnHover = new CheckBox { Text = "Highlight on hover", Checked = true, AutoSize = true, Tag = "loc:Text.HighlightOnHover" };
            this.chkShowOverlay = new CheckBox { Text = "Show overlay", Checked = true, AutoSize = true, Tag = "loc:Text.ShowOverlay" };
            flowWinToggles.Controls.Add(this.chkWindowShadow);
            flowWinToggles.Controls.Add(this.chkAfloat);
            flowWinToggles.Controls.Add(this.chkHighlightOnHover);
            flowWinToggles.Controls.Add(this.chkShowOverlay);

            tlpWin.Controls.Add(new Label { Text = "Options", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Tag = "loc:Text.Option" }, 0, 0);
            tlpWin.Controls.Add(flowWinToggles, 1, 0);

            // 色プリセット＋右プレビュー（透過 100% 固定）
            this.labelHoverPreset = NewRightLabel("Highlight color");
            this.labelHoverPreset.Tag = "loc:Text.HighlightColor";
            this.cmbHoverPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            this.cmbHoverPreset.Items.AddRange(new object[] {
                "Red", "Cyan", "Green", "Yellow", "Magenta", "Blue", "Orange", "Black", "White"
            });
            this.cmbHoverPreset.SelectedItem = "Cyan";

            this.previewHover = new AlphaPreviewPanel { Height = 20, Width = 50, Anchor = AnchorStyles.Left, RgbColor = Color.Cyan, AlphaPercent = 60 };

            var flowHover = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowHover.Controls.Add(this.cmbHoverPreset);
            flowHover.Controls.Add(this.previewHover);

            tlpWin.Controls.Add(this.labelHoverPreset, 0, 1);
            tlpWin.Controls.Add(flowHover, 1, 1);

            // 太さ（残す）
            this.labelHoverThickness = NewRightLabel("Highlight thickness (px)");
            this.labelHoverThickness.Tag = "loc:Text.HighlightThickness";
            this.numHoverThickness = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 2, Width = 60, Anchor = AnchorStyles.Left };
            tlpWin.Controls.Add(this.labelHoverThickness, 0, 2);
            tlpWin.Controls.Add(this.numHoverThickness, 1, 2);

            // Default Window Opacity（ここに統合）
            this.labelDefaultOpacity = NewRightLabel("Default opacity");
            this.labelDefaultOpacity.Tag = "loc:Text.WindowOpacity";
            this.trackbarDefaultOpacity = new TrackBar { Minimum = 10, Maximum = 100, TickFrequency = 10, Value = 100, Width = 240, Anchor = AnchorStyles.Left };
            this.labelDefaultOpacityVal = new Label { AutoSize = true, Text = "100%", Anchor = AnchorStyles.Left };

            var flowOpacity = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            flowOpacity.Controls.Add(this.trackbarDefaultOpacity);
            flowOpacity.Controls.Add(this.labelDefaultOpacityVal);

            tlpWin.Controls.Add(this.labelDefaultOpacity, 0, 3);
            tlpWin.Controls.Add(flowOpacity, 1, 3);

            this.grpWindowSettings.Controls.Add(tlpWin);

            // イベント：色プリセット変更
            this.cmbHoverPreset.SelectedIndexChanged += (s, e) =>
            {
                Color c = Color.Cyan;
                switch ((this.cmbHoverPreset.SelectedItem ?? "").ToString())
                {
                    case "Red": c = Color.Red; break;
                    case "Cyan": c = Color.Cyan; break;
                    case "Green": c = Color.Lime; break;
                    case "Yellow": c = Color.Yellow; break;
                    case "Magenta": c = Color.Magenta; break;
                    case "Blue": c = Color.Blue; break;
                    case "Orange": c = Color.Orange; break;
                    case "Black": c = Color.Black; break;
                    case "White": c = Color.White; break;
                }
                this.previewHover.RgbColor = c;
                this.previewHover.AlphaPercent = 100; // 透過なし
                this.previewHover.Invalidate();
            };

            // イベント：不透明度表示
            this.trackbarDefaultOpacity.Scroll += (s, e) =>
            {
                this.labelDefaultOpacityVal.Text = this.trackbarDefaultOpacity.Value + "%";
            };

            // stack へ追加（2グループのみ）
            stackAppearance.Controls.Add(this.grpCaptureSettings, 0, 0);
            stackAppearance.Controls.Add(this.grpWindowSettings, 0, 1);

            // =========================================================
            // Shortcuts タブ（縦積み）
            // =========================================================
            var stackShort = NewStack();
            this.tabShortcuts.Controls.Add(stackShort);

            this.grpShortcutsWindowOps = NewGroup("Window operations");
            this.grpShortcutsWindowOps.Tag = "loc:Text.WindowOperation";

            // 4行 × 5列 (左2列 + 仕切り + 右2列)
            this.tlpShortcutsWin = new TableLayoutPanel();
            this.tlpShortcutsWin.ColumnCount = 5;
            this.tlpShortcutsWin.RowCount = 7;
            this.tlpShortcutsWin.Dock = DockStyle.Top;
            this.tlpShortcutsWin.AutoSize = true;
            this.tlpShortcutsWin.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // 列スタイル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 左ラベル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f)); // 左テキスト
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12f));
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 右ラベル
            this.tlpShortcutsWin.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f)); // 右テキスト


            // セパレータ本体
            var sepPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Width = 1
            };
            sepPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(sepPanel.BackColor);
                using (var p = new Pen(Color.LightGray))
                {
                    int x = sepPanel.Width / 2;  // 真ん中に線を描画
                    e.Graphics.DrawLine(p, x, 0, x, sepPanel.Height);
                }
            };

            // 追加して全行に跨らせる
            this.tlpShortcutsWin.Controls.Add(sepPanel, 2, 0);
            this.tlpShortcutsWin.SetRowSpan(sepPanel, this.tlpShortcutsWin.RowCount);
            // 左段
            AddShortcutRow(this.tlpShortcutsWin, 0, "Close", out this.labelClose, out this.textBoxClose, "Ctrl + w, ESC", colOffset: 0, tagKey: "Text.Close");
            AddShortcutRow(this.tlpShortcutsWin, 1, "Minimize", out this.labelMinimize, out this.textBoxMinimize, "Ctrl + h", colOffset: 0, tagKey: "Text.Minimize");
            AddShortcutRow(this.tlpShortcutsWin, 2, "Always on top", out this.labelAfloat, out this.textBoxAfloat, "Ctrl + a", colOffset: 0, tagKey: "Text.AlwaysOnTop");
            AddShortcutRow(this.tlpShortcutsWin, 3, "Drop shadow", out this.labelDropShadow, out this.textBoxDropShadow, "Ctrl + d", colOffset: 0, tagKey: "Text.DropShadow");
            AddShortcutRow(this.tlpShortcutsWin, 4, "Hover highlight", out this.labelHoverHighlight, out this.textBoxHoverHighlight, "Ctrl + f", colOffset: 0, tagKey: "Text.HighlightOnHover");
            AddShortcutRow(this.tlpShortcutsWin, 5, "Move", out this.labelMove, out this.textBoxMove, "up/down/left/right", colOffset: 0, tagKey: "Text.Move");
            AddShortcutRow(this.tlpShortcutsWin, 6, "Run OCR", out this.labelOCR, out this.textBoxOCR, "Ctrl + t", colOffset: 0, tagKey: "Text.RunOCR");

            // 右段 (colOffset = 3 → ラベルが列3, TextBoxが列4)
            AddShortcutRow(this.tlpShortcutsWin, 0, "Paste", out this.labelPaste, out this.textBoxPaste, "Ctrl + v", colOffset: 3, tagKey: "Text.Paste");
            AddShortcutRow(this.tlpShortcutsWin, 1, "Copy", out this.labelCopy, out this.textBoxCopy, "Ctrl + c", colOffset: 3, tagKey: "Text.Copy");
            AddShortcutRow(this.tlpShortcutsWin, 2, "Save", out this.labelSave, out this.textBoxSave, "Ctrl + s", colOffset: 3, tagKey: "Text.Save");
            AddShortcutRow(this.tlpShortcutsWin, 3, "Print", out this.labelPrint, out this.textBoxPrint, "Ctrl + p", colOffset: 3, tagKey: "Text.Print");
            AddShortcutRow(this.tlpShortcutsWin, 4, "Zoom in", out this.labelZoomIn, out this.textBoxZoomIn, "Ctrl + +", colOffset: 3, tagKey: "Text.ZoomIn");
            AddShortcutRow(this.tlpShortcutsWin, 5, "Zoom out", out this.labelZoomOut, out this.textBoxZoomOut, "Ctrl + -", colOffset: 3, tagKey: "Text.ZoomOut");
            AddShortcutRow(this.tlpShortcutsWin, 6, "Zoom reset", out this.labelZoomOff, out this.textBoxZoomOff, "Ctrl + 0", colOffset: 3, tagKey: "Text.ZoomReset");

            this.grpShortcutsWindowOps.Controls.Add(this.tlpShortcutsWin);

            this.grpShortcutsCaptureOps = NewGroup("Capture operations");
            this.grpShortcutsCaptureOps.Tag = "loc:Text.CaptureOptionSetting";
            this.grpShortcutsCaptureOps.Margin = new Padding(0, 8, 0, 0);

            // 5行×4列 (左2列 + 右2列)
            this.tlpShortcutsCap = NewGrid(2, 3);

            // 左段
            AddShortcutRow(this.tlpShortcutsCap, 0, "Toggle guide lines", out this.labelScreenGuide, out this.textBoxScreenGuide, "Alt", colOffset: 0, tagKey: "Text.ToggleGuide");
            AddShortcutRow(this.tlpShortcutsCap, 1, "Square crop", out this.labelScreenSquare, out this.textBoxScreenSquare, "Shift", colOffset: 0, tagKey: "Text.SquareCrop");
            AddShortcutRow(this.tlpShortcutsCap, 2, "Snap", out this.labelScreenSnap, out this.textBoxScreenSnap, "Ctrl", colOffset: 0, tagKey: "Text.Snap");

            this.grpShortcutsCaptureOps.Controls.Add(this.tlpShortcutsCap);

            stackShort.Controls.Add(this.grpShortcutsWindowOps, 0, 0);
            stackShort.Controls.Add(this.grpShortcutsCaptureOps, 0, 1);
        }
    }
}

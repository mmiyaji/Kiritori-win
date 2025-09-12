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
        private GroupBox grpShortcutsWindowOps;
        private TableLayoutPanel tlpShortcutsWin;
        private Label labelClose; private TextBox textBoxClose;
        private Label labelMinimize; private TextBox textBoxMinimize;
        private Label labelAfloat; private TextBox textBoxAfloat;
        private Label labelDropShadow; private TextBox textBoxDropShadow;
        private Label labelHoverHighlight; private TextBox textBoxHoverHighlight;
        private Label labelMove; private TextBox textBoxMove;

        private GroupBox grpShortcutsCaptureOps;
        private TableLayoutPanel tlpShortcutsCap;
        private Label labelOCR; private TextBox textBoxOCR;
        private Label labelCopy; private TextBox textBoxCopy;
        private Label labelPaste; private TextBox textBoxPaste;
        private Label labelSave; private TextBox textBoxSave;
        private Label labelPrint; private TextBox textBoxPrint;
        private Label labelZoomIn; private TextBox textBoxZoomIn;
        private Label labelZoomOut; private TextBox textBoxZoomOut;
        private Label labelZoomOff; private TextBox textBoxZoomOff;
        private Label labelScreenGuide; private TextBox textBoxScreenGuide;
        private Label labelScreenSquare; private TextBox textBoxScreenSquare;
        private Label labelScreenSnap; private TextBox textBoxScreenSnap;

        private void BuildShortcutsTab()
        {
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

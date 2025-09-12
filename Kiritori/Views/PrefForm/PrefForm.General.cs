using Kiritori.Helpers;
using Kiritori.Views.Controls;
using Kiritori.Services.Ocr;
using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Globalization;
using Kiritori.Properties;

namespace Kiritori
{
    public partial class PrefForm
    {
        // Application Settings
        private GroupBox grpAppSettings;
        private Label labelLanguage;
        private ComboBox cmbLanguage;
        private Label labelOCRLanguage;
        private ComboBox cmbOCRLanguage;
        private Label labelStartup;
        private CheckBox chkRunAtStartup;
        private Button btnOpenStartupSettings;
        // private Label labelStartupInfo;
        private Label labelHistory;
        private NumericUpDown textBoxHistory;

        // Hotkeys
        private GroupBox grpHotkey;
        private Label labelHotkeyCapture;
        private TextBox textBoxKiritori;            // Capture (existing)
        private Label labelHotkeyCaptureOCR;
        private TextBox textBoxHotkeyCaptureOCR;
        private Label labelHotkeyLivePreview;       // Live Preview
        private TextBox textBoxHotkeyLivePreview;
        private Label labelHotkeyCapturePrev;       // Capture at previous region
        private TextBox textBoxCapturePrev;
        private void BuildGeneralTab()
        {
            var stackGeneral = NewStack();
            this.tabGeneral.Controls.Add(stackGeneral);

            // Application Settings
            this.grpAppSettings = NewGroup("Application Settings");
            this.grpAppSettings.Tag = "loc:Text.AppSetting";
            var tlpApp = NewGrid(3, 3);

            this.labelLanguage = NewRightLabel("Language");
            this.labelLanguage.Tag = "loc:Text.Language";
            this.cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            this.cmbLanguage.Items.AddRange(new object[] { "English (en)", "日本語 (ja)" });
            this.cmbLanguage.SelectedIndex = 0;

            this.labelOCRLanguage = NewRightLabel("OCR Language");
            this.labelOCRLanguage.Tag = "loc:Text.OcrLanguage";
            this.cmbOCRLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            PopulateOcrLanguageCombo();     // ← 利用可能言語で埋める
            RestoreOcrLanguageSelection();  // ← 保存値を選択

            this.labelStartup = NewRightLabel("Startup");
            this.labelStartup.Tag = "loc:Text.Startup";
            var flowStartup = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            this.chkRunAtStartup = new CheckBox
            {
                Text = "Run at startup",
                AutoSize = true,
                Enabled = false,
                Tag = "loc:Text.Runatstartup"
            };
            this.btnOpenStartupSettings = new Button
            {
                Text = "Open Startup",
                AutoSize = true,
                // Margin = new Padding(0, 0, 0, 5),
                Tag = "loc:Text.BtnStartupFolder"
            };
            this.btnOpenStartupSettings.Click += new EventHandler(this.btnOpenStartupSettings_Click);
            // this.labelStartupInfo = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Startup is managed by Windows.", Dock = DockStyle.Fill };
            // this.toolTip1.SetToolTip(this.labelStartupInfo, "Settings > Apps > Startup");
            flowStartup.Controls.Add(this.chkRunAtStartup);
            flowStartup.Controls.Add(this.btnOpenStartupSettings);

            this.labelHistory = NewRightLabel("History limit");
            this.labelHistory.Tag = "loc:Text.HistoryLimit";
            this.textBoxHistory = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 20, Width = 80, Anchor = AnchorStyles.Left };

            tlpApp.Controls.Add(this.labelLanguage,     0, 0);
            tlpApp.Controls.Add(this.cmbLanguage,       1, 0);

            tlpApp.Controls.Add(this.labelOCRLanguage,  0, 1);
            tlpApp.Controls.Add(this.cmbOCRLanguage,    1, 1);

            tlpApp.Controls.Add(this.labelStartup,      0, 2);

            var flowStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            flowStack.Controls.Add(flowStartup);
            // flowStack.Controls.Add(this.labelStartupInfo);

            tlpApp.Controls.Add(flowStack,              1, 2);
            tlpApp.Controls.Add(this.labelHistory,      0, 3);
            tlpApp.Controls.Add(this.textBoxHistory,    1, 3);

            this.grpAppSettings.Controls.Add(tlpApp);

            // Hotkeys（上マージンで間隔）
            this.grpHotkey = NewGroup("Hotkeys");
            this.grpHotkey.Tag = "loc:Text.Hotkeys";
            this.grpHotkey.Margin = new Padding(0, 8, 0, 0);

            var tlpHot = NewGrid(3, 4);
            this.labelHotkeyCapture = NewRightLabel("Image capture");
            this.labelHotkeyCapture.Tag = "loc:Text.ImageCapture";
            this.textBoxKiritori = new TextBox { Enabled = false, Width = 160, Text = "Ctrl + Shift + 5" };
            this.labelHotkeyCaptureOCR = NewRightLabel("OCR capture");
            this.labelHotkeyCaptureOCR.Tag = "loc:Text.OCRCapture";

            this.labelHotkeyLivePreview = NewRightLabel("Live preview");
            this.labelHotkeyLivePreview.Tag = "loc:Text.LivePreview";

            this.textBoxKiritori = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxKiritori).SetFromText(
                Properties.Settings.Default.HotkeyCapture, DEF_HOTKEY_CAP);
            ((HotkeyPicker)this.textBoxKiritori).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.image, (HotkeyPicker)this.textBoxKiritori);
            };

            this.textBoxHotkeyCaptureOCR = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxHotkeyCaptureOCR).SetFromText(
                Properties.Settings.Default.HotkeyOcr, DEF_HOTKEY_OCR);
            ((HotkeyPicker)this.textBoxHotkeyCaptureOCR).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.ocr, (HotkeyPicker)this.textBoxHotkeyCaptureOCR);
            };

            this.textBoxHotkeyLivePreview = new HotkeyPicker { ReadOnly = true, Width = 160 };
            ((HotkeyPicker)this.textBoxHotkeyLivePreview).SetFromText(
                Properties.Settings.Default.HotkeyLive, DEF_HOTKEY_LIVE);
            ((HotkeyPicker)this.textBoxHotkeyLivePreview).HotkeyPicked += (s, e) =>
            {
                SaveHotkeyFromPicker(CaptureMode.live, (HotkeyPicker)this.textBoxHotkeyLivePreview);
            };

            var btnResetCap = new Button { Text = "Reset", AutoSize = true };
            btnResetCap.Tag = "loc:Text.ResetDefault";
            btnResetCap.Click += (s, e) => ResetCaptureHotkeyToDefault();

            var btnResetOcr = new Button { Text = "Reset", AutoSize = true };
            btnResetOcr.Tag = "loc:Text.ResetDefault";
            btnResetOcr.Click += (s, e) => ResetOcrHotkeyToDefault();

            var btnResetLive = new Button { Text = "Reset", AutoSize = true };
            btnResetLive.Tag = "loc:Text.ResetDefault";
            btnResetLive.Click += (s, e) => ResetLiveHotkeyToDefault();

            this.labelHotkeyCapturePrev = NewRightLabel("Capture at previous region");
            this.labelHotkeyCapturePrev.Tag = "loc:Text.PreviousCapture";
            this.textBoxCapturePrev = new TextBox { Enabled = false, Width = 160, Text = "(disabled)" };

            tlpHot.Controls.Add(this.labelHotkeyCapture, 0, 0);
            tlpHot.Controls.Add(this.textBoxKiritori, 1, 0);
            tlpHot.Controls.Add(btnResetCap, 2, 0);
            tlpHot.Controls.Add(this.labelHotkeyCaptureOCR, 0, 1);
            tlpHot.Controls.Add(this.textBoxHotkeyCaptureOCR, 1, 1);
            tlpHot.Controls.Add(btnResetOcr, 2, 1);
            tlpHot.Controls.Add(this.labelHotkeyLivePreview, 0, 2);
            tlpHot.Controls.Add(this.textBoxHotkeyLivePreview, 1, 2);
            tlpHot.Controls.Add(btnResetLive, 2, 2);
            // tlpHot.Controls.Add(this.labelHotkeyCapturePrev, 0, 3);
            // tlpHot.Controls.Add(this.textBoxCapturePrev, 1, 3);

            this.grpHotkey.Controls.Add(tlpHot);

            // stack へ追加
            stackGeneral.Controls.Add(this.grpAppSettings, 0, 0);
            stackGeneral.Controls.Add(this.grpHotkey, 0, 1);

            this.cmbOCRLanguage.SelectedIndexChanged += (s, e) => SaveOcrLanguageSelection();
        }
        /// <summary>インストール済み OCR 言語を列挙してコンボに詰める（先頭に "Auto" を入れる）</summary>
        private void PopulateOcrLanguageCombo()
        {
            this.cmbOCRLanguage.Items.Clear();
            // 先頭は自動選択
            this.cmbOCRLanguage.Items.Add(new ComboItem("Auto (installed best)", "auto"));

            var tags = WindowsOcrHelper.GetAvailableLanguageTags();
            foreach (var tag in tags)
            {
                this.cmbOCRLanguage.Items.Add(new ComboItem(WindowsOcrHelper.ToDisplay(tag), tag));
            }

            if (this.cmbOCRLanguage.Items.Count == 0)
            {
                // OCR自体が使えないケース（Windows OCR API 未サポート等）
                this.cmbOCRLanguage.Items.Add(new ComboItem("(no OCR languages found)", "auto"));
            }
            this.cmbOCRLanguage.SelectedIndex = 0;
        }

        /// <summary>保存済みの OCRLanguage を UI に反映（無ければ "auto"）。</summary>
        private void RestoreOcrLanguageSelection()
        {
            var saved = Properties.Settings.Default.OcrLanguage;
            if (string.IsNullOrEmpty(saved)) saved = "auto";

            for (int i = 0; i < this.cmbOCRLanguage.Items.Count; i++)
            {
                var item = this.cmbOCRLanguage.Items[i] as ComboItem;
                if (item != null && string.Equals(item.Value, saved, StringComparison.OrdinalIgnoreCase))
                {
                    this.cmbOCRLanguage.SelectedIndex = i;
                    return;
                }
            }
            // マッチなし → auto
            this.cmbOCRLanguage.SelectedIndex = 0;
        }

        /// <summary>UI の選択を Settings に保存</summary>
        private void SaveOcrLanguageSelection()
        {
            var item = this.cmbOCRLanguage.SelectedItem as ComboItem;
            var val = (item != null) ? item.Value : "auto";
            if (string.IsNullOrEmpty(val)) val = "auto";

            if (!string.Equals(Properties.Settings.Default.OcrLanguage, val, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.OcrLanguage = val;
                Properties.Settings.Default.Save();
                // ログ出力など
                // AppLog.Info($"[OCR] Preferred language set: {val}");
            }
        }

        private sealed class ComboItem
        {
            public string Text;
            public string Value;
            public ComboItem(string text, string value) { Text = text; Value = value; }
            public override string ToString() { return Text; }
        }
    }
}

using Kiritori.Helpers;
using Kiritori.Startup;
using Kiritori.Views.Controls;
using Kiritori.Services.Settings;
using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;

namespace Kiritori
{
    public partial class PrefForm : Form
    {
        // =========================================================
        // ================ Fields / Properties ====================
        // =========================================================
        private static PrefForm _instance;
        private bool _initStartupToggle = false;
        private bool _initLang = false;
        // private bool _loadingUi = false;
        private bool _isDirty = false;          // 何か設定が変わった
        private int _suppressDirty = 0;        // 内部処理中は Dirty を抑制（Save/Reload 中など）
        private string _baselineHash = "";      // 保存済み（またはロード直後）の基準ハッシュ
        private System.Collections.Generic.Dictionary<string, string> _baselineMap =
                    new System.Collections.Generic.Dictionary<string, string>();

        private HotkeySpec DEF_HOTKEY_CAP   = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D5 };
        private HotkeySpec DEF_HOTKEY_OCR   = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D4 };
        private HotkeySpec DEF_HOTKEY_LIVE  = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D6 };
        private string _saveButtonDefaultText;
        private System.Windows.Forms.Timer _savedResetTimer;
        // =========================================================
        // ==================== Constructor ========================
        // =========================================================
        public PrefForm()
        {
            _initStartupToggle = true;
            _initLang = true;

            InitializeComponent();
            _loadingUi = true;

#if DEBUG
                DebugUiDecorator.Apply(this, new DebugUiOptions
                {
                    ShowBanner = true,        // 上部赤バナー
                    PrefixTitle = true,       // タイトルに [DEBUG]
                    PatchIcon = true,         // タスクバー/タイトルのアイコンに赤丸バッジ
                    Watermark = false,        // 透かし (大文字DEBUG) を背景に描画（必要なら true）
                });
#endif

            PopulatePresetCombos();
            WireUpDataBindings();
            SelectPresetComboFromSettings();

            // デザイナの勝手なバインディングを解除
            var b = this.chkRunAtStartup.DataBindings["Checked"];
            if (b != null) this.chkRunAtStartup.DataBindings.Remove(b);

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.StartPosition = FormStartPosition.CenterScreen;

            if (!IsInDesignMode())
            {
                this.Load += PrefForm_LoadAsync;

                // ← 共通ハンドラに寄せる
                this.Load += (_, __) => SafeApplyTextsAndLayout();
                SR.CultureChanged += () => SafeApplyTextsAndLayout();

                // // アプリアイコンのスケーリング
                // var src = Properties.Resources.icon_128x128;
                // picAppIcon.Image?.Dispose();
                // picAppIcon.Image = ScaleBitmap(src, 120, 120);
            }

            // Language コンボの初期化と保存値の復元（SelectedIndexChanged が走ってもガードできるように）
            InitLanguageCombo();

            _loadingUi = false;
            HookRuntimeEvents();
            WireAdvancedDirtyEvents();
            // ※ ここでは PropertyChanged を購読しない（初期化で発火するため）
            // 基準ハッシュの初期化も Load 完了後に行う
            // DumpSettingsKeys();
            // DumpControlBindings(this);
        }

        // =========================================================
        // ===================== Public API ========================
        // =========================================================
        /// <summary>
        /// 設定ウィンドウを常に1つだけ表示する。既にあれば前面化。
        /// </summary>
        public static void ShowSingleton(IWin32Window owner = null)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new PrefForm();
                _instance.FormClosed += (s, e) => _instance = null;

                if (owner != null) _instance.Show(owner);
                else _instance.Show();
            }
            else
            {
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;

                if (owner is Form f && _instance.Owner != f)
                    _instance.Owner = f;

                _instance.BringToFront();
                _instance.Activate();
            }
        }

        // =========================================================
        // =============== Form Lifecycle / Loads ==================
        // =========================================================
        private async void PrefForm_LoadAsync(object sender, EventArgs e)
        {
            try
            {
                if (PackagedHelper.IsPackaged())
                {
                    bool enabled = false;
                    try { enabled = await StartupManager.IsEnabledAsync(); }
                    catch { /* 失敗時は false のまま */ }

                    chkRunAtStartup.Checked = enabled;
                    chkRunAtStartup.Enabled = false;

                    toolTip1.SetToolTip(labelStartupInfo, "Managed by Windows Settings > Apps > Startup");
                    labelStartupInfo.Text = "Startup is managed by Windows.";
                }
                else
                {
                    // 非パッケージ：ショートカット有無で判定
                    string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");

                    chkRunAtStartup.Checked = File.Exists(shortcutPath);
                    chkRunAtStartup.Enabled = true;

                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string displayDir = startupDir.Replace(appData, "%APPDATA%");
                    labelStartupInfo.Text = "Shortcut: " + Path.Combine(displayDir, Application.ProductName + ".lnk");
                    toolTip1.SetToolTip(labelStartupInfo, shortcutPath);
                }

                // Info タブのバージョン表示
                UpdateVersionLabel();
            }
            finally
            {
                _initStartupToggle = false;
                chkRunAtStartup.CheckedChanged += ChkRunAtStartup_CheckedChanged;

                // 初期化がすべて終わってから基準を撮る
                using (SuppressDirtyScope())
                {
                    _baselineHash = SettingsDirtyTracker.ComputeSettingsHash();
                    _baselineMap = SettingsDirtyTracker.BuildSettingsSnapshotMap();
                    _isDirty = false;
                    UpdateDirtyUI();
                }
                // ここで初めて購読する（以後の変更だけ拾う）
                Properties.Settings.Default.PropertyChanged += (_, __) =>
                {
                    if (_suppressDirty > 0) return;
                    var now = SettingsDirtyTracker.ComputeSettingsHash();
                    _isDirty = (now != _baselineHash);
                    UpdateDirtyUI();
                };
                EnsureSavedUiInitialized();
            }
        }
        private bool EnsureSavedUiInitialized()
        {
            if (IsDisposed) return false;
            if (btnSaveSettings == null || btnSaveSettings.IsDisposed) return false;

            if (_saveButtonDefaultText == null)
                _saveButtonDefaultText = SR.T("Text.BtnSave", "Save");

            if (_savedResetTimer == null)
            {
                _savedResetTimer = new System.Windows.Forms.Timer { Interval = 1500 };
                _savedResetTimer.Tick += (s, e) =>
                {
                    _savedResetTimer.Stop();
                    if (!IsDisposed && btnSaveSettings != null && !btnSaveSettings.IsDisposed)
                    {
                        btnSaveSettings.Text = _saveButtonDefaultText;
                    }
                };
            }
            return true;
        }
        // 値が変わった（確定）→ Dirty ON
        private void OnAdvCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressDirty > 0) return;
            _isDirty = true;
            UpdateDirtyUI();
        }

        // 編集が終わった（確定）→ Dirty ON（保険）
        private void OnAdvCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressDirty > 0) return;
            _isDirty = true;
            UpdateDirtyUI();
        }

        // チェックボックス等：IsCurrentCellDirty の間に Commit して CellValueChanged を発火させる
        private void OnAdvCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_gridSettings == null) return;
            if (_gridSettings.IsCurrentCellDirty)
            {
                _gridSettings.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // 入力中のテキストでも“すぐ Save を光らせたい”場合（任意）
        private void OnAdvEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var tb = e.Control as TextBox;
            if (tb == null) return;

            // 多重登録回避のため、一旦外してから付ける
            tb.TextChanged -= AdvEditingTextChanged;
            tb.TextChanged += AdvEditingTextChanged;
        }

        private void AdvEditingTextChanged(object sender, EventArgs e)
        {
            if (_suppressDirty > 0) return;
            _isDirty = true;
            UpdateDirtyUI();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (this.Icon != null)
            {
                this.Icon.Dispose();
                this.Icon = null;
            }
            base.OnFormClosed(e);
        }

        // =========================================================
        // ================== UI Event Handlers ====================
        // =========================================================
        private bool _handlingStartupToggle = false;

        private async void ChkRunAtStartup_CheckedChanged(object sender, EventArgs e)
        {
            Log.Debug($"fired: init={_initStartupToggle}, handling={_handlingStartupToggle}, want={chkRunAtStartup.Checked}", "StartupToggle");
            if (_initStartupToggle || _handlingStartupToggle) return;
            _handlingStartupToggle = true;

            // 変更前の状態を保持（ロールバック用）
            var before = Properties.Settings.Default.RunAtStartup;
            var want   = chkRunAtStartup.Checked;

            // UI操作中は触らせない
            chkRunAtStartup.Enabled = false;
            var oldCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                bool ok = false;

                if (PackagedHelper.IsPackaged())
                {
                    if (want)
                    {
                        ok = await StartupManager.EnableAsync();           // ユーザー同意UIが出る場合あり（UIスレッドでOK）
                    }
                    else
                    {
                        await StartupManager.DisableAsync();
                        ok = !(await StartupManager.IsEnabledAsync());      // 実際に無効になったか確認
                    }
                }
                else
                {
                    // .lnk 作成/削除は STA 必須。専用 STA スレッドで実行して待機（UIはブロックしない）
                    await RunStaAsync(() => StartupManager.SetEnabled(want));
                    ok = true; // 例外が出なければ成功とみなす。必要なら .lnk の存在確認で厳密化
                }

                using (SuppressDirtyScope())
                {
                    if (ok)
                    {
                        // システム側が成功したときだけ設定値を確定
                        Properties.Settings.Default.RunAtStartup = want;
                    }
                    else
                    {
                        // ロールバック（イベント再発火を抑止）
                        _initStartupToggle = true;
                        chkRunAtStartup.Checked = before;
                        _initStartupToggle = false;
                    }
                }
            }
            catch (Exception ex)
            {
                // 失敗時は元に戻す（イベント再発火を抑止）
                _initStartupToggle = true;
                chkRunAtStartup.Checked = before;
                _initStartupToggle = false;

                MessageBox.Show(this, SR.F("Text.UnableSetStartup", ex.Message),
                    "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor.Current = oldCursor;
                chkRunAtStartup.Enabled = true;
                _handlingStartupToggle = false;
            }
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            using (SuppressDirtyScope())
            {
                ApplyAdvancedEditsToSettings();
                Properties.Settings.Default.Save();
                _baselineHash = SettingsDirtyTracker.ComputeSettingsHash();
                _baselineMap = SettingsDirtyTracker.BuildSettingsSnapshotMap();
                _isDirty = false;
                _gridSettings?.Invalidate();
                UpdateDirtyUI();
            }
            // this.Close();
        }

        private void btnCancelSettings_Click(object sender, EventArgs e)
        {
            using (SuppressDirtyScope())
            {
                Properties.Settings.Default.Reload();
                _baselineHash = SettingsDirtyTracker.ComputeSettingsHash();
                _baselineMap = SettingsDirtyTracker.BuildSettingsSnapshotMap();
                _isDirty = false;
                UpdateDirtyUI();
            }
            this.Close();
        }

        private void btnExitApp_Click(object sender, EventArgs e)
        {
            Log.Debug("Exit button clicked", "PrefForm");
            exitApp();

        }
        private void exitApp()
        {
            if (_isDirty)
            {
                int total;
                string diff = SettingsDirtyTracker.FormatSettingsDiff(6, out total, _baselineMap);
                string msg = TSafe("Text.ExitWithUnsavedChanges", "Do you want to save the changes?")
                        + Environment.NewLine + Environment.NewLine
                        + diff + (diff.Length > 0 ? Environment.NewLine : "")
                        + TSafe("Text.ExitWithUnsavedChangesTail",
                                "Yes: Save and Exit / No: Discard and Exit / Cancel: Stay in the app");

                var r = MessageBox.Show(
                    msg,
                    TSafe("Text.UnsavedChangesTitle", "Unsaved Changes"),
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning
                );

                if (r == DialogResult.Cancel) return;

                using (SuppressDirtyScope())
                {
                    if (r == DialogResult.Yes)
                    {
                        Properties.Settings.Default.Save();
                    }
                    else if (r == DialogResult.No)
                    {
                        Properties.Settings.Default.Reload();
                    }
                    _baselineHash = SettingsDirtyTracker.ComputeSettingsHash();
                    _baselineMap = SettingsDirtyTracker.BuildSettingsSnapshotMap();
                    _isDirty = false;
                    UpdateDirtyUI();
                }
                AppShutdown.ExitConfirmed = true;
                Application.Exit();
                return;
            }

            // 通常の終了確認（1回だけ）
            var result = MessageBox.Show(
                TSafe("Text.ConfirmExit", "Do you want to exit the application?"),
                TSafe("Text.ConfirmExitTitle", "Confirm Exit Kiritori"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (result == DialogResult.Yes)
            {
                AppShutdown.ExitConfirmed = true;
                Application.Exit();
            }

        }

        private void btnOpenStartupSettings_Click(object sender, EventArgs e)
        {
            if (PackagedHelper.IsPackaged())
                OpenSettingsStartupApps();
            else
                OpenStartupFolderOrSelectLink();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            labelLinkWebsite.LinkVisited = true;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://kiritori.ruhenheim.org",
                    UseShellExecute = true
                });
            }
            catch { /* 失敗時は無視 */ }
        }

        private void btnBgCustomColor_Click(object sender, System.EventArgs e)
        {
            using (var cd = new ColorDialog { Color = this.previewBg.RgbColor, FullOpen = true })
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    this.previewBg.RgbColor = cd.Color;   // バインドで Settings にも入る
                    this.previewBg.Invalidate();
                }
            }
        }

        // Language ComboBox 選択変更
        private void cmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initLang) return;

            var item = cmbLanguage.SelectedItem;
            var valProp = item?.GetType().GetProperty("Value");
            var culture = valProp?.GetValue(item)?.ToString() ?? "en";

            var cur = Properties.Settings.Default.UICulture ?? "";
            if (!string.Equals(cur, culture, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.UICulture = culture; // 変わる時だけセット
                // ここでは Save しない（Save ボタンで保存）
                SR.SetCulture(culture);
            }
        }

        private void SaveHotkeyFromPicker(CaptureMode mode, HotkeyPicker picker)
        {
            Log.Debug($"SaveHotkeyFromPicker(mode={mode})", "PrefForm");
            if (picker == null) { Log.Debug("picker is null", "PrefForm"); return; }
            if (picker.Value == null) { Log.Debug("picker.Value is null", "PrefForm"); return; }

            var pickedText = HotkeyUtil.ToText(picker.Value) ?? "";
            Log.Debug($"picked: '{pickedText}' (Mods={picker.Value.Mods}, Key={picker.Value.Key})", "PrefForm");

            // 現在値
            var currentCap  = Properties.Settings.Default.HotkeyCapture ?? "";
            var currentOcr  = Properties.Settings.Default.HotkeyOcr ?? "";
            var currentLive = Properties.Settings.Default.HotkeyLive ?? "";
            Log.Debug($"before: Cap='{currentCap}', Ocr='{currentOcr}', Live='{currentLive}'", "PrefForm");

            // 比較は大文字小文字を無視
            Func<string, string, bool> same = (a, b) =>
                string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

            // それぞれのデフォルト（重複時にUIへ戻すキー）

            // 重複チェック＆保存
            if (mode == CaptureMode.image)
            {
                if (same(pickedText, currentOcr) || same(pickedText, currentLive))
                {
                    MessageBox.Show(this,
                        SR.T("Prefs.Hotkey.DuplicateCapture", "Duplicate with another hotkey."),
                        "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ((HotkeyPicker)this.textBoxKiritori)?.SetFromText(currentCap, DEF_HOTKEY_CAP);
                    return;
                }
                Properties.Settings.Default.HotkeyCapture = pickedText;
            }
            else if (mode == CaptureMode.ocr)
            {
                if (same(pickedText, currentCap) || same(pickedText, currentLive))
                {
                    MessageBox.Show(this,
                        SR.T("Prefs.Hotkey.DuplicateOCR", "Duplicate with another hotkey."),
                        "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ((HotkeyPicker)this.textBoxHotkeyCaptureOCR)?.SetFromText(currentOcr, DEF_HOTKEY_OCR);
                    return;
                }
                Properties.Settings.Default.HotkeyOcr = pickedText;
            }
            else if (mode == CaptureMode.live)
            {
                if (same(pickedText, currentCap) || same(pickedText, currentOcr))
                {
                    MessageBox.Show(this,
                        SR.T("Prefs.Hotkey.DuplicateLive", "Duplicate with another hotkey."),
                        "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ((HotkeyPicker)this.textBoxHotkeyLivePreview)?.SetFromText(currentLive, DEF_HOTKEY_LIVE);
                    return;
                }
                Properties.Settings.Default.HotkeyLive = pickedText;
            }
            else
            {
                // 未知モードは何もしない
                Log.Debug("unknown mode; skipped", "PrefForm");
                return;
            }

            // ここで一回だけ保存
            Properties.Settings.Default.Save();
            Log.Debug($"saved: Cap='{Properties.Settings.Default.HotkeyCapture}', " +
                $"Ocr='{Properties.Settings.Default.HotkeyOcr}', Live='{Properties.Settings.Default.HotkeyLive}'", "PrefForm");
        }

        private void ResetCaptureHotkeyToDefault()
        {
            var defCapText  = HotkeyUtil.ToText(DEF_HOTKEY_CAP);
            var defOcrText  = HotkeyUtil.ToText(DEF_HOTKEY_OCR);
            var defLiveText = HotkeyUtil.ToText(DEF_HOTKEY_LIVE);

            // 現在値
            var curOcr  = Properties.Settings.Default.HotkeyOcr   ?? "";
            var curLive = Properties.Settings.Default.HotkeyLive  ?? "";

            // Capture 既定と衝突していたら OCR / Live も既定化
            if (string.Equals(defCapText, curOcr, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyOcr = defOcrText;
                (this.textBoxHotkeyCaptureOCR as HotkeyPicker)?.SetFromText(defOcrText, DEF_HOTKEY_OCR);
            }
            if (string.Equals(defCapText, curLive, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyLive = defLiveText;
                (this.textBoxHotkeyLivePreview as HotkeyPicker)?.SetFromText(defLiveText, DEF_HOTKEY_LIVE);
            }

            Properties.Settings.Default.HotkeyCapture = defCapText;
            (this.textBoxKiritori as HotkeyPicker)?.SetFromText(defCapText, DEF_HOTKEY_CAP);

            Properties.Settings.Default.Save();
        }

        private void ResetOcrHotkeyToDefault()
        {
            var defCapText  = HotkeyUtil.ToText(DEF_HOTKEY_CAP);
            var defOcrText  = HotkeyUtil.ToText(DEF_HOTKEY_OCR);
            var defLiveText = HotkeyUtil.ToText(DEF_HOTKEY_LIVE);

            var curCap  = Properties.Settings.Default.HotkeyCapture ?? "";
            var curLive = Properties.Settings.Default.HotkeyLive    ?? "";

            // OCR 既定と衝突していたら Capture / Live も既定化
            if (string.Equals(defOcrText, curCap, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyCapture = defCapText;
                (this.textBoxKiritori as HotkeyPicker)?.SetFromText(defCapText, DEF_HOTKEY_CAP);
            }
            if (string.Equals(defOcrText, curLive, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyLive = defLiveText;
                (this.textBoxHotkeyLivePreview as HotkeyPicker)?.SetFromText(defLiveText, DEF_HOTKEY_LIVE);
            }

            Properties.Settings.Default.HotkeyOcr = defOcrText;
            (this.textBoxHotkeyCaptureOCR as HotkeyPicker)?.SetFromText(defOcrText, DEF_HOTKEY_OCR);

            Properties.Settings.Default.Save();
        }

        private void ResetLiveHotkeyToDefault()
        {
            var defCapText  = HotkeyUtil.ToText(DEF_HOTKEY_CAP);
            var defOcrText  = HotkeyUtil.ToText(DEF_HOTKEY_OCR);
            var defLiveText = HotkeyUtil.ToText(DEF_HOTKEY_LIVE);

            var curCap = Properties.Settings.Default.HotkeyCapture ?? "";
            var curOcr = Properties.Settings.Default.HotkeyOcr      ?? "";

            // Live 既定と衝突していたら Capture / OCR も既定化
            if (string.Equals(defLiveText, curCap, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyCapture = defCapText;
                (this.textBoxKiritori as HotkeyPicker)?.SetFromText(defCapText, DEF_HOTKEY_CAP);
            }
            if (string.Equals(defLiveText, curOcr, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.HotkeyOcr = defOcrText;
                (this.textBoxHotkeyCaptureOCR as HotkeyPicker)?.SetFromText(defOcrText, DEF_HOTKEY_OCR);
            }

            Properties.Settings.Default.HotkeyLive = defLiveText;
            (this.textBoxHotkeyLivePreview as HotkeyPicker)?.SetFromText(defLiveText, DEF_HOTKEY_LIVE);

            Properties.Settings.Default.Save();
        }
        private void RebuildShortcutsInfo()
        {
            this.grpShortcuts.Controls.Clear();

            var tlpShortcutsInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(8),
            };
            tlpShortcutsInfo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlpShortcutsInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var capText = HotkeyTextForDisplay(Properties.Settings.Default.HotkeyCapture, DEF_HOTKEY_CAP);
            var ocrGlobalText = HotkeyTextForDisplay(Properties.Settings.Default.HotkeyOcr, DEF_HOTKEY_OCR);
            var liveGlobalText = HotkeyTextForDisplay(Properties.Settings.Default.HotkeyLive, DEF_HOTKEY_LIVE);

            AddShortcutInfo(tlpShortcutsInfo, capText,          "Start capture",                     tagKey: "Text.StartCapture");
            AddShortcutInfo(tlpShortcutsInfo, ocrGlobalText,    "Start OCR capture",         tagKey: "Text.StartOcrCapture");

            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + W, ESC",  "Close window",                      tagKey: "Text.CloseWindow");
            AddShortcutInfo(tlpShortcutsInfo, "Ctrl + C",       "Copy to clipboard",                 tagKey: "Text.CopyToClipboard");
            // AddShortcutInfo(tlpShortcutsInfo, "Ctrl + S",    "Save image",                         tagKey: "Text.SaveImage");
            // AddShortcutInfo(tlpShortcutsInfo, "Ctrl + T",       "Run OCR (copy result / show toast)", tagKey: "Text.RunOCRDesc");

            // AddShortcutInfo(tlpShortcutsInfo, "", "");
            AddShortcutInfo(tlpShortcutsInfo, "", "...and more. See Shortcuts",                       tagKey: "Text.AndMore");

            this.grpShortcuts.Controls.Add(tlpShortcutsInfo);
        }


        // =========================================================
        // ================= Layout / Painting =====================
        // =========================================================
        /// <summary>
        /// Infoタブのレイアウトをウィンドウ幅に応じて最適化（2カラム⇄1カラム、アイコンサイズ）
        /// </summary>
        private void LayoutInfoTabResponsive()
        {
            if (this.tlpInfoHeader == null || this.picAppIcon == null) return;

            // フォームの表示領域に対する閾値
            int w = this.tabInfo.ClientSize.Width;
            bool narrow = w < 560; // 狭いときは1カラムに折り返す

            // カラム切替
            this.tlpInfoHeader.SuspendLayout();
            if (narrow)
            {
                // 1カラム：アイコン→テキストの縦並び
                this.tlpInfoHeader.ColumnCount = 1;
                this.tlpInfoHeader.ColumnStyles.Clear();
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                // 再配置
                if (this.tlpInfoHeader.GetControlFromPosition(0, 0) != this.picAppIcon)
                {
                    this.tlpInfoHeader.Controls.Clear();
                    this.tlpInfoHeader.Controls.Add(this.picAppIcon, 0, 0);
                    // 右側のパネル（FlowLayoutPanel）は再取得せず既存参照を使う
                    var rightPanel = this.labelAppName.Parent;
                    this.tlpInfoHeader.Controls.Add(rightPanel, 0, 1);
                }
            }
            else
            {
                // 2カラム：左アイコン / 右テキスト
                this.tlpInfoHeader.ColumnCount = 2;
                this.tlpInfoHeader.ColumnStyles.Clear();
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
                this.tlpInfoHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                // 再配置（列0/列1へ）
                var rightPanel = this.labelAppName.Parent;
                if (this.tlpInfoHeader.GetControlFromPosition(0, 0) != this.picAppIcon ||
                    this.tlpInfoHeader.GetControlFromPosition(1, 0) != rightPanel)
                {
                    this.tlpInfoHeader.Controls.Clear();
                    this.tlpInfoHeader.Controls.Add(this.picAppIcon, 0, 0);
                    this.tlpInfoHeader.Controls.Add(rightPanel, 1, 0);
                }
            }
            this.tlpInfoHeader.ResumeLayout(true);

            // アイコンのスケール（小さい幅では96、大きい幅では128）
            int target = narrow ? 96 : 128;
            if (this.picAppIcon.Size.Width != target)
            {
                try
                {
                    var src = global::Kiritori.Properties.Resources.icon_128x128;
                    picAppIcon.Image?.Dispose();
                    picAppIcon.Image = ScaleBitmap(src, target, target);
                    picAppIcon.Size = new Size(target, target);
                }
                catch { /* ignore */ }
            }

            // テキストの最大幅（改行の自然発生を促す）
            var infoRight = this.labelAppName.Parent as FlowLayoutPanel;
            if (infoRight != null)
            {
                int padding = 24;
                int maxw = Math.Max(200, this.tlpInfoHeader.ClientSize.Width - (narrow ? 0 : (picAppIcon.Width + padding)));
                foreach (Control c in infoRight.Controls)
                {
                    c.MaximumSize = new Size(maxw, 0);
                    c.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                }
            }
        }

        private void SafeApplyTextsAndLayout()
        {
            // 破棄/未作成中は抜ける
            if (this.IsDisposed || !this.IsHandleCreated) return;

            // UI スレッドに移送（CultureChanged は別スレッド発火の可能性がある）
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke((Action)SafeApplyTextsAndLayout); } catch { }
                return;
            }

            try
            {
                // ここまで来れば UI は出来上がっている
                Localizer.Apply(this);
                ApplyDynamicTextsSafe();
                LayoutInfoTabResponsiveSafe();
            }
            catch { /* ここで握りつぶすよりログに出す方がよければ適宜 */ }
        }

        // =========================================================
        // =================== Helpers (UI) ========================
        // =========================================================
        private void InitLanguageCombo()
        {
            _initLang = true;

            this.cmbLanguage.DisplayMember = "Text";
            this.cmbLanguage.ValueMember = "Value";
            this.cmbLanguage.Items.Clear();
            this.cmbLanguage.Items.Add(new { Text = "English (en)", Value = "en" });
            this.cmbLanguage.Items.Add(new { Text = "日本語 (ja)", Value = "ja" });

            var saved = Properties.Settings.Default.UICulture;
            if (string.IsNullOrWhiteSpace(saved))
                saved = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            int index = 0;
            for (int i = 0; i < cmbLanguage.Items.Count; i++)
            {
                var item = cmbLanguage.Items[i];
                var valProp = item.GetType().GetProperty("Value");
                var val = valProp?.GetValue(item)?.ToString();
                if (val != null && val.Equals(saved, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            this.cmbLanguage.SelectedIndex = index;

            this.cmbLanguage.SelectedIndexChanged += cmbLanguage_SelectedIndexChanged;
            _initLang = false;
        }

        private void UpdateVersionLabel()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;

            this.labelVersion.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Version {0} Build Date: {1:dd MMM, yyyy}",
                ver,
                DateTime.Now
            );
        }

        private static void OpenSettingsStartupApps()
        {
            const string uri = "ms-settings:startupapps";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = uri,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show(
                        "Could not open Windows Settings.\n" +
                        "Please open 'Settings > Apps > Startup' manually and enable Kiritori.",
                        "Startup Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OpenStartupFolderOrSelectLink()
        {
            try
            {
                string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string lnk = Path.Combine(startupDir, Application.ProductName + ".lnk");

                if (File.Exists(lnk))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{lnk}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = startupDir,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open Startup folder.\n" + ex.Message,
                    "Startup Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void SetStartupShortcut(bool enable)
        {
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");
            if (!enable) { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); return; }

            string targetPath = Application.ExecutablePath;
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // WScript.Shell
            object shell = Activator.CreateInstance(t);
            object shortcut = t.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            try
            {
                t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 最初にスキップ判定（ここで終わればダイアログは出ない）
            if (AppShutdown.ExitConfirmed)
            {
                base.OnFormClosing(e);
                return;
            }
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                base.OnFormClosing(e);
                return;
            }

            if (_isDirty)
            {
                var r = MessageBox.Show(
                    TSafe("Text.ConfirmSaveChanges", "Do you want to save the changes?"),
                    TSafe("Text.ConfirmSaveChangesTitle", "Unsaved Changes"),
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question
                );

                if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return; // baseは呼ばない
                }

                using (SuppressDirtyScope())
                {
                    if (r == DialogResult.Yes)
                    {
                        Properties.Settings.Default.Save();
                    }
                    else // No = 破棄
                    {
                        Properties.Settings.Default.Reload();
                    }
                    _baselineHash = SettingsDirtyTracker.ComputeSettingsHash();
                    _baselineMap = SettingsDirtyTracker.BuildSettingsSnapshotMap();
                    _isDirty = false;
                    UpdateDirtyUI();
                }
                // ここまで来たら継続して閉じる
            }

            base.OnFormClosing(e);
        }


        // =========================================================
        // =================== Helpers (General) ===================
        // =========================================================
        private static bool IsInDesignMode()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
            var exe = Application.ExecutablePath ?? string.Empty;
            return exe.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase)
                || exe.EndsWith("Blend.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static Bitmap ScaleBitmap(Image src, int w, int h)
        {
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ApplyDynamicTextsSafe()
        {
            // 破棄・ハンドル未作成は何もしない
            if (IsDisposed || !IsHandleCreated) return;

            // 別スレッド発火（CultureChanged など）に備えて UI スレッドへ
            if (InvokeRequired) { try { BeginInvoke((Action)ApplyDynamicTextsSafe); } catch { } return; }

            // 翻訳キーが無い/例外でも落ちないようフォールバック
            string T(string key, string fallback)
            {
                try { return SR.T(key); } catch { return fallback; }
            }

            // タイトル
            Text = string.Format("{0} - {1}", T("App.Name", "Kiritori"), T("PrefForm.Title", "Preferences"));

            // MSIX/非MSIXで変わるボタン
            if (btnOpenStartupSettings != null && !btnOpenStartupSettings.IsDisposed)
            {
                btnOpenStartupSettings.Text = PackagedHelper.IsPackaged()
                    ? TSafe("Text.BtnStartupSetting", "Startup settings")
                    : TSafe("Text.BtnStartupFolder", "Open Startup folder");
            }

            // 起動管理の説明＆ツールチップ（あれば）
            if (labelStartupInfo != null && !labelStartupInfo.IsDisposed && toolTip1 != null)
            {
                if (PackagedHelper.IsPackaged())
                {
                    labelStartupInfo.Text = TSafe("Text.StartupManaged", "Startup is managed by Windows.");
                    toolTip1.SetToolTip(labelStartupInfo, TSafe("Text.StartupManagedTip", "Settings > Apps > Startup"));
                }
                // 非 MSIX側の表示は必要ならここに
            }

            // バージョン/ビルド日（ファイルの更新時刻を採用：再現性◎）
            var asm = Assembly.GetExecutingAssembly();
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? Application.ProductVersion;
            var buildDate = GetAssemblyWriteTime(asm);

            if (labelVersion != null && !labelVersion.IsDisposed)
                labelVersion.Text = string.Format(CultureInfo.InvariantCulture,
                    "Version {0}  Build Date: {1:dd MMM, yyyy}", infoVer, buildDate);

            if (labelCopyRight != null && !labelCopyRight.IsDisposed)
                labelCopyRight.Text = "© 2013–" + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);

            // 最後にレイアウト
            LayoutInfoTabResponsiveSafe();
            UpdateDirtyUI();
        }

        // Dirty 設定を抑制するスコープヘルパ
        private IDisposable SuppressDirtyScope()
        {
            _suppressDirty++;
            return new ActionOnDispose(delegate { _suppressDirty--; });
        }

        private sealed class ActionOnDispose : IDisposable
        {
            private readonly Action _a; public ActionOnDispose(Action a) { _a = a; }
            public void Dispose() { if (_a != null) _a(); }
        }
        private void WireAdvancedDirtyEvents()
        {
            if (_gridSettings == null || _gridSettings.IsDisposed) return;

            // 既存ハンドラの二重登録を避けるため一旦外す
            _gridSettings.CellValueChanged -= OnAdvCellValueChanged;
            _gridSettings.CurrentCellDirtyStateChanged -= OnAdvCurrentCellDirtyStateChanged;
            _gridSettings.CellEndEdit -= OnAdvCellEndEdit;
            _gridSettings.EditingControlShowing -= OnAdvEditingControlShowing;

            // 値が変わったら Dirty
            _gridSettings.CellValueChanged += OnAdvCellValueChanged;
            _gridSettings.CellEndEdit += OnAdvCellEndEdit;

            // チェックボックス等は Commit が必要
            _gridSettings.CurrentCellDirtyStateChanged += OnAdvCurrentCellDirtyStateChanged;

            // テキスト編集中にも（確定前に）Dirty表示したい場合
            _gridSettings.EditingControlShowing += OnAdvEditingControlShowing;
        }

        private void UpdateDirtyUI()
        {
            // UIスレッド保証
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateDirtyUI)); } catch { /* フォーム終了中 */ }
                return;
            }

            if (IsDisposed) return;
            if (!EnsureSavedUiInitialized()) return;

            // フォームタイトルの " *" 管理（nullセーフ）
            var title = this.Text ?? string.Empty;
            bool hasMark = title.EndsWith(" *", StringComparison.Ordinal);

            // Saveボタンの有効・無効
            btnSaveSettings.Enabled = _isDirty;

            if (_isDirty)
            {
                if (!hasMark) this.Text = title + " *";

                // Dirtyになったら即「通常表示」に戻す
                btnSaveSettings.Text = _saveButtonDefaultText;
                // 進行中の「Saved ✓」復帰タイマーは止める
                _savedResetTimer.Stop();
            }
            else
            {
                if (hasMark && title.Length >= 2)
                    this.Text = title.Substring(0, title.Length - 2);

                // 保存直後の軽いフィードバック
                btnSaveSettings.Text = _saveButtonDefaultText + " ✓";
                btnSaveSettings.Enabled = false;
                _savedResetTimer.Stop();
                _savedResetTimer.Start();
            }
        }

        private static DateTime GetAssemblyWriteTime(Assembly asm)
        {
            try { return File.GetLastWriteTime(asm.Location); }
            catch { return DateTime.Now; }
        }

        private void LayoutInfoTabResponsiveSafe()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke((Action)LayoutInfoTabResponsiveSafe); } catch { } return; }

            LayoutInfoTabResponsive();
        }

        // フォールバック付き翻訳
        private static string TSafe(string key, string fallback)
        {
            try
            {
                var s = SR.T(key); // 既存の単一引数版だけを前提
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }

        // === Settings のスナップショット（ハッシュ）を作る ===
        static void DumpControlBindings(Form f)
        {
            Log.Debug("=== Control Bindings to Settings ===", "PrefForm");
            void Walk(Control c)
            {
                foreach (Binding b in c.DataBindings)
                {
                    if (object.ReferenceEquals(b.DataSource, Kiritori.Properties.Settings.Default))
                        Log.Debug($"- {c.Name}.{b.PropertyName} <= {b.BindingMemberInfo.BindingField}", "PrefForm");
                }
                foreach (Control child in c.Controls) Walk(child);
                if (f.MainMenuStrip != null)
                    foreach (ToolStripItem it in f.MainMenuStrip.Items)
                        ; // （メニューに設定バインドしてなければスキップでOK）
            }
            Walk(f);
        }
        private static Task RunStaAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            var th = new Thread(() =>
            {
                try { action(); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
            return tcs.Task;
        }
        private static string HotkeyTextForDisplay(string stored, HotkeySpec fallback, bool addSpaces = true)
        {
            var spec = Kiritori.Helpers.HotkeyUtil.ParseOrDefault(stored, fallback);
            var s = Kiritori.Helpers.HotkeyUtil.ToText(spec);  // 例: "Ctrl+Shift+5"
            return addSpaces ? s.Replace("+", " + ") : s;
        }

        // =========================================================
        // ================== Nested UI Controls ===================
        // =========================================================

        internal static class AppShutdown
        {
            public static bool ExitConfirmed; // 終了確認/未保存処理は済んだ
        }
    }
}

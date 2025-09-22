using Kiritori.Helpers;
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

namespace Kiritori
{
    public partial class PrefForm
    {
        // ===== UI =====
        private bool _initLogTab;
        private RichTextBox _rtbLog;
        private ComboBox _cmbLogLevel;
        private Button _btnClear;
        private CheckBox _chkAutoScroll;

        // ローカル同期（UI 追記時の多重呼び出し抑制用）
        private readonly object _uiAppendSync = new object();

        // 画面に表示するレベル（None は出さない）
        private LogLevel _viewLevel = LogLevel.Info;

        // Settings キー
        private const string SettingsKey_ViewLevel = "LogView.Level";
        private const string SettingsKey_AutoScroll = "LogView.AutoScroll";

        /// <summary>コンストラクタの末尾などで呼んでください。</summary>
        private void InitLogTab()
        {
            _initLogTab = true;
            try
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                };
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 上部バー
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 出力

                // --- 上部バー ---
                var bar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(8, 8, 8, 4)
                };

                _cmbLogLevel = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 140,
                    Tag = "loc:Text.LogLevel"
                };
                _cmbLogLevel.Items.AddRange(new object[]
                {
                    "OFF", "INFO", "DEBUG", "TRACE", "WARN", "ERROR", "FATAL"
                });
                _cmbLogLevel.SelectedIndexChanged += (s, e) =>
                {
                    if (_initLogTab) return;
                    _viewLevel = IndexToLevel(_cmbLogLevel.SelectedIndex);
                    RedrawByFilter();          // 共有バッファから引き直し
                    SaveLogViewPrefs();

                    var opt = Log.GetCurrentOptions();
                    opt.MinLevel = _viewLevel; // OFF なら停止
                    Log.Configure(opt);
                    Log.Info($"Log level changed to {_viewLevel}", "Preferences");
                };

                _btnClear = new Button
                {
                    Text = "Clear View",
                    AutoSize = true,
                    Tag = "loc:Text.ClearView"
                };
                _btnClear.Click += (s, e) =>
                {
                    // 画面だけクリア（共有バッファは残す）
                    _rtbLog.Clear();
                };

                _chkAutoScroll = new CheckBox
                {
                    Text = "Auto Scroll to Latest",
                    AutoSize = true,
                    Checked = true,
                    Tag = "loc:Text.AutoScrollToLatest"
                };
                _chkAutoScroll.CheckedChanged += (s, e) => SaveLogViewPrefs();

                bar.Controls.Add(new Label
                {
                    AutoSize = true,
                    Padding = new Padding(0, 6, 6, 0),
                    Tag = "loc:Text.LogLevel"
                });
                bar.Controls.Add(_cmbLogLevel);
                bar.Controls.Add(_btnClear);
                bar.Controls.Add(_chkAutoScroll);

                // --- 出力部 ---
                _rtbLog = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    DetectUrls = false,
                    WordWrap = false,
                    HideSelection = false,
                    BorderStyle = BorderStyle.None,
                    BackColor = SystemColors.Window,
                    Font = new Font("Consolas", 9.0f, FontStyle.Regular, GraphicsUnit.Point)
                };

                // ルートに載せる
                root.Controls.Add(bar, 0, 0);
                root.Controls.Add(_rtbLog, 0, 1);

                // 既存の TabControl に追加（名前は環境に合わせて）
                this.tabLogs.Controls.Add(root);

                // 既定値＆保存値の復元
                LoadLogViewPrefs();
                var engineLv = Log.GetCurrentOptions()?.MinLevel ?? LogLevel.Info;
                _viewLevel = engineLv;
                _cmbLogLevel.SelectedIndex = LevelToIndex(_viewLevel);

                // 共有バッファから初期再描画
                RedrawByFilter();
            }
            finally
            {
                _initLogTab = false;
            }

            // ロガーへ接続（複数フォームでも OK：各フォームが購読）
            Log.LogWritten += OnLogWritten;

            Log.Info("Log tab initialized", "Preferences");
        }

        private static LogLevel IndexToLevel(int idx)
        {
            switch (idx)
            {
                case 0: return LogLevel.Off;
                case 1: return LogLevel.Info;
                case 2: return LogLevel.Debug;
                case 3: return LogLevel.Trace;
                case 4: return LogLevel.Warn;
                case 5: return LogLevel.Error;
                case 6: return LogLevel.Fatal;
                default: return LogLevel.Info;
            }
        }

        private int LevelToIndex(LogLevel lv)
        {
            if (lv == LogLevel.Off) return 0;
            switch (lv)
            {
                case LogLevel.Info: return 1;
                case LogLevel.Debug: return 2;
                case LogLevel.Trace: return 3;
                case LogLevel.Warn: return 4;
                case LogLevel.Error: return 5;
                case LogLevel.Fatal: return 6;
                default: return 1;
            }
        }

        private void LoadLogViewPrefs()
        {
            try
            {
                var lvString = Properties.Settings.Default[SettingsKey_ViewLevel] as string;
                LogLevel lv;
                if (!string.IsNullOrEmpty(lvString) && Enum.TryParse(lvString, out lv))
                    _viewLevel = lv;

                var auto = Properties.Settings.Default[SettingsKey_AutoScroll] as string;
                bool autoScroll;
                _chkAutoScroll.Checked = bool.TryParse(auto, out autoScroll) ? autoScroll : true;
            }
            catch
            { 
                Log.Debug("LoadLogViewPrefs: Could not load log view prefs", "Preferences");
            }

            _cmbLogLevel.SelectedIndex = LevelToIndex(_viewLevel);
        }

        private void SaveLogViewPrefs()
        {
            try
            {
                Properties.Settings.Default[SettingsKey_ViewLevel] = _viewLevel.ToString();
                Properties.Settings.Default[SettingsKey_AutoScroll] = _chkAutoScroll.Checked.ToString();
                Properties.Settings.Default.Save();
            }
            catch
            { 
                Log.Debug("SaveLogViewPrefs: Could not save log view prefs", "Preferences");
            }
        }

        /// <summary>外部から PrefForm のビューにも流したい場合に使用。</summary>
        public void AppendLog(LogLevel level, string message)
        {
            var now = DateTime.Now;
            // 共有バッファに積む（必ず先）
            LogViewSharedBuffer.Add(now, level, message);

            // 表示フィルタに合致すれば即時追記
            if (level >= _viewLevel && _viewLevel != LogLevel.Off)
            {
                AppendToRtb(new Kiritori.Services.Logging.LogItem
                {
                    Time = now,
                    Level = level,
                    Message = message
                });
            }
        }

        private void AppendToRtb(Kiritori.Services.Logging.LogItem item)
        {
            if (this.IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => AppendToRtb(item)));
                return;
            }

            // まとめて追記する箇所があるのでレイアウト抑制
            lock (_uiAppendSync)
            {
                var sb = new StringBuilder(64 + (item.Message != null ? item.Message.Length : 0));
                sb.Append(FormatTimestamp(item.Time)).Append('\t');
                sb.Append(LevelShort(item.Level)).Append('\t');
                sb.Append(item.Message ?? string.Empty);
                var line = sb.ToString();
                if (!line.EndsWith(Environment.NewLine))
                    line += Environment.NewLine;

                _rtbLog.SuspendLayout();
                try
                {
                    _rtbLog.AppendText(line);

                    if (_chkAutoScroll.Checked)
                    {
                        _rtbLog.SelectionStart = _rtbLog.TextLength;
                        _rtbLog.SelectionLength = 0;
                        _rtbLog.ScrollToCaret();
                    }
                }
                finally
                {
                    _rtbLog.ResumeLayout();
                }
            }
        }

        private static string LevelShort(LogLevel lv)
        {
            switch (lv)
            {
                case LogLevel.Trace: return "TRACE";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info: return "INFO";
                case LogLevel.Warn: return "WARN";
                case LogLevel.Error: return "ERROR";
                case LogLevel.Fatal: return "FATAL";
                case LogLevel.Off: return "OFF";
                default: return lv.ToString().ToUpperInvariant();
            }
        }

        private void RedrawByFilter()
        {
            if (this.IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)RedrawByFilter);
                return;
            }

            _rtbLog.SuspendLayout();
            try
            {
                _rtbLog.Clear();
                if (_viewLevel == LogLevel.Off) return;

                // 共有バッファからスナップショットを取得して再描画
                List<Kiritori.Services.Logging.LogItem> snap = LogViewSharedBuffer.Snapshot();
                for (int i = 0; i < snap.Count; i++)
                {
                    var it = snap[i];
                    if (it.Level >= _viewLevel)
                        AppendToRtb(it);
                }
            }
            finally
            {
                _rtbLog.ResumeLayout();
            }
        }

        private void OnLogWritten(DateTime time, LogLevel level, string category, string message, Exception ex)
        {
            // 共有バッファへの積み込みは常駐シンクが担当済み
            var msg = BuildMessageOnly(category, message, ex);
            // ここではビュー追従のみ
            _ui.Post(_ => AppendLogFromEvent(time, level, msg), null);
        }
        private void AppendLogFromEvent(DateTime time, LogLevel level, string message)
        {
            if (level >= _viewLevel && _viewLevel != LogLevel.Off)
            {
                AppendToRtb(new Kiritori.Services.Logging.LogItem
                {
                    Time = time,
                    Level = level,
                    Message = message ?? string.Empty
                });
            }
        }

        private static string BuildMessageOnly(string cat, string msg, Exception ex)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(cat)) sb.Append('[').Append(cat).Append("] ");
            sb.Append(msg ?? "");
            if (ex != null) sb.Append(" | EX: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            return sb.ToString();
        }

        private static string FormatTimestamp(DateTime t)
        {
            var fmt = Properties.Settings.Default != null ? Properties.Settings.Default.LogTimestampFormat : null;
            if (string.IsNullOrWhiteSpace(fmt)) fmt = "HH:mm:ss.fff";
            try { return t.ToString(fmt, CultureInfo.InvariantCulture); }
            catch { return t.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture); }
        }

        // PrefForm を閉じるたびに共有バッファは保持。購読だけ外す。
        private void DisposeLogTab()
        {
            Log.LogWritten -= OnLogWritten;
        }
    }
}

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
        private RichTextBox _rtbLog;
        private ComboBox _cmbLogLevel;
        private Button _btnClear;
        private CheckBox _chkAutoScroll;

        // ===== バッファ（フィルタ切替でも再描画できるよう保持） =====
        private readonly List<LogItem> _logBuffer = new List<LogItem>(capacity: 5000);
        private const int MaxBuffer = 10000; // メモリ保護（必要なら調整）

        private readonly object _logSync = new object();

        // 画面に表示するレベル（None は出さない）
        private LogLevel _viewLevel = LogLevel.Info;

        // 既存の Settings を使うならここに保存/復元
        private const string SettingsKey_ViewLevel = "LogView.Level";
        private const string SettingsKey_AutoScroll = "LogView.AutoScroll";

        // ===== 公開：ロガー連携用フック点 =====
        // 1) イベント購読型：LogManager.LogEmitted がある前提（なければコメントアウト）
        // 2) シンク登録型：LogManager.RegisterSink(ILogSink) がある前提（なければ無視）

        private struct LogItem
        {
            public DateTime Time;
            public LogLevel Level;
            public string Message;
        }

        /// <summary>コンストラクタの末尾などで呼んでください。</summary>
        private void InitLogTab()
        {
            // --- タブ作成 ---
            // tabLogs = new TabPage("ログ") { Name = "tabLog" /* Tag にlocキーを載せるならここ */ };

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

            // ログレベル
            _cmbLogLevel = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140,
                Tag = "loc:Text.LogLevel"
            };
            // 表示用（OFF/INFO/DEBUG…）
            _cmbLogLevel.Items.AddRange(new object[]
            {
                "OFF", "INFO", "DEBUG", "TRACE", "WARN", "ERROR", "FATAL"
            });
            _cmbLogLevel.SelectedIndexChanged += (s, e) =>
            {
                _viewLevel = IndexToLevel(_cmbLogLevel.SelectedIndex);
                RedrawByFilter();
                SaveLogViewPrefs();
                var opt = Log.GetCurrentOptions();
                opt.MinLevel = _viewLevel;            // Off を選べば完全停止
                Log.Configure(opt);
                Log.Info($"Log level changed to {_viewLevel}", "Preferences");
            };

            // 表示クリア
            _btnClear = new Button
            {
                Text = "Clear View",
                AutoSize = true,
                Tag = "loc:Text.ClearView"
            };
            _btnClear.Click += (s, e) =>
            {
                lock (_logSync)
                {
                    _logBuffer.Clear();
                }
                _rtbLog.Clear();
            };

            // 最新へ自動スクロール
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
                // Text = SR.T("Text.LogLevel", "LogLevel"),
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

            Properties.Settings.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Properties.Settings.Default.LogTimestampFormat))
                {
                    // UI 再描画
                    RedrawByFilter();

                    // ロガー側の書式も同期（任意）
                    var opt = Log.GetCurrentOptions();
                    opt.TimestampFormat = Properties.Settings.Default.LogTimestampFormat;
                    Log.Configure(opt);
                }
            };

            // 既定値＆保存値の復元
            LoadLogViewPrefs();

            // ロガーへ接続
            Log.LogWritten += OnLogWritten;
            
            Log.Info("Log tab initialized", "Preferences");
        }

        private static LogLevel IndexToLevel(int idx)
        {
            // 0:なし → 特別扱い。以降は主に INFO/DEBUG/TRACE/WARN/ERROR/FATAL
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
                // 設定ストアがあれば使う。無ければ既定。
                var lvString = Properties.Settings.Default[SettingsKey_ViewLevel] as string;
                if (!string.IsNullOrEmpty(lvString) && Enum.TryParse(lvString, out LogLevel lv))
                    _viewLevel = lv;

                var auto = Properties.Settings.Default[SettingsKey_AutoScroll] as string;
                bool autoScroll;
                _chkAutoScroll.Checked = bool.TryParse(auto, out autoScroll) ? autoScroll : true;
            }
            catch { /* 初回などは無視 */ }

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
            catch { /* 非致命 */ }
        }

        /// <summary>
        /// 外部（任意の場所）から生ログを出したいとき用。
        /// </summary>
        public void AppendLog(LogLevel level, string message)
        {
            var item = new LogItem { Time = DateTime.Now, Level = level, Message = message };
            lock (_logSync)
            {
                _logBuffer.Add(item);
                if (_logBuffer.Count > MaxBuffer) _logBuffer.RemoveRange(0, _logBuffer.Count - MaxBuffer);
            }

            // 表示フィルタに合致すれば即時追記
            if (level >= _viewLevel && _viewLevel != LogLevel.Off)
            {
                AppendToRtb(item);
            }
        }

        private void AppendToRtb(LogItem item)
        {
            if (this.IsDisposed) return;

            // UI スレッドへ
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => AppendToRtb(item)));
                return;
            }

            var sb = new StringBuilder(64 + (item.Message?.Length ?? 0));
            sb.Append(FormatTimestamp(item.Time)).Append('\t');
            sb.Append(LevelShort(item.Level)).Append('\t');
            sb.Append(item.Message ?? string.Empty);
            if (!sb.ToString().EndsWith("\n")) sb.Append(Environment.NewLine);

            _rtbLog.SuspendLayout();
            try
            {
                _rtbLog.AppendText(sb.ToString());

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

                // バッファから再描画
                foreach (var it in _logBuffer)
                {
                    if (it.Level >= _viewLevel) AppendToRtb(it);
                }
            }
            finally
            {
                _rtbLog.ResumeLayout();
            }
        }
        private void OnLogWritten(DateTime time, LogLevel level, string category, string message, Exception ex)
        {
            var msg = BuildMessageOnly(category, message, ex);
            // UI スレッドに投げる（ハンドル未生成でもOK）
            _ui.Post(_ => AppendLogFromEvent(time, level, msg), null);
        }
        // イベントから直接積む
        private void AppendLogFromEvent(DateTime time, LogLevel level, string message)
        {
            var item = new LogItem { Time = time, Level = level, Message = message ?? "" };
            lock (_logSync)
            {
                _logBuffer.Add(item);
                if (_logBuffer.Count > MaxBuffer) _logBuffer.RemoveRange(0, _logBuffer.Count - MaxBuffer);
            }
            if (level >= _viewLevel && _viewLevel != LogLevel.Off)
                AppendToRtb(item);
        }

        // 本文だけ成形（時刻/レベルは入れない）
        private static string BuildMessageOnly(string cat, string msg, Exception ex)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(cat)) sb.Append('[').Append(cat).Append("] ");
            sb.Append(msg ?? "");
            if (ex != null) sb.Append(" | EX: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            return sb.ToString();
        }

        private static string BuildUiLine(DateTime t, LogLevel lv, string cat, string msg, Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append(FormatTimestamp(t)).Append('\t')
                .Append(lv.ToString().ToUpperInvariant()).Append('\t');
            if (!string.IsNullOrEmpty(cat)) sb.Append('[').Append(cat).Append("] ");
            sb.Append(msg ?? "");
            if (ex != null) sb.Append(" | EX: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            return sb.ToString();
        }
        private static string FormatTimestamp(DateTime t)
        {
            var fmt = Properties.Settings.Default?.LogTimestampFormat;
            if (string.IsNullOrWhiteSpace(fmt)) fmt = "HH:mm:ss.fff";   // 既定

            try { return t.ToString(fmt, CultureInfo.InvariantCulture); }
            catch { return t.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture); } // 保険
        }
        // 終了時にシンクを外す
        private void DisposeLogTab()
        {
            Log.LogWritten -= OnLogWritten;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Kiritori.Helpers;

namespace Kiritori.Services.Logging
{
    public sealed class LoggerOptions
    {
        public bool Enabled = true;
        public LogLevel MinLevel = LogLevel.Info;

        public bool WriteToDebug = true;
        public bool WriteToFile = true;

        public string FilePath = Path.Combine(Path.GetTempPath(), "Kiritori", "Kiritori.log");
        public long MaxFileSizeBytes = 5L * 1024 * 1024; // 5 MB
        public int MaxRollFiles = 3; // ローテーション世代数

        // 出力フォーマット拡張
        public bool IncludeTimestamp = true;
        public bool IncludeThreadId = false;
        public bool IncludeProcessId = false;
        public bool IncludeCategoryTag = true;

        // 例: "HH:mm:ss.fff" / "yyyy-MM-dd HH:mm:ss.fff"
        public string TimestampFormat = "HH:mm:ss.fff";
    }

    /// <summary>
    /// アプリ全体で使うファサード。Log.Configure() 後に利用。
    /// </summary>
    public static class Log
    {
        private static readonly object _sync = new object();
        private static LoggerOptions _opt = new LoggerOptions();
        private static volatile bool _initialized;
        private static BlockingCollection<LogItem> _queue;
        private static Thread _worker;
        private static int _procId = Process.GetCurrentProcess().Id;
        public static event Action<DateTime, LogLevel, string, string, Exception> LogWritten;

        private class LogItem
        {
            public DateTime Time;
            public LogLevel Level;
            public string Category;
            public string Message;
            public int ThreadId;
            public Exception Exception;
        }

        public static void Configure(LoggerOptions options)
        {
            if (options == null) options = new LoggerOptions();
            lock (_sync)
            {
                _opt = options;
                EnsureInit_NoLock();
            }
        }

        public static LoggerOptions GetCurrentOptions()
        {
            lock (_sync) { return Clone(_opt); }
        }

        private static LoggerOptions Clone(LoggerOptions o)
        {
            return new LoggerOptions
            {
                Enabled = o.Enabled,
                MinLevel = o.MinLevel,
                WriteToDebug = o.WriteToDebug,
                WriteToFile = o.WriteToFile,
                FilePath = o.FilePath,
                MaxFileSizeBytes = o.MaxFileSizeBytes,
                MaxRollFiles = o.MaxRollFiles,
                IncludeTimestamp = o.IncludeTimestamp,
                IncludeThreadId = o.IncludeThreadId,
                IncludeProcessId = o.IncludeProcessId,
                IncludeCategoryTag = o.IncludeCategoryTag,
                TimestampFormat = o.TimestampFormat
            };
        }

        private static void EnsureInit_NoLock()
        {
            if (_initialized) return;
            _queue = new BlockingCollection<LogItem>(new ConcurrentQueue<LogItem>());
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "KiritoriLogger" };
            _worker.Start();
            _initialized = true;
        }

        private static bool IsEnabled(LogLevel level)
        {
            if (!_opt.Enabled) return false;
            return level >= _opt.MinLevel && level != LogLevel.Off;
        }

        // Public APIs
        public static void Trace(string msg, string category = null) { Write(LogLevel.Trace, msg, category, null); }
        public static void Debug(string msg, string category = null) { Write(LogLevel.Debug, msg, category, null); }
        public static void Info(string msg, string category = null) { Write(LogLevel.Info, msg, category, null); }
        public static void Warn(string msg, string category = null) { Write(LogLevel.Warn, msg, category, null); }
        public static void Error(string msg, string category = null, Exception ex = null) { Write(LogLevel.Error, msg, category, ex); }
        public static void Fatal(string msg, string category = null, Exception ex = null) { Write(LogLevel.Fatal, msg, category, ex); }

        private static void Write(LogLevel level, string msg, string category, Exception ex)
        {
            lock (_sync) { EnsureInit_NoLock(); }
            if (!IsEnabled(level)) return;

            var item = new LogItem
            {
                Time = DateTime.Now,
                Level = level,
                Category = category,
                Message = msg ?? "",
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                Exception = ex
            };
            try { _queue.Add(item); } catch { /* app exit */ }
        }

        private static void WorkerLoop()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var line = FormatLine(item);
                    if (_opt.WriteToDebug)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine(line);
                        }
                        catch { }
                    }
                    if (_opt.WriteToFile)
                    {
                        try
                        {
                            EnsureDir(_opt.FilePath);
                            RollIfNeeded(_opt.FilePath, _opt.MaxFileSizeBytes, _opt.MaxRollFiles);
                            File.AppendAllText(_opt.FilePath, line + Environment.NewLine, Encoding.UTF8);
                        }
                        catch { /* ignore IO errors */ }
                    }
                    try
                    {
                        LogWritten?.Invoke(item.Time, item.Level, item.Category, item.Message, item.Exception);
                    }
                    catch { /* swallow */ }
                }
                catch { /* never throw from logger */ }
            }
        }

        private static string FormatLine(LogItem x)
        {
            var sb = new StringBuilder(128);
            if (_opt.IncludeTimestamp) sb.Append(x.Time.ToString(_opt.TimestampFormat)).Append(' ');
            sb.Append('[').Append(x.Level.ToString().ToUpperInvariant()).Append(']');

            if (_opt.IncludeProcessId) sb.Append(" (P").Append(_procId).Append(')');
            if (_opt.IncludeThreadId) sb.Append(" (T").Append(x.ThreadId).Append(')');

            if (_opt.IncludeCategoryTag && !string.IsNullOrEmpty(x.Category))
                sb.Append(" [").Append(x.Category).Append(']');

            sb.Append(' ').Append(x.Message);

            if (x.Exception != null)
            {
                sb.Append(" | EX: ").Append(x.Exception.GetType().Name)
                    .Append(": ").Append(x.Exception.Message);
            }
            return sb.ToString();
        }

        private static void EnsureDir(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }
        }

        private static void RollIfNeeded(string file, long maxSize, int maxKeep)
        {
            try
            {
                var fi = new FileInfo(file);
                if (fi.Exists && fi.Length >= maxSize)
                {
                    // 例: Kiritori.log -> Kiritori-20250906-1.log
                    var baseName = Path.GetFileNameWithoutExtension(file);
                    var ext = Path.GetExtension(file);
                    var dir = Path.GetDirectoryName(file) ?? "";
                    var date = DateTime.Now.ToString("yyyyMMdd");

                    int index = 1;
                    string rolled;
                    do
                    {
                        rolled = Path.Combine(dir, $"{baseName}-{date}-{index}{ext}");
                        index++;
                    } while (File.Exists(rolled) && index < 100);

                    File.Move(file, rolled);

                    // 古い順に削除
                    var pattern = $"{baseName}-???????.?-*.?"; // not used directly; we’ll filter by prefix
                    var old = Directory.GetFiles(dir, baseName + "-*" + ext)
                                        .OrderBy(f => File.GetCreationTime(f))
                                        .ToList();
                    while (old.Count > maxKeep)
                    {
                        try { File.Delete(old[0]); } catch { }
                        old.RemoveAt(0);
                    }
                }
            }
            catch { }
        }

        // アプリ終了時に呼び出すと多少きれいに終われます（任意）
        public static void Shutdown()
        {
            try
            {
                if (_queue != null && !_queue.IsAddingCompleted) _queue.CompleteAdding();
            }
            catch { }
        }
    }
}

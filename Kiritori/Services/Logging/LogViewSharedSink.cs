using Kiritori.Helpers;
using System;

namespace Kiritori.Services.Logging
{
    /// <summary>
    /// アプリ全体で1回だけ登録する常駐シンク。
    /// PrefFormが開いていなくても、全ログを共有バッファに積む。
    /// </summary>
    internal static class LogViewSharedSink
    {
        private static readonly object _sync = new object();
        private static bool _registered;

        public static void EnsureRegistered()
        {
            lock (_sync)
            {
                if (_registered) return;
                Log.LogWritten += OnLogWritten; // ← 常駐購読
                _registered = true;
            }
        }

        private static void OnLogWritten(DateTime time, LogLevel level, string category, string message, Exception ex)
        {
            // PrefFormが存在しない/閉じている間も常に共有バッファに積む
            var msg = BuildMessageOnly(category, message, ex);
            LogViewSharedBuffer.Add(time, level, msg);
        }

        private static string BuildMessageOnly(string cat, string msg, Exception ex)
        {
            // PrefForm側と同じ書式（カテゴリは [Cat] 前置、例外は " | EX: ..."）
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(cat)) sb.Append('[').Append(cat).Append("] ");
            sb.Append(msg ?? "");
            if (ex != null) sb.Append(" | EX: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            return sb.ToString();
        }
    }
}

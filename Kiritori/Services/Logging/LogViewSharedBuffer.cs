using Kiritori.Helpers;
using System;
using System.Collections.Generic;

namespace Kiritori.Services.Logging
{
    /// <summary>
    /// PrefForm（など複数ビュー）と共有するログのリングバッファ。
    /// スレッドセーフ。最大件数を超えたら先頭から捨てる。
    /// </summary>
    internal static class LogViewSharedBuffer
    {
        private static readonly object _sync = new object();
        private static readonly List<LogItem> _buffer = new List<LogItem>(2000);

        // 必要に応じて調整してください
        public const int MaxBuffer = 10000;

        public static void Add(DateTime time, LogLevel level, string message)
        {
            var item = new LogItem { Time = time, Level = level, Message = message ?? string.Empty };
            lock (_sync)
            {
                _buffer.Add(item);
                if (_buffer.Count > MaxBuffer)
                {
                    // 超過分だけ先頭をまとめて捨てる
                    _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);
                }
            }
        }

        /// <summary>現在の内容をコピーして返す（ビュー側で安全に列挙可能）。</summary>
        public static List<LogItem> Snapshot()
        {
            lock (_sync)
            {
                return new List<LogItem>(_buffer);
            }
        }

        /// <summary>（必要なら）明示的にクリア。</summary>
        public static void Clear()
        {
            lock (_sync)
            {
                _buffer.Clear();
            }
        }
    }

    /// <summary>ビュー描画用の最小ログアイテム型（共有用）。</summary>
    internal struct LogItem
    {
        public DateTime Time;
        public LogLevel Level;
        public string Message;
    }
}

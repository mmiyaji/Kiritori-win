using System;
using System.IO;

namespace Kiritori.Services.Recording
{
    internal static class FfmpegLocator
    {
        /// <summary>
        /// ffmpeg.exe を同梱パス→PATH の順で探索して返す。見つからなければ null。
        /// </summary>
        internal static string Resolve()
        {
            // アプリの実行ベース（MSIX でも OK／読み取り専用）
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 推奨の同梱場所: Kiritori/ThirdParty/ffmpeg/ffmpeg.exe
            var bundled = Path.Combine(baseDir, "ThirdParty", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(bundled)) return bundled;

            // PATH から探索（ユーザーがシステムに入れている場合）
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var parts = pathEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                try
                {
                    var cand = Path.Combine(p.Trim(), "ffmpeg.exe");
                    if (File.Exists(cand)) return cand;
                }
                catch { /* ignore */ }
            }

            return null;
        }
    }
}

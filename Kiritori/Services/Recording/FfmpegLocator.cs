using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kiritori.Services.Extensions;
using Kiritori.Services.Logging;

namespace Kiritori.Services.Recording
{
    internal static class FfmpegLocator
    {
        public static string Resolve(bool autoInstall = true, IWin32Window owner = null)
        {
            // 1) 設定で明示パスがあれば最優先
            var cfgPath = TryNormalize(Properties.Settings.Default.FfmpegPath);
            if (IsGood(cfgPath)) return cfgPath;

            // 2) 拡張（x64/x86）から探す
            var id = "ffmpeg";
            var ext = TryFromExtension(id);
            if (IsGood(ext)) return ext;

            // 3) ポータブル同梱（EXE隣 or ThirdParty\ffmpeg\ffmpeg.exe）
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var portable1 = Path.Combine(exeDir, "ffmpeg.exe");
            if (IsGood(portable1)) return portable1;

            var portable2 = Path.Combine(exeDir, "ThirdParty", "ffmpeg", "ffmpeg.exe");
            if (IsGood(portable2)) return portable2;

            // 4) PATH から探す
            var fromPath = WhichOnPath("ffmpeg.exe");
            if (IsGood(fromPath)) return fromPath;

            // 5) 他の候補が無い場合だけ自動導入を提案
            if (autoInstall && !ExtensionsManager.IsInstalled(id) &&
                ExtensionsAuto.TryEnsure(id, owner, prompt: true, throwOnDecline: true))
            {
                ext = TryFromExtension(id);
                if (IsGood(ext)) return ext;
            }

            // 見つからず
            return null;
        }

        private static string TryFromExtension(string id)
        {
            try
            {
                var dir = ExtensionsManager.GetInstallDir(id);
                if (string.IsNullOrEmpty(dir)) return null;
                var exe = Path.Combine(dir, "ffmpeg.exe");
                return File.Exists(exe) ? exe : null;
            }
            catch { return null; }
        }

        private static bool IsGood(string exe)
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
            // 簡易健全性チェック: 実行して "-version" が返るか
            try
            {
                using (var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = "-hide_banner -version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                })
                {
                    if (!p.Start()) return false;

                    var stdout = p.StandardOutput.ReadToEndAsync();
                    var stderr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(3000))
                    {
                        try { p.Kill(); } catch { }
                        try { p.WaitForExit(1000); } catch { }
                        return false;
                    }

                    if (!Task.WaitAll(new Task[] { stdout, stderr }, 1000)) return false;
                    var outStr = (stdout.Result + stderr.Result) ?? "";
                    return outStr.IndexOf("ffmpeg", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        private static string WhichOnPath(string exeName)
        {
            try
            {
                if (File.Exists(exeName)) return Path.GetFullPath(exeName);
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in path.Split(Path.PathSeparator).Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    try
                    {
                        var p = Path.Combine(dir.Trim(), exeName);
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private static string TryNormalize(string p)
        {
            try { return string.IsNullOrWhiteSpace(p) ? null : Path.GetFullPath(p); }
            catch { return null; }
        }
    }
}

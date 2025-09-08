using Kiritori.Helpers;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsPaths
    {
    // exe名に portable が含まれるか（-/_/. の区切りや連結も許容）
        private static readonly Regex PortableNameRegex =
            new Regex(@"(?:^|[-_.])portable(?:$|[-_.0-9])",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool IsPortableExeName()
        {
            try
            {
                // EXE のファイル名（拡張子なし）
                var exe = Process.GetCurrentProcess().MainModule.FileName;
                var name = Path.GetFileNameWithoutExtension(exe) ?? string.Empty;

                // 連結（KiritoriPortable）も拾いたいなら IndexOf で先に見る
                if (name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                // 区切り（Kiritori-portable / _portable / .portable / portable-1.2）もOK
                return PortableNameRegex.IsMatch(name);
            }
            catch { return false; }
        }

        private static bool IsPortableMarkerPresent()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return File.Exists(Path.Combine(baseDir, "Kiritori.portable"))
                || File.Exists(Path.Combine(baseDir, "PORTABLE"));
        }

        private static bool IsPortableRequested()
        {
            // 環境変数でも強制可（例: set KIRITORI_PORTABLE=1）
            var env = Environment.GetEnvironmentVariable("KIRITORI_PORTABLE");
            bool envOn = !string.IsNullOrEmpty(env) &&
                        (env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase));

            return IsPortableMarkerPresent() || IsPortableExeName() || envOn;
        }

        public static string Root
        {
            get
            {
                // MSIX/パッケージ時は常に LocalAppData（書き込み不可対策）
                try
                {
                    if (Helpers.PackagedHelper.IsPackaged())
                        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kiritori");
                }
                catch { /* 旧環境 protect */ }

                // ポータブル要求なら EXE 隣を使う
                if (IsPortableRequested())
                    return AppDomain.CurrentDomain.BaseDirectory;

                // 既定：ユーザー領域
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kiritori");
            }
        }
        public static string Expand(string relative) =>
            Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        // ユーザー領域（オンライン更新で上書きしたい場合）
        public static string ManifestsLocal =>
            Path.Combine(Root, "manifests");
        public static string StateJson =>
            Path.Combine(Root, "state", "extensions.json");

        // 出力フォルダ内（Content としてコピーしておく）
        public static string RepoManifestsInApp =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThirdPartyManifests");

        // 開発時の相対（bin/Debug から上位を遡って ThirdPartyManifests を探す）
        public static IEnumerable<string> CandidateManifestDirs()
        {
            yield return ManifestsLocal;
            yield return RepoManifestsInApp;

            var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 5 && d != null; i++, d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "ThirdPartyManifests");
                if (Directory.Exists(p)) yield return p;
            }
        }

        public static void EnsureDirs()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, "state"));
            Directory.CreateDirectory(ManifestsLocal);
        }
    }
}

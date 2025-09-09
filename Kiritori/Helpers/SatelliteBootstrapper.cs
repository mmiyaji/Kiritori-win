using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;

namespace Kiritori.Helpers
{
    internal static class SatelliteBootstrapper
    {
        private static bool _initialized;
        private static readonly object _gate = new object();

        // 例: %LocalAppData%\Kiritori\i18n\ja\Kiritori.resources.dll
        private static readonly string BaseDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Kiritori", "i18n");

        /// <summary>アプリ起動の最初期に一度だけ呼ぶ</summary>
        internal static void Init()
        {
            if (_initialized) return;
            lock (_gate)
            {
                if (_initialized) return;
                EnsureSatellitesExtracted();
                AppDomain.CurrentDomain.AssemblyResolve += OnResolveSatellite;
                _initialized = true;
            }
        }

        /// <summary>
        /// EXE に埋め込まれた "{AsmName}.i18n.{culture}.{AsmName}.resources.dll" を
        /// %LocalAppData%\Kiritori\i18n\{culture}\ に展開する
        /// </summary>
        private static void EnsureSatellitesExtracted()
        {
            var asm = Assembly.GetExecutingAssembly();
            var asmName = asm.GetName().Name;
            var prefix = asmName + ".i18n.";

            // 抽出対象カルチャ = 現在のUIカルチャ + 親チェーン (ja-JP -> ja)
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var c = CultureInfo.CurrentUICulture; c != CultureInfo.InvariantCulture && c != null; c = c.Parent)
                if (!string.IsNullOrEmpty(c.Name)) wanted.Add(c.Name);

            var resourceNames = asm.GetManifestResourceNames()
                                .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                            && n.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                                .ToArray();

            foreach (var resName in resourceNames)
            {
                var parts = resName.Split('.');
                var idx = Array.IndexOf(parts, "i18n");
                if (idx < 0 || idx + 1 >= parts.Length) {
                    Log.Debug($"Skip extract (invalid name): {resName}", "Satellite");
                    continue;
                }

                var culture = parts[idx + 1];
                if (!wanted.Contains(culture))
                {
                    Log.Debug($"Skip extract (not wanted): {culture} {resName}", "Satellite");
                    continue;
                }

                var outDir = Path.Combine(BaseDir, culture);
                var outPath = Path.Combine(outDir, asmName + ".resources.dll");

                Directory.CreateDirectory(outDir);

                using (var s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null || s.Length == 0) continue;

                    // サイズ一致ならスキップ（高速）
                    var fi = new FileInfo(outPath);
                    if (fi.Exists && fi.Length == s.Length)
                    {
                        Log.Debug($"Skip extract (size match): {culture} {outPath}", "Satellite");
                        continue;
                    }

                    var tmpPath = outPath + ".tmp_" + System.Diagnostics.Process.GetCurrentProcess().Id;
                    using (var f = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        s.CopyTo(f);

                    try
                    {
                        // 置換は原子的に（存在すれば Replace、なければ Move）
                        if (File.Exists(outPath)) File.Replace(tmpPath, outPath, null);
                        else File.Move(tmpPath, outPath);
                    }
                    catch
                    {
                        // 置換に失敗した場合のフォールバック
                        try { File.Copy(tmpPath, outPath, true); } catch { /* best-effort */ }
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
            }
        }

        /// <summary>
        /// .NET が衛星アセンブリ（*.resources）を解決できなかったときのフォールバック
        /// </summary>
        private static Assembly OnResolveSatellite(object sender, ResolveEventArgs e)
        {
            var req = new AssemblyName(e.Name);

            // *.resources 以外は対象外
            if (!req.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            // 中立カルチャは対象外
            if (string.IsNullOrEmpty(req.CultureName) || req.CultureName == "neutral")
                return null;

            // "Kiritori.resources" → "Kiritori"
            var asmName = req.Name.Substring(0, req.Name.Length - ".resources".Length);
            var path = Path.Combine(BaseDir, req.CultureName, asmName + ".resources.dll");

            try
            {
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}

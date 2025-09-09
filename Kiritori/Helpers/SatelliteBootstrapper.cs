using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
            var asmName = asm.GetName().Name; // "Kiritori"
            var prefix = asmName + ".i18n.";  // "Kiritori.i18n."

            var resourceNames = asm.GetManifestResourceNames()
                                   .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                            && n.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                                   .ToArray();

            foreach (var resName in resourceNames)
            {
                // 期待形: Kiritori.i18n.ja.Kiritori.resources.dll
                //                                  ^ idx
                var parts = resName.Split('.');
                var idx = Array.IndexOf(parts, "i18n");
                if (idx < 0 || idx + 1 >= parts.Length) continue;

                var culture = parts[idx + 1]; // ja / fr / zh-Hans など
                var outDir = Path.Combine(BaseDir, culture);
                var outPath = Path.Combine(outDir, asmName + ".resources.dll");

                Directory.CreateDirectory(outDir);

                // 埋め込みを1回読み切ってファイルへコピー（Positionは触らない）
                using (var s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null || s.Length == 0) continue;

                    // 既存と同サイズならスキップ（簡易最適化。完全一致保証が必要ならSHA-256を計算してください）
                    try
                    {
                        var fi = new FileInfo(outPath);
                        if (fi.Exists && fi.Length == s.Length)
                        {
                            // ※ ここで s をもう使わないので using ブロックの終了で破棄される
                            continue;
                        }
                    }
                    catch { /* 読めなければ上書きへ */ }

                    // 原子更新: temp に書いてから Move
                    var tmpPath = outPath + ".tmp_" + System.Diagnostics.Process.GetCurrentProcess().Id;
                    using (var f = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        s.CopyTo(f); // ここで1回読み切り。Positionのリセットは不要。
                    }
                    try
                    {
                        if (File.Exists(outPath)) File.Delete(outPath);
                    }
                    catch { /* 他プロセスが掴んでいたら上書きに失敗することがある */ }
                    File.Move(tmpPath, outPath);
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

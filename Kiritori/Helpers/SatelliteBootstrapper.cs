using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;


namespace Kiritori.Helpers
{
    static class SatelliteBootstrapper
    {
        // 既存コードで使っているルート（%LocalAppData%\Kiritori 相当）に合わせてください
        static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kiritori");

        public static void EnsureSatellitesExtracted()
        {
            var asm = typeof(Properties.Strings).Assembly;
            var resNames = asm.GetManifestResourceNames()
                            .Where(n => n.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)
                                    && n.IndexOf(".i18n.", StringComparison.OrdinalIgnoreCase) >= 0)
                            .ToList();

            Kiritori.Services.Logging.Log.Info(
                $"[i18n] Embedded satellites found: {resNames.Count}", "Startup");

            foreach (var name in resNames)
            {
                var cul = TryGetCultureFromResourceName(name);
                if (string.IsNullOrEmpty(cul)) continue;

                var outDir = Path.Combine(Root, "i18n", cul);
                var outFile = Path.Combine(outDir, "Kiritori.resources.dll");
                Directory.CreateDirectory(outDir);

                using (var src = asm.GetManifestResourceStream(name))
                {
                    if (src == null) continue;

                    bool write = !File.Exists(outFile) || !StreamsEqual(src, File.OpenRead(outFile));
                    Kiritori.Services.Logging.Log.Debug(
                        $"[i18n] {name} -> {outFile} {(write ? "(write)" : "(skip)")}", "Startup");

                    if (write)
                    {
                        src.Position = 0;
                        using (var fs = File.Create(outFile))
                            src.CopyTo(fs);
                    }
                }
            }
        }


        static string TryGetCultureFromResourceName(string resName)
        {
            // ★判定用に正規化（呼び出し元では resName=元の名前のまま保持）
            var norm = resName.Replace('\\', '.').Replace('/', '.');

            // 期待形: "... .i18n.<culture> .Kiritori.resources.dll"
            const string head = ".i18n.";
            const string tail = ".Kiritori.resources.dll";

            int i = norm.IndexOf(head, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += head.Length;

            int j = norm.IndexOf(tail, i, StringComparison.OrdinalIgnoreCase);
            if (j < 0 || j <= i) return null;

            return norm.Substring(i, j - i); // "ja" / "fr" / "zh-CN" など
        }

        static bool StreamsEqual(Stream a, Stream b)
        {
            using (a)
            using (b)
            using (var sha = SHA256.Create())
            {
                var ha = sha.ComputeHash(a);
                var hb = sha.ComputeHash(b);
                return ha.SequenceEqual(hb);
            }
        }
    }
}

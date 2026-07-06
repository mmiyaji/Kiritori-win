using System;
using System.IO;
using System.IO.Compression;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsZip
    {
        public static void ExtractZipAllowOverwrite(string zipPath, string targetDir)
        {
            var root = Path.GetFullPath(targetDir);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);

            using (var za = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in za.Entries)
                {
                    var destPath = Path.Combine(root, entry.FullName);
                    var full = Path.GetFullPath(destPath);
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Invalid zip entry path.");

                    // ディレクトリ判定
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(full);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    // 上書き抽出（ロック時は一度削除を試す）
                    try
                    {
                        entry.ExtractToFile(full, overwrite: true);
                    }
                    catch (IOException)
                    {
                        try { File.SetAttributes(full, FileAttributes.Normal); } catch { }
                        try { File.Delete(full); } catch { }
                        entry.ExtractToFile(full, overwrite: false);
                    }
                }
            }
        }
    }
}

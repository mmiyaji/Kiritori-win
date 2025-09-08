using System;
using System.IO;
using System.IO.Compression;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsZip
    {
        public static void ExtractZipAllowOverwrite(string zipPath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            using (var za = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in za.Entries)
                {
                    // ディレクトリ判定
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                        continue;
                    }

                    var destPath = Path.Combine(targetDir, entry.FullName);
                    var dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    // パストラバーサル防止
                    var full = Path.GetFullPath(destPath);
                    var root = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Invalid zip entry path.");

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

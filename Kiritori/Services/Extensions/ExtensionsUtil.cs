using System;
using System.IO;
using System.Security.Cryptography;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsUtil
    {
        public static bool VerifySha256(string path, string expectedHex)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var got = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                    return string.Equals(got, (expectedHex ?? "").ToLowerInvariant(), StringComparison.Ordinal);
                }
            }
            catch { return false; }
        }
    }
}

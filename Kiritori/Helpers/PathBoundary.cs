using System;
using System.IO;

namespace Kiritori.Helpers
{
    internal static class PathBoundary
    {
        internal static string ResolveUnder(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(relative)) throw new ArgumentNullException(nameof(relative));
            if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Absolute paths are not allowed.");

            var fullRoot = NormalizeRoot(root);
            var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!IsStrictlyUnderNormalized(fullRoot, full))
                throw new InvalidOperationException("Path escapes the allowed root.");

            return full;
        }

        internal static bool IsStrictlyUnder(string root, string candidate)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate)) return false;

            try
            {
                return IsStrictlyUnderNormalized(NormalizeRoot(root), Path.GetFullPath(candidate));
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeRoot(string root)
        {
            return Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsStrictlyUnderNormalized(string fullRoot, string fullCandidate)
        {
            var prefix = fullRoot + Path.DirectorySeparatorChar;
            return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}

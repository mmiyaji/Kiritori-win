using System;
using System.Collections.Generic;
using System.IO;

namespace Kiritori.Helpers
{
    internal static class ImageFileSupport
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"
            };

        public static bool IsSupportedImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return SupportedExtensions.Contains(Path.GetExtension(path));
        }
    }
}

using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Kiritori.Services.Settings
{
    internal static class SettingsDirtyTracker
    {
        public static string ComputeSettingsHash()
        {
            var s = Properties.Settings.Default;
            var sb = new StringBuilder(256);
            foreach (SettingsProperty p in s.Properties.Cast<SettingsProperty>().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                object v = s[p.Name];
                string str;
                try
                {
                    var tc = TypeDescriptor.GetConverter(p.PropertyType);
                    str = (tc != null && tc.CanConvertTo(typeof(string)))
                        ? (tc.ConvertToInvariantString(v) ?? "")
                        : (v != null ? v.ToString() : "");
                }
                catch
                {
                    str = (v != null ? v.ToString() : "");
                }
                sb.Append(p.Name).Append('=').Append(str).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        public static Dictionary<string, string> BuildSettingsSnapshotMap()
        {
            var s = Properties.Settings.Default;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (SettingsProperty p in s.Properties)
            {
                object v = s[p.Name];
                string str;
                try
                {
                    var tc = TypeDescriptor.GetConverter(p.PropertyType);
                    str = (tc != null && tc.CanConvertTo(typeof(string)))
                        ? (tc.ConvertToInvariantString(v) ?? "")
                        : (v != null ? v.ToString() : "");
                }
                catch
                {
                    str = (v != null ? v.ToString() : "");
                }
                map[p.Name] = str;
            }
            return map;
        }

        public static string FormatSettingsDiff(int maxLines, out int totalChanges, Dictionary<string, string> baselineMap)
        {
            var now = BuildSettingsSnapshotMap();
            var lines = new System.Collections.Generic.List<string>();
            totalChanges = 0;

            foreach (var kv in now)
            {
                var key = kv.Key;
                var cur = kv.Value ?? "";
                string old;
                baselineMap.TryGetValue(key, out old);
                old = old ?? "";

                if (!string.Equals(old, cur, StringComparison.Ordinal))
                {
                    totalChanges++;
                    if (lines.Count < maxLines)
                    {
                        var displayKey = DisplayNameFor(key);
                        var oldV = FormatValueForDisplay(old, key);
                        var newV = FormatValueForDisplay(cur, key);

                        // 1行フォーマット（多言語）。既定: "{0}: {1} -> {2}"
                        var line = string.Format(
                            SR.T("Text.DiffLine", "{0}: {1} -> {2}"),
                            displayKey, oldV, newV
                        );
                        lines.Add(line);
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append("• ").Append(lines[i]).Append(Environment.NewLine);
            }
            if (totalChanges > lines.Count)
            {
                // 既定: "…and {0} more"
                sb.Append(string.Format(SR.T("Text.AndMoreNum", "…and {0} more"), totalChanges - lines.Count));
            }
            return sb.ToString();
        }

        private static string DisplayNameFor(string key)
        {
            // 見つからなければキー名をそのまま出す
            return SR.T("Setting.Display." + key, key);
        }
        private static string FormatValueForDisplay(string raw, string key)
        {
            if (string.IsNullOrEmpty(raw)) return SR.T("Common.Empty", "(empty)");

            // bool → On/Off（多言語）
            if (string.Equals(raw, "True", StringComparison.OrdinalIgnoreCase)) return SR.T("Common.On", "On");
            if (string.Equals(raw, "False", StringComparison.OrdinalIgnoreCase)) return SR.T("Common.Off", "Off");

            // Color [Cyan] → Cyan
            if (raw.StartsWith("Color [", StringComparison.Ordinal) && raw.EndsWith("]", StringComparison.Ordinal))
                return raw.Substring(7, raw.Length - 8);

            // 数値系に単位を付けたい場合（キー名で判断）
            if (key.EndsWith("AlphaPercent", StringComparison.Ordinal)) return raw + SR.T("Common.PercentSuffix", "%");
            if (key.EndsWith("Thickness", StringComparison.Ordinal)) return raw + SR.T("Common.PixelSuffix", " px");

            return raw;
        }
        public static void DumpSettingsKeys()
        {
            Debug.WriteLine("=== Settings.Keys ===");
            foreach (SettingsProperty p in Kiritori.Properties.Settings.Default.Properties)
                Debug.WriteLine($"- {p.Name} : {p.PropertyType}");
        }

    }
}

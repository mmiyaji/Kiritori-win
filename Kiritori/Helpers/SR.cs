using System;
using System.Globalization;
using System.Threading;

namespace Kiritori.Helpers
{
    public static class SR
    {
        public static string T(string key) =>
            Properties.Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string T(string key, string fallback)
            {
                try
                {
                    var s = T(key); // 既存の単一引数版を呼ぶ
                    return string.IsNullOrEmpty(s) ? fallback : s;
                }
                catch
                {
                    return fallback;
                }
            }
        // 文字埋め込み（string.Format）
        public static string F(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentUICulture, T(key), args);
        
        public static event System.Action CultureChanged;

        public static void SetCulture(string cultureName)
        {
            var c = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = c;
            CultureInfo.CurrentUICulture = c;
            CultureChanged?.Invoke();
        }

    }
}

using System.Globalization;
using System.Threading;

namespace Kiritori
{
    public static class SR
    {
        public static string T(string key) =>
            Properties.Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

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

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Kiritori.Helpers
{
 internal static class PackagedHelper
    {
        // https://learn.microsoft.com/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        private const int APPMODEL_ERROR_NO_PACKAGE = 15700; // 0x3D54
        private const int ERROR_INSUFFICIENT_BUFFER = 122;   // 0x7A

        private static bool? _cached;

        public static bool IsPackaged()
        {
            if (_cached.HasValue) return _cached.Value;

            // Windows 8 (6.2) 以降でのみ AppModel API が存在
            var ver = Environment.OSVersion.Version; // 6.2=Win8, 6.3=8.1, 10.0=Win10+
            if (ver.Major < 6 || (ver.Major == 6 && ver.Minor < 2))
            {
                _cached = false;
                return false;
            }

            try
            {
                int length = 0;
                int rc = GetCurrentPackageFullName(ref length, null);
                if (rc == APPMODEL_ERROR_NO_PACKAGE)
                {
                    _cached = false;         // 非パッケージ実行
                    return false;
                }
                if (rc == ERROR_INSUFFICIENT_BUFFER && length > 0)
                {
                    var sb = new StringBuilder(length);
                    rc = GetCurrentPackageFullName(ref length, sb);
                    _cached = (rc == 0);     // 取得できた＝パッケージ実行
                    return _cached.Value;
                }

                _cached = (rc == 0);         // 念のため
                return _cached.Value;
            }
            catch (EntryPointNotFoundException)
            {
                // まれに古い互換レイヤ/特殊環境で発生しうる
                _cached = false;
                return false;
            }
            catch
            {
                // 何かあったら「非パッケージ扱い」に倒す
                _cached = false;
                return false;
            }
        }
    }
}
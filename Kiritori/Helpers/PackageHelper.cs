using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Kiritori.Helpers
{
    internal static class PackagedHelper
    {
        // https://learn.microsoft.com/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        private const int APPMODEL_ERROR_NO_PACKAGE = 15700; // 0x3D54
        private const int ERROR_INSUFFICIENT_BUFFER = 122;   // 0x7A

        public static bool IsPackaged()
        {
            int length = 0;
            int rc = GetCurrentPackageFullName(ref length, null);
            if (rc == APPMODEL_ERROR_NO_PACKAGE) return false;                   // 非パッケージ実行
            if (rc == ERROR_INSUFFICIENT_BUFFER && length > 0)
            {
                var sb = new StringBuilder(length);
                rc = GetCurrentPackageFullName(ref length, sb);
                return rc == 0;                                                  // パッケージ実行
            }
            return rc == 0;
        }
    }
}
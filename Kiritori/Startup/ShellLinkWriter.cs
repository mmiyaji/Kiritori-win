using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Kiritori.Startup
{
    /// <summary>
    /// Start メニューのショートカット(.lnk)を作成/更新し、AUMID (AppUserModelID) を設定します。
    /// </summary>
    internal static class ShellLinkWriter
    {
        // AppUserModel の PropertyKey (PKEY_AppUserModel_ID)
        // fmtid: 9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3, pid: 5
        private static readonly PROPERTYKEY PKEY_AppUserModel_ID =
            new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };

        /// <summary>
        /// AUMID を持つ .lnk を作成（既存なら上書き/更新）します。
        /// </summary>
        /// <param name="linkPath">保存先（例：%AppData%\Microsoft\Windows\Start Menu\Programs\Kiritori.lnk）</param>
        /// <param name="exePath">リンク先 EXE のフルパス</param>
        /// <param name="aumid">AppUserModelID（例：Kiritori.Desktop）</param>
        /// <param name="arguments">任意の引数</param>
        /// <param name="iconPath">アイコンのパス（null/空なら exePath）</param>
        /// <param name="iconIndex">アイコンのインデックス（通常 0）</param>
        /// <param name="description">説明</param>
        public static void CreateOrUpdateShortcutWithAumid(
            string linkPath,
            string exePath,
            string aumid,
            string arguments = null,
            string iconPath = null,
            int iconIndex = 0,
            string description = null)
        {
            if (string.IsNullOrEmpty(linkPath)) throw new ArgumentNullException(nameof(linkPath));
            if (string.IsNullOrEmpty(exePath)) throw new ArgumentNullException(nameof(exePath));
            if (string.IsNullOrEmpty(aumid)) throw new ArgumentNullException(nameof(aumid));

            Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? "");

            // IShellLink を作成
            var link = (IShellLinkW)new CShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath));
            if (!string.IsNullOrEmpty(arguments)) link.SetArguments(arguments);
            if (!string.IsNullOrEmpty(description)) link.SetDescription(description);
            link.SetIconLocation(string.IsNullOrEmpty(iconPath) ? exePath : iconPath, iconIndex);

            // IPropertyStore で AppUserModel.ID を設定
            var store = (IPropertyStore)link;
            var key = PKEY_AppUserModel_ID;
            PROPVARIANT pv = null;
            try
            {
                pv = PROPVARIANT.FromString(aumid);
                store.SetValue(ref key, pv);
                store.Commit();
            }
            finally
            {
                if (pv != null) pv.Dispose();
            }

            // 保存（上書き）
            ((IPersistFile)link).Save(linkPath, true);
        }

        // ===== COM interop 定義（必要最小限） =====

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue(ref PROPERTYKEY key, [In] PROPVARIANT pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        // 文字列用（VT_LPWSTR）の最小 PROPVARIANT 実装
        [StructLayout(LayoutKind.Sequential)]
        private sealed class PROPVARIANT : IDisposable
        {
            // VT=31 (VT_LPWSTR) を自前で作る簡易版
            ushort vt; ushort w1; ushort w2; ushort w3;
            IntPtr ptr;
            int i1; int i2;

            public static PROPVARIANT FromString(string s)
            {
                var pv = new PROPVARIANT();
                pv.vt = 31; // VT_LPWSTR
                pv.ptr = Marshal.StringToCoTaskMemUni(s ?? string.Empty);
                return pv;
            }

            public void Dispose()
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                    ptr = IntPtr.Zero;
                }
            }
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("0000010B-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}

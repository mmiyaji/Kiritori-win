using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Kiritori.Startup
{
    internal static class ShellLinkWriter
    {
        // PKEY_AppUserModel_ID
        private static readonly PROPERTYKEY PKEY_AppUserModel_ID =
            new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

        // PKEY_AppUserModel_ToastActivatorCLSID
        private static readonly PROPERTYKEY PKEY_AppUserModel_ToastActivatorCLSID =
            new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 26);

        /// <summary>
        /// スタートメニューのショートカットを作成/更新し、AUMID と（任意で）ToastActivator CLSID を設定します。
        /// STA スレッドで呼び出してください。
        /// </summary>
        public static void CreateOrUpdateShortcutWithAumid(
            string linkPath,
            string exePath,
            string aumid,
            Guid? toastActivatorClsid = null,
            string arguments = null,
            string iconPath = null,
            int iconIndex = 0)
        {
            var dir = Path.GetDirectoryName(linkPath)
              ?? throw new InvalidOperationException("Invalid link path.");
                Directory.CreateDirectory(dir);

            // ShellLink COM
            var shellLink = (IShellLinkW)new CShellLink();
            shellLink.SetPath(exePath);
            shellLink.SetArguments(arguments ?? string.Empty);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath));
            shellLink.SetIconLocation(iconPath ?? exePath, iconIndex);

            // PropertyStore で AUMID / ToastActivatorCLSID を設定
            var store = (IPropertyStore)shellLink;

            // AUMID
            using (var pvAumid = PropVariant.FromString(aumid))
            {
                var keyAumid = PKEY_AppUserModel_ID;               // ← ローカルにコピー
                store.SetValue(ref keyAumid, pvAumid);
            }

            if (toastActivatorClsid.HasValue)
            {
                using (var pvClsid = PropVariant.FromString(toastActivatorClsid.Value.ToString("B")))
                {
                    var keyClsid = PKEY_AppUserModel_ToastActivatorCLSID;  // ← ローカルにコピー
                    store.SetValue(ref keyClsid, pvClsid);
                }
            }

            store.Commit();

            // 保存
            ((IPersistFile)shellLink).Save(linkPath, true);
            Marshal.ReleaseComObject(store);
            Marshal.ReleaseComObject(shellLink);
        }

        #region COM interop (最小限)

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        private interface IPropertyStore
        {
            uint GetCount(out uint cProps);
            uint GetAt(uint iProp, out PROPERTYKEY pkey);
            uint GetValue(ref PROPERTYKEY key, out PropVariant pv);
            uint SetValue(ref PROPERTYKEY key, [In] PropVariant pv);
            uint Commit();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
            public PROPERTYKEY(Guid f, uint p) { fmtid = f; pid = p; }
        }

        // 文字列用だけの簡易 PropVariant
        [StructLayout(LayoutKind.Sequential)]
        private sealed class PropVariant : IDisposable
        {
            // VT_LPWSTR = 31
            ushort vt = 31;
            ushort r1, r2, r3;
            IntPtr p; // LPWSTR
            int i1, i2, i3;

            private PropVariant() { }
            public static PropVariant FromString(string s)
            {
                var pv = new PropVariant();
                pv.p = Marshal.StringToCoTaskMemUni(s);
                return pv;
            }
            public void Dispose()
            {
                if (p != IntPtr.Zero)
                {
                    PropVariantClear(this);
                    p = IntPtr.Zero;
                }
                GC.SuppressFinalize(this);
            }
            ~PropVariant() { Dispose(); }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear([In, Out] PropVariant pvar);

        #endregion
    }
}

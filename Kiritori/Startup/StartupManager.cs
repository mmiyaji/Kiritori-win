using Kiritori.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms; // Application.ProductName / ExecutablePath
using System.Runtime.InteropServices.WindowsRuntime; // AsTask()
using Windows.ApplicationModel;
using Kiritori.Services.Logging; // StartupTask / StartupTaskState

namespace Kiritori.Startup
{
    public static class StartupManager
    {
        private const string TaskId = "KiritoriStartup";

        // ===================== Packaged (MSIX) 側 =====================

        public static async Task<bool> IsEnabledAsync()
        {
            if (!PackagedHelper.IsPackaged()) return false;

            // IAsyncOperation<StartupTask> → AsTask()
            var task = await StartupTask.GetAsync(TaskId).AsTask().ConfigureAwait(false);
            Log.Debug($"StartupTask pre: {task.State}", "StartupToggle");
            var state = task.State;

            // Disabled=0, EnabledByUser=1, Enabled=2, DisabledByUser=3, EnabledByPolicy=4
            return state == StartupTaskState.Enabled
                || state == StartupTaskState.EnabledByPolicy;
        }

        public static async Task<bool> EnableAsync()
        {
            if (!PackagedHelper.IsPackaged()) return false;

            var task = await StartupTask.GetAsync(TaskId).AsTask().ConfigureAwait(false);
            Log.Debug($"StartupTask pre: {task.State}", "StartupToggle");
            var state = task.State;

            if (state == StartupTaskState.Disabled || state == StartupTaskState.DisabledByUser)
            {
                // ユーザー同意 UI が出る可能性あり（UI スレッド呼び出し推奨）
                var newState = await task.RequestEnableAsync().AsTask().ConfigureAwait(false);
                state = newState;
            }

            return state == StartupTaskState.Enabled
                || state == StartupTaskState.EnabledByPolicy;
        }

        public static async Task DisableAsync()
        {
            if (!PackagedHelper.IsPackaged()) return;

            var task = await StartupTask.GetAsync(TaskId).AsTask().ConfigureAwait(false);

            // Disable は同期メソッド（IAsyncOperation ではない）
            task.Disable();
        }

        /// <summary>
        /// Packaged: 起動時有効化を保証（有効でなければ RequestEnableAsync まで）
        /// Unpackaged: .lnk を作って保証
        /// </summary>
        public static async Task<bool> EnsureEnabledAsync()
        {
            if (PackagedHelper.IsPackaged())
            {
                if (await IsEnabledAsync().ConfigureAwait(false)) return true;
                try { return await EnableAsync().ConfigureAwait(false); }
                catch { return await IsEnabledAsync().ConfigureAwait(false); }
            }
            else
            {
                return EnsureEnabled();
            }
        }

        // ===================== Unpackaged (.exe) 側 =====================

        /// <summary>
        /// 非パッケージ(.exe)：スタートアップ .lnk が無ければ作成（あれば true）。
        /// Packaged の場合は同期 wait で IsEnabled/Enable を呼ぶ（UIでは async 推奨）。
        /// </summary>
        public static bool EnsureEnabled()
        {
            if (PackagedHelper.IsPackaged())
            {
                try
                {
                    if (IsEnabledAsync().GetAwaiter().GetResult()) return true;
                    return EnableAsync().GetAwaiter().GetResult();
                }
                catch { return false; }
            }

            var lnk = GetStartupShortcutPath();
            if (File.Exists(lnk)) return true;

            // COM(.lnk) は STA 必須。呼び出し元が MTA の場合は別スレッドで。
            CreateStartupShortcut(lnk, Application.ExecutablePath, Path.GetDirectoryName(Application.ExecutablePath));
            return File.Exists(lnk);
        }

        /// <summary>
        /// 非パッケージ(.exe)：スタートアップの有効/無効を切り替える。
        /// Packaged の場合は Enable/Disable を呼ぶ（UI側では async 推奨）。
        /// </summary>
        public static void SetEnabled(bool enable)
        {
            if (PackagedHelper.IsPackaged())
            {
                if (enable) EnableAsync().GetAwaiter().GetResult();
                else        DisableAsync().GetAwaiter().GetResult();
                return;
            }

            var lnk = GetStartupShortcutPath();
            if (!enable)
            {
                if (File.Exists(lnk))
                {
                    try { File.Delete(lnk); } catch { /* ignore */ }
                }
                return;
            }

            // enable = true → .lnk 作成/更新
            CreateStartupShortcut(lnk, Application.ExecutablePath, Path.GetDirectoryName(Application.ExecutablePath));
        }

        // ===================== helpers =====================

        private static string GetStartupShortcutPath()
        {
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startupDir, Application.ProductName + ".lnk");
        }

        // .lnk を作成/上書き（WScript.Shell 経由）— STA 必須
        private static void CreateStartupShortcut(string lnkPath, string targetPath, string workingDir, string arguments = null, string iconPath = null, int iconIndex = 0)
        {
            // 既存が壊れていたら消す
            try
            {
                var dir = Path.GetDirectoryName(lnkPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(lnkPath)) File.Delete(lnkPath);
            }
            catch { /* ignore */ }

            var clsidWscriptShell = new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"); // WScript.Shell
            object shell = null, shortcut = null;
            try
            {
                var t = Type.GetTypeFromCLSID(clsidWscriptShell);
                shell = Activator.CreateInstance(t);
                shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });

                t.InvokeMember("TargetPath",       BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir ?? Path.GetDirectoryName(targetPath) });
                if (!string.IsNullOrEmpty(arguments))
                    t.InvokeMember("Arguments",    BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                t.InvokeMember("IconLocation",     BindingFlags.SetProperty, null, shortcut, new object[] { (iconPath ?? targetPath) + "," + iconIndex.ToString() });
                t.InvokeMember("Description",      BindingFlags.SetProperty, null, shortcut, new object[] { Application.ProductName });

                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null)    Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}

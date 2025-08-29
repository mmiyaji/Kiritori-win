using Kiritori.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms; // Application.ProductName / ExecutablePath 用

namespace Kiritori.Startup
{
    public static class StartupManager
    {
        private const string TaskId = "KiritoriStartup";

        private static Type TStartupTask =>
            Type.GetType("Windows.ApplicationModel.StartupTask, Windows, ContentType=WindowsRuntime");

        // ===== 既存 =====
        public static async Task<bool> IsEnabledAsync()
        {
            if (!PackagedHelper.IsPackaged()) return false;
            var tStartupTask = TStartupTask;
            if (tStartupTask == null) return false;

            var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
            var op = getAsync.Invoke(null, new object[] { TaskId });
            var startupTask = await AwaitIAsyncOperation(op); // StartupTask インスタンス

            var propState = tStartupTask.GetProperty("State");
            int stateVal = Convert.ToInt32(propState.GetValue(startupTask));

            // Disabled=0, EnabledByUser=1, Enabled=2, DisabledByUser=3, EnabledByPolicy=4
            return stateVal == 2 || stateVal == 4 || stateVal == 1;
        }

        public static async Task<bool> EnableAsync()
        {
            if (!PackagedHelper.IsPackaged()) return false;
            var tStartupTask = TStartupTask;
            if (tStartupTask == null) return false;

            var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
            var op = getAsync.Invoke(null, new object[] { TaskId });
            var startupTask = await AwaitIAsyncOperation(op);

            var propState = tStartupTask.GetProperty("State");
            int stateVal = Convert.ToInt32(propState.GetValue(startupTask));

            if (stateVal == 0 /*Disabled*/ || stateVal == 3 /*DisabledByUser*/)
            {
                // ユーザー同意 UI が出る。UI スレッドで呼ぶのが安全。
                var reqEnable = tStartupTask.GetMethod("RequestEnableAsync");
                var op2 = reqEnable.Invoke(startupTask, null);
                var res = await AwaitIAsyncOperation(op2); // StartupTaskState
                stateVal = Convert.ToInt32(res);
            }

            return stateVal == 2 || stateVal == 4 || stateVal == 1;
        }

        public static async Task DisableAsync()
        {
            if (!PackagedHelper.IsPackaged()) return;
            var tStartupTask = TStartupTask;
            if (tStartupTask == null) return;

            var getAsync = tStartupTask.GetMethod("GetAsync", BindingFlags.Public | BindingFlags.Static);
            var op = getAsync.Invoke(null, new object[] { TaskId });
            var startupTask = await AwaitIAsyncOperation(op);

            var disable = tStartupTask.GetMethod("Disable");
            disable.Invoke(startupTask, null);
        }

        // ===== ここから 追加分 =====

        /// <summary>
        /// Packaged の場合：有効でないなら RequestEnableAsync まで行って“有効化を保証”する。
        /// Unpackaged の場合：.lnk を作成して“有効化を保証”する。
        /// </summary>
        public static async Task<bool> EnsureEnabledAsync()
        {
            if (PackagedHelper.IsPackaged())
            {
                // すでに有効なら終了、そうでなければ有効化を試みる
                if (await IsEnabledAsync().ConfigureAwait(false)) return true;
                try { return await EnableAsync().ConfigureAwait(false); }
                catch { return await IsEnabledAsync().ConfigureAwait(false); }
            }
            else
            {
                // 非同期 API はないので同期版に委譲
                return EnsureEnabled();
            }
        }

        /// <summary>
        /// 非パッケージ(.exe)向け：スタートアップ .lnk が無ければ作る（あれば true）。
        /// Packaged なら IsEnabledAsync→EnableAsync を同期待ちするフォールバック。
        /// </summary>
        public static bool EnsureEnabled()
        {
            if (PackagedHelper.IsPackaged())
            {
                try
                {
                    // 同期で待つ（UI で呼ぶ場合はなるべく EnsureEnabledAsync を）
                    if (IsEnabledAsync().GetAwaiter().GetResult()) return true;
                    return EnableAsync().GetAwaiter().GetResult();
                }
                catch { return false; }
            }

            var lnk = GetStartupShortcutPath();
            if (File.Exists(lnk)) return true;

            // COM(.lnk) は STA 必須。呼び出し側が STA でないなら別スレッドで実行して下さい。
            CreateStartupShortcut(lnk, Application.ExecutablePath, Path.GetDirectoryName(Application.ExecutablePath));
            return File.Exists(lnk);
        }

        /// <summary>
        /// 非パッケージ(.exe)向け：スタートアップの有効/無効を切り替える。
        /// Packaged の場合は Enable/Disable を呼ぶ（必要なら UI スレッドで）。
        /// </summary>
        public static void SetEnabled(bool enable)
        {
            if (PackagedHelper.IsPackaged())
            {
                // 可能なら同期 wait。UI 側では async を推奨。
                if (enable) EnableAsync().GetAwaiter().GetResult();
                else DisableAsync().GetAwaiter().GetResult();
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

            // enable = true → .lnk を作成/更新
            CreateStartupShortcut(lnk, Application.ExecutablePath, Path.GetDirectoryName(Application.ExecutablePath));
        }

        // ===== helpers =====

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

                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir ?? Path.GetDirectoryName(targetPath) });
                if (!string.IsNullOrEmpty(arguments))
                    t.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { (iconPath ?? targetPath) + "," + iconIndex.ToString() });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ProductName });

                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static async Task<object> AwaitIAsyncOperation(object iasyncOp)
        {
            if (iasyncOp == null)
                throw new InvalidOperationException("IAsyncOperation object is null.");

            var opType = iasyncOp.GetType();

            // IAsyncOperation<T> → Task<T> へブリッジ
            var iAsyncOperationOpen = Type.GetType("Windows.Foundation.IAsyncOperation`1, Windows, ContentType=WindowsRuntime");
            if (iAsyncOperationOpen != null)
            {
                var iAsyncIface = opType.GetInterfaces()
                    .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == iAsyncOperationOpen);

                if (iAsyncIface != null)
                {
                    var resultType = iAsyncIface.GetGenericArguments()[0];
                    var extType = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime");
                    if (extType == null) throw new InvalidOperationException("System.Runtime.WindowsRuntime not found.");

                    var asTaskGen = extType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "AsTask"
                                        && m.IsGenericMethodDefinition
                                        && m.GetGenericArguments().Length == 1
                                        && m.GetParameters().Length == 1);

                    var asTaskClosed = asTaskGen.MakeGenericMethod(resultType);
                    var taskObj = asTaskClosed.Invoke(null, new object[] { iasyncOp }); // Task<TResult>
                    var task = (Task)taskObj;
                    await task.ConfigureAwait(false);

                    var propResult = taskObj.GetType().GetProperty("Result");
                    return propResult?.GetValue(taskObj);
                }
            }

            // フォールバック：Status/GetResults をポーリング
            var propStatus = opType.GetProperty("Status");
            var getResults = opType.GetMethod("GetResults");
            var propError = opType.GetProperty("ErrorCode");
            if (propStatus == null || getResults == null)
                throw new InvalidOperationException("Object is not a valid IAsyncOperation.");

            while (true)
            {
                int status = Convert.ToInt32(propStatus.GetValue(iasyncOp, null)); // 0 Started, 1 Completed, 2 Canceled, 3 Error
                if (status == 1) return getResults.Invoke(iasyncOp, null);
                if (status == 2) throw new TaskCanceledException("IAsyncOperation was canceled.");
                if (status == 3)
                {
                    var ex = propError?.GetValue(iasyncOp, null) as Exception;
                    if (ex != null) throw new InvalidOperationException($"IAsyncOperation failed. HResult=0x{ex.HResult:X8} Message={ex.Message}", ex);
                    throw new InvalidOperationException("IAsyncOperation failed.");
                }
                await Task.Delay(20).ConfigureAwait(false);
            }
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Kiritori.Services.Extensions;
using Kiritori.Services.Logging;

namespace Kiritori.Services.Notifications
{
    internal static class ToastRuntime
    {
        private static bool _resolveHooked;
        private static bool _declined; // ユーザーが導入を拒否したセッション中フラグ

        private static string GetToastDllPath()
        {
            try
            {
                // 規約: %LocalAppData%\Kiritori\bin\toast\<ver>\CommunityToolkit.WinUI.Notifications.dll
                var dir = ExtensionsManager.GetInstallDir("toast");
                if (string.IsNullOrEmpty(dir))
                {
                    var ver = ExtensionsManager.InstalledVersion("toast");
                    if (!string.IsNullOrEmpty(ver))
                        dir = Path.Combine(ExtensionsPaths.Root, "bin", "toast", ver);
                }
                if (string.IsNullOrEmpty(dir)) return null;

                var dll = Path.Combine(dir, "CommunityToolkit.WinUI.Notifications.dll");
                return File.Exists(dll) ? dll : null;
            }
            catch { return null; }
        }

        private static void EnsureAssemblyResolveHook()
        {
            if (_resolveHooked) return;
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                try
                {
                    var name = new AssemblyName(e.Name).Name;
                    if (!name.Equals("CommunityToolkit.WinUI.Notifications", StringComparison.OrdinalIgnoreCase))
                        return null;

                    var dll = GetToastDllPath();
                    return (dll != null && File.Exists(dll)) ? Assembly.LoadFrom(dll) : null;
                }
                catch { return null; }
            };
            _resolveHooked = true;
        }

        /// <summary>
        /// Toast拡張を使える状態にする。未導入ならプロンプトで導入。
        /// ユーザーが拒否したら OperationCanceledException を投げる（FFmpegと同様）。
        /// </summary>
        public static bool EnsureAvailable(IWin32Window owner, bool promptToInstall)
        {
            if (_declined) return false;

            // 既に導入済みなら解決フックだけ掛けてOK
            if (ExtensionsManager.IsInstalled("toast"))
            {
                EnsureAssemblyResolveHook();
                return GetToastDllPath() != null;
            }

            // 未導入: プロンプトポリシーに従う
            if (!promptToInstall) return false;

            try
            {
                // 導入確認（拒否なら例外で上に伝播）
                if (ExtensionsAuto.TryEnsure("toast", owner, prompt: true, throwOnDecline: true))
                {
                    EnsureAssemblyResolveHook();
                    return GetToastDllPath() != null;
                }
            }
            catch (OperationCanceledException)
            {
                _declined = true; // セッション中は二度と聞かない
                throw;
            }
            return false;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Startup;

namespace Kiritori.Services.Notifications
{
    internal static class ToastBootstrapper
    {
        internal const string Aumid = "Kiritori.Desktop";

        internal static void Initialize()
        {
            // Packaged (MSIX) の場合は AUMID 登録不要
            if (!PackagedHelper.IsPackaged())
            {
                DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyToastActivator>("Kiritori.Desktop");
                DesktopNotificationManagerCompat.RegisterActivator<MyToastActivator>();
            }
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                try
                {
                    var ta = ToastArguments.Parse(e.Argument ?? string.Empty);

                    string action = null;
                    string path = null;
                    ta.TryGetValue("action", out action);
                    ta.TryGetValue("path", out path);

                    // 既定動作（本文クリックで引数が無い場合の保険）
                    if (string.IsNullOrEmpty(action)) action = "open";

                    // UI スレッドへ
                    var anyForm = System.Windows.Forms.Application.OpenForms.Count > 0
                                ? System.Windows.Forms.Application.OpenForms[0]
                                : null;

                    void RunOnUI(Action act)
                    {
                        if (anyForm != null && anyForm.IsHandleCreated)
                            anyForm.BeginInvoke(act);
                        else
                        {
                            // 念のため単発 STA スレッドでも対応
                            var t = new System.Threading.Thread(() => act());
                            t.SetApartmentState(System.Threading.ApartmentState.STA);
                            t.Start();
                        }
                    }

                    if (action == "copy" && !string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        //RunOnUI(() => CopyImageToClipboardSafe(path));
                    }
                    else if (action == "open" && !string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    else if (action == "openFolder")
                    {
                        var dir = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir))
                            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[Toast OnActivated] " + ex);
                }
            };


            // クリック時ハンドラ（どこか1回だけ購読）
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                var args = ToastArguments.Parse(e.Argument);
                var action = args.Get("action");
                var path = args.Get("path");
                // TODO: あなたの処理へ委譲
            };
        }

        private static void EnsureStartMenuShortcut(string linkName, string aumid, string exePath)
        {
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            string linkPath = Path.Combine(startMenu, "Programs", linkName);
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? startMenu);
            if (File.Exists(linkPath)) return;

            // （先にお渡しした IShellLink / IPropertyStore の実装をそのまま使用）
            ShellLinkWriter.CreateOrUpdateShortcutWithAumid(linkPath, exePath, aumid);
        }
    }
}

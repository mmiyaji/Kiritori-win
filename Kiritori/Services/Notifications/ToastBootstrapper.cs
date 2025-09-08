using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications; // DesktopNotificationManagerCompat, ToastArguments
using System.Runtime.CompilerServices; 
using Kiritori.Helpers;
using Kiritori.Services.Logging;

namespace Kiritori.Services.Notifications
{
    internal static class ToastBootstrapper
    {
        private static string Aumid = NotificationService.GetAppAumid();
        private const string ShortcutName = "Kiritori";     // Startメニューに表示される名前

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        private static bool _initialized;

        internal static void Initialize()
        {
            Log.Debug($"Initialize: {Aumid}", "Toast");
            if (_initialized) return;
            _initialized = true;
            if (!ToastRuntime.EnsureAvailable(owner: null, promptToInstall: false))
            {
                Log.Info("Toast extension not available. Skip bootstrap.", "Toast");
                return;
            }
            InitToolkit(Aumid);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitToolkit(string aumid)
        {
#pragma warning disable CS0618
            if (!PackagedHelper.IsPackaged())
            {
                EnsureStartMenuShortcut(ShortcutName + ".lnk", aumid, Application.ExecutablePath);
                DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyToastActivator>(aumid);
                DesktopNotificationManagerCompat.RegisterActivator<MyToastActivator>();
                try { SetCurrentProcessExplicitAppUserModelID(aumid); } catch { }
            }
            else
            {
                DesktopNotificationManagerCompat.RegisterActivator<MyToastActivator>();
            }
#pragma warning restore CS0618

            // クリック時ハンドラ（既存の中身をそのまま移動）
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                try
                {
                    var ta = ToastArguments.Parse(e.Argument ?? string.Empty);
                    var action = ta.Get("action") ?? "open";
                    var path   = ta.Get("path");

                    var anyForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                    void OnUI(Action a)
                    {
                        if (anyForm != null && anyForm.IsHandleCreated) anyForm.BeginInvoke(a);
                        else { var t = new System.Threading.Thread(() => a()); t.SetApartmentState(System.Threading.ApartmentState.STA); t.Start(); }
                    }

                    if (action == "open" && !string.IsNullOrEmpty(path) && File.Exists(path))
                        OnUI(() => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
                    else if (action == "openFolder" && !string.IsNullOrEmpty(path))
                        OnUI(() =>
                        {
                            var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                        });
                }
                catch (Exception ex)
                {
                    Log.Debug($"[Toast OnActivated] {ex}", "Toast");
                }
            };
        }

        private static void EnsureStartMenuShortcut(string linkName, string aumid, string exePath)
        {
            if (string.IsNullOrWhiteSpace(linkName)) throw new ArgumentException(nameof(linkName));
            if (string.IsNullOrWhiteSpace(aumid)) throw new ArgumentException(nameof(aumid));
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentException(nameof(exePath));

            // 拡張子が無ければ .lnk を付ける
            if (Path.GetExtension(linkName).Length == 0)
                linkName += ".lnk";

            // 「スタートメニュー > プログラム」直下のパスを取得
            var programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (string.IsNullOrEmpty(programsDir))
                throw new InvalidOperationException("Failed to resolve Start Menu Programs folder.");

            var linkPath = Path.Combine(programsDir, linkName);

            // 存在しなくても OK（存在しても例外にならない）
            Directory.CreateDirectory(programsDir);

            // 既存 .lnk があっても AUMID 更新が必要なことがあるので、毎回「作成または更新」するのがおすすめ
            // 既存なら何もしない方が良ければ、事前に File.Exists(linkPath) を見て return してください。
            Startup.ShellLinkWriter.CreateOrUpdateShortcutWithAumid(linkPath, exePath, aumid);
        }
    }
}

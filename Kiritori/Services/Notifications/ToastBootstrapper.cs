using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications; // DesktopNotificationManagerCompat, ToastArguments
using Kiritori.Helpers;

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
            Debug.WriteLine("[ToastBootstrapper] Initialize: " + Aumid);
            if (_initialized) return;
            _initialized = true;

            #pragma warning disable CS0618
            if (!PackagedHelper.IsPackaged())
            {
                // 1) Start メニューに AUMID 付きショートカットを用意
                EnsureStartMenuShortcut(ShortcutName + ".lnk", Aumid, Application.ExecutablePath);

                // 2) AUMID と COM アクティベータを登録（非管理者でOK）
                DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyToastActivator>(Aumid);
                DesktopNotificationManagerCompat.RegisterActivator<MyToastActivator>();

                // 3) （推奨）プロセスにも AUMID を付与
                try { SetCurrentProcessExplicitAppUserModelID(Aumid); } catch { }
            }
            else
            {
                // パッケージ時は AUMID/lnk 不要。OnActivated を使うため RegisterActivator のみ。
                DesktopNotificationManagerCompat.RegisterActivator<MyToastActivator>();
            }
            #pragma warning restore CS0618
            // クリック時ハンドラ（重複登録しない）
            ToastNotificationManagerCompat.OnActivated += e =>
            {
                try
                {
                    var ta = ToastArguments.Parse(e.Argument ?? string.Empty);
                    var action = ta.Get("action") ?? "open";
                    var path   = ta.Get("path");

                    // UIスレッドへ（起動直後などフォームが無いケースも考慮）
                    var anyForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                    void OnUI(Action a)
                    {
                        if (anyForm != null && anyForm.IsHandleCreated) anyForm.BeginInvoke(a);
                        else
                        {
                            var t = new System.Threading.Thread(() => a());
                            t.SetApartmentState(System.Threading.ApartmentState.STA);
                            t.Start();
                        }
                    }

                    if (action == "open" && !string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        OnUI(() => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
                    }
                    else if (action == "openFolder" && !string.IsNullOrEmpty(path))
                    {
                        OnUI(() =>
                        {
                            var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir))
                                Process.Start("explorer.exe", "/select,\"" + path + "\"");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Toast OnActivated] " + ex);
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

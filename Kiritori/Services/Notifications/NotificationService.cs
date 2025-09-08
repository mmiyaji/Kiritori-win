using Kiritori.Services.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications; // ToastContentBuilder, ToastNotificationManagerCompat

namespace Kiritori.Services.Notifications
{
    internal static class NotificationService
    {
        private const string AppAumid = "Kiritori.Desktop";

        public static string GetAppAumid()
        {
            return AppAumid;
        }
        public static void ShowInfo(string title, string message, IWin32Window owner = null)
        {
            try
            {
                // 送信直前に可用化（未導入ならプロンプト／拒否は例外で中断）
                if (!ToastRuntime.EnsureAvailable(owner, promptToInstall: true))
                {
                    ShowBalloon(title, message);
                    return;
                }

                // Toolkit をここで初めて触る
                // var content = new ToastContentBuilder()
                var content = new CommunityToolkit.WinUI.Notifications.ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .GetToastContent();

                // ToastNotificationManagerCompat.CreateToastNotifier(AppAumid)
                //     .Show(new Windows.UI.Notifications.ToastNotification(content.GetXml()));
                ToastBootstrapper.Initialize();
                CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat
                    .CreateToastNotifier()
                    .Show(new Windows.UI.Notifications.ToastNotification(content.GetXml()));
            }
            catch (OperationCanceledException)
            {
                // ユーザーが導入を拒否 → 何も出さない（必要なら ShowBalloon に変えてOK）
                return;
            }
            catch (Exception ex)
            {
                Log.Warn($"Toast failed, fallback to balloon: {ex.Message}", "Notifications");
                ShowBalloon(title, message);
            }
        }

        private static void ShowBalloon(string title, string message)
        {
            try
            {
                var owner = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                var ni = new NotifyIcon
                {
                    Visible = true,
                    Icon = owner?.Icon ?? System.Drawing.SystemIcons.Information,
                    BalloonTipTitle = title,
                    BalloonTipText = message
                };
                ni.ShowBalloonTip(4000);
                var t = new Timer { Interval = 4500 };
                t.Tick += (s, e) => { ni.Visible = false; ni.Dispose(); t.Stop(); t.Dispose(); };
                t.Start();
            }
            catch { /* ignore */ }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using Kiritori.Helpers;
using Kiritori.Startup;

namespace Kiritori
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        /// 
        // Win10+: 最も強力（Per-Monitor v2）
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4); // 定数

        // Win8.1+: Per-Monitor（v1）
        enum PROCESS_DPI_AWARENESS
        {
            PROCESS_DPI_UNAWARE = 0,
            PROCESS_SYSTEM_DPI_AWARE = 1,
            PROCESS_PER_MONITOR_DPI_AWARE = 2
        }
        [DllImport("Shcore.dll")] static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS value);

        // Vista/Win7: System-DPI aware
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [DllImport("Shcore.dll")] static extern int GetProcessDpiAwareness(IntPtr hprocess, out int value);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();



        [STAThread]
        static void Main()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.UICulture);
            }
            catch { }
            // ===== DPI Awareness を可能な限り高く設定 =====
            bool dpiSet = false;
            try
            {
                // Win10 以降なら PMv2 を要求
                dpiSet = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { /* OS が古い等 */ }

            if (!dpiSet)
            {
                try
                {
                    // Win8.1 以降なら Per-Monitor（v1）
                    int hr = SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
                    dpiSet = (hr == 0 /*S_OK*/ || hr == 0x5 /*E_ACCESSDENIED: 既に設定済み*/);
                }
                catch { }
            }

            if (!dpiSet)
            {
                try
                {
                    // さらに古い OS では System-DPI Aware
                    dpiSet = SetProcessDPIAware();
                }
                catch { }
            }

            // （任意）今の DPI Awareness をデバッグ出力
            try
            {
                int v; GetProcessDpiAwareness(GetCurrentProcess(), out v);
                // 0:Unaware 1:System 2:PerMonitor
                System.Diagnostics.Debug.WriteLine($"[DPI] Awareness={v}");
            }
            catch { }

            // 例: Program.cs / Main の最初で
            Kiritori.Services.Notifications.ToastBootstrapper.Initialize();

            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainApplication());
        }
        static void CopyImageToClipboardSafe(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs)) // ここでの img はストリームに依存
                using (var bmp = new Bitmap(img))      // 独立したコピーを作る（ファイルロック回避）
                {
                    // クリップボード競合に備えて軽くリトライ（任意）
                    const int maxTry = 3;
                    for (int i = 0; i < maxTry; i++)
                    {
                        try
                        {
                            Clipboard.SetImage(bmp);
                            // （任意）トレイバルーン or トーストで「コピーしました」通知してもOK
                            break;
                        }
                        catch (ExternalException) // クリップボードが他プロセスにロック等
                        {
                            Thread.Sleep(60);
                            if (i == maxTry - 1) throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 失敗時はログだけ（必要ならバルーンでエラー表示）
                System.Diagnostics.Debug.WriteLine("[CopyImageToClipboardSafe] " + ex);
            }
        }
        static void HealUserConfig()
        {
            try
            {
                Kiritori.Properties.Settings.Default.Upgrade();
                Kiritori.Properties.Settings.Default.Save();
            }
            catch (ConfigurationErrorsException ex)
            {
                var bad = (ex.InnerException as ConfigurationErrorsException)?.Filename;
                if (!string.IsNullOrEmpty(bad) && File.Exists(bad))
                {
                    try { File.Delete(bad); } catch { }
                    Kiritori.Properties.Settings.Default.Reload();
                    Kiritori.Properties.Settings.Default.Save();
                    Debug.WriteLine("[Settings] user.config reset: " + bad);
                }
            }
        }
    }
    #pragma warning disable CS0618
    [ComVisible(true)]
    [Guid("013AE16E-2760-4474-845B-7F47DF556826")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class MyToastActivator : CommunityToolkit.WinUI.Notifications.NotificationActivator
    {
        public override void OnActivated(string arguments,
                                        CommunityToolkit.WinUI.Notifications.NotificationUserInput userInput,
                                        string appUserModelId) { }
    }
    #pragma warning restore CS0618
}
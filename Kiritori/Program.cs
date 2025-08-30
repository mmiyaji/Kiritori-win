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
    internal enum AppStartupMode
    {
        Normal,
        Viewer
    }

    internal sealed class AppStartupOptions
    {
        internal AppStartupMode Mode { get; set; } = AppStartupMode.Normal;
        internal string[] ImagePaths { get; set; } = Array.Empty<string>();
    }

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
        static void Main(string[] args)
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

            Debug.WriteLine($"[Startup] Args: {string.Join(" ", args ?? Array.Empty<string>())}");
            var opt = ParseArgs(args);
            using (var mutex = new Mutex(true, SingleInstance.MutexName, out bool isNew))
                {
                    if (isNew)
                    {
                        // 1) まず待受け開始（Mainより前）
                        SingleInstance.StartServer(null);

                        // 2) Main を作成
                        var main = new MainApplication(opt);

                        // 3) Main ができたらハンドラを登録（キュー分も即消化）
                        SingleInstance.SetHandler(paths =>
                        {
                            if (main != null && main.IsHandleCreated)
                                main.BeginInvoke(new Action(() => main.OpenImagesFromIpc(paths)));
                        });

                        Application.ApplicationExit += (s, e) => SingleInstance.StopServer();
                        Application.Run(main);
                    }
                    else
                    {
                        // 既存へ渡して終了（リトライ込み）
                        if (opt.Mode == AppStartupMode.Viewer && opt.ImagePaths.Length > 0)
                            if (SingleInstance.TrySendToExisting(opt.ImagePaths)) return;

                        // 渡すものがない場合は単に終了でもOK
                        return;
                    }
                }
        }
        private static AppStartupOptions ParseArgs(string[] args)
        {
            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" /*, ".heic"*/ };

            var files = (args ?? Array.Empty<string>())
                .Select(a => a?.Trim('"'))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Where(File.Exists)
                .Where(p => exts.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Distinct()
                .ToArray();

            Debug.WriteLine($"[Startup] Parsed files: {string.Join(", ", files)}");

            return files.Length > 0
                ? new AppStartupOptions { Mode = AppStartupMode.Viewer, ImagePaths = files }
                : new AppStartupOptions { Mode = AppStartupMode.Normal };
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
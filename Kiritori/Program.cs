using Kiritori.Helpers;
using Kiritori.Startup;
using Kiritori.Services.Logging;
using Kiritori.Services.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using System.Configuration;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
            var logopt = LoggerSettingsLoader.LoadFromSettings();
#if DEBUG
            if (!logopt.WriteToDebug) logopt.WriteToDebug = true;
            if (logopt.MinLevel > LogLevel.Debug) logopt.MinLevel = LogLevel.Debug;
            if (!logopt.WriteToFile) logopt.WriteToFile = true;
#endif
            Log.Configure(logopt);
            Application.ApplicationExit += (s, e) => Log.Shutdown();
            Kiritori.Services.Logging.LogViewSharedSink.EnsureRegistered();
            Log.Info($"Kiritori starting (v{Application.ProductVersion})", "Startup");

            // SatelliteBootstrapper.EnsureSatellitesExtracted();
            Kiritori.Helpers.SatelliteBootstrapper.Init();

            EarlyExtensionsInit();
            RegisterAssemblyResolvers();
            try
            {
                var culture = Properties.Settings.Default.UICulture;
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                Log.Debug("Set UI Culture: " + culture, "Startup");
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
                Log.Debug($"[DPI] Awareness={v}", "Startup");
            }
            catch { }

            // 例: Program.cs / Main の最初で
            // Kiritori.Services.Notifications.ToastBootstrapper.Initialize();
            try
            {
                if (ExtensionsManager.IsInstalled("toast") && ExtensionsManager.IsEnabled("toast"))
                {
                    Log.Debug("Toast start initialize", "Startup");
                    Kiritori.Services.Notifications.ToastBootstrapper.Initialize();
                    Log.Debug("Toast initialized", "Startup");
                }
                else
                {
                    Log.Debug("Toast extension not installed or not enabled", "Startup");
                }
            }
            catch (Exception ex)
            {
                Log.Debug("" + ex, "Ext");
            }
            try { Kiritori.Services.Extensions.ExtensionsManager.RepairStateIfMissing(); } catch { }

            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Log.Info($"Exec Args: {string.Join(" ", args ?? Array.Empty<string>())}", "Startup");
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
        private static void RegisterAssemblyResolvers()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                try
                {
                    var an = new System.Reflection.AssemblyName(e.Name);

                    // 1) 衛星アセンブリ（i18n）：Kiritori.resources.dll を i18n\<cul>\ 下から解決
                    if (an.Name != null && an.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                    {
                        var culture = an.CultureName; // "ja" など
                        if (!string.IsNullOrEmpty(culture))
                        {
                            var baseDir = System.IO.Path.Combine(
                                Kiritori.Services.Extensions.ExtensionsPaths.Root, "i18n", culture);
                            var resPath = System.IO.Path.Combine(baseDir, "Kiritori.resources.dll");
                            Kiritori.Services.Logging.Log.Debug("[AssemblyResolve] Resource " + culture + " => " + resPath, "Startup");
                            if (System.IO.File.Exists(resPath))
                                return System.Reflection.Assembly.LoadFrom(resPath);
                        }
                    }

                    // 2) Toast 拡張（導入・有効なときのみ）
                    if (Kiritori.Services.Extensions.ExtensionsManager.IsInstalled("toast") &&
                        Kiritori.Services.Extensions.ExtensionsManager.IsEnabled("toast"))
                    {
                        var ver = Kiritori.Services.Extensions.ExtensionsManager.InstalledVersion("toast") ?? "1.0.0";
                        var dir = System.IO.Path.Combine(
                            Kiritori.Services.Extensions.ExtensionsPaths.Root, "bin", "toast", ver);
                        var name = an.Name + ".dll";
                        var p = System.IO.Path.Combine(dir, name);
                        Kiritori.Services.Logging.Log.Debug("[AssemblyResolve] Toast " + name + " => " + p, "Startup");
                        if (System.IO.File.Exists(p))
                            return System.Reflection.Assembly.LoadFrom(p);
                    }
                }
                catch
                {
                    // 失敗時は null を返して標準解決に委ねる
                }

                return null;
            };
        }

        static void InstallSatellitesFromAppFolder()
        {
            try
            {
                var appI18n = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
                if (!Directory.Exists(appI18n)) return;

                var destRoot = Path.Combine(ExtensionsPaths.Root, "i18n"); // 既に使っている場所
                Directory.CreateDirectory(destRoot);

                foreach (var culDir in Directory.EnumerateDirectories(appI18n)) // 例: ...\i18n\ja
                {
                    var cul = Path.GetFileName(culDir);
                    var src = Path.Combine(culDir, "Kiritori.resources.dll");
                    if (!File.Exists(src)) continue;

                    var destDir = Path.Combine(destRoot, cul);
                    var dest = Path.Combine(destDir, "Kiritori.resources.dll");
                    Directory.CreateDirectory(destDir);

                    // 既にある場合はサイズ/更新時刻で簡易判定して上書き（必要ならバージョン比較でもOK）
                    bool needCopy = !File.Exists(dest)
                                || new FileInfo(src).Length != new FileInfo(dest).Length
                                || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest);

                    if (needCopy)
                    {
                        File.Copy(src, dest, true);
                        Log.Info($"[i18n] Installed/updated '{cul}' to {dest}", "Startup");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("[i18n] Install failed: " + ex, "Startup");
            }
        }

        private static void EarlyExtensionsInit()
        {
            try
            {
                Kiritori.Services.Extensions.ExtensionsPaths.EnsureDirs();
                Kiritori.Services.Extensions.ExtensionsManager.RepairStateIfMissing();

                Kiritori.Services.Logging.Log.Info(
                    "Root=" + Kiritori.Services.Extensions.ExtensionsPaths.Root +
                    "  State=" + Kiritori.Services.Extensions.ExtensionsPaths.StateJson,
                    "Extensions");
            }
            catch { /* 起動阻害は避ける */ }
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

            Log.Info($"Parsed files: {string.Join(", ", files)}", "Startup");

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
                Log.Debug("[CopyImageToClipboardSafe] " + ex, "Startup");
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
                    Log.Debug("user.config reset: " + bad, "Startup");
                }
            }
        }
    }
    public static class LoggerSettingsLoader
    {
        public static Kiritori.Services.Logging.LoggerOptions LoadFromSettings()
        {
            var o = new Kiritori.Services.Logging.LoggerOptions();
            try
            {
                // ※ Properties.Settings.Default に追加した設定項目を読み取る
                var s = Properties.Settings.Default;
                o.Enabled           = s.LogEnabled;
                o.MinLevel          = (LogLevel) s.LogMinLevel;
                o.WriteToDebug      = s.LogWriteToDebug;
                o.WriteToFile       = s.LogWriteToFile;
                o.FilePath          = string.IsNullOrEmpty(s.LogFilePath)
                                    ? o.FilePath : s.LogFilePath;
                o.MaxFileSizeBytes  = s.LogMaxFileSizeBytes;
                o.MaxRollFiles      = s.LogMaxRollFiles;
                o.IncludeTimestamp  = s.LogIncludeTimestamp;
                o.IncludeThreadId   = s.LogIncludeThreadId;
                o.IncludeProcessId  = s.LogIncludeProcessId;
                o.IncludeCategoryTag= s.LogIncludeCategoryTag;
                o.TimestampFormat   = string.IsNullOrEmpty(s.LogTimestampFormat)
                                    ? o.TimestampFormat : s.LogTimestampFormat;
            }
            catch { /* 既定値で継続 */ }
            return o;
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
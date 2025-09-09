using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsManager
    {
        public static ExtensionState State { get; private set; } = ExtensionState.Load();
        internal struct DownloadProgress
        {
            public int Percent;    // 0-100, 未設定は -1
            public string Stage;   // "ダウンロード中…", "検証中…", "展開中…" など
            public long BytesReceived;
            public long TotalBytes;
        }


        public static IEnumerable<ExtensionManifest> LoadRepoManifests()
        {
            var list = new List<ExtensionManifest>();
            try
            {
                // 1) 外部ディレクトリ群（ユーザー領域→出力同梱→開発時の親）を走査
                foreach (var dir in ExtensionsPaths.CandidateManifestDirs())
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var m = LoadManifest(f);
                        if (m != null) list.Add(m);
                    }
                    Kiritori.Services.Logging.Log.Debug($"[Ext] Manifests scanned: {dir} => {list.Count}", "Extensions");
                }

                // 2) 見つからなければ EXE に埋め込んだデフォルトを使う
                if (list.Count == 0)
                {
                    var emb = ExtensionsEmbedded.LoadEmbedded();
                    list.AddRange(emb);
                    Kiritori.Services.Logging.Log.Info($"[Ext] Using embedded default manifests: {emb.Count}", "Extensions");
                }

                // 3) 同一IDが重複したら最初のものを採用
                list = list
                    .GroupBy(m => m.Id ?? "", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Kiritori.Services.Logging.Log.Error("[Ext] LoadRepoManifests failed: " + ex.Message, "Extensions");
            }
            return list;
        }


        private static IEnumerable<ExtensionManifest> LoadAllFrom(string dir)
        {
            var list = new List<ExtensionManifest>();
            try
            {
                if (!Directory.Exists(dir)) return list;
                foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var m = LoadManifest(f);
                    if (m != null) list.Add(m);
                }
            }
            catch { }
            return list;
        }

        public static ExtensionManifest LoadManifest(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(ExtensionManifest));
                    return (ExtensionManifest)ser.ReadObject(fs);
                }
            }
            catch { return null; }
        }

        public static bool IsInstalled(string id)
        {
            ExtensionState.Item it;
            return State.Items.TryGetValue(id, out it) && it.Installed;
        }
        public static bool IsEnabled(string id)
        {
            ExtensionState.Item it;
            return State.Items.TryGetValue(id, out it) && it.Enabled;
        }
        public static string InstalledVersion(string id)
        {
            ExtensionState.Item it;
            return State.Items.TryGetValue(id, out it) ? it.Version : null;
        }

        public static void SetEnabled(string id, bool enabled)
        {
            ExtensionState.Item it;
            if (!State.Items.TryGetValue(id, out it)) it = new ExtensionState.Item();
            it.Enabled = enabled;
            State.Items[id] = it;
            State.Save();
        }

        public static void MarkInstalled(string id, string version)
        {
            ExtensionState.Item it;
            if (!State.Items.TryGetValue(id, out it)) it = new ExtensionState.Item();
            it.Installed = true;
            it.Version = version;
            if (!it.Enabled) it.Enabled = true;
            State.Items[id] = it;
            State.Save();
        }

        public static void MarkUninstalled(string id)
        {
            ExtensionState.Item it;
            if (State.Items.TryGetValue(id, out it))
            {
                it.Installed = false;
                State.Items[id] = it;
                State.Save();
            }
        }

        public static string Install(ExtensionManifest m)
        {
            if (m == null || m.Download == null || m.Install == null) throw new ArgumentException("bad manifest");

            ExtensionsPaths.EnsureDirs();

            // 1) ダウンロード
            var tmpZip = Path.Combine(Path.GetTempPath(), $"kiritori_ext_{m.Id}_{m.Version}.zip");
            using (var wc = new WebClient())
            {
                wc.DownloadFile(m.Download.Url, tmpZip);
            }

            // // 2) 検証
            // if (!ExtensionsUtil.VerifySha256(tmpZip, m.Download.Sha256))
            //     throw new InvalidOperationException("SHA256 verification failed.");
            bool verify = true;
#if DEBUG
            verify = false; // DebugビルドはSHAチェックをスキップ
#endif

            if (verify)
            {
                if (!ExtensionsUtil.VerifySha256(tmpZip, m.Download.Sha256))
                    throw new InvalidOperationException("SHA256 verification failed.");
            }
            else
            {
                Kiritori.Services.Logging.Log.Debug("[Ext] SHA256 skipped (DEBUG build).", "Extensions");
            }
            // 3) 展開
            var target = ExtensionsPaths.Expand(m.Install.TargetDir);
            Directory.CreateDirectory(target);
            // ZipFile.ExtractToDirectory(tmpZip, target);
            ExtensionsZip.ExtractZipAllowOverwrite(tmpZip, target);

            // 4) 必要ファイルが揃っているか軽く検証
            foreach (var f in m.Install.Files ?? Array.Empty<string>())
            {
                var p = Path.Combine(target, f);
                if (!File.Exists(p)) throw new FileNotFoundException("Missing file in extension package.", p);
            }

            MarkInstalled(m.Id, m.Version);
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Ext");
            return target;
        }
        public static void Enable(string id, bool enabled)
        {
            SetEnabled(id, enabled); // 既存の内部APIをラップ
        }

        public static void Uninstall(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            try
            {
                var root = ExtensionsPaths.Root;
                var ver = InstalledVersion(id);

                // bin\<id>\<ver>
                if (!string.IsNullOrEmpty(ver))
                {
                    var dir = Path.Combine(root, "bin", id, ver);
                    SafeDeleteDirectory(dir);

                    // 親（bin\<id>）が空なら掃除
                    var parent = Path.GetDirectoryName(dir);
                    TryDeleteIfEmpty(parent);
                }

                // 言語拡張: id = "lang_xxx" → i18n\xxx
                if (id.StartsWith("lang_", StringComparison.OrdinalIgnoreCase))
                {
                    var culture = id.Substring("lang_".Length);
                    if (!string.IsNullOrWhiteSpace(culture))
                    {
                        var dir = Path.Combine(root, "i18n", culture);
                        SafeDeleteDirectory(dir);
                    }
                }
            }
            catch { /* ベストエフォート */ }

            MarkUninstalled(id); // 状態更新
        }
        public static string GetInstallDir(string id)
        {
            var ver = InstalledVersion(id);
            if (string.IsNullOrEmpty(ver)) return null;

            // 通常ツールは bin/<id>/<ver> を規約に
            return Path.Combine(ExtensionsPaths.Root, "bin", id, ver);
        }
        public static void RepairStateIfMissing()
        {
            try
            {
                ExtensionsPaths.EnsureDirs();
                var path = ExtensionsPaths.StateJson;

                // 既存 state が有効ならそれを採用（「ファイルはあるが空/壊れている」ケースもここで弾く）
                bool needRepair = true;
                try
                {
                    if (File.Exists(path))
                    {
                        var loaded = ExtensionState.Load();
                        if (loaded != null && loaded.Items != null && loaded.Items.Count > 0)
                        {
                            State = loaded;
                            needRepair = false;
                        }
                        else
                        {
                            State = loaded ?? new ExtensionState();
                        }
                    }
                }
                catch
                {
                    // ロード失敗 → 復旧へ
                }

                if (!needRepair) return;

                var st = new ExtensionState();

                // bin\<id>\<ver> をスキャンして最新（文字列降順）を採用
                var binRoot = Path.Combine(ExtensionsPaths.Root, "bin");
                if (Directory.Exists(binRoot))
                {
                    foreach (var idDir in Directory.EnumerateDirectories(binRoot))
                    {
                        var id = Path.GetFileName(idDir);
                        string latestVer = null;

                        foreach (var vDir in Directory.EnumerateDirectories(idDir))
                        {
                            var v = Path.GetFileName(vDir);
                            if (string.IsNullOrEmpty(latestVer) ||
                                string.Compare(v, latestVer, StringComparison.OrdinalIgnoreCase) > 0)
                            {
                                latestVer = v;
                            }
                        }

                        if (!string.IsNullOrEmpty(latestVer))
                        {
                            var vdir = Path.Combine(idDir, latestVer);
                            bool hasAny = false;
                            try
                            {
                                hasAny = Directory.Exists(vdir) &&
                                        Directory.GetFileSystemEntries(vdir).Length > 0;
                            }
                            catch { /* ignore */ }

                            if (hasAny)
                            {
                                st.Items[id] = new ExtensionState.Item
                                {
                                    Installed = true,
                                    Enabled = true,
                                    Version = latestVer
                                };
                            }
                        }
                    }
                }

                // i18n\<culture>\Kiritori.resources.dll → id=lang_<culture>
                var i18nRoot = Path.Combine(ExtensionsPaths.Root, "i18n");
                if (Directory.Exists(i18nRoot))
                {
                    foreach (var culDir in Directory.EnumerateDirectories(i18nRoot))
                    {
                        var cul = Path.GetFileName(culDir);
                        var resDll = Path.Combine(culDir, "Kiritori.resources.dll");
                        if (File.Exists(resDll))
                        {
                            st.Items["lang_" + cul] = new ExtensionState.Item
                            {
                                Installed = true,
                                Enabled = true,
                                Version = "embedded"
                            };
                        }
                    }
                }

                st.Save();
                State = st; // メモリにも反映
                Kiritori.Services.Logging.Log.Info(
                    "[Ext] Repaired missing state: " + path + " (" + st.Items.Count + " items)", "Extensions");
            }
            catch (Exception ex)
            {
                Kiritori.Services.Logging.Log.Warn("[Ext] RepairStateIfMissing failed: " + ex.Message, "Extensions");
            }
        }

        // ---- ここから下はヘルパー（private） ----
        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* ignore */ }
        }
        private static void TryDeleteIfEmpty(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) &&
                    Directory.GetFileSystemEntries(path).Length == 0)
                {
                    Directory.Delete(path, false);
                }
            }
            catch { /* ignore */ }
        }
        public static string InstallWithProgress(
            ExtensionManifest m,
            IProgress<int> progress = null)
        {
            if (m == null || m.Download == null || m.Install == null) 
                throw new ArgumentException("bad manifest");

            ExtensionsPaths.EnsureDirs();

            // 1) ダウンロード
            var tmpZip = Path.Combine(Path.GetTempPath(), $"kiritori_ext_{m.Id}_{m.Version}.zip");
            using (var wc = new WebClient())
            {
                if (progress != null)
                {
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        progress.Report(e.ProgressPercentage);
                        Log.Debug($"[Ext] Download {m.Id}: {e.ProgressPercentage}%", "Extensions");
                    };
                }

                // ブロッキングで進捗も拾いたいなら Async+WaitOne を使う
                var done = new System.Threading.AutoResetEvent(false);
                Exception error = null;

                wc.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Error != null) error = e.Error;
                    done.Set();
                };

                wc.DownloadFileAsync(new Uri(m.Download.Url), tmpZip);
                done.WaitOne();

                if (error != null) throw error;
            }

            // 2) 検証
            if (!ExtensionsUtil.VerifySha256(tmpZip, m.Download.Sha256))
                throw new InvalidOperationException("SHA256 verification failed.");

            // 3) 展開
            var target = ExtensionsPaths.Expand(m.Install.TargetDir);
            Directory.CreateDirectory(target);
            ExtensionsZip.ExtractZipAllowOverwrite(tmpZip, target);

            // 4) 必要ファイルが揃っているか検証
            foreach (var f in m.Install.Files ?? Array.Empty<string>())
            {
                var p = Path.Combine(target, f);
                if (!File.Exists(p)) throw new FileNotFoundException("Missing file in extension package.", p);
            }

            MarkInstalled(m.Id, m.Version);
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Ext");
            return target;
        }

        public static async Task<string> InstallAsync(
            ExtensionManifest m,
            IProgress<DownloadProgress> progress,
            System.Threading.CancellationToken ct)
        {
            if (m == null || m.Download == null || m.Install == null) throw new ArgumentException("bad manifest");

            ExtensionsPaths.EnsureDirs();

            var tmpZip = Path.Combine(Path.GetTempPath(), $"kiritori_ext_{m.Id}_{m.Version}.zip");

            // 1) ダウンロード
            progress?.Report(new DownloadProgress { Percent = 0, Stage = "ダウンロード中…" });

            using (var wc = new WebClient())
            using (ct.Register(() => wc.CancelAsync()))
            {
                var tcs = new TaskCompletionSource<bool>();
                Exception error = null;

                wc.DownloadProgressChanged += (s, e) =>
                {
                    var p = e.ProgressPercentage >= 0 ? e.ProgressPercentage : 0;
                    progress?.Report(new DownloadProgress
                    {
                        Percent = p,
                        Stage = $"ダウンロード中… {p}%",
                        BytesReceived = e.BytesReceived,
                        TotalBytes = e.TotalBytesToReceive
                    });
                };
                wc.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Cancelled) { tcs.TrySetCanceled(); return; }
                    if (e.Error != null) { error = e.Error; tcs.TrySetException(e.Error); return; }
                    tcs.TrySetResult(true);
                };

                wc.DownloadFileAsync(new Uri(m.Download.Url), tmpZip);
                await tcs.Task.ConfigureAwait(false);
                if (error != null) throw error;
                ct.ThrowIfCancellationRequested();
            }

            // 2) 検証（DEBUG ではスキップする現在の仕様を踏襲）
            bool verify = true;
#if DEBUG
            verify = false; // DebugビルドはSHAチェックをスキップ
#endif
            if (verify)
            {
                progress?.Report(new DownloadProgress { Percent = 100, Stage = "検証中…" });
                if (!ExtensionsUtil.VerifySha256(tmpZip, m.Download.Sha256))
                    throw new InvalidOperationException("SHA256 verification failed.");
            }
            else
            {
                Log.Debug("[Ext] SHA256 skipped (DEBUG build).", "Extensions");
            }

            ct.ThrowIfCancellationRequested();

            // 3) 展開
            progress?.Report(new DownloadProgress { Percent = -1, Stage = "展開中…" });
            var target = ExtensionsPaths.Expand(m.Install.TargetDir);
            Directory.CreateDirectory(target);
            ExtensionsZip.ExtractZipAllowOverwrite(tmpZip, target); // 既存仕様を流用（上書き展開）

            // 4) 必須ファイル検証（既存仕様）
            foreach (var f in m.Install.Files ?? Array.Empty<string>())
            {
                var p = Path.Combine(target, f);
                if (!File.Exists(p)) throw new FileNotFoundException("Missing file in extension package.", p);
            }

            MarkInstalled(m.Id, m.Version);
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Ext");
            return target;
        }
    }
}

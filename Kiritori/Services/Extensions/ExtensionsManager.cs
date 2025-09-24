using Kiritori.Services.Logging;
using Kiritori.Helpers;
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
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsManager
    {
        public static ExtensionState State { get; private set; } = ExtensionState.GetState();
        internal struct DownloadProgress
        {
            public int Percent;    // 0-100, 未設定は -1
            public string Stage;   // "ダウンロード中…", "検証中…", "展開中…" など
            public long BytesReceived;
            public long TotalBytes;
        }

        private static readonly object _repoCacheSync = new object();
        private static List<ExtensionManifest> _repoCache; // プロセス内キャッシュ

        public static void InvalidateRepoCache()
        {
            lock (_repoCacheSync) { _repoCache = null; }
        }

        public static IEnumerable<ExtensionManifest> LoadRepoManifests()
        {
            // 1) キャッシュがあれば即返す（呼び出しが連続しても実スキャンは1回）
            lock (_repoCacheSync)
            {
                if (_repoCache != null) return new List<ExtensionManifest>(_repoCache);
            }

            var list = new List<ExtensionManifest>();
            try
            {
                // --- ディレクトリ重複の排除（大小文字や相対→絶対の差分も吸収） ---
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in ExtensionsPaths.CandidateManifestDirs())
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    string dir = raw;
                    try { dir = Path.GetFullPath(raw); } catch { /* 失敗時はそのまま */ }

                    if (!Directory.Exists(dir)) continue;
                    if (!seen.Add(dir)) continue; // 同一パスはスキップ

                    // --- 実スキャン ---
                    foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var m = LoadManifest(f);
                        if (m != null) list.Add(m);
                    }
                    Kiritori.Services.Logging.Log.Debug($"Manifests scanned: {dir} => {list.Count}", "Extensions");
                }

                // 2) 見つからなければ埋め込み既定を採用
                if (list.Count == 0)
                {
                    var emb = ExtensionsEmbedded.LoadEmbedded();
                    list.AddRange(emb);
                    Kiritori.Services.Logging.Log.Info($"Using embedded default manifests: {emb.Count}", "Extensions");
                }

                // 3) ID重複を排除（最初のものを採用）
                list = list
                    .GroupBy(m => m.Id ?? "", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                // 4) キャッシュへ保存
                lock (_repoCacheSync)
                {
                    _repoCache = new List<ExtensionManifest>(list);
                }
            }
            catch (Exception ex)
            {
                Kiritori.Services.Logging.Log.Error("LoadRepoManifests failed: " + ex.Message, "Extensions");
            }

            // 呼び出し側が安全に列挙できるようにスナップショットを返す
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
            ExtensionState.SaveState(State);
        }

        public static void MarkInstalled(string id, string version, string location)
        {
            ExtensionState.Item it;
            if (!State.Items.TryGetValue(id, out it)) it = new ExtensionState.Item();
            it.Installed = true;
            it.Version   = version;
            if (!it.Enabled) it.Enabled = true;
            if (!string.IsNullOrWhiteSpace(location)) it.Location = location;
            State.Items[id] = it;
            ExtensionState.SaveState(State);
        }

        public static void MarkUninstalled(string id)
        {
            ExtensionState.Item it;
            if (State.Items.TryGetValue(id, out it))
            {
                it.Installed = false;
                State.Items[id] = it;
                ExtensionState.SaveState(State);
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
                var u = new Uri(m.Download.Url);
                if (!u.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("HTTPS only.");
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
                Kiritori.Services.Logging.Log.Debug("SHA256 skipped (DEBUG build).", "Extensions");
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

            MarkInstalled(m.Id, m.Version, target);
            InvalidateRepoCache();
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Extensions");
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
            InvalidateRepoCache();
        }
        public static string GetInstallDir(string id)
        {
            if (State.Items.TryGetValue(id, out var it))
            {
                if (!string.IsNullOrWhiteSpace(it.Location))
                {
                    // Location がファイルでもディレクトリでも対応
                    if (Directory.Exists(it.Location)) return it.Location;
                    if (File.Exists(it.Location))      return Path.GetDirectoryName(it.Location);
                }
            }

            var ver = InstalledVersion(id);
            if (string.IsNullOrEmpty(ver)) return null;

            // 後方互換：従来の bin/<id>/<ver>
            return Path.Combine(ExtensionsPaths.Root, "bin", id, ver);
        }

        public static void RepairStateIfMissing()
        {
            try
            {
                ExtensionsPaths.EnsureDirs();
                var path = ExtensionsPaths.StateJson;

                // 既存 state が有効なら採用
                bool needRepair = true;
                try
                {
                    if (File.Exists(path))
                    {
                        var loaded = ExtensionState.GetState();
                        if (loaded != null && loaded.Items != null && loaded.Items.Count > 0)
                        {
                            State = loaded;
                            needRepair = false;
                            Log.Debug("State file loaded: " + path, "Extensions");
                        }
                        else
                        {
                            State = loaded ?? new ExtensionState();
                            Log.Warn("State file invalid: " + path, "Extensions");
                        }
                    }
                    else
                    {
                        Log.Debug("State file not found: " + path, "Extensions");
                    }
                }
                catch
                {
                    Log.Debug("State file load failed: " + path, "Extensions");
                }

                if (!needRepair) return;

                var st = new ExtensionState();

                // --- 1) bin\<id>\<ver> をスキャン（従来通り） ---
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
                            // 何かしら入っていることを軽く確認
                            var vdir = Path.Combine(idDir, latestVer);
                            bool hasAny = false;
                            try
                            {
                                hasAny = Directory.Exists(vdir) &&
                                        Directory.GetFileSystemEntries(vdir).Length > 0;
                            }
                            catch { }

                            if (hasAny)
                            {
                                st.Items[id] = new ExtensionState.Item
                                {
                                    Installed = true,
                                    Enabled = true,
                                    Version = latestVer
                                };
                                Log.Debug($"Repaired state: {id} {latestVer}", "Extensions");
                            }
                        }
                    }
                }

                // --- 2) i18n\<culture>\Kiritori.resources.dll → lang_<culture>（従来通り） ---
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
                            Log.Debug($"Repaired state: lang_{cul} embedded", "Extensions");
                        }
                    }
                }

                // --- 3) exe の横（AppContext.BaseDirectory）も見る ---
                var exeDir = GetExeDirSafe();

                // 3-a) exe の横の ja\Kiritori.resources.dll → lang_ja
                try
                {
                    foreach (var culDir in Directory.EnumerateDirectories(exeDir))
                    {
                        var cul = Path.GetFileName(culDir);
                        Log.Debug($"Checking exe-side i18n: {culDir}", "Extensions");
                        if (string.IsNullOrEmpty(cul)) continue;

                        var resDll = Path.Combine(culDir, "Kiritori.resources.dll");
                        if (!File.Exists(resDll)) continue;

                        // 見つかったカルチャ名で登録（例: lang_ja, lang_ja-JP）
                        AddLangEntryIfMissing(st, cul);

                        // 可能であれば、親カルチャ（ja-JP → ja）や 2 文字コードも “エイリアス”的に登録
                        // UI/判定コードが lang_ja を期待しているケースの後方互換に効く
                        try
                        {
                            var ci = new System.Globalization.CultureInfo(cul);
                            var two = ci.TwoLetterISOLanguageName;      // "ja"
                            var parent = ci.Parent?.Name;               // "ja" (for ja-JP), "" for invariant

                            if (!string.IsNullOrEmpty(parent) && !parent.Equals(cul, StringComparison.OrdinalIgnoreCase))
                                AddLangEntryIfMissing(st, parent);

                            if (!string.IsNullOrEmpty(two) && !two.Equals(cul, StringComparison.OrdinalIgnoreCase))
                                AddLangEntryIfMissing(st, two);
                        }
                        catch { /* 不正なフォルダ名は無視 */ }
                    }
                }
                catch { /* 列挙失敗は無視 */ }

                var ffmpegExeCandidates = new[]
                {
                    Path.Combine(exeDir, "ThirdParty", "ffmpeg", "ffmpeg.exe"),
                    Path.Combine(exeDir, "ffmpeg", "ffmpeg.exe"),
                };

                string ffmpegFound = null;
                foreach (var p in ffmpegExeCandidates)
                {
                    if (File.Exists(p)) { ffmpegFound = p; break; }
                }

                // 3-c) まだ無ければ PATH も探す（起動ディレクトリ以外の同梱やユーザー配布を拾う）
                if (ffmpegFound == null)
                {
                    ffmpegFound = FindOnPath("ffmpeg.exe");
                }

                if (ffmpegFound != null)
                {
                    var verNum = TryGetFfmpegVersion(ffmpegFound) ?? GetFileVersionOrFallback(ffmpegFound, "external");
                    var display = string.IsNullOrWhiteSpace(verNum) ? "external" : (verNum + " (external)");
                    var folder  = Path.GetDirectoryName(ffmpegFound);

                    if (!st.Items.ContainsKey("ffmpeg"))
                    {
                        st.Items["ffmpeg"] = new ExtensionState.Item
                        {
                            Installed = true,
                            Enabled   = true,
                            Version   = display,   // 例: "7.3 (external)"
                            Location  = folder     // 例: "C:\tools\ffmpeg\bin"
                        };
                        Log.Debug($"Repaired state: ffmpeg {display} ({ffmpegFound})", "Extensions");
                    }
                    else
                    {
                        var cur = st.Items["ffmpeg"];
                        // 表示がダミーなら更新
                        if (string.IsNullOrWhiteSpace(cur.Version) || cur.Version == "external" || cur.Version == "1.0.0.0" || cur.Version == "0.0.0.0")
                            cur.Version = display;
                        if (string.IsNullOrWhiteSpace(cur.Location))
                            cur.Location = folder;
                        st.Items["ffmpeg"] = cur;
                    }
                }

                // 保存
                ExtensionState.SaveState(st);
                State = st;
                Log.Info("Repaired missing state: " + path + " (" + st.Items.Count + " items)", "Extensions");

#if DEBUG
                try
                {
                    var len = new FileInfo(path).Length;
                    Log.Debug($"State saved: {path} ({len} bytes)", "Extensions");
                    using (var r = new StreamReader(path, Encoding.UTF8))
                    {
                        char[] buf = new char[300];
                        int n = r.ReadBlock(buf, 0, buf.Length);
                        Log.Debug("State preview: " + new string(buf, 0, n), "Extensions");
                    }
                }
                catch { }
#endif
            }
            catch (Exception ex)
            {
                Log.Warn("RepairStateIfMissing failed: " + ex.Message, "Extensions");
            }
        }

        // ====== ここから private ヘルパー ======
        private static bool IsBadVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return true;
            v = v.Trim();
            return v == "external" || v == "1.0.0.0" || v == "0.0.0.0" || v.Equals("unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static string TryGetFfmpegVersion(string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null) return null;
                    // 1行目だけで十分（"ffmpeg version 7.3 ..."）
                    string first = p.StandardOutput.ReadLine();
                    // 念のため残りを非同期で読ませてすぐタイムアウト待ち
                    p.WaitForExit(1500);

                    if (string.IsNullOrEmpty(first)) return null;

                    // "ffmpeg version <token>" を取る
                    var m = Regex.Match(first, @"ffmpeg\s+version\s+([^\s]+)", RegexOptions.IgnoreCase);
                    if (!m.Success) return null;

                    var token = m.Groups[1].Value; // 例: 7.3, 7.1-full_build-..., N-113490-g...
                    // できれば数値だけに正規化（7.3 / 7.1.2 など）。無ければ token を返す
                    var n = Regex.Match(token, @"\d+(\.\d+){0,2}");
                    return n.Success ? n.Value : token;
                }
            }
            catch { return null; }
        }

        private static void AddLangEntryIfMissing(ExtensionState st, string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName)) return;

            var key = "lang_" + cultureName; // 例: lang_ja / lang_ja-JP
            if (st.Items.ContainsKey(key)) return;

            st.Items[key] = new ExtensionState.Item
            {
                Installed = true,
                Enabled = true,
                Version = "embedded"
            };
            Log.Debug($"Repaired state: {key} embedded (exe side)", "Extensions");
        }

        private static string GetExeDirSafe()
        {
            try
            {
                // WinForms/WPF でなくても使える、.NET 標準の exe ベースディレクトリ取得
                var d = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(d)) return Path.GetFullPath(d);
            }
            catch { }

            try
            {
                var d = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(d)) return Path.GetFullPath(d);
            }
            catch { }

            // フォールバック：ExtensionsPaths.Root にしない（そこは AppData 用）
            return Environment.CurrentDirectory;
        }

        private static string FindOnPath(string fileName)
        {
            try
            {
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                var parts = path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var raw in parts)
                {
                    string dir = raw;
                    try { dir = Path.GetFullPath(raw); } catch { }
                    var p = Path.Combine(dir, fileName);
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }
        private static string GetFileVersionOrFallback(string filePath, string fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return fallback;

                var vi = FileVersionInfo.GetVersionInfo(filePath);
                if (vi != null)
                {
                    // FileVersion が優先、無ければ ProductVersion
                    if (!string.IsNullOrWhiteSpace(vi.FileVersion))
                        return vi.FileVersion;
                    if (!string.IsNullOrWhiteSpace(vi.ProductVersion))
                        return vi.ProductVersion;
                }
            }
            catch
            {
                // ignore and fallback
            }
            return fallback;
        }

        // ---- ここから下はヘルパー（private） ----
        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                    Log.Info($"Deleted directory: {path}", "Extensions");
                }
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
                    Log.Info($"Deleted empty directory: {path}", "Extensions");
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

            var u = new Uri(m.Download.Url);
            if (!u.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("HTTPS only.");

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
                        Log.Debug($"Download {m.Id}: {e.ProgressPercentage}%", "Extensions");
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

            MarkInstalled(m.Id, m.Version, target);
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Extensions");
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
            progress?.Report(new DownloadProgress { Percent = 0, Stage = SR.T("Extensions.InstallDialog.Downloading", "Downloading...") });

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
                        Stage = SR.T("Extensions.InstallDialog.Downloading", "Downloading...") + $" {p}%",
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

            // 2) 検証（DEBUG ではスキップする）
            bool verify = true;
#if DEBUG
            verify = false; // DebugビルドはSHAチェックをスキップ
#endif
            if (verify)
            {
                progress?.Report(new DownloadProgress { Percent = 100, Stage = SR.T("Extensions.InstallDialog.Verifying", "Verifying...") });
                if (!ExtensionsUtil.VerifySha256(tmpZip, m.Download.Sha256))
                    throw new InvalidOperationException("SHA256 verification failed.");
            }
            else
            {
                Log.Debug("SHA256 skipped (DEBUG build).", "Extensions");
            }

            ct.ThrowIfCancellationRequested();

            // 3) 展開
            progress?.Report(new DownloadProgress { Percent = -1, Stage = SR.T("Extensions.InstallDialog.Extracting", "Extracting...") });
            var target = ExtensionsPaths.Expand(m.Install.TargetDir);
            Directory.CreateDirectory(target);
            ExtensionsZip.ExtractZipAllowOverwrite(tmpZip, target); // 上書き展開

            // 4) 必須ファイル検証
            foreach (var f in m.Install.Files ?? Array.Empty<string>())
            {
                var p = Path.Combine(target, f);
                if (!File.Exists(p)) throw new FileNotFoundException("Missing file in extension package.", p);
            }

            MarkInstalled(m.Id, m.Version, target);
            Log.Info($"Extension installed: {m.Id} {m.Version}", "Extensions");
            return target;
        }
    }
}

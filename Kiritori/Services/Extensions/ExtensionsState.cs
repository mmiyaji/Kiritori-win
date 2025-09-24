using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Kiritori.Services.Extensions
{
    [DataContract]
    public sealed class ExtensionState
    {
        // 既存コードが参照している入れ子型（復活）
        [DataContract]
        public sealed class Item
        {
            [DataMember] public bool Enabled { get; set; }
            [DataMember] public bool Installed { get; set; }
            [DataMember] public string Version { get; set; }
            [DataMember] public string Location  { get; set; }
        }

        [DataMember(Name = "Items")]
        public Dictionary<string, Item> Items { get; set; } = new Dictionary<string, Item>();

        private static ExtensionState _cached;
        private static readonly object _stateSync = new object();
        public static ExtensionState GetState()
        {
            // 先に速いパス
            var s = _cached;
            if (s != null) {
                Log.Debug("ExtensionState.GetState: using cached", "Extensions");
                return s;
            }
            lock (_stateSync)
            {
                if (_cached == null)
                {
                    _cached = Load();   // ← 既存の堅牢化済み Load を使用
                    Log.Debug("ExtensionState.GetState: loading", "Extensions");
                }
                return _cached;
            }
        }

        // 保存してキャッシュも更新
        public static void SaveState(ExtensionState s)
        {
            if (s == null) return;

            lock (_stateSync)
            {
                s.Save();        // ← 既存の原子的保存＋リトライ付き Save を使用
                _cached = s;     // メモリにも反映
                Log.Debug("ExtensionState.SaveState: saved and cached", "Extensions");
            }
        }

        // 外部から明示的にキャッシュを捨てたい場合
        public static void InvalidateCache()
        {
            lock (_stateSync)
            {
                _cached = null;
                Log.Debug("ExtensionState.InvalidateCache: cache cleared", "Extensions");
            }
        }
        // ---- Load ----
        public static ExtensionState Load()
        {
            var path = ExtensionsPaths.StateJson;
            try
            {
                if (!File.Exists(path))
                {
                    Log.Warn($"ExtensionState.Load: not found: {path}", "Extensions");
                    return new ExtensionState();
                }

                byte[] bytes;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bytes = new byte[fs.Length];
                    int read = fs.Read(bytes, 0, bytes.Length);
                    if (read != bytes.Length)
                        Log.Warn($"ExtensionState.Load: short read {read}/{bytes.Length}", "Extensions");
                }

                // 文字列化（BOM検出あり）→ 先頭BOM/空白除去
                var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true).GetString(bytes);
                if (!string.IsNullOrEmpty(text) && text[0] == '\uFEFF') text = text.Substring(1);
                var cleaned = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                if (cleaned.Length == 0)
                {
                    Log.Warn($"ExtensionState.Load: empty after trim", "Extensions");
                    return new ExtensionState();
                }

                var cleanedBytes = new UTF8Encoding(false).GetBytes(cleaned);
                using (var ms = new MemoryStream(cleanedBytes))
                {
                    var ser = new DataContractJsonSerializer(typeof(ExtensionState));
                    var obj = (ExtensionState)ser.ReadObject(ms);
                    if (obj == null) return new ExtensionState();
                    if (obj.Items == null) obj.Items = new Dictionary<string, Item>();

                    Log.Debug($"ExtensionState.Load: OK", "Extensions");
                    return obj;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"ExtensionState.Load failed: {ex.GetType().Name}: {ex.Message}", "Extensions");
                return new ExtensionState();
            }
        }

        // ---- Save ----
        public void Save()
        {
            var path = ExtensionsPaths.StateJson;
            try
            {
                ExtensionsPaths.EnsureDirs();

                // シリアライズ
                byte[] raw;
                using (var ms = new MemoryStream())
                {
                    var ser = new DataContractJsonSerializer(typeof(ExtensionState));
                    ser.WriteObject(ms, this);
                    raw = ms.ToArray();
                }

                // 文字列化 → BOM除去 → BOMなしUTF-8
                var json = new UTF8Encoding(true).GetString(raw);
                if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF') json = json.Substring(1);
                json = json.Trim();
                var data = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

                // 原子的保存＋リトライ
                var tmp = path + ".tmp";
                const int maxRetry = 4;
                for (int i = 0; i < maxRetry; i++)
                {
                    try
                    {
                        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            fs.Write(data, 0, data.Length);
                            fs.Flush(true);
                        }

                        if (File.Exists(path))
                        {
                            try { File.Replace(tmp, path, null); }
                            catch
                            {
                                if (File.Exists(path)) File.Delete(path);
                                File.Move(tmp, path);
                            }
                        }
                        else
                        {
                            File.Move(tmp, path);
                        }

                        Log.Debug($"ExtensionState.Save: OK len={data.Length}", "Extensions");
                        return;
                    }
                    catch (IOException ioex)
                    {
                        Log.Debug($"ExtensionState.Save: retry {i + 1}/{maxRetry} after IOException: {ioex.Message}", "Extensions");
                        Thread.Sleep(30 * (i + 1));
                    }
                }

                Log.Debug($"ExtensionState.Save failed: max retries reached", "Extensions");
            }
            catch (Exception ex)
            {
                Log.Debug($"ExtensionState.Save failed: {ex.GetType().Name}: {ex.Message}", "Extensions");
            }
        }
    }
}
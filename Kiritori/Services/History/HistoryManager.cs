using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Services.History
{

    public sealed class HistoryEntry
    {
        public string Path;
        public Bitmap Thumb;
        public DateTime LoadedAt;     // いつ履歴に積んだか
        public Size Resolution;   // 画像解像度
        public LoadMethod Method;     // Path / Capture / Clipboard
        public string Description;  // 任意の説明テキスト   
    }
    internal sealed class HistoryManager
    {
        private readonly object _sync = new object();
        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly string _rootDir;       // 例: %TEMP%\Kiritori\history
        private readonly string _tsvPath;       // 例: %TEMP%\Kiritori\history\history.tsv
        private readonly int _maxEntries;

        // 直近の変更をまとめて保存したいときに使うフラグ（IdleでFlush用）
        private bool _dirty;
        public event EventHandler Changed;

        public HistoryManager(string rootDir, int maxEntries = 500)
        {
            if (string.IsNullOrEmpty(rootDir)) throw new ArgumentNullException(nameof(rootDir));
            _rootDir = rootDir;
            _tsvPath = Path.Combine(_rootDir, "history.tsv");
            _maxEntries = Math.Max(1, maxEntries);

            Directory.CreateDirectory(_rootDir);
            LoadFromTsvIfExists();
        }

        // ===== 取得系 =====
        public IList<HistoryEntry> Snapshot()
        {
            lock (_sync)
            {
                return new List<HistoryEntry>(_entries);
            }
        }

        public int Count
        {
            get { lock (_sync) return _entries.Count; }
        }

        // ===== 追加・削除 =====
        public void Add(HistoryEntry he, bool saveImmediately = true)
        {
            if (he == null) return;

            lock (_sync)
            {
                _entries.Insert(0, he);      // 新しいものが先頭
                Prune_NoLock();
                _dirty = true;
                if (saveImmediately) AppendOneLine_NoLock(he);
            }
            OnChanged();
        }

        public bool Remove(HistoryEntry he, bool saveImmediately = true)
        {
            if (he == null) return false;
            bool removed = false;

            lock (_sync)
            {
                removed = _entries.Remove(he);
                if (removed)
                {
                    _dirty = true;
                    if (saveImmediately) SaveAll_NoLock();  // 単純化のため全書き直し
                }
            }
            if (removed) OnChanged();
            return removed;
        }

        public void Clear(bool saveImmediately = true)
        {
            lock (_sync)
            {
                _entries.Clear();
                _dirty = true;
                if (saveImmediately && File.Exists(_tsvPath))
                    File.Delete(_tsvPath);
            }
            OnChanged();
        }

        // ===== 保存/読込 =====
        public void FlushIfDirty()
        {
            lock (_sync)
            {
                if (!_dirty) return;
                SaveAll_NoLock();
                _dirty = false;
            }
        }

        private void LoadFromTsvIfExists()
        {
            if (!File.Exists(_tsvPath)) return;

            // 形式: ticks \t width \t height \t method \t path
            // 例:   638632851234567890  1920 1080  Capture  C:\...\img.png
            try
            {
                var list = new List<HistoryEntry>();
                foreach (var line in File.ReadAllLines(_tsvPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split('\t');
                    if (cols.Length < 5) continue;

                    long ticks;
                    int w, h;
                    if (!long.TryParse(cols[0], out ticks)) continue;
                    if (!int.TryParse(cols[1], out w)) continue;
                    if (!int.TryParse(cols[2], out h)) continue;

                    LoadMethod m;
                    if (!Enum.TryParse(cols[3], out m)) m = LoadMethod.Path;

                    var path = cols[4];
                    // Thumb はここではロードしない（必要時にLazy）
                    list.Add(new HistoryEntry
                    {
                        LoadedAt = new DateTime(ticks),
                        Resolution = new Size(w, h),
                        Method = m,
                        Path = string.IsNullOrEmpty(path) ? null : path,
                        Thumb = null
                    });
                }

                lock (_sync)
                {
                    _entries.Clear();
                    // ファイルは古い→新しいの順の可能性があるため、新しい順に揃える
                    list.Sort((a, b) => b.LoadedAt.CompareTo(a.LoadedAt));
                    _entries.AddRange(list);
                    _dirty = false;
                }
            }
            catch
            {
                // 破損時は無視（必要ならバックアップ/再生成ロジックを追加）
            }
        }

        private void AppendOneLine_NoLock(HistoryEntry he)
        {
            // 追記のみ（壊れにくい）
            try
            {
                using (var sw = new StreamWriter(_tsvPath, true))
                {
                    sw.Write(he.LoadedAt.Ticks);
                    sw.Write('\t');
                    sw.Write(he.Resolution.Width);
                    sw.Write('\t');
                    sw.Write(he.Resolution.Height);
                    sw.Write('\t');
                    sw.Write(he.Method.ToString());
                    sw.Write('\t');
                    sw.Write(he.Path ?? string.Empty);
                    sw.WriteLine();
                }
                _dirty = false;
            }
            catch
            {
                // 失敗は握りつぶし（必要ならリトライ管理）
            }
        }

        private void SaveAll_NoLock()
        {
            try
            {
                using (var sw = new StreamWriter(_tsvPath, false))
                {
                    // 新しい→古いの順で保存
                    foreach (var he in _entries)
                    {
                        sw.Write(he.LoadedAt.Ticks);
                        sw.Write('\t');
                        sw.Write(he.Resolution.Width);
                        sw.Write('\t');
                        sw.Write(he.Resolution.Height);
                        sw.Write('\t');
                        sw.Write(he.Method.ToString());
                        sw.Write('\t');
                        sw.Write(he.Path ?? string.Empty);
                        sw.WriteLine();
                    }
                }
            }
            catch
            {
                // 失敗は握りつぶし
            }
        }

        private void Prune_NoLock()
        {
            if (_entries.Count <= _maxEntries) return;

            // 末尾から削る（古いもの）
            int removeCount = _entries.Count - _maxEntries;
            _entries.RemoveRange(_maxEntries, removeCount);
        }

        private void OnChanged()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

}

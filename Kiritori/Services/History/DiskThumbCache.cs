using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kiritori.Services.History
{
    internal sealed class DiskThumbCache : IDisposable
    {
        private readonly string _dir;
        private readonly int _w, _h;
        private readonly int _lruCap;
        private readonly ConcurrentQueue<HistoryEntry> _q = new ConcurrentQueue<HistoryEntry>();
        private readonly ConcurrentDictionary<string, byte> _inflight = new ConcurrentDictionary<string, byte>();
        private readonly Dictionary<string, Bitmap> _lruMap = new Dictionary<string, Bitmap>();
        private readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private readonly object _lruLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;

        public event Action<string> ThumbReady; // key

        public DiskThumbCache(string baseDir, int w, int h, int lruCapacity = 64)
        {
            _w = w; _h = h; _lruCap = Math.Max(0, lruCapacity);
            _dir = Path.Combine(baseDir, $"{w}x{h}");
            Directory.CreateDirectory(_dir);
            _worker = Task.Run(WorkerLoop);
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _worker.Wait(1500); } catch { }
            lock (_lruLock)
            {
                foreach (var b in _lruMap.Values) try { b.Dispose(); } catch { }
                _lruMap.Clear();
                _lruOrder.Clear();
            }
            _cts.Dispose();
        }

        // ------- Public API -------

        public void DrawThumb(Graphics g, Rectangle rect, HistoryEntry he, Image placeholder)
        {
            if (he == null) { DrawPlaceholder(g, rect, placeholder); return; }
            var key = StableKey(he);

            // メモリ（LRU）
            if (TryGetFromLru(key, out var bmp))
            {
                g.DrawImage(bmp, rect); return;
            }

            // ディスク
            using (var fromDisk = TryLoadFromDisk(key))
            {
                if (fromDisk != null)
                {
                    var mem = AddToLru(key, new Bitmap(fromDisk));
                    g.DrawImage(mem, rect);
                    Touch(key);
                    return;
                }
            }

            // 生成依頼
            EnqueueIfNeeded(he);
            DrawPlaceholder(g, rect, placeholder);
        }

        public void Prefetch(IEnumerable<HistoryEntry> items, int max = 24)
        {
            if (items == null) return;
            int n = 0; foreach (var he in items) { if (n++ >= max) break; EnqueueIfNeeded(he); }
        }

        // ------- Internal -------

        public static string StableKey(HistoryEntry he) =>
            (he.Path ?? "clipboard") + "|" + he.LoadedAt.Ticks + "|" + he.Resolution.Width + "x" + he.Resolution.Height;

        private string PathOf(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
                var name = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return System.IO.Path.Combine(_dir, name + ".jpg");
            }
        }

        private static Bitmap LoadBitmapNoLock(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var img = Image.FromStream(fs, false, false))
                return new Bitmap(img);
        }

        private static Bitmap RenderThumb(Bitmap src, int w, int h)
        {
            float sx = (float)w / src.Width, sy = (float)h / src.Height, s = Math.Min(sx, sy);
            int rw = Math.Max(1, (int)(src.Width * s)), rh = Math.Max(1, (int)(src.Height * s));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                int dx = (w - rw) / 2, dy = (h - rh) / 2;
                g.DrawImage(src, new Rectangle(dx, dy, rw, rh));
                using (var pen = new Pen(Color.Silver)) g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
            }
            return bmp;
        }

        private void EnqueueIfNeeded(HistoryEntry he)
        {
            var key = StableKey(he);
            if (_inflight.TryAdd(key, 0)) _q.Enqueue(he);
        }

        private async Task WorkerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (!_q.TryDequeue(out var he))
                {
                    await Task.Delay(50, _cts.Token).ConfigureAwait(false);
                    continue;
                }
                var key = StableKey(he);
                try
                {
                    var dest = PathOf(key);
                    if (!File.Exists(dest))
                    {
                        using (var src = SourceFor(he))
                        {
                            if (src != null)
                                using (var th = RenderThumb(src, _w, _h))
                                    SaveJpeg(dest, th, 85L);
                        }
                    }
                    ThumbReady?.Invoke(key);
                }
                catch { }
                finally { byte _; _inflight.TryRemove(key, out _); }

                try { CleanupIfOver(200L * 1024 * 1024); } catch { }
            }
        }

        private static Bitmap SourceFor(HistoryEntry he)
        {
            if (!string.IsNullOrEmpty(he.Path) && File.Exists(he.Path))
                return LoadBitmapNoLock(he.Path);
            if (he.Thumb != null && he.Thumb.Width > 0 && he.Thumb.Height > 0)
                return new Bitmap(he.Thumb);
            return null;
        }

        private static void SaveJpeg(string path, Bitmap bmp, long quality)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var enc = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (enc == null) { bmp.Save(path, ImageFormat.Jpeg); return; }
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(path, enc, ep);
            }
        }

        private Bitmap TryLoadFromDisk(string key)
        {
            var p = PathOf(key);
            if (!File.Exists(p)) return null;
            try
            {
                using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = Image.FromStream(fs, false, false))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        private void Touch(string key)
        {
            var p = PathOf(key);
            try { if (File.Exists(p)) File.SetLastAccessTimeUtc(p, DateTime.UtcNow); } catch { }
        }

        private void CleanupIfOver(long maxBytes)
        {
            var di = new DirectoryInfo(_dir);
            if (!di.Exists) return;
            var files = di.GetFiles("*.jpg").OrderBy(f => f.LastAccessTimeUtc).ToList();
            long total = 0;
            for (int i = 0; i < files.Count; i++) total += files[i].Length;
            int idx = 0;
            while (total > maxBytes && idx < files.Count)
            {
                try { total -= files[idx].Length; files[idx].Delete(); } catch { }
                idx++;
            }
        }

        private static void DrawPlaceholder(Graphics g, Rectangle r, Image ph)
        {
            if (ph != null) g.DrawImage(ph, r);
            else { using (var p = new Pen(Color.Silver)) g.DrawRectangle(p, r); }
        }

        private bool TryGetFromLru(string key, out Bitmap bmp)
        {
            lock (_lruLock)
            {
                if (_lruMap.TryGetValue(key, out bmp))
                {
                    var node = _lruOrder.Find(key);
                    if (node != null) { _lruOrder.Remove(node); _lruOrder.AddLast(node); }
                    return true;
                }
            }
            bmp = null; return false;
        }

        private Bitmap AddToLru(string key, Bitmap bmp)
        {
            if (_lruCap <= 0) return bmp;
            lock (_lruLock)
            {
                if (_lruMap.ContainsKey(key))
                {
                    var old = _lruMap[key];
                    _lruMap[key] = bmp;
                    _lruOrder.Remove(key);
                    _lruOrder.AddLast(key);
                    try { old.Dispose(); } catch { }
                }
                else
                {
                    _lruMap[key] = bmp;
                    _lruOrder.AddLast(key);
                    while (_lruMap.Count > _lruCap)
                    {
                        var toEvict = _lruOrder.First?.Value;
                        if (toEvict == null) break;
                        _lruOrder.RemoveFirst();
                        var ev = _lruMap[toEvict];
                        _lruMap.Remove(toEvict);
                        try { ev.Dispose(); } catch { }
                    }
                }
                return _lruMap[key];
            }
        }
        public bool ExistsOnDisk(HistoryEntry he)
        {
            if (he == null) return false;
            var key = StableKey(he);
            return File.Exists(PathOf(key));
        }
    }
}

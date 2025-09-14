using Kiritori.Properties;
using Kiritori.Helpers;
using Kiritori.Services.Logging;
using Kiritori.Services.Ocr;
using Kiritori.Services.History;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;


namespace Kiritori
{
    public partial class MainApplication : Form
    {
        private const int WM_DPICHANGED = 0x02E0;
        private ScreenWindow s;
        private int _screenOpenGate = 0;
        private readonly AppStartupOptions _opt;
        private static readonly string HistoryTempDir = Path.Combine(Path.GetTempPath(), "Kiritori", "History");
        private static readonly string HistoryIndexPath = Path.Combine(HistoryTempDir, "history.index.tsv");
        private const int IDX_COL_PATH      = 0;
        private const int IDX_COL_LOADEDAT  = 1;   // ticks(long)
        private const int IDX_COL_WIDTH     = 2;
        private const int IDX_COL_HEIGHT    = 3;
        private const int IDX_COL_METHOD    = 4;   // enum int
        private bool _allowShow = false;
        private readonly System.Windows.Forms.Timer _bootTimer;

        // Win32 P/Invoke と定数
        private const int WM_HOTKEY = 0x0312;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int UnregisterHotKey(IntPtr hWnd, int id);

        // デバッグ用ID（他IDと被らない値に）
        private const int HOTKEY_ID_CAPTURE = 9001;
        private const int HOTKEY_ID_OCR = 9002;
        private const int HOTKEY_ID_LIVE = 9003;
        // Win32のMOD定数（既存MOD_KEYと同値だがintで扱う）
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008; // 使うなら
        private const int MOD_NOREPEAT = 0x4000; // チャタリング対策
        private bool _historyDirty = false;
        private int  _historyDirtyCount = 0;

        internal MainApplication(AppStartupOptions opt = null)
        {
            _opt = opt ?? new AppStartupOptions();
            InitializeComponent();
            notifyIcon1.Icon = Properties.Resources.AppIcon;
            this.Icon = Properties.Resources.AppIcon;
            // hotKey = new HotKey(MOD_KEY.CONTROL | MOD_KEY.SHIFT, Keys.D5);
            // hotKey.HotKeyPush += new EventHandler(hotKey_HotKeyPush);
            this.HandleCreated += (s2, e2) =>
            {
                try
                {
                    Log.Debug("HandleCreated: registering hotkeys", "Hotkey");
                    ReloadHotkeysFromSettings();
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error on HandleCreated: {ex}", "Hotkey");
                }
            };

            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            s = new ScreenWindow(this);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);

            ApplyTrayLocalization();
            SR.CultureChanged += () =>
            {
                Localizer.Apply(this);
                ApplyTrayLocalization();
            };

            ApplyDpiToUi(GetDpiForWindowSafe(this.Handle));
            // MaybeShowPreferencesOnStartup();
            _bootTimer = new System.Windows.Forms.Timer { Interval = 10, Enabled = true };
            _bootTimer.Tick += async (_, __) =>
            {
                _bootTimer.Enabled = false;
                await EnsureStartupAsync();
                try
                {
                    Directory.CreateDirectory(HistoryTempDir);
                    LoadHistoryFromIndex();           // インデックスからメニュー復元
                    PruneHistoryFilesBeyondLimit();   // 上限超過や孤児ファイルを削除
                    Kiritori.Services.History.HistoryBridge.SetProvider(() => GetHistoryEntriesSnapshot());
                    HistoryBridge.RaiseChanged(this);
                    HistoryBridge.HistoryChanged += (s3, e3) =>
                    {
                        var snap = HistoryBridge.GetSnapshot();
                        PrefForm.InstanceIfOpen?.SetupHistoryTabIfNeededAndShow(snap);
                    };
                }
                catch (Exception ex)
                {
                    Log.Debug($"History restore/prune error: {ex}", "History");
                }
                MaybeShowPreferencesOnStartup();
            };
            Properties.Settings.Default.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Properties.Settings.Default.HotkeyCapture) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HotkeyOcr) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HotkeyLive))
                {
                    Log.Info($"Settings changed: {e.PropertyName} → reload hotkeys", "Hotkey");
                    // UIスレッドに投げる
                    if (this.IsHandleCreated)
                        this.BeginInvoke((Action)ReloadHotkeysFromSettings);
                    else
                    {
                        void handler(object s2, EventArgs e2)
                        {
                            this.HandleCreated -= handler;
                            ReloadHotkeysFromSettings();
                        }
                        this.HandleCreated += handler;
                    }
                }
                else if (e.PropertyName == nameof(Properties.Settings.Default.HistoryLimit))
                {
                    int limit = Properties.Settings.Default.HistoryLimit;
                    if (limit <= 0)
                    {
                        ClearHistoryMenu();
                        PruneHistoryFilesBeyondLimit();
                        // SaveHistoryToIndex(); // 必要ならindexも更新
                    }
                    else
                    {
                        // 上限を増減したら読み直して数を合わせる
                        ClearHistoryMenu();
                        LoadHistoryFromIndex();                 // limitに従って読み込む（既存）
                        PruneHistoryFilesBeyondLimit();         // 余剰ファイルを削る
                    }
                }
            };
            this.historyToolStripMenuItem.DropDownOpening += (_, __) => SaveHistoryIfDirty();
        }
        internal IList<HistoryEntry> GetHistoryEntriesSnapshot()
        {
            var list = new List<HistoryEntry>(historyToolStripMenuItem.DropDownItems.Count);
            foreach (ToolStripItem tsi in historyToolStripMenuItem.DropDownItems)
                if (tsi is ToolStripMenuItem mi && mi.Tag is HistoryEntry he) list.Add(he);
            return list;
        }
        private void NotifyPrefFormHistoryChanged()
        {
            var pref = PrefForm.InstanceIfOpen;
            if (pref != null)
            {
                var snap = Kiritori.Services.History.HistoryBridge.GetSnapshot();
                pref.SetupHistoryTabIfNeededAndShow(snap);
            }
        }
        private static string HistoryKey(HistoryEntry he)
        {
            if (he == null) return "";
            var path = he.Path ?? "clipboard";
            return path + "|" + he.LoadedAt.Ticks.ToString()
                + "|" + he.Resolution.Width + "x" + he.Resolution.Height;
        }
        public void RefreshHistoryMenuText(HistoryEntry entry)
        {
            if (entry == null) return;
            foreach (ToolStripItem tsi in historyToolStripMenuItem.DropDownItems)
            {
                if (tsi is ToolStripMenuItem mi && ReferenceEquals(mi.Tag, entry))
                {
                    mi.Text = FormatHistoryText(
                        path: (!string.IsNullOrEmpty(entry.Path) ? entry.Path : null),
                        method: entry.Method,
                        res: entry.Resolution,
                        loadedAt: entry.LoadedAt,
                        description: entry.Description
                    );
                    mi.AutoToolTip = !string.IsNullOrEmpty(entry.Path);
                    mi.ToolTipText = entry.Path;
                    break;
                }
            }
            MarkHistoryDirty();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_ID_CAPTURE)
                {
                    Log.Debug("WM_HOTKEY: CAPTURE", "Hotkey");
                    openScreen();
                    return;
                }
                if (id == HOTKEY_ID_OCR)
                {
                    Log.Debug("WM_HOTKEY: OCR", "Hotkey");
                    openScreenOCR();
                    return;
                }
                if (id == HOTKEY_ID_LIVE)
                {
                    Log.Debug("WM_HOTKEY: LIVE", "Hotkey");
                    openScreenLive();
                    return;
                }
            }

            base.WndProc(ref m);

            // --- DPI 変更を受け取り、UI のスケール依存を更新
            if (m.Msg == WM_DPICHANGED)
            {
                int newDpi = (int)((uint)m.WParam & 0xFFFF);
                ApplyDpiToUi(newDpi);
            }
        }
        internal void OpenImagesFromIpc(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            foreach (var path in paths)
            {
                this.openImage(path);
            }
        }

        private void ApplyDpiToUi(int dpi)
        {
            // ToolStrip / Menu の画像スケール
            // 例: 16px 基準で DPI に合わせる
            int px = (int)Math.Round(16 * dpi / 96.0);
            foreach (var ts in this.ComponentsRecursive<ToolStrip>())
            {
                ts.ImageScalingSize = new Size(px, px);
            }
            // NotifyIcon のアイコンそのものは OS がよしなにしてくれますが、
            // 必要に応じて DPI 切替時にアイコンを差し替える処理を追加してもOK。

            // ScreenWindow 側に DPI 変更を知らせたい場合（後述のフック用）
            try { s?.OnHostDpiChanged(dpi); } catch { /* ScreenWindow 未対応でもOK */ }
        }

        private void MaybeShowPreferencesOnStartup()
        {
            if (_opt.Mode == AppStartupMode.Viewer && _opt.ImagePaths.Length > 0)
            {
                foreach (var path in _opt.ImagePaths)
                {
                    this.openImage(path);
                }
                return;
            }
            // 初回だけは強制表示
            if (!Settings.Default.FirstRunShown)
            {
                Log.Info($"First run detected", "Startup");
                Settings.Default.FirstRunShown = true;
                Settings.Default.Save();
                // PrefForm.ShowSingleton(this);
                var pref = PrefForm.ShowSingleton(this);
                pref?.SetupHistoryTabIfNeededAndShow(GetHistoryEntriesSnapshot());

                return;
            }
            if (Settings.Default.OpenPreferencesOnStartup)
            {
                Log.Info($"Opening preferences on startup", "Startup");
                // PrefForm.ShowSingleton(this);
                var pref = PrefForm.ShowSingleton(this);
                pref?.SetupHistoryTabIfNeededAndShow(GetHistoryEntriesSnapshot());
            }
        }

        // Utility: コントロール木から指定型を列挙
        private IEnumerable<T> ComponentsRecursive<T>() where T : Control
        {
            Queue<Control> q = new Queue<Control>();
            q.Enqueue(this);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c is T t) yield return t;
                foreach (Control child in c.Controls) q.Enqueue(child);
            }
        }

        // Utility: GetDpiForWindow の安全呼び出し
        private int GetDpiForWindowSafe(IntPtr hWnd)
        {
            try { return Native.GetDpiForWindow(hWnd); } catch { return 96; }
        }

        private static class Native
        {
            [DllImport("user32.dll")] public static extern int GetDpiForWindow(IntPtr hWnd);
        }
        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            try
            {
                if (notifyIcon1 == null) return;
                // ClearHistoryMenu();
                if (notifyIcon1 != null) notifyIcon1.Dispose();
                if (this.Icon != null) this.Icon.Dispose();

                UnregisterHotKey(this.Handle, HOTKEY_ID_CAPTURE);
                UnregisterHotKey(this.Handle, HOTKEY_ID_OCR);
                SaveHistoryToIndex();
            }
            catch { }
        }


        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void captureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openScreen();
        }
        private void captureOCRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openScreenOCR();
        }
        private void livePreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openScreenLive();
        }

        public void openScreen()
        {
            if (Interlocked.Exchange(ref _screenOpenGate, 1) == 1)
            {
                Log.Debug("openScreen: busy -> ignored", "Hotkey");
                return;
            }
            try
            {
                if (s == null || s.IsDisposed)
                    s = new ScreenWindow(this, () => GetDpiForWindowSafe(this.Handle));

                if (!s.Visible)
                    s.showScreenAll();
                else
                    s.Activate(); // 既に表示中なら前面化だけ
            }
            catch (Exception ex)
            {
                Log.Debug($"openScreen exception: {ex}", "Hotkey");
                Interlocked.Exchange(ref _screenOpenGate, 0); // 失敗時はゲート解放
            }
        }
        public void openScreenOCR()
        {
            if (Interlocked.Exchange(ref _screenOpenGate, 1) == 1)
            {
                Log.Debug("openScreenOCR: busy -> ignored", "Hotkey");
                return;
            }
            try
            {
                if (s == null || s.IsDisposed)
                    s = new ScreenWindow(this, () => GetDpiForWindowSafe(this.Handle));

                if (!s.Visible)
                    s.showScreenOCR();
                else
                    s.Activate();
            }
            catch (Exception ex)
            {
                Log.Debug($"openScreenOCR exception: {ex}", "Hotkey");
                Interlocked.Exchange(ref _screenOpenGate, 0);
            }
        }
        public void openScreenLive()
        {
            if (Interlocked.Exchange(ref _screenOpenGate, 1) == 1)
            {
                Log.Debug("openScreenLive: busy -> ignored", "Hotkey");
                return;
            }
            try
            {
                if (s == null || s.IsDisposed)
                    s = new ScreenWindow(this, () => GetDpiForWindowSafe(this.Handle));

                if (!s.Visible)
                    s.showScreenLive();
                else
                    s.Activate();
            }
            catch (Exception ex)
            {
                Log.Debug($"openScreenLive exception: {ex}", "Hotkey");
                Interlocked.Exchange(ref _screenOpenGate, 0);
            }
        }
        internal void ReleaseScreenGate()
        {
            Interlocked.Exchange(ref _screenOpenGate, 0);
            Log.Debug("screen gate released", "Hotkey");
        }
        public void openImage(String path = null)
        {
            if (s == null)
            {
                s = new ScreenWindow(this);
            }
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                s.openImage(path);
            }
            else
            {
                s.openImage();
            }
        }
        // クリップボードに画像/画像ファイルがあれば SnapWindow に貼り付けて表示
        public bool pasteFromClipboard()
        {
            try
            {
                // 1) 画像そのもの
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        Directory.CreateDirectory(HistoryTempDir);
                        string path = Path.Combine(HistoryTempDir, $"{DateTime.Now:yyyyMMdd_HHmmssfff}_clip.png");

                        // ロック回避のため Bitmap にクローンして保存
                        using (var bmp = new Bitmap(img))
                        {
                            bmp.Save(path, ImageFormat.Png);
                        }

                        var sw = new SnapWindow(this)
                        {
                            StartPosition = FormStartPosition.CenterScreen
                        };

                        // 可能なら「クリップボード由来」であることをマーク（存在しない場合は無視）
                        // try { sw.CurrentLoadMethod = LoadMethod.Clipboard; } catch { /* no-op */ }

                        sw.setImageFromPath(path);
                        sw.Show();
                        return true;
                    }
                }

                // 2) 画像ファイルのパスがコピーされている場合（複数なら先頭だけ）
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection files = Clipboard.GetFileDropList();
                    foreach (string p in files)
                    {
                        if (File.Exists(p) && IsImageExt(Path.GetExtension(p)))
                        {
                            var sw = new SnapWindow(this)
                            {
                                StartPosition = FormStartPosition.CenterScreen
                            };
                            // try { sw.CurrentLoadMethod = LoadMethod.Path; } catch { /* no-op */ }
                            sw.setImageFromPath(p);
                            sw.Show();
                            return true;
                        }
                    }
                }

                // 画像が無い
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug($"PasteFromClipboard error: {ex}", "Clipboard");
                return false;
            }
        }

        public void openImageFromHistory(ToolStripMenuItem item)
        {
            if (s == null)
            {
                s = new ScreenWindow(this);
            }
            s.openImageFromHistory(item);
        }

        public void setHistory(SnapWindow sw, string description = null)
        {
            if (sw == null) return;

            if (Properties.Settings.Default.HistoryLimit <= 0)
                return;

            Bitmap src = sw.GetCurrentBitmapClone();
            if (src == null) return;

            // 実体PNG保存→entry作成→UI追加（ここは今のまま）
            Directory.CreateDirectory(HistoryTempDir);
            string path = Path.Combine(HistoryTempDir, DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png");
            try { using (var saveCopy = new Bitmap(src)) saveCopy.Save(path, ImageFormat.Png); } catch { path = null; }

            var entry = new HistoryEntry
            {
                Path        = path,
                Thumb       = MakeThumbnailSafe(src),
                Resolution  = src.Size,
                LoadedAt    = DateTime.Now,
                Method      = LoadMethod.Capture,
                Description = description   // ここは最初はだいたい null
            };
            AddHistoryEntryAndRefreshUI(entry);

            // フラグOFFならここで終わり（OCRは走らない）
            if (!Properties.Settings.Default.HistoryIncludeOcr)
            {
                try { src.Dispose(); } catch { }
                return;
            }

            // 以降は今の非同期OCR（順序は src→ocrCopy→src.Dispose の順）
            Bitmap ocrCopy = null;
            try { ocrCopy = new Bitmap(src); } catch { }
            try { src.Dispose(); } catch { }

            if (ocrCopy == null) return;

            Task.Run(async () =>
            {
                try
                {
                    var text = await OcrFacade.RunAsync(
                        ocrCopy,
                        copyToClipboard: false,
                        preprocess: true
                        ).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(text) && this != null && this.IsHandleCreated)
                    {
                        this.BeginInvoke((Action)(() =>
                        {
                            // 念のため、反映直前にもフラグ確認（途中で設定がOFFになったケースに配慮）
                            if (!Properties.Settings.Default.HistoryIncludeOcr) return;

                            entry.Description = text;
                            UpdateHistoryRow(entry);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("Async OCR in setHistory failed: " + ex.Message, "History");
                }
                finally
                {
                    try { ocrCopy.Dispose(); } catch { }
                }
            });
        }
        // 履歴が変わったときに呼ぶ（軽量）
        private void MarkHistoryDirty()
        {
            _historyDirty = true;

            // タイマーは使わず、回数でだけ間引く（3回ごとに即時保存）
            _historyDirtyCount++;
            if (_historyDirtyCount >= 3)
            {
                SaveHistoryIfDirty();
            }
        }

        // 「必要な時だけ」保存する軽量フラッシュ
        private void SaveHistoryIfDirty()
        {
            if (!_historyDirty) return;
            _historyDirty = false;
            _historyDirtyCount = 0;
            SaveHistoryToIndex();   // 既存：TSVを書き出すだけの軽処理
        }
        private void AddHistoryEntryAndRefreshUI(HistoryEntry entry)
        {
            if (Properties.Settings.Default.HistoryLimit <= 0) return;
            // 表示テキストを生成（ファイル名・解像度・時刻・説明など）
            var text = FormatHistoryText(
                path: entry.Path,
                method: entry.Method,
                res: entry.Resolution,
                loadedAt: entry.LoadedAt,
                description: entry.Description);

            var mi = new ToolStripMenuItem
            {
                Image = entry.Thumb,
                Text = text,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ImageAlign = ContentAlignment.MiddleLeft,
                ImageScaling = ToolStripItemImageScaling.None,
                Tag = entry,
                AutoSize = true,
                AutoToolTip = !string.IsNullOrEmpty(entry.Path),
                ToolTipText = entry.Path
            };
            mi.Click += historyToolStripMenuItem_item_Click;

            // 先頭に挿入（新しい順）
            historyToolStripMenuItem.DropDownItems.Insert(0, mi);

            // 上限を超えたら末尾から削除
            int limit = Properties.Settings.Default.HistoryLimit;
            if (limit > 0 && historyToolStripMenuItem.DropDownItems.Count > limit)
            {
                var last = historyToolStripMenuItem.DropDownItems[historyToolStripMenuItem.DropDownItems.Count - 1] as ToolStripMenuItem;
                historyToolStripMenuItem.DropDownItems.RemoveAt(historyToolStripMenuItem.DropDownItems.Count - 1);
                try { (last?.Image as Bitmap)?.Dispose(); } catch { }
                try { (last?.Tag as HistoryEntry)?.Thumb?.Dispose(); } catch { }
                last?.Dispose();
            }

            // すぐにインデックスへも反映したいならここで保存（任意）
            // SaveHistoryToIndex();
            MarkHistoryDirty();
            NotifyPrefFormHistoryChanged();
        }

        private void UpdateHistoryRow(HistoryEntry entry)
        {
            // OCR完了時など、同じ参照の行だけテキストを差し替え
            foreach (ToolStripItem tsi in historyToolStripMenuItem.DropDownItems)
            {
                var mi = tsi as ToolStripMenuItem;
                if (mi?.Tag == entry)
                {
                    mi.Text = FormatHistoryText(
                        path: (!string.IsNullOrEmpty(entry.Path) ? entry.Path : null),
                        method: entry.Method,
                        res: entry.Resolution,
                        loadedAt: entry.LoadedAt,
                        description: entry.Description);
                    mi.AutoToolTip = !string.IsNullOrEmpty(entry.Path);
                    mi.ToolTipText = entry.Path;
                    break;
                }
            }

            // 変更を保存したい場合は任意で
            // SaveHistoryToIndex();
            MarkHistoryDirty();
            NotifyPrefFormHistoryChanged();
            HistoryBridge.RaiseChanged(this);
        }


        private static Bitmap MakeThumbnailSafe(Image src)
        {
            try
            {
                int w = 90;
                int h = System.Math.Max(1, src.Height * w / System.Math.Max(1, src.Width));
                Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, w, h));
                }
                return bmp;
            }
            catch { return null; }
        }
        private static Bitmap CreateThumb(Image src, int maxW, int maxH, Color? bg = null)
        {
            if (src == null) return null;

            // アスペクト維持で縮小サイズ計算
            double rw = (double)maxW / src.Width;
            double rh = (double)maxH / src.Height;
            double r = Math.Min(1.0, Math.Min(rw, rh)); // 拡大しない
            int w = Math.Max(1, (int)Math.Round(src.Width * r));
            int h = Math.Max(1, (int)Math.Round(src.Height * r));

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                // 高品質縮小
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                if (bg.HasValue) using (var br = new SolidBrush(bg.Value)) g.FillRectangle(br, 0, 0, w, h);
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }
        private static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace('\t', ' ')
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
            return s;
        }

        private static string FormatHistoryText(string path, LoadMethod method, Size res, DateTime loadedAt, string description = null)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(loadedAt.ToString("yyyy/MM/dd HH:mm:ss")).Append(']');
            if (res.Width > 0 && res.Height > 0)
                sb.Append(' ').Append('(').Append(res.Width).Append('x').Append(res.Height).Append(')');

            // 2行目は「OCR を優先」。なければファイル名（どちらか一方だけ）
            string second = null;
            if (!string.IsNullOrEmpty(description))
            {
                second = MiddleEllipsis(OneLine(description), 32);
            }
            else if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                second = MiddleEllipsis(Path.GetFileName(path), 32);
            }
            else
            {
                // ファイルも説明も無いときのフォールバック（任意）
                // second = SR.T("Text.History.Method.Capture", "Capture");
            }

            if (!string.IsNullOrEmpty(second))
                sb.AppendLine().Append(second);

            return sb.ToString();
        }

        // ========== 履歴インデックス ==========

        // 現在のメニューの上から順に TSV で保存
        private void SaveHistoryToIndex()
        {
            try
            {
                Directory.CreateDirectory(HistoryTempDir);
                var sb = new StringBuilder();

                foreach (ToolStripItem tsi in historyToolStripMenuItem.DropDownItems)
                {
                    var mi = tsi as ToolStripMenuItem;
                    if (mi?.Tag is HistoryEntry he && !string.IsNullOrEmpty(he.Path))
                    {
                        // 失効ファイルはスキップ
                        if (!File.Exists(he.Path)) continue;

                        long ticks = he.LoadedAt.Ticks;
                        int w = he.Resolution.Width;
                        int h = he.Resolution.Height;
                        int method = (int)he.Method;

                        // path \t ticks \t w \t h \t method
                        sb.Append(he.Path.Replace('\t', ' ')); sb.Append('\t');
                        sb.Append(ticks.ToString(CultureInfo.InvariantCulture)); sb.Append('\t');
                        sb.Append(w.ToString(CultureInfo.InvariantCulture)); sb.Append('\t');
                        sb.Append(h.ToString(CultureInfo.InvariantCulture)); sb.Append('\t');
                        sb.Append(method.ToString(CultureInfo.InvariantCulture));
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(HistoryIndexPath, sb.ToString(), Encoding.UTF8);
                Log.Debug($"History index saved: {HistoryIndexPath}", "History");
            }
            catch (Exception ex)
            {
                Log.Debug($"SaveHistoryToIndex error: {ex}", "History");
            }
        }

        // 読込：TSV を読み、存在するファイルのみ復元してメニューを作り直す
        private void LoadHistoryFromIndex()
        {
            try
            {
                int limit = Properties.Settings.Default.HistoryLimit;
                if (limit <= 0)
                {
                    ClearHistoryMenu();
                    return;
                }
                if (!File.Exists(HistoryIndexPath))
                {
                    Log.Debug("History index not found.", "History");
                    return;
                }

                var lines = File.ReadAllLines(HistoryIndexPath, Encoding.UTF8);
                if (lines.Length == 0) return;

                // まず現在のメニューをクリア（Disposeは既存メソッド利用）
                ClearHistoryMenu();

                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = line.Split('\t');
                    if (cols.Length < 5) continue;

                    string p = cols[IDX_COL_PATH];
                    if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) continue;

                    long ticks;
                    int w, h, methodInt;
                    if (!long.TryParse(cols[IDX_COL_LOADEDAT], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) ticks = DateTime.Now.Ticks;
                    if (!int.TryParse(cols[IDX_COL_WIDTH], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)) w = 0;
                    if (!int.TryParse(cols[IDX_COL_HEIGHT], NumberStyles.Integer, CultureInfo.InvariantCulture, out h)) h = 0;
                    if (!int.TryParse(cols[IDX_COL_METHOD], NumberStyles.Integer, CultureInfo.InvariantCulture, out methodInt)) methodInt = (int)LoadMethod.Path;

                    var he = new HistoryEntry
                    {
                        Path = p,
                        LoadedAt = new DateTime(ticks),
                        Resolution = new Size(w, h),
                        Method = (LoadMethod)methodInt
                    };

                    // サムネ生成
                    const int TH_W = 64, TH_H = 64;
                    using (var img = SafeLoadImageForThumb(p))
                    {
                        he.Thumb = (img != null) ? CreateThumb(img, TH_W, TH_H, Color.Transparent) : null;
                    }

                    // 表示テキスト
                    var text = FormatHistoryText(
                        path: (he.Method == LoadMethod.Path) ? he.Path : null,
                        method: he.Method,
                        res: (he.Resolution.Width > 0 && he.Resolution.Height > 0) ? he.Resolution : new Size(0, 0),
                        loadedAt: he.LoadedAt
                    );

                    var mi = new ToolStripMenuItem
                    {
                        Image = he.Thumb,
                        Text = text,
                        DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                        ImageAlign = ContentAlignment.MiddleLeft,
                        ImageScaling = ToolStripItemImageScaling.None,
                        Tag = he,
                        AutoSize = true
                    };
                    if (he.Method == LoadMethod.Path)
                    {
                        mi.AutoToolTip = true;
                        mi.ToolTipText = he.Path;
                    }
                    mi.Click += historyToolStripMenuItem_item_Click;

                    historyToolStripMenuItem.DropDownItems.Add(mi);
                    count++;
                    if (limit > 0 && count >= limit) break;
                }

                Log.Debug($"History restored: {count} items.", "History");
            }
            catch (Exception ex)
            {
                Log.Debug($"LoadHistoryFromIndex error: {ex}", "History");
            }
        }

        // サムネ用に安全に画像を開く（ロックしない）
        private static Image SafeLoadImageForThumb(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    return Image.FromStream(ms);
                }
            }
            catch { return null; }
        }

        // 追加：履歴フォルダ配下チェック（正規化して厳密比較）
        private static bool IsUnderDir(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            var full = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void PruneHistoryFilesBeyondLimit()
        {
            try
            {
                int limit = Properties.Settings.Default.HistoryLimit;
                Directory.CreateDirectory(HistoryTempDir);

                // 履歴インデックスに載っている“管理対象”だけを集合化
                var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(HistoryIndexPath))
                {
                    foreach (var line in File.ReadAllLines(HistoryIndexPath, Encoding.UTF8))
                    {
                        var cols = line.Split('\t');
                        if (cols.Length > 0)
                        {
                            var p = cols[IDX_COL_PATH];
                            if (!string.IsNullOrWhiteSpace(p))
                                managed.Add(Path.GetFullPath(p));
                        }
                    }
                }

                // --- 履歴OFF（0以下）は“管理対象だけ”を安全に削除 ---
                if (limit <= 0)
                {
                    foreach (var p in managed)
                    {
                        if (File.Exists(p) && IsUnderDir(p, HistoryTempDir) && IsImageExt(Path.GetExtension(p)))
                            SafeDelete(p);
                    }
                    try { if (File.Exists(HistoryIndexPath)) File.Delete(HistoryIndexPath); } catch { }
                    return;
                }

                // ★「管理対象外は削除する」処理はやめる（安全策）
                // （元コードの all→managedに無いものを消すループは削除）

                // --- 上限超過の削除も“管理対象だけ”で実施 ---
                var remaining = managed
                    .Select(p => { try { return new FileInfo(p); } catch { return null; } })
                    .Where(fi => fi != null && fi.Exists
                                && IsUnderDir(fi.FullName, HistoryTempDir)
                                && IsImageExt(fi.Extension))
                    .OrderByDescending(fi => fi.CreationTimeUtc)
                    .ToList();

                if (remaining.Count > limit)
                {
                    foreach (var fi in remaining.Skip(limit))
                        SafeDelete(fi.FullName);
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"PruneHistoryFilesBeyondLimit error: {ex}", "History");
            }
        }

        public void RemoveHistoryEntries(IEnumerable<HistoryEntry> targets)
        {
            if (targets == null) return;

            // 参照ではなくキーで一致判定する
            var targetKeys = new HashSet<string>(
                targets.Where(t => t != null).Select(HistoryKey),
                StringComparer.OrdinalIgnoreCase);

            int removed = 0;

            // 1) トレイの履歴メニューから除去
            for (int i = historyToolStripMenuItem.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (historyToolStripMenuItem.DropDownItems[i] is ToolStripMenuItem mi)
                {
                    var he = mi.Tag as HistoryEntry;
                    var key = HistoryKey(he);
                    if (targetKeys.Contains(key))
                    {
                        try { (mi.Image as Bitmap)?.Dispose(); } catch { }
                        try { he?.Thumb?.Dispose(); } catch { }
                        historyToolStripMenuItem.DropDownItems.RemoveAt(i);
                        mi.Dispose();
                        removed++;
                    }
                }
            }

            // 2) インデックス(TSV)にも反映（既存の保存経路を使う）
            try
            {
                MarkHistoryDirty();   // 変更フラグ
                SaveHistoryIfDirty(); // すぐ保存
            }
            catch
            {
                Log.Debug("RemoveHistoryEntries: failed to save index", "History");
            }

            // 3) PrefForm などへ変更通知
            try { Kiritori.Services.History.HistoryBridge.RaiseChanged(this); }
            catch
            {
                Log.Debug("RemoveHistoryEntries: failed to notify PrefForm", "History");
            }

            // 4) デバッグログ
            Log.Debug($"RemoveHistoryEntries: removed={removed}, requested={targetKeys.Count}", "History");
        }



        private static bool IsImageExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.Trim().ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        private static string MiddleEllipsis(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            const string ell = "…";
            int keep = max - ell.Length;
            if (keep <= 1) return s.Substring(0, max); // ほぼ入らないケース

            int left = keep / 2;
            int right = keep - left;
            return s.Substring(0, left) + ell + s.Substring(s.Length - right);
        }

        private void hideAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.hideWindows();
        }

        private void showAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.showWindows();
        }
        private void closeAllWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            s.closeWindows();
        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // PrefForm.ShowSingleton(this);
            var pref = PrefForm.ShowSingleton(this);
            pref?.SetupHistoryTabIfNeededAndShow(GetHistoryEntriesSnapshot());
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openImage();
        }
        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.pasteFromClipboard();
        }

        private void historyToolStripMenuItem_item_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            if (item.Tag is HistoryEntry he && File.Exists(he.Path))
            {
                var sw = new SnapWindow(this)
                {
                    StartPosition = FormStartPosition.CenterScreen,
                    SuppressHistory = true
                };
                sw.setImageFromPath(he.Path);
                sw.Show();
            }
        }

        private String substringAtCount(string source, int count)
        {
            String newStr = "";
            int length = (int)Math.Ceiling((double)source.Length / (double)count);

            for (int i = 0; i < length; i++)
            {
                int start = count * i;
                if (start >= source.Length)
                {
                    break;
                }
                if (i > 0)
                {
                    newStr += Environment.NewLine;
                }
                if (start + count > source.Length)
                {
                    newStr += source.Substring(start);
                }
                else
                {
                    newStr += source.Substring(start, count);
                }
            }
            return newStr;
        }
        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // PrefForm.ShowSingleton(this);
                var pref = PrefForm.ShowSingleton(this);
                pref?.SetupHistoryTabIfNeededAndShow(GetHistoryEntriesSnapshot());
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右クリックは ContextMenuStrip が出るので、通常は何もしない
            }
        }
        private void ClearHistoryMenu()
        {
            foreach (ToolStripItem tsi in historyToolStripMenuItem.DropDownItems)
            {
                if (tsi is ToolStripMenuItem mi)
                {
                    if (mi.Tag is HistoryEntry he)
                    {
                        // if (IsHistoryTempPath(he.Path)) SafeDelete(he.Path);
                        he.Thumb?.Dispose();
                        mi.Tag = null;
                    }
                    mi.Image?.Dispose();
                    mi.Dispose();
                }
            }
            historyToolStripMenuItem.DropDownItems.Clear();
        }


        private static bool IsHistoryTempPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                string baseDir = Path.GetFullPath(HistoryTempDir)
                                + Path.DirectorySeparatorChar; // 末尾セパレータで誤一致防止
                return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ロック中などは無視（次回のクリーンアップで再挑戦）
            }
        }
        private void ApplyTrayLocalization()
        {
            // メニュー（Tagベースで一括）
            if (trayContextMenuStrip != null)
                ApplyToolStripLocalization(trayContextMenuStrip.Items);

            // トレイアイコンのヒント
            if (notifyIcon1 != null)
                notifyIcon1.Text = SR.T("Tray.TrayIcon");  // 文字数は63字以内推奨
        }
        // ToolStrip / ContextMenuStrip 用のローカライズ適用
        private static void ApplyToolStripLocalization(ToolStripItemCollection items)
        {
            foreach (ToolStripItem it in items)
            {
                // 自分自身
                if (it.Tag is string tag && tag.StartsWith("loc:", StringComparison.Ordinal))
                {
                    it.Text = SR.T(tag.Substring(4));
                }

                // サブメニューを再帰
                if (it is ToolStripDropDownItem dd)
                {
                    ApplyToolStripLocalization(dd.DropDownItems);
                }
            }
        }
        public NotifyIcon NotifyIcon
        {
            get { return this.notifyIcon1; }
        }
        // 最初は絶対に Visible にしない（ちらつき・表示を防止）
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(_allowShow && value);
        }

        // 間違って Close されたら Hide に置き換える（本当に終了するときは Application.Exit 呼び出し側で）
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        private async Task EnsureStartupAsync()
        {
            try
            {
                if (Properties.Settings.Default.RunAtStartup)
                {
                    if (Helpers.PackagedHelper.IsPackaged())
                    {
                        // MSIX: 必要なら同意ダイアログ
                        await Startup.StartupManager.EnsureEnabledAsync();
                    }
                    else
                    {
                        // 非MSIX: .lnk 作成は STA が安全
                        await RunStaAsync(() => Startup.StartupManager.EnsureEnabled());
                    }
                }
                else
                {
                    if (Helpers.PackagedHelper.IsPackaged())
                        await Startup.StartupManager.DisableAsync();
                    else
                        await RunStaAsync(() => Startup.StartupManager.SetEnabled(false));
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Startup error: {ex}", "Startup");
            }
        }

        private static Task RunStaAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            var th = new Thread(() => { try { action(); tcs.SetResult(null); } catch (Exception ex) { tcs.SetException(ex); } });
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
            return tcs.Task;
        }


        public void ReloadHotkeysFromSettings()
        {
            Log.Debug("ReloadHotkeysFromSettings() called", "Hotkey");
            Log.Debug($"raw settings: Cap='{Properties.Settings.Default.HotkeyCapture}', Ocr='{Properties.Settings.Default.HotkeyOcr}'", "Hotkey");

            // ウィンドウ ハンドルが未作成だと RegisterHotKey に失敗するので保護
            if (!this.IsHandleCreated)
            {
                Log.Debug("Handle not created; deferring until HandleCreated.", "Hotkey");

                // 起動直後などで未作成なら、HandleCreated 後にもう一度やる
                void handler(object s, EventArgs e)
                {
                    this.HandleCreated -= handler;
                    ReloadHotkeysFromSettings();
                }
                this.HandleCreated += handler;
                return;
            }
            // 念のため一旦解除
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_CAPTURE); } catch { }
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_OCR); } catch { }
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_LIVE); } catch { }
            HotkeySpec DEF_HOTKEY_CAP   = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D5 };
            HotkeySpec DEF_HOTKEY_OCR   = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D4 };
            HotkeySpec DEF_HOTKEY_LIVE  = new HotkeySpec { Mods = ModMask.Ctrl | ModMask.Shift, Key = Keys.D6 };


            // 設定値を解析（ログ多め）
            var capSpec = HotkeyUtil.ParseOrDefault(Properties.Settings.Default.HotkeyCapture, DEF_HOTKEY_CAP);
            var ocrSpec = HotkeyUtil.ParseOrDefault(Properties.Settings.Default.HotkeyOcr, DEF_HOTKEY_OCR);
            var liveSpec = HotkeyUtil.ParseOrDefault(Properties.Settings.Default.HotkeyLive, DEF_HOTKEY_LIVE);

            Log.Debug($"Capture parsed: {HotkeyUtil.ToText(capSpec)}  (Key={capSpec.Key})", "Hotkey");
            Log.Debug($"OCR     parsed: {HotkeyUtil.ToText(ocrSpec)}  (Key={ocrSpec.Key})", "Hotkey");
            Log.Debug($"Live    parsed: {HotkeyUtil.ToText(liveSpec)}  (Key={liveSpec.Key})", "Hotkey");

            // 変換（intに）
            int capMods = 0, ocrMods = 0, liveMods = 0;
            if ((capSpec.Mods & ModMask.Ctrl) != 0) capMods |= MOD_CONTROL;
            if ((capSpec.Mods & ModMask.Shift) != 0) capMods |= MOD_SHIFT;
            if ((capSpec.Mods & ModMask.Alt) != 0) capMods |= MOD_ALT;

            if ((ocrSpec.Mods & ModMask.Ctrl) != 0) ocrMods |= MOD_CONTROL;
            if ((ocrSpec.Mods & ModMask.Shift) != 0) ocrMods |= MOD_SHIFT;
            if ((ocrSpec.Mods & ModMask.Alt) != 0) ocrMods |= MOD_ALT;

            if ((liveSpec.Mods & ModMask.Ctrl) != 0) liveMods |= MOD_CONTROL;
            if ((liveSpec.Mods & ModMask.Shift) != 0) liveMods |= MOD_SHIFT;
            if ((liveSpec.Mods & ModMask.Alt) != 0) liveMods |= MOD_ALT;

            // 実登録（Win32直）
            int ok1 = RegisterHotKey(this.Handle, HOTKEY_ID_CAPTURE, capMods /* | MOD_NOREPEAT */, (int)capSpec.Key);
            if (ok1 == 0)
            {
                int err = Marshal.GetLastWin32Error();
                Log.Debug($"Register CAPTURE failed. mods={capMods}, vk={(Keys)capSpec.Key}, lastError=0x{err:X8}", "Hotkey");
            }
            else
            {
                Log.Debug($"Register CAPTURE success. mods={capMods}, vk={(Keys)capSpec.Key}", "Hotkey");
            }

            int ok2 = RegisterHotKey(this.Handle, HOTKEY_ID_OCR, ocrMods /* | MOD_NOREPEAT */, (int)ocrSpec.Key);
            if (ok2 == 0)
            {
                int err = Marshal.GetLastWin32Error();
                Log.Debug($"Register OCR failed. mods={ocrMods}, vk={(Keys)ocrSpec.Key}, lastError=0x{err:X8}", "Hotkey");
            }
            else
            {
                Log.Debug($"Register OCR success. mods={ocrMods}, vk={(Keys)ocrSpec.Key}", "Hotkey");
            }

            int ok3 = RegisterHotKey(this.Handle, HOTKEY_ID_LIVE, liveMods /* | MOD_NOREPEAT */, (int)liveSpec.Key);
            if (ok3 == 0)
            {
                int err = Marshal.GetLastWin32Error();
                Log.Debug($"Register LIVE failed. mods={liveMods}, vk={(Keys)liveSpec.Key}, lastError=0x{err:X8}", "Hotkey");
            }
            else
            {
                Log.Debug($"Register LIVE success. mods={liveMods}, vk={(Keys)liveSpec.Key}", "Hotkey");
            }

        }
    }
}

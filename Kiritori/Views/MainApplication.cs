using Kiritori.Properties;
using Kiritori.Helpers;
using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
//using System.Windows.Forms.Cursor;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Kiritori
{
    internal sealed class HistoryEntry
    {
        public string Path;
        public Bitmap Thumb;
    }
    public partial class MainApplication : Form
    {
        private const int WM_DPICHANGED = 0x02E0;
        private ScreenWindow s;
        private int _screenOpenGate = 0;
        private readonly AppStartupOptions _opt;
        private static readonly string HistoryTempDir = Path.Combine(Path.GetTempPath(), "Kiritori", "History");

        private bool _allowShow = false;
        private readonly System.Windows.Forms.Timer _bootTimer;

        // 追加: Win32 P/Invoke と定数
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
            this.AutoScaleDimensions = new SizeF(96F, 96F);

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
            };
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
            if (!Settings.Default.isFirstRunShown)
            {
                Log.Info($"First run detected", "Startup");
                Settings.Default.isFirstRunShown = true;
                Settings.Default.Save();
                PrefForm.ShowSingleton(this);
                return;
            }
            if (Settings.Default.isOpenMenuOnAppStart)
            {
                Log.Info($"Opening preferences on startup", "Startup");
                PrefForm.ShowSingleton(this);
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
                ClearHistoryMenu();
                if (notifyIcon1 != null) notifyIcon1.Dispose();
                if (this.Icon != null) this.Icon.Dispose();

                UnregisterHotKey(this.Handle, HOTKEY_ID_CAPTURE);
                UnregisterHotKey(this.Handle, HOTKEY_ID_OCR);
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
        public void openImageFromHistory(ToolStripMenuItem item)
        {
            if (s == null)
            {
                s = new ScreenWindow(this);
            }
            s.openImageFromHistory(item);
        }

        public void setHistory(SnapWindow sw)
        {
            int limit = Properties.Settings.Default.HistoryLimit;
            if (limit == 0) return;

            // 実パスの確定（既存ロジックのまま）
            string path = sw.Text;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                var tagged = sw.GetImageSourcePath();
                if (!string.IsNullOrEmpty(tagged) && File.Exists(tagged))
                {
                    path = tagged;
                }
                else
                {
                    Directory.CreateDirectory(HistoryTempDir);
                    path = Path.Combine(HistoryTempDir, $"{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
                    using (var clone = (Bitmap)sw.main_image.Clone())
                        clone.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    Log.Debug($"History: saved temp image to {path}", "History");
                }
            }

            // 小さめサムネ（前回の CreateThumb 推奨）
            const int TH_W = 64, TH_H = 64;
            var srcForThumb = (Image)(sw.thumbnail_image ?? sw.main_image);
            Bitmap thumb = CreateThumb(srcForThumb, TH_W, TH_H, Color.Transparent);

            var entry = new HistoryEntry { Path = path, Thumb = thumb };

            // 表示テキスト：ファイルならファイル名1行＋詳細1行 / それ以外は「キャプチャー/クリップボード」
            // sw.CurrentLoadMethod が History の場合は、path が実在すれば Path扱い、なければ Capture扱い等でOK
            var displayMethod =
                (sw.CurrentLoadMethod == LoadMethod.Path && !IsHistoryTempPath(path))
                    ? LoadMethod.Path
                    : (sw.CurrentLoadMethod == LoadMethod.Clipboard
                        ? LoadMethod.Clipboard
                        : LoadMethod.Capture);

            // 表示用の path は、Path 扱いのときだけ渡す
            var text = FormatHistoryText(
                path: (displayMethod == LoadMethod.Path) ? path : null,
                method: displayMethod,
                res: new Size(sw.main_image.Width, sw.main_image.Height),
                loadedAt: DateTime.Now
            );

            var item = new ToolStripMenuItem
            {
                Image = thumb,
                Text = text,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ImageAlign = ContentAlignment.MiddleLeft,
                ImageScaling = ToolStripItemImageScaling.None,
                Tag = entry,
                AutoSize = true
            };
            // ツールチップにフルパスを出す（ファイル時のみ）
            if (displayMethod == LoadMethod.Path)
            {
                item.AutoToolTip = true;
                item.ToolTipText = path;
            }

            item.Click += historyToolStripMenuItem1_item_Click;
            historyToolStripMenuItem1.DropDownItems.Insert(0, item);

            // 上限掃除（既存のDispose順でOK）
            while (historyToolStripMenuItem1.DropDownItems.Count > limit)
            {
                int lastIdx = historyToolStripMenuItem1.DropDownItems.Count - 1;
                var last = historyToolStripMenuItem1.DropDownItems[lastIdx] as ToolStripMenuItem;
                historyToolStripMenuItem1.DropDownItems.RemoveAt(lastIdx);
                if (last != null)
                {
                    if (last.Tag is HistoryEntry he)
                    {
                        if (IsHistoryTempPath(he.Path)) SafeDelete(he.Path);
                        he.Thumb?.Dispose();
                        last.Tag = null;
                    }
                    last.Image?.Dispose();
                    last.Dispose();
                }
            }
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
        private static string FormatHistoryText(string path, LoadMethod method, Size res, DateTime loadedAt)
        {
            Log.Debug($"FormatHistoryText: {path}, {method}, {res}, {loadedAt}", "History");
            // 1行目：ファイルならファイル名（長ければ省略）／それ以外は種別
            // 2行目：解像度 + 読み込み時刻
            string text = $"[{loadedAt:yyyy/MM/dd HH:mm:ss}] ({res.Width}x{res.Height})";

            if (method == LoadMethod.Path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var name = Path.GetFileName(path);
                text += Environment.NewLine + MiddleEllipsis(name, 32);   // 例：32文字に中省略
            }
            // else
            // {
            //     // 日本語表示（必要なら SR.T(...) に置き換え）
            //     line1 = (method == LoadMethod.Clipboard) ? "クリップボード" : "キャプチャー";
            // }
            
            return text;
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
            // PrefForm pref = new PrefForm();
            // pref.Show();
            PrefForm.ShowSingleton(this);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openImage();
        }

        private void historyToolStripMenuItem1_item_Click(object sender, EventArgs e)
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
                PrefForm.ShowSingleton(this);
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右クリックは ContextMenuStrip が出るので、通常は何もしない
            }
        }
        private void ClearHistoryMenu()
        {
            foreach (ToolStripItem tsi in historyToolStripMenuItem1.DropDownItems)
            {
                if (tsi is ToolStripMenuItem mi)
                {
                    if (mi.Tag is HistoryEntry he)
                    {
                        if (IsHistoryTempPath(he.Path)) SafeDelete(he.Path);
                        he.Thumb?.Dispose();
                        mi.Tag = null;
                    }
                    mi.Image?.Dispose();
                    mi.Dispose();
                }
            }
            historyToolStripMenuItem1.DropDownItems.Clear();
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
            if (contextMenuStrip1 != null)
                ApplyToolStripLocalization(contextMenuStrip1.Items);

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
                if (Properties.Settings.Default.isStartup)
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

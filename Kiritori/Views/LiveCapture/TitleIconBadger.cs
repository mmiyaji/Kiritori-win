using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    internal enum LiveBadgeState { None, Recording, Paused }

    internal sealed class TitleIconBadger : IDisposable
    {
        private readonly Form _form;
        private readonly Icon _baseIcon;          // 元のアイコンを保持
        private readonly Timer _blinkTimer;
        private bool _blinkOn;
        private LiveBadgeState _state = LiveBadgeState.None;
        private bool _disposed;
        private readonly Dictionary<string, Icon> _cache = new Dictionary<string, Icon>();

        public TitleIconBadger(Form form)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
            _baseIcon = _form.Icon;               // nullでもOK（後段でfallback）
            _blinkTimer = new Timer { Interval = 500 }; // 0.5秒点滅
            _blinkTimer.Tick += (s, e) =>
            {
                _blinkOn = !_blinkOn;
                Apply();
            };
        }

        public void SetState(LiveBadgeState state, bool blink)
        {
            _state = state;
            if (blink && state == LiveBadgeState.Recording)
            {
                if (!_blinkTimer.Enabled) { _blinkOn = true; _blinkTimer.Start(); }
            }
            else
            {
                _blinkTimer.Stop();
                _blinkOn = true; // 停止時は常時表示
            }
            Apply();
        }

        private void Apply()
        {
            if (_disposed || _form.IsDisposed) return;

            var small = SystemInformation.SmallIconSize;     // タイトルバー用（通常16x16）
            var large = new Size(32, 32);                    // タスクバー/Alt+Tab向け簡易

            var icoSmall = BuildIconForSize(small);
            var icoLarge = BuildIconForSize(large);

            // Windowsは複数サイズ入りICOが理想だが、ここでは小を優先して設定
            // 大きい方は NotifyIcon 等で使いたい場合に取り出せるように返すだけでもOK
            // フォームに設定
            _form.Icon = icoSmall ?? _baseIcon;
        }

        private Icon BuildIconForSize(Size size)
        {
            string key = $"{size.Width}x{size.Height}-{_state}-{(_blinkOn ? 1 : 0)}";
            Icon cached;
            if (_cache.TryGetValue(key, out cached)) return cached;

            // 元アイコン → 指定サイズに描画した下地
            using (var bmp = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                if (_baseIcon != null)
                    g.DrawIcon(_baseIcon, new Rectangle(Point.Empty, size));
                else
                    DrawFallbackBase(g, size); // 万一アイコンが無い場合の無地

                // バッジ描画
                DrawBadge(g, size, _state, _blinkOn);

                // ビットマップ→アイコン
                var hIcon = bmp.GetHicon();
                var ico = Icon.FromHandle(hIcon);
                _cache[key] = ico;
                return ico;
            }
        }

        private static void DrawFallbackBase(Graphics g, Size size)
        {
            using (var br = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                g.FillRectangle(br, new Rectangle(Point.Empty, size));
        }

        private static void DrawBadge(Graphics g, Size size, LiveBadgeState state, bool on)
        {
            if (state == LiveBadgeState.None) return;

            // バッジ位置：右下コーナー
            var w = size.Width; var h = size.Height;
            float d = Math.Max(6f, Math.Min(w, h) * 0.5f); // バッジ直径
            float margin = Math.Max(1f, d * 0.12f);
            float x = w - d - margin;
            float y = h - d - margin;

            // 影（ドロップシャドウ）
            using (var sb = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                g.FillEllipse(sb, x + 1, y + 1, d, d);

            switch (state)
            {
                case LiveBadgeState.Recording:
                    // 録画：赤い点（点滅 on/off）
                    using (var br = new SolidBrush(on ? Color.FromArgb(230, 220, 30, 38) : Color.FromArgb(150, 180, 60, 66)))
                        g.FillEllipse(br, x, y, d, d);
                    using (var pen = new Pen(Color.FromArgb(230, 255, 240, 240), Math.Max(1f, d * 0.08f)))
                        g.DrawEllipse(pen, x, y, d, d);
                    break;

                case LiveBadgeState.Paused:
                    // 一時停止：黄色の二重バー
                    using (var br = new SolidBrush(Color.FromArgb(235, 255, 200, 0)))
                        g.FillEllipse(br, x, y, d, d);
                    // pause bars
                    float barW = d * 0.22f;
                    float barGap = d * 0.14f;
                    float barH = d * 0.60f;
                    float cx = x + d / 2f;
                    float top = y + (d - barH) / 2f;
                    using (var br2 = new SolidBrush(Color.FromArgb(230, 80, 60, 0)))
                    {
                        g.FillRectangle(br2, cx - barGap / 2f - barW, top, barW, barH);
                        g.FillRectangle(br2, cx + barGap / 2f, top, barW, barH);
                    }
                    using (var pen = new Pen(Color.FromArgb(230, 255, 240, 240), Math.Max(1f, d * 0.08f)))
                        g.DrawEllipse(pen, x, y, d, d);
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _blinkTimer.Stop();
            _blinkTimer.Dispose();
            // 元アイコンに戻す
            if (!_form.IsDisposed) _form.Icon = _baseIcon;
            // キャッシュのアイコンハンドル解放
            foreach (var kv in _cache)
            {
                try { DestroyIcon(kv.Value.Handle); } catch { }
            }
            _cache.Clear();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

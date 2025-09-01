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

        public void SetState(LiveBadgeState state)
        {
            if (_disposed) return;
            if (_state == state) return; // 変化なしなら何もしない
            _state = state;
            Apply();
        }

        private void Apply()
        {
            if (_disposed || _form.IsDisposed) return;

            // 状態×サイズだけをキーに（点滅は無し）
            var size = SystemInformation.SmallIconSize;
            var ico = BuildIconForSizeAndState(size, _state) ?? _baseIcon;

            // 同一参照なら再設定しない
            if (!ReferenceEquals(_form.Icon, ico))
                _form.Icon = ico;
        }

        private Icon BuildIconForSizeAndState(Size size, LiveBadgeState state)
        {
            var key = $"{size.Width}x{size.Height}-{state}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            using (var bmp = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                if (_baseIcon != null)
                    g.DrawIcon(_baseIcon, new Rectangle(Point.Empty, size));
                else
                    DrawFallbackBase(g, size);

                DrawBadge(g, size, state, on:true); // 点滅なし＝常時 on

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(h))
                    {
                        var clone = (Icon)tmp.Clone(); // マネージド実体
                        _cache[key] = clone;
                        return clone;
                    }
                }
                finally
                {
                    DestroyIcon(h); // 生成直後に必ず解放
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 元アイコンへ戻す（任意）
            if (!_form.IsDisposed) _form.Icon = _baseIcon;

            // キャッシュ破棄（Clone は Dispose でOK）
            foreach (var ico in _cache.Values) try { ico.Dispose(); } catch { }
            _cache.Clear();
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


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

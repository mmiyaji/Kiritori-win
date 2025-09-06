using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    // Rendering（再生中/描画中）, Paused（一時停止）, Stopped（停止）, Recording（録画中）
    internal enum LiveBadgeState { None, Rendering, Paused, Stopped, Recording }

    internal sealed class TitleIconBadger : IDisposable
    {
        private readonly Form _form;
        private readonly Icon _baseIcon;          // 元のアイコン
        private LiveBadgeState _state = LiveBadgeState.None;
        private bool _disposed;
        private readonly Dictionary<string, Icon> _cache = new Dictionary<string, Icon>();

        public TitleIconBadger(Form form)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
            _baseIcon = _form.Icon;               // null可
        }

        public void SetState(LiveBadgeState state)
        {
            if (_disposed) return;
            if (_state == state) return;
            _state = state;
            Apply();
        }

        private void Apply()
        {
            if (_disposed || _form.IsDisposed) return;

            var size = SystemInformation.SmallIconSize;
            var ico = BuildIconForSizeAndState(size, _state) ?? _baseIcon;

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
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                if (_baseIcon != null)
                    g.DrawIcon(_baseIcon, new Rectangle(Point.Empty, size));
                else
                    DrawFallbackBase(g, size);

                DrawBadge(g, size, state);

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(h))
                    {
                        var clone = (Icon)tmp.Clone();
                        _cache[key] = clone;
                        return clone;
                    }
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!_form.IsDisposed) _form.Icon = _baseIcon;

            foreach (var ico in _cache.Values) try { ico.Dispose(); } catch { }
            _cache.Clear();
        }

        private static void DrawFallbackBase(Graphics g, Size size)
        {
            using (var br = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                g.FillRectangle(br, new Rectangle(Point.Empty, size));
        }

        private static void DrawBadge(Graphics g, Size size, LiveBadgeState state)
        {
            if (state == LiveBadgeState.None) return;

            var w = size.Width; var h = size.Height;
            float d = Math.Max(6f, Math.Min(w, h) * 0.5f);
            float margin = Math.Max(1f, d * 0.12f);
            float x = w - d - margin;
            float y = h - d - margin;

            using (var sb = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                g.FillEllipse(sb, x + 1, y + 1, d, d);

            using (var pen = new Pen(Color.FromArgb(230, 255, 240, 240), Math.Max(1f, d * 0.08f)))
            {
                switch (state)
                {
                    case LiveBadgeState.Recording:
                        using (var br = new SolidBrush(Color.FromArgb(230, 220, 30, 38))) // 赤
                            g.FillEllipse(br, x, y, d, d);
                        g.DrawEllipse(pen, x, y, d, d);
                        break;

                    case LiveBadgeState.Rendering:
                        using (var br = new SolidBrush(Color.FromArgb(230, 40, 190, 90))) // 緑
                            g.FillEllipse(br, x, y, d, d);
                        g.DrawEllipse(pen, x, y, d, d);
                        break;

                    case LiveBadgeState.Paused:
                        using (var br = new SolidBrush(Color.FromArgb(235, 255, 200, 0))) // 黄
                            g.FillEllipse(br, x, y, d, d);

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
                        g.DrawEllipse(pen, x, y, d, d);
                        break;

                    case LiveBadgeState.Stopped:
                        using (var br = new SolidBrush(Color.FromArgb(220, 120, 120, 120))) // 灰色
                            g.FillEllipse(br, x, y, d, d);

                        float sq = d * 0.46f;
                        float sx = x + (d - sq) / 2f;
                        float sy = y + (d - sq) / 2f;
                        using (var br2 = new SolidBrush(Color.FromArgb(245, 70, 70, 70)))
                            g.FillRectangle(br2, sx, sy, sq, sq);

                        g.DrawEllipse(pen, x, y, d, d);
                        break;
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

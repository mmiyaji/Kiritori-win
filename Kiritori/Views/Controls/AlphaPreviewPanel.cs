using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Kiritori.Views.Controls
{
    public class AlphaPreviewPanel : Panel
    {
        private Color _rgbColor = Color.Red;
        private int _alphaPercent = 60;
        private Bitmap _checkerCache;
        private Size _checkerCacheSize;

        [Bindable(true)]
        public Color RgbColor
        {
            get { return _rgbColor; }
            set { if (_rgbColor != value) { _rgbColor = value; Invalidate(); } }
        }

        [Bindable(true)]
        public int AlphaPercent
        {
            get { return _alphaPercent; }
            set
            {
                var v = Math.Max(0, Math.Min(100, value));
                if (_alphaPercent != v) { _alphaPercent = v; Invalidate(); }
            }
        }

        public AlphaPreviewPanel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint
                        | ControlStyles.OptimizedDoubleBuffer
                        | ControlStyles.UserPaint, true);
            this.BorderStyle = BorderStyle.FixedSingle;
            this.Height = 24;
            this.Width = 120;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // チェッカーボードをサイズが変わった時だけ再生成
            var sz = ClientSize;
            if (_checkerCache == null || _checkerCacheSize != sz)
            {
                _checkerCache?.Dispose();
                _checkerCache = new Bitmap(sz.Width > 0 ? sz.Width : 1, sz.Height > 0 ? sz.Height : 1);
                _checkerCacheSize = sz;
                const int cell = 6;
                using (var gBmp = Graphics.FromImage(_checkerCache))
                using (var brushDark = new SolidBrush(Color.LightGray))
                using (var brushLight = new SolidBrush(Color.White))
                {
                    for (int y = 0; y < sz.Height; y += cell)
                        for (int x = 0; x < sz.Width; x += cell)
                            gBmp.FillRectangle((((x / cell) + (y / cell)) % 2 == 0) ? brushDark : brushLight, x, y, cell, cell);
                }
            }
            g.DrawImageUnscaled(_checkerCache, 0, 0);

            int alpha = (int)Math.Round(_alphaPercent * 2.55);
            using (var overlay = new SolidBrush(Color.FromArgb(alpha, _rgbColor)))
            {
                g.FillRectangle(overlay, ClientRectangle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _checkerCache?.Dispose(); _checkerCache = null; }
            base.Dispose(disposing);
        }
    }

}

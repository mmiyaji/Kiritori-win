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
            int sz = 6;
            using (var brushDark = new SolidBrush(Color.LightGray))
            using (var brushLight = new SolidBrush(Color.White))
            {
                for (int y = 0; y < Height; y += sz)
                    for (int x = 0; x < Width; x += sz)
                        g.FillRectangle((((x / sz) + (y / sz)) % 2 == 0) ? brushDark : brushLight, x, y, sz, sz);
            }

            int alpha = (int)Math.Round(_alphaPercent * 2.55);
            using (var overlay = new SolidBrush(Color.FromArgb(alpha, _rgbColor)))
            {
                g.FillRectangle(overlay, ClientRectangle);
            }
        }
    }

}

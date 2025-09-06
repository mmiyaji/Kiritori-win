using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== 効果 / 見た目 =====
        public void ToggleShadow(bool enable)
        {
            this.isWindowShadow = enable;
            this.RecreateHandle();
            ShowOverlay(this.isWindowShadow ? "SHADOW: ON" : "SHADOW: OFF");
        }

        public void ToggleHoverHighlight(bool enable)
        {
            this.isHighlightOnHover = enable;
            ShowOverlay(this.isHighlightOnHover ? "HOVER HIGHLIGHT: ON" : "HOVER HIGHLIGHT: OFF");
        }

        public void setAlpha(double alpha)
        {
            this.Opacity = alpha;
            this.WindowAlphaPercent = alpha;
        }

        public void ShowOverlay(string text)
        {
            if (!this.isOverlay) return;
            const int MIN_W = 100;
            const int MIN_H = 50;
            if (this.ClientSize.Width < MIN_W || this.ClientSize.Height < MIN_H) return;

            _overlayText = text;
            _overlayStart = DateTime.Now;
            _overlayTimer.Start();
            pictureBox1.Invalidate();
        }

        private float HoverThicknessDpi()
        {
            float t = _hoverThicknessPx * (this.DeviceDpi / 96f);
            return (t < 1f) ? 1f : t;
        }

        private Pen MakeHoverPen()
        {
            int a = Math.Max(0, Math.Min(255, (int)Math.Round(_hoverAlphaPercent * 2.55)));
            float w = HoverThicknessDpi();
            Color c = Color.FromArgb(a, _hoverColor);
            var pen = new Pen(c, w) { Alignment = PenAlignment.Inset };
            return pen;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private (Bitmap normal, Bitmap hover) GetCloseBitmapsForDpi(int dpi)
        {
            int key = (dpi <= 0 ? this.DeviceDpi : dpi);
            if (_closeIconCache.TryGetValue(key, out var cached)) return cached;

            float scale = key / 96f;
            int size = (int)Math.Round(20 * scale);

            Bitmap bmpNormal = new Bitmap(Properties.Resources.close, new Size(size, size));
            Bitmap bmpHover = new Bitmap(Properties.Resources.close_bold, new Size(size, size));

            _closeIconCache[key] = (bmpNormal, bmpHover);
            return _closeIconCache[key];
        }

        #endregion

    }
}
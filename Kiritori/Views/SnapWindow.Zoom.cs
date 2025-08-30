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
        #region ===== ズーム関連 =====
        public void zoomIn()
        {
            _zoomStep++;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
        }

        public void zoomOut()
        {
            _zoomStep--;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
        }

        public void zoomOff()
        {
            _zoomStep = 0;
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom 100%");
        }

        public void ZoomToPercent(int percent)
        {
            _zoomStep = (int)Math.Round((percent - 100) / (STEP_LINEAR * 100f));
            UpdateScaleFromStep();
            ApplyZoom(false);
            ShowOverlay("Zoom " + percent + "%");
        }

        private void UpdateScaleFromStep()
        {
            _scale = 1.0f + (_zoomStep * STEP_LINEAR);
            if (_scale < MIN_SCALE)
            {
                _scale = MIN_SCALE;
                _zoomStep = (int)Math.Round((MIN_SCALE - 1.0f) / STEP_LINEAR);
            }
            else if (_scale > MAX_SCALE)
            {
                _scale = MAX_SCALE;
                _zoomStep = (int)Math.Round((MAX_SCALE - 1.0f) / STEP_LINEAR);
            }
        }

        private void SetImageAndResetZoom(Image img)
        {
            _originalImage?.Dispose();
            _originalImage = (Image)img.Clone();

            _scale = 1f;
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage; // ストレッチ採用
            ApplyZoom(false);
        }

        private void ApplyZoom(bool redrawOnly)
        {
            if (_originalImage == null) return;

            int newW = Math.Max(1, (int)Math.Round(_originalImage.Width * _scale));
            int newH = Math.Max(1, (int)Math.Round(_originalImage.Height * _scale));

            Size oldClient = this.ClientSize;

            Bitmap bmp = new Bitmap(newW, newH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(_originalImage, 0, 0, newW, newH);
            }

            var oldImg = pictureBox1.Image;
            pictureBox1.Image = bmp;
            oldImg?.Dispose();

            pictureBox1.Width = newW;
            pictureBox1.Height = newH;

            this.ClientSize = new Size(newW, newH);

            if (!redrawOnly)
            {
                int dx = (this.ClientSize.Width - oldClient.Width) / 2;
                int dy = (this.ClientSize.Height - oldClient.Height) / 2;
                this.Left -= dx;
                this.Top  -= dy;
            }
        }

        private void ApplyInitialDisplayZoomIfNeeded()
        {
            if (_originalImage == null) return;

            var wa = Screen.FromControl(this).WorkingArea;

            double capByHalfWidth = (wa.Width * 0.5) / (double)_originalImage.Width;
            double capByHeight = (wa.Height * 1.0) / (double)_originalImage.Height;
            double desired = Math.Min(1.0, Math.Min(capByHalfWidth, capByHeight));

            if (desired >= 1.0) return;

            double clamped = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, desired));
            int step = (int)Math.Round((clamped - 1.0) / STEP_LINEAR);

            _zoomStep = step;
            UpdateScaleFromStep();

            ApplyZoom(redrawOnly: false);
        }

        #endregion

    }
}
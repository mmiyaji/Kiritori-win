using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private readonly List<Rectangle> _annotationRects = new List<Rectangle>();
        private bool _annotationMode;
        private bool _annotationDragging;
        private bool _annotationFeatureInitialized;
        private bool _standardMouseHandlersDetached;
        private Point _annotationStartImage;
        private Rectangle _annotationPreviewImage = Rectangle.Empty;
        private ToolStripMenuItem _annotationModeMenuItem;
        private ToolStripMenuItem _annotationClearMenuItem;

        private void InitializeAnnotationFeature()
        {
            if (_annotationFeatureInitialized || pictureBox1 == null) return;
            _annotationFeatureInitialized = true;

            pictureBox1.Paint += PictureBox1_PaintAnnotations;
            pictureBox1.MouseDown += PictureBox1_MouseDownAnnotations;
            pictureBox1.MouseMove += PictureBox1_MouseMoveAnnotations;
            pictureBox1.MouseUp += PictureBox1_MouseUpAnnotations;
            pictureBox1.Disposed += (s, e) => RestoreStandardMouseHandlers();

            _annotationModeMenuItem = new ToolStripMenuItem("Rectangle annotate")
            {
                CheckOnClick = false,
                ShortcutKeyDisplayString = "Drag"
            };
            _annotationModeMenuItem.Click += (s, e) => ToggleAnnotationMode();

            _annotationClearMenuItem = new ToolStripMenuItem("Clear annotations");
            _annotationClearMenuItem.Click += (s, e) => ClearAnnotations();
            _annotationClearMenuItem.Enabled = false;

            if (editParentMenu != null)
            {
                editParentMenu.DropDownItems.Add(new ToolStripSeparator());
                editParentMenu.DropDownItems.Add(_annotationModeMenuItem);
                editParentMenu.DropDownItems.Add(_annotationClearMenuItem);
            }
        }

        private void ResetAnnotations()
        {
            _annotationRects.Clear();
            _annotationPreviewImage = Rectangle.Empty;
            _annotationDragging = false;
            RestoreStandardMouseHandlers();
            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
        }

        private Bitmap CloneCurrentBitmapWithAnnotations(Bitmap source)
        {
            if (source == null) return null;
            var clone = new Bitmap(source);
            if (_annotationRects.Count == 0) return clone;

            using (var g = Graphics.FromImage(clone))
            using (var fill = new SolidBrush(Color.FromArgb(48, 255, 159, 67)))
            using (var pen = new Pen(Color.FromArgb(255, 255, 159, 67), Math.Max(2f, source.Width / 500f)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var rect in _annotationRects)
                {
                    if (rect.Width <= 0 || rect.Height <= 0) continue;
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(pen, rect);
                }
            }

            return clone;
        }

        private void ToggleAnnotationMode()
        {
            _annotationMode = !_annotationMode;
            _annotationDragging = false;
            _annotationPreviewImage = Rectangle.Empty;
            if (_annotationMode) DetachStandardMouseHandlers(); else RestoreStandardMouseHandlers();
            UpdateAnnotationMenuState();
            this.Cursor = _annotationMode ? Cursors.Cross : Cursors.Default;
            ShowOverlay(_annotationMode ? "ANNOTATE" : "ANNOTATE OFF");
            pictureBox1?.Invalidate();
        }

        private void ClearAnnotations()
        {
            if (_annotationRects.Count == 0) return;
            _annotationRects.Clear();
            _annotationPreviewImage = Rectangle.Empty;
            UpdateAnnotationMenuState();
            ShowOverlay("ANNOTATIONS CLEARED");
            pictureBox1?.Invalidate();
        }

        private void DetachStandardMouseHandlers()
        {
            if (_standardMouseHandlersDetached || pictureBox1 == null) return;
            pictureBox1.MouseDown -= pictureBox1_MouseDown;
            pictureBox1.MouseMove -= pictureBox1_MouseMove;
            pictureBox1.MouseUp -= pictureBox1_MouseUp;
            _standardMouseHandlersDetached = true;
        }

        private void RestoreStandardMouseHandlers()
        {
            if (!_standardMouseHandlersDetached || pictureBox1 == null || pictureBox1.IsDisposed) return;
            pictureBox1.MouseDown += pictureBox1_MouseDown;
            pictureBox1.MouseMove += pictureBox1_MouseMove;
            pictureBox1.MouseUp += pictureBox1_MouseUp;
            _standardMouseHandlersDetached = false;
        }

        private void UpdateAnnotationMenuState()
        {
            if (_annotationModeMenuItem != null)
                _annotationModeMenuItem.Checked = _annotationMode;
            if (_annotationClearMenuItem != null)
                _annotationClearMenuItem.Enabled = _annotationRects.Count > 0;
        }

        private void PictureBox1_MouseDownAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode || e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

            _annotationDragging = true;
            _annotationStartImage = imagePoint;
            _annotationPreviewImage = new Rectangle(imagePoint, Size.Empty);
            pictureBox1.Capture = true;
        }

        private void PictureBox1_MouseMoveAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationDragging) return;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

            _annotationPreviewImage = NormalizeRect(_annotationStartImage, imagePoint);
            pictureBox1.Invalidate();
        }

        private void PictureBox1_MouseUpAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationDragging || e.Button != MouseButtons.Left) return;

            _annotationDragging = false;
            pictureBox1.Capture = false;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint))
            {
                _annotationPreviewImage = Rectangle.Empty;
                pictureBox1.Invalidate();
                return;
            }

            var rect = NormalizeRect(_annotationStartImage, imagePoint);
            _annotationPreviewImage = Rectangle.Empty;

            if (rect.Width >= 8 && rect.Height >= 8)
            {
                _annotationRects.Add(rect);
                UpdateAnnotationMenuState();
                ShowOverlay("ANNOTATED");
                Log.Info("Annotation rectangle added", "SnapWindow");
            }

            pictureBox1.Invalidate();
        }

        private void PictureBox1_PaintAnnotations(object sender, PaintEventArgs e)
        {
            Rectangle displayRect;
            Bitmap source;
            if (!TryGetDisplayImageRect(out displayRect, out source)) return;

            using (var fill = new SolidBrush(Color.FromArgb(52, 255, 159, 67)))
            using (var pen = new Pen(Color.FromArgb(255, 255, 159, 67), Math.Max(2f, DeviceDpi / 96f * 2f)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var rect in _annotationRects)
                    DrawAnnotationRect(e.Graphics, pen, fill, displayRect, source.Size, rect);

                if (_annotationDragging && _annotationPreviewImage.Width > 0 && _annotationPreviewImage.Height > 0)
                    DrawAnnotationRect(e.Graphics, pen, fill, displayRect, source.Size, _annotationPreviewImage);
            }
        }

        private void DrawAnnotationRect(Graphics g, Pen pen, Brush fill, Rectangle displayRect, Size sourceSize, Rectangle imageRect)
        {
            var clientRect = ImageRectToClientRect(displayRect, sourceSize, imageRect);
            if (clientRect.Width <= 0 || clientRect.Height <= 0) return;
            g.FillRectangle(fill, clientRect);
            g.DrawRectangle(pen, clientRect);
        }

        private bool TryGetDisplayImageRect(out Rectangle displayRect, out Bitmap source)
        {
            source = main_image ?? _originalImage as Bitmap;
            displayRect = Rectangle.Empty;
            if (source == null || pictureBox1 == null) return false;

            var client = pictureBox1.ClientRectangle;
            if (client.Width <= 0 || client.Height <= 0) return false;

            if (pictureBox1.SizeMode == PictureBoxSizeMode.StretchImage)
            {
                displayRect = client;
                return true;
            }

            float scale = Math.Min((float)client.Width / source.Width, (float)client.Height / source.Height);
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));
            int x = client.X + (client.Width - w) / 2;
            int y = client.Y + (client.Height - h) / 2;
            displayRect = new Rectangle(x, y, w, h);
            return true;
        }

        private bool TryClientToImagePoint(Point clientPoint, out Point imagePoint)
        {
            imagePoint = Point.Empty;
            Rectangle displayRect;
            Bitmap source;
            if (!TryGetDisplayImageRect(out displayRect, out source)) return false;
            if (!displayRect.Contains(clientPoint)) return false;

            double rx = (double)(clientPoint.X - displayRect.X) / Math.Max(1, displayRect.Width);
            double ry = (double)(clientPoint.Y - displayRect.Y) / Math.Max(1, displayRect.Height);
            int x = Math.Max(0, Math.Min(source.Width - 1, (int)Math.Round(rx * source.Width)));
            int y = Math.Max(0, Math.Min(source.Height - 1, (int)Math.Round(ry * source.Height)));
            imagePoint = new Point(x, y);
            return true;
        }

        private Rectangle ImageRectToClientRect(Rectangle displayRect, Size sourceSize, Rectangle imageRect)
        {
            int x = displayRect.X + (int)Math.Round((double)imageRect.X * displayRect.Width / Math.Max(1, sourceSize.Width));
            int y = displayRect.Y + (int)Math.Round((double)imageRect.Y * displayRect.Height / Math.Max(1, sourceSize.Height));
            int w = Math.Max(1, (int)Math.Round((double)imageRect.Width * displayRect.Width / Math.Max(1, sourceSize.Width)));
            int h = Math.Max(1, (int)Math.Round((double)imageRect.Height * displayRect.Height / Math.Max(1, sourceSize.Height)));
            return new Rectangle(x, y, w, h);
        }

        private Rectangle NormalizeRect(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
    }
}

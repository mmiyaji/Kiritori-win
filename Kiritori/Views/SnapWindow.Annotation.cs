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
        private enum AnnotationTool
        {
            Rectangle,
            Arrow,
        }

        private enum AnnotationShapeKind
        {
            Rectangle,
            Arrow,
        }

        private sealed class AnnotationShape
        {
            public AnnotationShapeKind Kind;
            public Point Start;
            public Point End;
            public Color StrokeColor;
            public int StrokeWidth;
            public Color FillColor;

            public Rectangle Bounds
            {
                get { return Rectangle.FromLTRB(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y), Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)); }
            }
        }

        private readonly List<AnnotationShape> _annotations = new List<AnnotationShape>();
        private bool _annotationMode;
        private bool _annotationDragging;
        private bool _annotationFeatureInitialized;
        private bool _standardMouseHandlersDetached;
        private Point _annotationStartImage;
        private AnnotationShape _annotationPreview;
        private AnnotationTool _annotationTool = AnnotationTool.Rectangle;
        private ToolStripMenuItem _annotationModeMenuItem;
        private ToolStripMenuItem _annotationClearMenuItem;
        private ToolStripMenuItem _annotationUndoMenuItem;
        private Panel _annotationPalette;
        private Label _annotationPaletteLabel;
        private Button _annotationRectButton;
        private Button _annotationArrowButton;
        private Button _annotationUndoButton;
        private Button _annotationClearButton;
        private Button _annotationDoneButton;

        private void InitializeAnnotationFeature()
        {
            if (_annotationFeatureInitialized || pictureBox1 == null) return;
            _annotationFeatureInitialized = true;

            pictureBox1.Paint += PictureBox1_PaintAnnotations;
            pictureBox1.MouseDown += PictureBox1_MouseDownAnnotations;
            pictureBox1.MouseMove += PictureBox1_MouseMoveAnnotations;
            pictureBox1.MouseUp += PictureBox1_MouseUpAnnotations;
            pictureBox1.Disposed += (s, e) => RestoreStandardMouseHandlers();

            CreateAnnotationPalette();
            CreateAnnotationMenuItems();
            this.Resize += (s, e) => RepositionAnnotationPalette();
        }

        private void CreateAnnotationMenuItems()
        {
            _annotationModeMenuItem = new ToolStripMenuItem("Edit annotations");
            _annotationModeMenuItem.Click += (s, e) => ToggleAnnotationMode();

            _annotationUndoMenuItem = new ToolStripMenuItem("Undo last annotation");
            _annotationUndoMenuItem.Click += (s, e) => UndoLastAnnotation();

            _annotationClearMenuItem = new ToolStripMenuItem("Clear annotations");
            _annotationClearMenuItem.Click += (s, e) => ClearAnnotations();

            if (editParentMenu != null)
            {
                editParentMenu.DropDownItems.Add(new ToolStripSeparator());
                editParentMenu.DropDownItems.Add(_annotationModeMenuItem);
                editParentMenu.DropDownItems.Add(_annotationUndoMenuItem);
                editParentMenu.DropDownItems.Add(_annotationClearMenuItem);
            }

            UpdateAnnotationMenuState();
        }

        private void CreateAnnotationPalette()
        {
            if (_annotationPalette != null) return;

            _annotationPalette = new Panel
            {
                Visible = false,
                Size = new Size(352, 46),
                BackColor = Color.FromArgb(232, 26, 29, 34),
                Padding = new Padding(8)
            };

            _annotationPaletteLabel = new Label
            {
                AutoSize = false,
                Text = "Edit",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleLeft,
                Size = new Size(38, 28),
                Location = new Point(10, 9)
            };

            _annotationRectButton = CreatePaletteButton("Rect", 48, 52, (s, e) => SetAnnotationTool(AnnotationTool.Rectangle));
            _annotationArrowButton = CreatePaletteButton("Arrow", 104, 60, (s, e) => SetAnnotationTool(AnnotationTool.Arrow));
            _annotationUndoButton = CreatePaletteButton("Undo", 168, 52, (s, e) => UndoLastAnnotation());
            _annotationClearButton = CreatePaletteButton("Clear", 224, 52, (s, e) => ClearAnnotations());
            _annotationDoneButton = CreatePaletteButton("Done", 280, 52, (s, e) => ExitAnnotationMode());

            _annotationPalette.Controls.Add(_annotationPaletteLabel);
            _annotationPalette.Controls.Add(_annotationRectButton);
            _annotationPalette.Controls.Add(_annotationArrowButton);
            _annotationPalette.Controls.Add(_annotationUndoButton);
            _annotationPalette.Controls.Add(_annotationClearButton);
            _annotationPalette.Controls.Add(_annotationDoneButton);

            Controls.Add(_annotationPalette);
            _annotationPalette.BringToFront();
            RepositionAnnotationPalette();
        }

        private Button CreatePaletteButton(string text, int x, int width, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(width, 28),
                Location = new Point(x, 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 49, 57),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            return button;
        }

        private void RepositionAnnotationPalette()
        {
            if (_annotationPalette == null) return;
            _annotationPalette.Location = new Point(14, 14);
            _annotationPalette.BringToFront();
        }

        private void ResetAnnotations()
        {
            _annotations.Clear();
            _annotationPreview = null;
            _annotationDragging = false;
            ExitAnnotationMode(silent: true);
            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
        }

        private Bitmap CloneCurrentBitmapWithAnnotations(Bitmap source)
        {
            if (source == null) return null;
            var clone = new Bitmap(source);
            if (_annotations.Count == 0) return clone;

            using (var g = Graphics.FromImage(clone))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RenderAnnotationsToImage(g, includePreview: false);
            }

            return clone;
        }

        private void ToggleAnnotationMode()
        {
            if (_annotationMode) ExitAnnotationMode();
            else EnterAnnotationMode();
        }

        private void EnterAnnotationMode()
        {
            _annotationMode = true;
            _annotationDragging = false;
            _annotationPreview = null;
            DetachStandardMouseHandlers();
            if (_annotationPalette != null) _annotationPalette.Visible = true;
            RepositionAnnotationPalette();
            UpdateAnnotationMenuState();
            this.Cursor = Cursors.Cross;
            ShowOverlay("EDIT MODE");
            pictureBox1?.Invalidate();
        }

        private void ExitAnnotationMode(bool silent = false)
        {
            _annotationMode = false;
            _annotationDragging = false;
            if (pictureBox1 != null) pictureBox1.Capture = false;
            _annotationPreview = null;
            RestoreStandardMouseHandlers();
            if (_annotationPalette != null) _annotationPalette.Visible = false;
            UpdateAnnotationMenuState();
            this.Cursor = Cursors.Default;
            if (!silent) ShowOverlay("EDIT OFF");
            pictureBox1?.Invalidate();
        }

        private void SetAnnotationTool(AnnotationTool tool)
        {
            _annotationTool = tool;
            _annotationPreview = null;
            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
        }

        private void UndoLastAnnotation()
        {
            if (_annotations.Count == 0) return;
            _annotations.RemoveAt(_annotations.Count - 1);
            UpdateAnnotationMenuState();
            ShowOverlay("ANNOTATION UNDONE");
            pictureBox1?.Invalidate();
        }

        private void ClearAnnotations()
        {
            if (_annotations.Count == 0) return;
            _annotations.Clear();
            _annotationPreview = null;
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
            {
                _annotationModeMenuItem.Checked = _annotationMode;
                _annotationModeMenuItem.Text = _annotationMode ? "Finish annotations" : "Edit annotations";
            }

            var hasAnnotations = _annotations.Count > 0;
            if (_annotationClearMenuItem != null) _annotationClearMenuItem.Enabled = hasAnnotations;
            if (_annotationUndoMenuItem != null) _annotationUndoMenuItem.Enabled = hasAnnotations;

            if (_annotationPalette == null) return;

            UpdatePaletteButtonState(_annotationRectButton, _annotationTool == AnnotationTool.Rectangle);
            UpdatePaletteButtonState(_annotationArrowButton, _annotationTool == AnnotationTool.Arrow);
            if (_annotationUndoButton != null) _annotationUndoButton.Enabled = hasAnnotations;
            if (_annotationClearButton != null) _annotationClearButton.Enabled = hasAnnotations;
        }

        private void UpdatePaletteButtonState(Button button, bool selected)
        {
            if (button == null) return;
            button.BackColor = selected ? Color.FromArgb(255, 138, 61) : Color.FromArgb(44, 49, 57);
            button.ForeColor = Color.White;
        }

        private void PictureBox1_MouseDownAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode || e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

            _annotationDragging = true;
            _annotationStartImage = imagePoint;
            _annotationPreview = CreateAnnotationShape(_annotationTool, imagePoint, imagePoint);
            pictureBox1.Capture = true;
        }

        private void PictureBox1_MouseMoveAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationDragging || _annotationPreview == null) return;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

            _annotationPreview.End = imagePoint;
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
                _annotationPreview = null;
                pictureBox1.Invalidate();
                return;
            }

            if (_annotationPreview == null) return;
            _annotationPreview.End = imagePoint;

            if (IsAnnotationShapeUsable(_annotationPreview))
            {
                _annotations.Add(_annotationPreview);
                UpdateAnnotationMenuState();
                ShowOverlay(_annotationTool == AnnotationTool.Arrow ? "ARROW ADDED" : "RECT ADDED");
                Log.Info("Annotation added: " + _annotationPreview.Kind, "SnapWindow");
            }

            _annotationPreview = null;
            pictureBox1.Invalidate();
        }

        private void PictureBox1_PaintAnnotations(object sender, PaintEventArgs e)
        {
            Rectangle displayRect;
            Bitmap source;
            if (!TryGetDisplayImageRect(out displayRect, out source)) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RenderAnnotationsToClient(e.Graphics, displayRect, source.Size, includePreview: true);
        }

        private void RenderAnnotationsToImage(Graphics g, bool includePreview)
        {
            foreach (var shape in _annotations)
                DrawAnnotationShape(g, shape, null, Size.Empty);

            if (includePreview && _annotationPreview != null)
                DrawAnnotationShape(g, _annotationPreview, null, Size.Empty);
        }

        private void RenderAnnotationsToClient(Graphics g, Rectangle displayRect, Size sourceSize, bool includePreview)
        {
            foreach (var shape in _annotations)
                DrawAnnotationShape(g, shape, displayRect, sourceSize);

            if (includePreview && _annotationPreview != null)
                DrawAnnotationShape(g, _annotationPreview, displayRect, sourceSize);
        }

        private void DrawAnnotationShape(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            if (shape == null) return;

            using (var pen = CreateShapePen(shape))
            using (var brush = new SolidBrush(shape.FillColor))
            {
                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    var rect = displayRect.HasValue
                        ? ImageRectToClientRect(displayRect.Value, sourceSize, shape.Bounds)
                        : shape.Bounds;
                    if (rect.Width <= 0 || rect.Height <= 0) return;
                    g.FillRectangle(brush, rect);
                    g.DrawRectangle(pen, rect);
                    return;
                }

                var start = displayRect.HasValue
                    ? ImagePointToClientPoint(displayRect.Value, sourceSize, shape.Start)
                    : shape.Start;
                var end = displayRect.HasValue
                    ? ImagePointToClientPoint(displayRect.Value, sourceSize, shape.End)
                    : shape.End;

                g.DrawLine(pen, start, end);
            }
        }

        private Pen CreateShapePen(AnnotationShape shape)
        {
            var pen = new Pen(shape.StrokeColor, Math.Max(2f, shape.StrokeWidth))
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = shape.Kind == AnnotationShapeKind.Arrow ? LineCap.ArrowAnchor : LineCap.Round
            };
            return pen;
        }

        private AnnotationShape CreateAnnotationShape(AnnotationTool tool, Point start, Point end)
        {
            return new AnnotationShape
            {
                Kind = tool == AnnotationTool.Arrow ? AnnotationShapeKind.Arrow : AnnotationShapeKind.Rectangle,
                Start = start,
                End = end,
                StrokeColor = Color.FromArgb(255, 255, 138, 61),
                FillColor = Color.FromArgb(48, 255, 138, 61),
                StrokeWidth = Math.Max(2, DeviceDpi / 48)
            };
        }

        private bool IsAnnotationShapeUsable(AnnotationShape shape)
        {
            if (shape == null) return false;
            if (shape.Kind == AnnotationShapeKind.Arrow)
                return DistanceSquared(shape.Start, shape.End) >= 144;

            var bounds = shape.Bounds;
            return bounds.Width >= 8 && bounds.Height >= 8;
        }

        private static int DistanceSquared(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
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

        private Point ImagePointToClientPoint(Rectangle displayRect, Size sourceSize, Point imagePoint)
        {
            int x = displayRect.X + (int)Math.Round((double)imagePoint.X * displayRect.Width / Math.Max(1, sourceSize.Width));
            int y = displayRect.Y + (int)Math.Round((double)imagePoint.Y * displayRect.Height / Math.Max(1, sourceSize.Height));
            return new Point(x, y);
        }

        private Rectangle ImageRectToClientRect(Rectangle displayRect, Size sourceSize, Rectangle imageRect)
        {
            int x = displayRect.X + (int)Math.Round((double)imageRect.X * displayRect.Width / Math.Max(1, sourceSize.Width));
            int y = displayRect.Y + (int)Math.Round((double)imageRect.Y * displayRect.Height / Math.Max(1, sourceSize.Height));
            int w = Math.Max(1, (int)Math.Round((double)imageRect.Width * displayRect.Width / Math.Max(1, sourceSize.Width)));
            int h = Math.Max(1, (int)Math.Round((double)imageRect.Height * displayRect.Height / Math.Max(1, sourceSize.Height)));
            return new Rectangle(x, y, w, h);
        }
    }
}
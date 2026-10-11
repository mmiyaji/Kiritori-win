using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private int _annotationFontSize = 18;
        private string _annotationFontFamily = "Segoe UI";
        private Color _annotationTextOutlineColor = Color.Black;
        private bool _annotationTextBackground;
        private int _annotationMosaicBlockSize = 16;

        private sealed class MosaicCacheEntry
        {
            public AnnotationShape Shape;
            public Bitmap Source;
            public Rectangle Bounds;
            public int BlockSize;
            public Bitmap Pixels;
        }

        private readonly List<MosaicCacheEntry> _mosaicCache = new List<MosaicCacheEntry>();
        private const int MaxMosaicCacheEntries = 8;
        private const long MaxMosaicCacheBytes = 64L * 1024 * 1024;

        private void DisposeExtendedAnnotationResources()
        {
            foreach (var entry in _mosaicCache)
                entry.Pixels.Dispose();
            _mosaicCache.Clear();
        }

        private bool TryHandleSpecialAnnotationMouseDown(Point imagePoint)
        {
            if (_annotationTool == AnnotationTool.Text)
            {
                var text = string.Empty;
                var fontSize = _annotationFontSize;
                var fontFamily = _annotationFontFamily;
                var outlineColor = _annotationTextOutlineColor;
                var background = _annotationTextBackground;
                if (!ShowAnnotationTextEditor(ref text, ref fontSize, ref fontFamily, ref outlineColor, ref background)) return true;

                var shape = CreateSpecialAnnotationShape(AnnotationShapeKind.Text, imagePoint);
                shape.Text = text;
                shape.FontSize = fontSize;
                shape.FontFamily = fontFamily;
                shape.TextOutlineColor = outlineColor;
                shape.TextBackground = background;
                UpdateTextShapeBounds(shape);
                BeginAnnotationHistoryChange();
                _annotationFontSize = fontSize;
                _annotationFontFamily = fontFamily;
                _annotationTextOutlineColor = outlineColor;
                _annotationTextBackground = background;
                _annotations.Add(shape);
                _selectedAnnotationIndex = _annotations.Count - 1;
                CommitAnnotationHistoryChange();
                RefreshAnnotationUi();
                return true;
            }

            if (_annotationTool == AnnotationTool.Step)
            {
                var shape = CreateSpecialAnnotationShape(AnnotationShapeKind.Step, imagePoint);
                var next = 1;
                foreach (var existing in _annotations)
                    if (existing.Kind == AnnotationShapeKind.Step)
                        next = Math.Max(next, existing.StepNumber + 1);
                shape.StepNumber = next;
                PositionStepShape(shape, imagePoint);
                BeginAnnotationHistoryChange();
                _annotations.Add(shape);
                _selectedAnnotationIndex = _annotations.Count - 1;
                CommitAnnotationHistoryChange();
                RefreshAnnotationUi();
                return true;
            }

            return false;
        }

        private AnnotationShape CreateSpecialAnnotationShape(AnnotationShapeKind kind, Point imagePoint)
        {
            return new AnnotationShape
            {
                Kind = kind,
                Start = imagePoint,
                End = imagePoint,
                StrokeColor = _annotationStrokeColor,
                FillColor = _annotationFillColor,
                StrokeWidth = _annotationStrokeWidth,
                FontSize = _annotationFontSize,
                FontFamily = _annotationFontFamily,
                TextOutlineColor = _annotationTextOutlineColor,
                TextBackground = _annotationTextBackground,
                MosaicBlockSize = _annotationMosaicBlockSize
            };
        }

        private bool IsExtendedAnnotationShapeUsable(AnnotationShape shape)
        {
            if (shape.Kind == AnnotationShapeKind.Text) return !string.IsNullOrWhiteSpace(shape.Text);
            if (shape.Kind == AnnotationShapeKind.Step) return shape.StepNumber > 0;
            if (shape.Kind == AnnotationShapeKind.Mosaic)
                return shape.Bounds.Width >= 8 && shape.Bounds.Height >= 8;
            return false;
        }

        private bool DrawExtendedAnnotationShape(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            switch (shape.Kind)
            {
                case AnnotationShapeKind.Text:
                    DrawTextAnnotation(g, shape, displayRect, sourceSize);
                    return true;
                case AnnotationShapeKind.Step:
                    DrawStepAnnotation(g, shape, displayRect, sourceSize);
                    return true;
                case AnnotationShapeKind.Mosaic:
                    DrawMosaicAnnotation(g, shape, displayRect, sourceSize);
                    return true;
                default:
                    return false;
            }
        }

        private void SetAnnotationFontSize(int value)
        {
            value = Math.Max(12, Math.Min(72, value));
            BeginAnnotationHistoryChange();
            var selected = GetSelectedAnnotation();
            if (selected != null && (selected.Kind == AnnotationShapeKind.Text || selected.Kind == AnnotationShapeKind.Step))
            {
                selected.FontSize = value;
                if (selected.Kind == AnnotationShapeKind.Text)
                    UpdateTextShapeBounds(selected);
                else
                    PositionStepShape(selected, AnnotationGeometry.GetRectangleCenter(selected.Bounds));
            }
            _annotationFontSize = value;
            CommitAnnotationHistoryChange();
            RefreshAnnotationUi();
        }

        private void SetAnnotationTextBackground(bool value)
        {
            BeginAnnotationHistoryChange();
            var selected = GetSelectedAnnotation();
            if (selected != null && selected.Kind == AnnotationShapeKind.Text)
                selected.TextBackground = value;
            _annotationTextBackground = value;
            CommitAnnotationHistoryChange();
            RefreshAnnotationUi();
        }

        private void SetAnnotationTextOutlineColor(Color value)
        {
            if (value != Color.Black && value != Color.White) return;
            BeginAnnotationHistoryChange();
            var selected = GetSelectedAnnotation();
            if (selected != null && selected.Kind == AnnotationShapeKind.Text)
                selected.TextOutlineColor = value;
            _annotationTextOutlineColor = value;
            CommitAnnotationHistoryChange();
            RefreshAnnotationUi();
        }

        private void SetAnnotationMosaicBlockSize(int value)
        {
            value = Math.Max(4, Math.Min(32, value));
            BeginAnnotationHistoryChange();
            var selected = GetSelectedAnnotation();
            if (selected != null && selected.Kind == AnnotationShapeKind.Mosaic)
                selected.MosaicBlockSize = value;
            _annotationMosaicBlockSize = value;
            CommitAnnotationHistoryChange();
            RefreshAnnotationUi();
        }

        private void EditSelectedAnnotationText()
        {
            var selected = GetSelectedAnnotation();
            if (selected == null || selected.Kind != AnnotationShapeKind.Text) return;
            var text = selected.Text ?? string.Empty;
            var fontSize = selected.FontSize > 0 ? selected.FontSize : _annotationFontSize;
            var fontFamily = string.IsNullOrWhiteSpace(selected.FontFamily) ? _annotationFontFamily : selected.FontFamily;
            var outlineColor = selected.TextOutlineColor.IsEmpty ? Color.Black : selected.TextOutlineColor;
            var background = selected.TextBackground;
            if (!ShowAnnotationTextEditor(ref text, ref fontSize, ref fontFamily, ref outlineColor, ref background)) return;
            BeginAnnotationHistoryChange();
            selected.Text = text;
            selected.FontSize = fontSize;
            selected.FontFamily = fontFamily;
            selected.TextOutlineColor = outlineColor;
            selected.TextBackground = background;
            _annotationFontSize = fontSize;
            _annotationFontFamily = fontFamily;
            _annotationTextOutlineColor = outlineColor;
            _annotationTextBackground = background;
            UpdateTextShapeBounds(selected);
            CommitAnnotationHistoryChange();
            RefreshAnnotationUi();
        }

        private bool ShowAnnotationTextEditor(ref string text, ref int fontSize, ref string fontFamily, ref Color outlineColor, ref bool background)
        {
            using (var form = new Form())
            using (var editor = new TextBox())
            using (var sizeLabel = new Label())
            using (var sizeInput = new NumericUpDown())
            using (var backgroundInput = new CheckBox())
            using (var fontLabel = new Label())
            using (var fontInput = new ComboBox())
            using (var outlineLabel = new Label())
            using (var outlineInput = new ComboBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = SR.T("Annotation.TextEditor.Title");
                form.AutoScaleMode = AutoScaleMode.Dpi;
                form.AutoScaleDimensions = new SizeF(96f, 96f);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(440, 250);
                editor.Multiline = true;
                editor.AcceptsReturn = true;
                editor.ScrollBars = ScrollBars.Vertical;
                editor.AccessibleName = SR.T("Annotation.TextEditor.Title");
                editor.SetBounds(12, 12, 416, 110);
                editor.Text = text;
                fontLabel.Text = SR.T("Annotation.FontFamily");
                fontLabel.TextAlign = ContentAlignment.MiddleLeft;
                fontLabel.SetBounds(12, 130, 90, 26);
                fontInput.DropDownStyle = ComboBoxStyle.DropDownList;
                fontInput.AccessibleName = fontLabel.Text;
                fontInput.SetBounds(105, 130, 220, 26);
                PopulateAnnotationFontFamilies(fontInput, fontFamily);
                outlineLabel.Text = SR.T("Annotation.TextEditor.OutlineColor");
                outlineLabel.TextAlign = ContentAlignment.MiddleLeft;
                outlineLabel.SetBounds(12, 163, 90, 26);
                outlineInput.DropDownStyle = ComboBoxStyle.DropDownList;
                outlineInput.AccessibleName = outlineLabel.Text;
                outlineInput.Items.Add(SR.T("Annotation.TextEditor.OutlineBlack"));
                outlineInput.Items.Add(SR.T("Annotation.TextEditor.OutlineWhite"));
                outlineInput.SelectedIndex = outlineColor == Color.White ? 1 : 0;
                outlineInput.SetBounds(105, 163, 140, 26);
                sizeLabel.Text = SR.T("Annotation.FontSize");
                sizeLabel.AutoSize = false;
                sizeLabel.TextAlign = ContentAlignment.MiddleLeft;
                sizeLabel.SetBounds(265, 163, 65, 26);
                sizeInput.Minimum = 12;
                sizeInput.Maximum = 72;
                sizeInput.Value = Math.Max(12, Math.Min(72, fontSize));
                sizeInput.AccessibleName = sizeLabel.Text;
                sizeInput.SetBounds(332, 163, 64, 26);
                backgroundInput.Text = SR.T("Annotation.TextEditor.Background");
                backgroundInput.Checked = background;
                backgroundInput.AccessibleName = backgroundInput.Text;
                backgroundInput.SetBounds(12, 196, 260, 26);
                ok.Text = SR.T("Annotation.TextEditor.Ok");
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(272, 215, 75, 25);
                cancel.Text = SR.T("Annotation.TextEditor.Cancel");
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(353, 215, 75, 25);
                form.Controls.AddRange(new Control[] { editor, fontLabel, fontInput, outlineLabel, outlineInput, sizeLabel, sizeInput, backgroundInput, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(editor.Text)) return false;
                text = editor.Text.TrimEnd();
                fontSize = (int)sizeInput.Value;
                fontFamily = fontInput.SelectedItem as string ?? "Segoe UI";
                outlineColor = outlineInput.SelectedIndex == 1 ? Color.White : Color.Black;
                background = backgroundInput.Checked;
                return true;
            }
        }

        private static void UpdateTextShapeBounds(AnnotationShape shape)
        {
            shape.TextNaturalSize = MeasureTextAnnotationSize(shape.Text, shape.FontSize, shape.FontFamily);
            shape.End = new Point(shape.Start.X + shape.TextNaturalSize.Width,
                shape.Start.Y + shape.TextNaturalSize.Height);
        }

        private static void PopulateAnnotationFontFamilies(ComboBox combo, string selectedFamily)
        {
            var names = new List<string>();
            using (var fonts = new InstalledFontCollection())
            {
                foreach (var family in fonts.Families)
                {
                    try { names.Add(family.Name); }
                    finally { family.Dispose(); }
                }
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            foreach (var name in names) combo.Items.Add(name);
            var selected = -1;
            for (var i = 0; i < combo.Items.Count; i++)
                if (string.Equals((string)combo.Items[i], selectedFamily, StringComparison.OrdinalIgnoreCase)) { selected = i; break; }
            if (selected < 0)
            {
                combo.Items.Add("Segoe UI");
                selected = combo.Items.Count - 1;
            }
            combo.SelectedIndex = selected;
        }

        private static Font CreateAnnotationFont(string familyName, float size)
        {
            try
            {
                using (var family = new FontFamily(string.IsNullOrWhiteSpace(familyName) ? "Segoe UI" : familyName))
                {
                    if (family.IsStyleAvailable(FontStyle.Regular))
                        return new Font(family, Math.Max(1f, size), FontStyle.Regular, GraphicsUnit.Pixel);
                }
            }
            catch (ArgumentException) { }
            return new Font(FontFamily.GenericSansSerif, Math.Max(1f, size), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static Size MeasureTextAnnotationSize(string text, int fontSize, string fontFamily)
        {
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            using (var font = CreateAnnotationFont(fontFamily, Math.Max(12, fontSize)))
            {
                var size = g.MeasureString(text ?? " ", font, int.MaxValue, StringFormat.GenericTypographic);
                return new Size(Math.Max(8, (int)Math.Ceiling(size.Width) + 8),
                    Math.Max(8, (int)Math.Ceiling(size.Height) + 8));
            }
        }

        private void ResizeTextAnnotationShape(AnnotationShape shape, Rectangle newBounds,
            Rectangle originBounds, int originFontSize)
        {
            var widthScale = newBounds.Width / (float)Math.Max(1, originBounds.Width);
            var heightScale = newBounds.Height / (float)Math.Max(1, originBounds.Height);
            shape.FontSize = Math.Max(12, Math.Min(72,
                (int)Math.Round(originFontSize * Math.Sqrt(Math.Max(0.01f, widthScale * heightScale)))));
            shape.TextNaturalSize = MeasureTextAnnotationSize(shape.Text, shape.FontSize, shape.FontFamily);
            shape.Start = newBounds.Location;
            shape.End = new Point(newBounds.Right, newBounds.Bottom);
            _annotationFontSize = shape.FontSize;
        }

        private static void PositionStepShape(AnnotationShape shape, Point center)
        {
            var diameter = Math.Max(22, shape.FontSize * 2);
            var radius = diameter / 2;
            shape.Start = new Point(center.X - radius, center.Y - radius);
            shape.End = new Point(shape.Start.X + diameter, shape.Start.Y + diameter);
        }

        private void DrawTextAnnotation(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            if (string.IsNullOrEmpty(shape.Text)) return;
            var rect = shape.Bounds;
            if (rect.Width < 1 || rect.Height < 1) return;
            var natural = shape.TextNaturalSize.IsEmpty
                ? MeasureTextAnnotationSize(shape.Text, shape.FontSize, shape.FontFamily) : shape.TextNaturalSize;
            using (var font = CreateAnnotationFont(shape.FontFamily, Math.Max(1f, shape.FontSize)))
            using (var path = new GraphicsPath())
            using (var fill = new SolidBrush(shape.StrokeColor))
            {
                path.AddString(shape.Text, font.FontFamily, (int)font.Style, font.Size,
                    new PointF(4, 4), StringFormat.GenericTypographic);
                float scaleX = rect.Width / (float)natural.Width;
                float scaleY = rect.Height / (float)natural.Height;
                float targetX = rect.Left, targetY = rect.Top;
                if (displayRect.HasValue && sourceSize.Width > 0 && sourceSize.Height > 0)
                {
                    var viewportScaleX = displayRect.Value.Width / (float)sourceSize.Width;
                    var viewportScaleY = displayRect.Value.Height / (float)sourceSize.Height;
                    scaleX *= viewportScaleX;
                    scaleY *= viewportScaleY;
                    targetX = displayRect.Value.Left + rect.Left * viewportScaleX;
                    targetY = displayRect.Value.Top + rect.Top * viewportScaleY;
                }
                using (var transform = new Matrix(scaleX, 0, 0, scaleY, targetX, targetY))
                    path.Transform(transform);
                if (displayRect.HasValue)
                    rect = ImageRectToClientRect(displayRect.Value, sourceSize, rect);
                if (shape.TextBackground)
                {
                    using (var background = new SolidBrush(Color.FromArgb(190, 25, 25, 25)))
                        g.FillRectangle(background, rect);
                }
                else
                {
                    using (var outline = new Pen(Color.FromArgb(230, shape.TextOutlineColor.IsEmpty ? Color.Black : shape.TextOutlineColor),
                        Math.Max(2f, shape.FontSize / 8f) * Math.Min(scaleX, scaleY)))
                    {
                        outline.LineJoin = LineJoin.Round;
                        g.DrawPath(outline, path);
                    }
                }
                g.FillPath(fill, path);
            }
        }

        private void DrawStepAnnotation(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            var rect = GetShapeRect(shape, displayRect, sourceSize);
            if (rect.Width < 1 || rect.Height < 1) return;
            var diameter = Math.Min(rect.Width, rect.Height);
            rect = new Rectangle(rect.Left + (rect.Width - diameter) / 2,
                rect.Top + (rect.Height - diameter) / 2, diameter, diameter);
            using (var brush = new SolidBrush(shape.StrokeColor))
            using (var border = new Pen(Color.White, Math.Max(1f, rect.Width / 18f)))
            using (var number = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", Math.Max(8f, rect.Height * 0.49f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillEllipse(brush, rect);
                g.DrawEllipse(border, rect);
                g.DrawString(shape.StepNumber.ToString(), font, number, rect, format);
            }
        }

        private void DrawMosaicAnnotation(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            var source = main_image ?? _originalImage as Bitmap;
            if (source == null) return;
            var bounds = Rectangle.Intersect(shape.Bounds, new Rectangle(Point.Empty, source.Size));
            if (bounds.Width < 1 || bounds.Height < 1) return;
            var pixels = GetMosaicPixels(shape, source, bounds);
            var oldCompositing = g.CompositingMode;
            var oldInterpolation = g.InterpolationMode;
            try
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                if (displayRect.HasValue)
                {
                    var target = ImageRectToClientRect(displayRect.Value, sourceSize, bounds);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.DrawImage(pixels, target);
                }
                else
                {
                    g.DrawImageUnscaled(pixels, bounds.Location);
                }
            }
            finally
            {
                g.InterpolationMode = oldInterpolation;
                g.CompositingMode = oldCompositing;
            }
        }

        private Bitmap GetMosaicPixels(AnnotationShape shape, Bitmap source, Rectangle bounds)
        {
            var blockSize = Math.Max(4, Math.Min(32, shape.MosaicBlockSize <= 0 ? 16 : shape.MosaicBlockSize));
            for (var i = _mosaicCache.Count - 1; i >= 0; i--)
            {
                var entry = _mosaicCache[i];
                if (ReferenceEquals(entry.Shape, shape) && ReferenceEquals(entry.Source, source)
                    && entry.Bounds == bounds && entry.BlockSize == blockSize)
                {
                    _mosaicCache.RemoveAt(i);
                    _mosaicCache.Add(entry);
                    return entry.Pixels;
                }
                if (ReferenceEquals(entry.Shape, shape) || !ReferenceEquals(entry.Source, source))
                {
                    entry.Pixels.Dispose();
                    _mosaicCache.RemoveAt(i);
                }
            }

            var bitmap = BuildMosaicPixels(source, bounds, blockSize);
            _mosaicCache.Add(new MosaicCacheEntry
            {
                Shape = shape, Source = source, Bounds = bounds, BlockSize = blockSize, Pixels = bitmap
            });
            long cacheBytes = 0;
            foreach (var entry in _mosaicCache)
                cacheBytes += (long)entry.Pixels.Width * entry.Pixels.Height * 4;
            while (_mosaicCache.Count > 1 &&
                (_mosaicCache.Count > MaxMosaicCacheEntries || cacheBytes > MaxMosaicCacheBytes))
            {
                cacheBytes -= (long)_mosaicCache[0].Pixels.Width * _mosaicCache[0].Pixels.Height * 4;
                _mosaicCache[0].Pixels.Dispose();
                _mosaicCache.RemoveAt(0);
            }
            return bitmap;
        }

        private static Bitmap BuildMosaicPixels(Bitmap source, Rectangle bounds, int blockSize)
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(source, new Rectangle(Point.Empty, bounds.Size), bounds, GraphicsUnit.Pixel);
            }

            var lockRect = new Rectangle(Point.Empty, bitmap.Size);
            var data = bitmap.LockBits(lockRect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var stride = Math.Abs(data.Stride);
                var bytes = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                for (var y = 0; y < bitmap.Height; y += blockSize)
                for (var x = 0; x < bitmap.Width; x += blockSize)
                {
                    var right = Math.Min(bitmap.Width, x + blockSize);
                    var bottom = Math.Min(bitmap.Height, y + blockSize);
                    long b = 0, green = 0, r = 0, a = 0;
                    for (var yy = y; yy < bottom; yy++)
                    for (var xx = x; xx < right; xx++)
                    {
                        var offset = yy * stride + xx * 4;
                        b += bytes[offset]; green += bytes[offset + 1];
                        r += bytes[offset + 2]; a += bytes[offset + 3];
                    }
                    var count = (right - x) * (bottom - y);
                    var blue = (byte)(b / count);
                    var gByte = (byte)(green / count);
                    var red = (byte)(r / count);
                    var alpha = (byte)(a / count);
                    for (var yy = y; yy < bottom; yy++)
                    for (var xx = x; xx < right; xx++)
                    {
                        var offset = yy * stride + xx * 4;
                        bytes[offset] = blue; bytes[offset + 1] = gByte;
                        bytes[offset + 2] = red; bytes[offset + 3] = alpha;
                    }
                }
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}

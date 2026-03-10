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

        private enum AnnotationArrowStyle
        {
            Single,
            Double,
            Line,
        }

        private enum AnnotationShapeKind
        {
            Rectangle,
            Arrow,
        }

        private enum AnnotationInteraction
        {
            None,
            Create,
            MoveRectangle,
            ResizeRectangle,
            MoveArrow,
            EditArrowStart,
            EditArrowEnd,
        }

        private enum AnnotationHandle
        {
            None,
            Move,
            TopLeft,
            Top,
            TopRight,
            Right,
            BottomRight,
            Bottom,
            BottomLeft,
            Left,
            ArrowStart,
            ArrowEnd,
        }

        private sealed class AnnotationShape
        {
            public AnnotationShapeKind Kind;
            public Point Start;
            public Point End;
            public Color StrokeColor;
            public int StrokeWidth;
            public Color FillColor;
            public AnnotationArrowStyle ArrowStyle;

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
        private AnnotationInteraction _annotationInteraction = AnnotationInteraction.None;
        private AnnotationHandle _annotationHandle = AnnotationHandle.None;
        private int _selectedAnnotationIndex = -1;
        private int _hoverAnnotationIndex = -1;
        private Point _annotationDragOriginImage;
        private Rectangle _annotationEditOriginBounds = Rectangle.Empty;
        private Point _annotationEditOriginStart;
        private Point _annotationEditOriginEnd;
        private ToolStripMenuItem _annotationModeMenuItem;
        private ToolStripMenuItem _annotationClearMenuItem;
        private ToolStripMenuItem _annotationUndoMenuItem;
        private Panel _annotationPalette;
        private Label _annotationPaletteLabel;
        private Button _annotationRectButton;
        private Button _annotationArrowButton;
        private Button _annotationColorOrangeButton;
        private Button _annotationColorBlueButton;
        private Button _annotationColorGreenButton;
        private Button _annotationColorPinkButton;
        private Button _annotationArrowSingleButton;
        private Button _annotationArrowDoubleButton;
        private Button _annotationArrowLineButton;
        private Button _annotationUndoButton;
        private Button _annotationClearButton;
        private Button _annotationDoneButton;
        private Color _annotationStrokeColor = Color.FromArgb(255, 255, 138, 61);
        private Color _annotationFillColor = Color.FromArgb(48, 255, 138, 61);
        private AnnotationArrowStyle _annotationArrowStyle = AnnotationArrowStyle.Single;

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
            Resize += (s, e) => RepositionAnnotationPalette();
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
                Size = new Size(404, 88),
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
            _annotationUndoButton = CreatePaletteButton("Undo", 172, 52, (s, e) => UndoLastAnnotation());
            _annotationClearButton = CreatePaletteButton("Clear", 228, 52, (s, e) => ClearAnnotations());
            _annotationDoneButton = CreatePaletteButton("Done", 284, 56, (s, e) => ExitAnnotationMode());
            _annotationColorOrangeButton = CreateColorButton(56, 50, Color.FromArgb(255, 138, 61), Color.FromArgb(48, 255, 138, 61));
            _annotationColorBlueButton = CreateColorButton(84, 50, Color.FromArgb(88, 166, 255), Color.FromArgb(48, 88, 166, 255));
            _annotationColorGreenButton = CreateColorButton(112, 50, Color.FromArgb(78, 201, 140), Color.FromArgb(48, 78, 201, 140));
            _annotationColorPinkButton = CreateColorButton(140, 50, Color.FromArgb(255, 105, 180), Color.FromArgb(48, 255, 105, 180));
            _annotationArrowSingleButton = CreatePaletteButton("->", 192, 40, (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Single));
            _annotationArrowSingleButton.Location = new Point(192, 48);
            _annotationArrowDoubleButton = CreatePaletteButton("<->", 240, 48, (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Double));
            _annotationArrowDoubleButton.Location = new Point(240, 48);
            _annotationArrowLineButton = CreatePaletteButton("Line", 296, 48, (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Line));
            _annotationArrowLineButton.Location = new Point(296, 48);

            _annotationPalette.Controls.Add(_annotationPaletteLabel);
            _annotationPalette.Controls.Add(_annotationRectButton);
            _annotationPalette.Controls.Add(_annotationArrowButton);
            _annotationPalette.Controls.Add(_annotationUndoButton);
            _annotationPalette.Controls.Add(_annotationClearButton);
            _annotationPalette.Controls.Add(_annotationDoneButton);
            _annotationPalette.Controls.Add(_annotationColorOrangeButton);
            _annotationPalette.Controls.Add(_annotationColorBlueButton);
            _annotationPalette.Controls.Add(_annotationColorGreenButton);
            _annotationPalette.Controls.Add(_annotationColorPinkButton);
            _annotationPalette.Controls.Add(_annotationArrowSingleButton);
            _annotationPalette.Controls.Add(_annotationArrowDoubleButton);
            _annotationPalette.Controls.Add(_annotationArrowLineButton);

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

        private Button CreateColorButton(int x, int y, Color strokeColor, Color fillColor)
        {
            var button = new Button
            {
                Size = new Size(24, 24),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = strokeColor,
                ForeColor = Color.White,
                TabStop = false,
                Text = string.Empty,
                Tag = Tuple.Create(strokeColor, fillColor)
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(90, 255, 255, 255);
            button.Click += (s, e) =>
            {
                var pair = (Tuple<Color, Color>)button.Tag;
                SetAnnotationColor(pair.Item1, pair.Item2);
            };
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
            _selectedAnnotationIndex = -1;
            _annotationPreview = null;
            _annotationDragging = false;
            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
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
            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
            DetachStandardMouseHandlers();
            if (_annotationPalette != null) _annotationPalette.Visible = true;
            RepositionAnnotationPalette();
            UpdateAnnotationMenuState();
            Cursor = Cursors.Cross;
            ShowOverlay("EDIT MODE");
            pictureBox1?.Invalidate();
        }

        private void ExitAnnotationMode(bool silent = false)
        {
            _annotationMode = false;
            _annotationDragging = false;
            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
            _selectedAnnotationIndex = -1;
            if (pictureBox1 != null) pictureBox1.Capture = false;
            _annotationPreview = null;
            RestoreStandardMouseHandlers();
            if (_annotationPalette != null) _annotationPalette.Visible = false;
            UpdateAnnotationMenuState();
            Cursor = Cursors.Default;
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

        private void SetAnnotationColor(Color strokeColor, Color fillColor)
        {
            if (TryGetSelectedShape(out var selected))
            {
                selected.StrokeColor = strokeColor;
                selected.FillColor = fillColor;
            }
            else
            {
                _annotationStrokeColor = strokeColor;
                _annotationFillColor = fillColor;
            }

            if (_annotationPreview != null)
            {
                _annotationPreview.StrokeColor = strokeColor;
                _annotationPreview.FillColor = fillColor;
            }

            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
        }

        private void SetAnnotationArrowStyle(AnnotationArrowStyle style)
        {
            if (TryGetSelectedShape(out var selected) && selected.Kind == AnnotationShapeKind.Arrow)
                selected.ArrowStyle = style;
            else
                _annotationArrowStyle = style;

            if (_annotationPreview != null && _annotationPreview.Kind == AnnotationShapeKind.Arrow)
                _annotationPreview.ArrowStyle = style;

            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
        }

        private void UndoLastAnnotation()
        {
            if (_annotations.Count == 0) return;
            _annotations.RemoveAt(_annotations.Count - 1);
            if (_selectedAnnotationIndex >= _annotations.Count) _selectedAnnotationIndex = _annotations.Count - 1;
            UpdateAnnotationMenuState();
            ShowOverlay("ANNOTATION UNDONE");
            pictureBox1?.Invalidate();
        }

        private void ClearAnnotations()
        {
            if (_annotations.Count == 0) return;
            _annotations.Clear();
            _selectedAnnotationIndex = -1;
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

        private void SyncAnnotationDefaultsFromShape(AnnotationShape shape)
        {
            if (shape == null) return;
            _annotationStrokeColor = shape.StrokeColor;
            _annotationFillColor = shape.FillColor;
            if (shape.Kind == AnnotationShapeKind.Arrow)
                _annotationArrowStyle = shape.ArrowStyle;
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
            UpdatePaletteButtonState(_annotationArrowSingleButton, _annotationArrowStyle == AnnotationArrowStyle.Single);
            UpdatePaletteButtonState(_annotationArrowDoubleButton, _annotationArrowStyle == AnnotationArrowStyle.Double);
            UpdatePaletteButtonState(_annotationArrowLineButton, _annotationArrowStyle == AnnotationArrowStyle.Line);
            UpdateColorButtonState(_annotationColorOrangeButton, _annotationStrokeColor == Color.FromArgb(255, 255, 138, 61));
            UpdateColorButtonState(_annotationColorBlueButton, _annotationStrokeColor == Color.FromArgb(88, 166, 255));
            UpdateColorButtonState(_annotationColorGreenButton, _annotationStrokeColor == Color.FromArgb(78, 201, 140));
            UpdateColorButtonState(_annotationColorPinkButton, _annotationStrokeColor == Color.FromArgb(255, 105, 180));
            if (_annotationUndoButton != null) _annotationUndoButton.Enabled = hasAnnotations;
            if (_annotationClearButton != null) _annotationClearButton.Enabled = hasAnnotations;
        }

        private void UpdatePaletteButtonState(Button button, bool selected)
        {
            if (button == null) return;
            button.BackColor = selected ? Color.FromArgb(255, 138, 61) : Color.FromArgb(44, 49, 57);
            button.ForeColor = Color.White;
        }

        private void UpdateColorButtonState(Button button, bool selected)
        {
            if (button == null) return;
            button.FlatAppearance.BorderColor = selected ? Color.White : Color.FromArgb(90, 255, 255, 255);
        }

        private void PictureBox1_MouseDownAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode || e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

            int hitIndex;
            AnnotationHandle hitHandle;
            if (TryHitAnnotation(imagePoint, out hitIndex, out hitHandle))
            {
                _selectedAnnotationIndex = hitIndex;
                SyncAnnotationDefaultsFromShape(_annotations[hitIndex]);
                _annotationDragging = true;
                _annotationDragOriginImage = imagePoint;
                _annotationEditOriginBounds = _annotations[hitIndex].Bounds;
                _annotationEditOriginStart = _annotations[hitIndex].Start;
                _annotationEditOriginEnd = _annotations[hitIndex].End;
                _annotationHandle = hitHandle;
                _annotationInteraction = GetInteractionForHit(_annotations[hitIndex], hitHandle);
                pictureBox1.Capture = true;
                UpdateAnnotationCursor(hitHandle);
                pictureBox1.Invalidate();
                return;
            }

            _selectedAnnotationIndex = -1;
            _annotationDragging = true;
            _annotationInteraction = AnnotationInteraction.Create;
            _annotationHandle = AnnotationHandle.None;
            _annotationStartImage = imagePoint;
            _annotationPreview = CreateAnnotationShape(_annotationTool, imagePoint, imagePoint);
            pictureBox1.Capture = true;
            pictureBox1.Invalidate();
        }

        private void PictureBox1_MouseMoveAnnotations(object sender, MouseEventArgs e)
        {
            Point imagePoint;
            if (_annotationDragging)
            {
                if (!TryClientToImagePoint(e.Location, out imagePoint)) return;

                switch (_annotationInteraction)
                {
                    case AnnotationInteraction.Create:
                        if (_annotationPreview != null)
                            _annotationPreview.End = imagePoint;
                        break;
                    case AnnotationInteraction.MoveRectangle:
                        MoveSelectedRectangle(imagePoint);
                        break;
                    case AnnotationInteraction.ResizeRectangle:
                        ResizeSelectedRectangle(imagePoint);
                        break;
                    case AnnotationInteraction.MoveArrow:
                        MoveSelectedArrow(imagePoint);
                        break;
                    case AnnotationInteraction.EditArrowStart:
                        EditSelectedArrowEndpoint(imagePoint, true);
                        break;
                    case AnnotationInteraction.EditArrowEnd:
                        EditSelectedArrowEndpoint(imagePoint, false);
                        break;
                }

                pictureBox1.Invalidate();
                return;
            }

            if (!TryClientToImagePoint(e.Location, out imagePoint))
            {
                if (_hoverAnnotationIndex != -1)
                {
                    _hoverAnnotationIndex = -1;
                    pictureBox1.Invalidate();
                }
                UpdateAnnotationCursor(AnnotationHandle.None);
                return;
            }

            int hitIndex;
            AnnotationHandle hitHandle;
            if (TryHitAnnotation(imagePoint, out hitIndex, out hitHandle))
            {
                if (_hoverAnnotationIndex != hitIndex)
                {
                    _hoverAnnotationIndex = hitIndex;
                    pictureBox1.Invalidate();
                }
                UpdateAnnotationCursor(hitHandle);
                return;
            }

            if (_hoverAnnotationIndex != -1)
            {
                _hoverAnnotationIndex = -1;
                pictureBox1.Invalidate();
            }
            UpdateAnnotationCursor(AnnotationHandle.None);
        }

        private void PictureBox1_MouseUpAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationDragging || e.Button != MouseButtons.Left) return;

            _annotationDragging = false;
            pictureBox1.Capture = false;

            Point imagePoint;
            TryClientToImagePoint(e.Location, out imagePoint);

            if (_annotationInteraction == AnnotationInteraction.Create)
            {
                if (_annotationPreview != null && IsAnnotationShapeUsable(_annotationPreview))
                {
                    _annotations.Add(_annotationPreview);
                    _selectedAnnotationIndex = _annotations.Count - 1;
                    SyncAnnotationDefaultsFromShape(_annotationPreview);
                    UpdateAnnotationMenuState();
                    ShowOverlay(_annotationTool == AnnotationTool.Arrow ? "ARROW ADDED" : "RECT ADDED");
                    Log.Info("Annotation added: " + _annotationPreview.Kind, "SnapWindow");
                }

                _annotationPreview = null;
            }
            else if (_selectedAnnotationIndex >= 0 && _selectedAnnotationIndex < _annotations.Count)
            {
                if (!IsAnnotationShapeUsable(_annotations[_selectedAnnotationIndex]))
                {
                    _annotations.RemoveAt(_selectedAnnotationIndex);
                    _selectedAnnotationIndex = -1;
                }
            }

            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
            UpdateAnnotationCursor(AnnotationHandle.None);
            pictureBox1.Invalidate();
        }

        private void MoveSelectedRectangle(Point imagePoint)
        {
            if (!TryGetSelectedRectangle(out var shape)) return;
            var dx = imagePoint.X - _annotationDragOriginImage.X;
            var dy = imagePoint.Y - _annotationDragOriginImage.Y;
            var moved = new Rectangle(_annotationEditOriginBounds.X + dx, _annotationEditOriginBounds.Y + dy, _annotationEditOriginBounds.Width, _annotationEditOriginBounds.Height);
            shape.Start = moved.Location;
            shape.End = new Point(moved.Right, moved.Bottom);
        }

        private void ResizeSelectedRectangle(Point imagePoint)
        {
            if (!TryGetSelectedRectangle(out var shape)) return;
            var rect = _annotationEditOriginBounds;
            var left = rect.Left;
            var top = rect.Top;
            var right = rect.Right;
            var bottom = rect.Bottom;

            switch (_annotationHandle)
            {
                case AnnotationHandle.TopLeft:
                    left = imagePoint.X;
                    top = imagePoint.Y;
                    break;
                case AnnotationHandle.Top:
                    top = imagePoint.Y;
                    break;
                case AnnotationHandle.TopRight:
                    right = imagePoint.X;
                    top = imagePoint.Y;
                    break;
                case AnnotationHandle.Right:
                    right = imagePoint.X;
                    break;
                case AnnotationHandle.BottomRight:
                    right = imagePoint.X;
                    bottom = imagePoint.Y;
                    break;
                case AnnotationHandle.Bottom:
                    bottom = imagePoint.Y;
                    break;
                case AnnotationHandle.BottomLeft:
                    left = imagePoint.X;
                    bottom = imagePoint.Y;
                    break;
                case AnnotationHandle.Left:
                    left = imagePoint.X;
                    break;
            }

            var resized = Rectangle.FromLTRB(Math.Min(left, right), Math.Min(top, bottom), Math.Max(left, right), Math.Max(top, bottom));
            shape.Start = resized.Location;
            shape.End = new Point(resized.Right, resized.Bottom);
        }

        private AnnotationInteraction GetInteractionForHit(AnnotationShape shape, AnnotationHandle handle)
        {
            if (shape.Kind == AnnotationShapeKind.Arrow)
            {
                if (handle == AnnotationHandle.ArrowStart) return AnnotationInteraction.EditArrowStart;
                if (handle == AnnotationHandle.ArrowEnd) return AnnotationInteraction.EditArrowEnd;
                return AnnotationInteraction.MoveArrow;
            }

            return handle == AnnotationHandle.Move ? AnnotationInteraction.MoveRectangle : AnnotationInteraction.ResizeRectangle;
        }

        private void MoveSelectedArrow(Point imagePoint)
        {
            if (!TryGetSelectedShape(out var shape) || shape.Kind != AnnotationShapeKind.Arrow) return;
            var dx = imagePoint.X - _annotationDragOriginImage.X;
            var dy = imagePoint.Y - _annotationDragOriginImage.Y;
            shape.Start = new Point(_annotationEditOriginStart.X + dx, _annotationEditOriginStart.Y + dy);
            shape.End = new Point(_annotationEditOriginEnd.X + dx, _annotationEditOriginEnd.Y + dy);
        }

        private void EditSelectedArrowEndpoint(Point imagePoint, bool editStart)
        {
            if (!TryGetSelectedShape(out var shape) || shape.Kind != AnnotationShapeKind.Arrow) return;
            if (editStart)
                shape.Start = imagePoint;
            else
                shape.End = imagePoint;
        }

        private bool TryGetSelectedRectangle(out AnnotationShape shape)
        {
            shape = null;
            if (!TryGetSelectedShape(out shape)) return false;
            return shape.Kind == AnnotationShapeKind.Rectangle;
        }

        private bool TryGetSelectedShape(out AnnotationShape shape)
        {
            shape = null;
            if (_selectedAnnotationIndex < 0 || _selectedAnnotationIndex >= _annotations.Count) return false;
            shape = _annotations[_selectedAnnotationIndex];
            return true;
        }

        private bool TryHitAnnotation(Point imagePoint, out int index, out AnnotationHandle handle)
        {
            for (int i = _annotations.Count - 1; i >= 0; i--)
            {
                var shape = _annotations[i];
                handle = shape.Kind == AnnotationShapeKind.Rectangle
                    ? HitTestRectangleHandle(shape.Bounds, imagePoint)
                    : HitTestArrowHandle(shape, imagePoint);
                if (handle != AnnotationHandle.None)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            handle = AnnotationHandle.None;
            return false;
        }

        private AnnotationHandle HitTestArrowHandle(AnnotationShape shape, Point imagePoint)
        {
            const int radius = 10;
            var startRect = new Rectangle(shape.Start.X - radius, shape.Start.Y - radius, radius * 2, radius * 2);
            if (startRect.Contains(imagePoint)) return AnnotationHandle.ArrowStart;

            var endRect = new Rectangle(shape.End.X - radius, shape.End.Y - radius, radius * 2, radius * 2);
            if (endRect.Contains(imagePoint)) return AnnotationHandle.ArrowEnd;

            return DistancePointToSegmentSquared(imagePoint, shape.Start, shape.End) <= 100 ? AnnotationHandle.Move : AnnotationHandle.None;
        }

        private static int DistancePointToSegmentSquared(Point p, Point a, Point b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            if (dx == 0 && dy == 0) return DistanceSquared(p, a);

            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (double)((dx * dx) + (dy * dy));
            t = Math.Max(0d, Math.Min(1d, t));
            var projX = a.X + (t * dx);
            var projY = a.Y + (t * dy);
            var px = p.X - projX;
            var py = p.Y - projY;
            return (int)Math.Round((px * px) + (py * py));
        }

        private AnnotationHandle HitTestRectangleHandle(Rectangle rect, Point imagePoint)
        {
            const int radius = 10;
            var handles = GetRectangleHandleRects(rect, radius);
            foreach (var pair in handles)
            {
                if (pair.Value.Contains(imagePoint)) return pair.Key;
            }

            return rect.Contains(imagePoint) ? AnnotationHandle.Move : AnnotationHandle.None;
        }

        private Dictionary<AnnotationHandle, Rectangle> GetRectangleHandleRects(Rectangle rect, int radius)
        {
            var size = radius * 2;
            var centerX = rect.Left + rect.Width / 2;
            var centerY = rect.Top + rect.Height / 2;
            return new Dictionary<AnnotationHandle, Rectangle>
            {
                { AnnotationHandle.TopLeft, new Rectangle(rect.Left - radius, rect.Top - radius, size, size) },
                { AnnotationHandle.Top, new Rectangle(centerX - radius, rect.Top - radius, size, size) },
                { AnnotationHandle.TopRight, new Rectangle(rect.Right - radius, rect.Top - radius, size, size) },
                { AnnotationHandle.Right, new Rectangle(rect.Right - radius, centerY - radius, size, size) },
                { AnnotationHandle.BottomRight, new Rectangle(rect.Right - radius, rect.Bottom - radius, size, size) },
                { AnnotationHandle.Bottom, new Rectangle(centerX - radius, rect.Bottom - radius, size, size) },
                { AnnotationHandle.BottomLeft, new Rectangle(rect.Left - radius, rect.Bottom - radius, size, size) },
                { AnnotationHandle.Left, new Rectangle(rect.Left - radius, centerY - radius, size, size) },
            };
        }

        private void UpdateAnnotationCursor(AnnotationHandle handle)
        {
            if (!_annotationMode)
            {
                Cursor = Cursors.Default;
                return;
            }

            switch (handle)
            {
                case AnnotationHandle.TopLeft:
                case AnnotationHandle.BottomRight:
                    Cursor = Cursors.SizeNWSE;
                    break;
                case AnnotationHandle.TopRight:
                case AnnotationHandle.BottomLeft:
                    Cursor = Cursors.SizeNESW;
                    break;
                case AnnotationHandle.Top:
                case AnnotationHandle.Bottom:
                    Cursor = Cursors.SizeNS;
                    break;
                case AnnotationHandle.Left:
                case AnnotationHandle.Right:
                    Cursor = Cursors.SizeWE;
                    break;
                case AnnotationHandle.Move:
                    Cursor = Cursors.SizeAll;
                    break;
                case AnnotationHandle.ArrowStart:
                case AnnotationHandle.ArrowEnd:
                    Cursor = Cursors.Hand;
                    break;
                default:
                    Cursor = Cursors.Cross;
                    break;
            }
        }

        private void PictureBox1_PaintAnnotations(object sender, PaintEventArgs e)
        {
            Rectangle displayRect;
            Bitmap source;
            if (!TryGetDisplayImageRect(out displayRect, out source)) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RenderAnnotationsToClient(e.Graphics, displayRect, source.Size, includePreview: true);
            DrawHoverOverlay(e.Graphics, displayRect, source.Size);
            DrawSelectionOverlay(e.Graphics, displayRect, source.Size);
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

        private void DrawHoverOverlay(Graphics g, Rectangle displayRect, Size sourceSize)
        {
            if (!_annotationMode) return;
            if (_annotationDragging) return;
            if (_hoverAnnotationIndex < 0 || _hoverAnnotationIndex >= _annotations.Count) return;
            if (_hoverAnnotationIndex == _selectedAnnotationIndex) return;

            var shape = _annotations[_hoverAnnotationIndex];
            using (var pen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f))
            {
                pen.DashStyle = DashStyle.Dot;

                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    var rect = ImageRectToClientRect(displayRect, sourceSize, shape.Bounds);
                    if (rect.Width <= 0 || rect.Height <= 0) return;
                    g.DrawRectangle(pen, rect);
                    return;
                }

                var start = ImagePointToClientPoint(displayRect, sourceSize, shape.Start);
                var end = ImagePointToClientPoint(displayRect, sourceSize, shape.End);
                g.DrawLine(pen, start, end);
            }
        }

        private void DrawSelectionOverlay(Graphics g, Rectangle displayRect, Size sourceSize)
        {
            if (!_annotationMode) return;
            if (_selectedAnnotationIndex < 0 || _selectedAnnotationIndex >= _annotations.Count) return;
            var shape = _annotations[_selectedAnnotationIndex];

            using (var dashPen = new Pen(Color.FromArgb(255, 255, 255, 255), 1f))
            using (var handleBrush = new SolidBrush(Color.White))
            using (var handlePen = new Pen(Color.FromArgb(255, 255, 138, 61), 1f))
            {
                dashPen.DashStyle = DashStyle.Dash;

                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    var rect = ImageRectToClientRect(displayRect, sourceSize, shape.Bounds);
                    if (rect.Width <= 0 || rect.Height <= 0) return;
                    g.DrawRectangle(dashPen, rect);

                    foreach (var pair in GetRectangleHandleRects(rect, 4))
                    {
                        g.FillRectangle(handleBrush, pair.Value);
                        g.DrawRectangle(handlePen, pair.Value);
                    }
                    return;
                }

                var start = ImagePointToClientPoint(displayRect, sourceSize, shape.Start);
                var end = ImagePointToClientPoint(displayRect, sourceSize, shape.End);
                g.DrawLine(dashPen, start, end);
                DrawArrowHandle(g, handleBrush, handlePen, start);
                DrawArrowHandle(g, handleBrush, handlePen, end);
            }
        }

        private void DrawArrowHandle(Graphics g, Brush fill, Pen border, Point point)
        {
            var rect = new Rectangle(point.X - 4, point.Y - 4, 8, 8);
            g.FillEllipse(fill, rect);
            g.DrawEllipse(border, rect);
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
                EndCap = LineCap.Round
            };

            if (shape.Kind == AnnotationShapeKind.Arrow)
            {
                switch (shape.ArrowStyle)
                {
                    case AnnotationArrowStyle.Single:
                        pen.CustomEndCap = new AdjustableArrowCap(4, 6, true);
                        break;
                    case AnnotationArrowStyle.Double:
                        pen.CustomStartCap = new AdjustableArrowCap(4, 6, true);
                        pen.CustomEndCap = new AdjustableArrowCap(4, 6, true);
                        break;
                    case AnnotationArrowStyle.Line:
                        break;
                }
            }

            return pen;
        }

        private AnnotationShape CreateAnnotationShape(AnnotationTool tool, Point start, Point end)
        {
            return new AnnotationShape
            {
                Kind = tool == AnnotationTool.Arrow ? AnnotationShapeKind.Arrow : AnnotationShapeKind.Rectangle,
                Start = start,
                End = end,
                StrokeColor = _annotationStrokeColor,
                FillColor = _annotationFillColor,
                ArrowStyle = _annotationArrowStyle,
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



using Kiritori.Helpers;
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
        private const int AnnotationArrowHandleRadius = 10;
        private const int AnnotationRectangleHandleRadius = 10;
        private static readonly Point AnnotationPaletteDefaultLocation = new Point(14, 14);
        private static readonly Size AnnotationPaletteSize = new Size(504, 44);
        private static readonly Padding AnnotationPalettePadding = new Padding(8);
        private static readonly Size AnnotationPaletteDragHandleSize = new Size(28, 28);
        private static readonly Point AnnotationPaletteDragHandleLocation = new Point(10, 8);
        private static readonly Size AnnotationPaletteButtonSize = new Size(34, 28);
        private const int AnnotationPaletteButtonY = 8;
        private const int AnnotationPaletteClearButtonX = 44;
        private const int AnnotationPaletteToolButtonX = 84;
        private const int AnnotationPaletteToolButtonWidth = 96;
        private const int AnnotationPaletteColorButtonX = 186;
        private const int AnnotationPaletteColorButtonWidth = 92;
        private const int AnnotationPaletteStyleButtonX = 284;
        private const int AnnotationPaletteStyleButtonWidth = 104;
        private const int AnnotationPaletteUndoButtonX = 394;
        private const int AnnotationPaletteDoneButtonX = 434;

        private enum AnnotationTool
        {
            Move,
            Rectangle,
            Arrow,
        }

        private enum AnnotationArrowStyle
        {
            Single,
            Double,
            Line,
            Tapered,
        }
        private enum AnnotationRectangleStyle
        {
            Filled,
            Outline,
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
        private Button _annotationToolButton;
        private Button _annotationColorButton;
        private Button _annotationStyleButton;
        private Button _annotationUndoButton;
        private Button _annotationClearButton;
        private Button _annotationDoneButton;
        private ContextMenuStrip _annotationToolMenu;
        private ContextMenuStrip _annotationColorMenu;
        private ContextMenuStrip _annotationStyleMenu;
        private bool _annotationPaletteDragging;
        private Point _annotationPaletteDragOrigin;
        private Point _annotationPaletteOrigin;
        private Point _annotationPaletteLocation = AnnotationPaletteDefaultLocation;
        private Color _annotationStrokeColor = Color.FromArgb(255, 255, 138, 61);
        private Color _annotationFillColor = Color.FromArgb(48, 255, 138, 61);
        private AnnotationArrowStyle _annotationArrowStyle = AnnotationArrowStyle.Single;
        private AnnotationRectangleStyle _annotationRectangleStyle = AnnotationRectangleStyle.Filled;

        private void ResetAnnotations()
        {
            _annotations.Clear();
            _selectedAnnotationIndex = -1;
            ResetTransientAnnotationState();
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
            ResetTransientAnnotationState();
            DetachStandardMouseHandlers();
            SetAnnotationPaletteVisible(true);
            RepositionAnnotationPalette();
            UpdateAnnotationMenuState();
            UpdateAnnotationCursor();
            ShowOverlay("EDIT MODE");
            pictureBox1?.Invalidate();
        }

        private void ExitAnnotationMode(bool silent = false)
        {
            _annotationMode = false;
            ResetTransientAnnotationState();
            _selectedAnnotationIndex = -1;
            if (pictureBox1 != null) pictureBox1.Capture = false;
            ResetStandardDragState();
            RestoreStandardMouseHandlers();
            SetAnnotationPaletteVisible(false);
            UpdateAnnotationMenuState();
            UpdateAnnotationCursor();
            if (!silent) ShowOverlay("EDIT OFF");
            pictureBox1?.Invalidate();
        }

        private void ResetStandardDragState()
        {
            _isDragging = false;
            _isResizing = false;
            _imgAspect = null;
            this.Opacity = this.WindowOpacityPercent;
        }

        private void SetAnnotationTool(AnnotationTool tool)
        {
            _annotationTool = tool;
            _annotationPreview = null;
            UpdateAnnotationCursor();
            RefreshAnnotationUi();
        }

        private void ResetTransientAnnotationState()
        {
            _annotationDragging = false;
            _annotationPreview = null;
            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
        }

        private void SetAnnotationPaletteVisible(bool visible)
        {
            if (_annotationPalette != null)
                _annotationPalette.Visible = visible;
        }

        private void UpdateAnnotationCursor()
        {
            if (!_annotationMode)
            {
                Cursor = Cursors.Default;
                return;
            }

            Cursor = _annotationTool == AnnotationTool.Move ? Cursors.SizeAll : Cursors.Cross;
        }

        private void SetAnnotationColor(Color strokeColor, Color fillColor)
        {
            ApplyToSelectedShapeOrDefaults(
                selected =>
                {
                    selected.StrokeColor = strokeColor;
                    selected.FillColor = ResolveShapeFillColor(selected, fillColor);
                },
                () =>
                {
                    _annotationStrokeColor = strokeColor;
                    _annotationFillColor = ResolveDefaultFillColor(fillColor);
                });

            ApplyToPreviewShape(preview =>
            {
                preview.StrokeColor = strokeColor;
                preview.FillColor = ResolveShapeFillColor(preview, fillColor);
            });

            RefreshAnnotationUi();
        }

        private void SetAnnotationArrowStyle(AnnotationArrowStyle style)
        {
            ApplyToSelectedShapeOrDefaults(
                selected =>
                {
                    if (selected.Kind == AnnotationShapeKind.Arrow)
                        selected.ArrowStyle = style;
                },
                () => _annotationArrowStyle = style);

            ApplyToPreviewShape(preview =>
            {
                if (preview.Kind == AnnotationShapeKind.Arrow)
                    preview.ArrowStyle = style;
            });

            RefreshAnnotationUi();
        }

        private void SetAnnotationRectangleStyle(AnnotationRectangleStyle style)
        {
            ApplyToSelectedShapeOrDefaults(
                selected =>
                {
                    if (selected.Kind != AnnotationShapeKind.Rectangle) return;
                    selected.RectangleStyle = style;
                    selected.FillColor = ResolveRectangleFillColor(style, selected.StrokeColor);
                },
                () =>
                {
                    _annotationRectangleStyle = style;
                    _annotationFillColor = ResolveRectangleFillColor(style, _annotationStrokeColor);
                });

            ApplyToPreviewShape(preview =>
            {
                if (preview.Kind != AnnotationShapeKind.Rectangle) return;
                preview.RectangleStyle = style;
                preview.FillColor = ResolveRectangleFillColor(style, preview.StrokeColor);
            });

            RefreshAnnotationUi();
        }
        private void UndoLastAnnotation()
        {
            if (_annotations.Count == 0) return;
            _annotations.RemoveAt(_annotations.Count - 1);
            _selectedAnnotationIndex = AnnotationSelectionState.NormalizeIndex(_selectedAnnotationIndex, _annotations.Count);
            RefreshAnnotationUi();
            ShowOverlay("ANNOTATION UNDONE");
        }

        private void ClearAnnotations()
        {
            if (_annotations.Count == 0) return;
            _annotations.Clear();
            _selectedAnnotationIndex = -1;
            _annotationPreview = null;
            RefreshAnnotationUi();
            ShowOverlay("ANNOTATIONS CLEARED");
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
            else
                _annotationRectangleStyle = shape.RectangleStyle;
        }

        private void ApplyToSelectedShapeOrDefaults(Action<AnnotationShape> applyToSelected, Action applyToDefaults)
        {
            if (TryGetSelectedShape(out var selected))
            {
                applyToSelected(selected);
                return;
            }

            applyToDefaults();
        }

        private void ApplyToPreviewShape(Action<AnnotationShape> applyToPreview)
        {
            if (_annotationPreview == null) return;
            applyToPreview(_annotationPreview);
        }

        private Color ResolveShapeFillColor(AnnotationShape shape, Color requestedFillColor)
        {
            return shape.Kind == AnnotationShapeKind.Rectangle && shape.RectangleStyle == AnnotationRectangleStyle.Outline
                ? Color.Transparent
                : requestedFillColor;
        }

        private Color ResolveDefaultFillColor(Color requestedFillColor)
        {
            return _annotationRectangleStyle == AnnotationRectangleStyle.Outline
                ? Color.Transparent
                : requestedFillColor;
        }

        private Color ResolveRectangleFillColor(AnnotationRectangleStyle style, Color strokeColor)
        {
            return style == AnnotationRectangleStyle.Outline
                ? Color.Transparent
                : CreateFillColorFromStroke(strokeColor);
        }

        private void RefreshAnnotationUi()
        {
            UpdateAnnotationMenuState();
            pictureBox1?.Invalidate();
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

            UpdatePaletteButtonState(_annotationToolButton, true, GetToolButtonLabel(), GetAnnotationPaletteIcon(GetToolIconKey()));
            UpdatePaletteButtonState(_annotationColorButton, true, GetColorButtonLabel(_annotationStrokeColor), CreateAnnotationColorSwatch(_annotationStrokeColor));
            UpdatePaletteButtonState(_annotationStyleButton, true, GetStyleButtonLabel(), GetAnnotationPaletteIcon(GetStyleIconKey()));
            UpdatePaletteButtonState(_annotationClearButton, false, string.Empty, GetAnnotationPaletteIcon("annotation_clear"));
            UpdatePaletteButtonState(_annotationUndoButton, false, string.Empty, GetAnnotationPaletteIcon("annotation_undo"));
            UpdatePaletteButtonState(_annotationDoneButton, false, string.Empty, GetAnnotationPaletteIcon("annotation_done"));
            if (_annotationUndoButton != null) _annotationUndoButton.Enabled = hasAnnotations;
            if (_annotationClearButton != null) _annotationClearButton.Enabled = hasAnnotations;
        }

        private void UpdatePaletteButtonState(Button button, bool emphasize, string text, Image icon)
        {
            if (button == null) return;

            var oldImage = button.Image;
            button.Text = text;
            button.BackColor = emphasize ? Color.FromArgb(56, 62, 72) : Color.FromArgb(44, 49, 57);
            button.ForeColor = Color.White;
            button.Image = icon;

            if (oldImage != null && !ReferenceEquals(oldImage, icon))
                oldImage.Dispose();
        }

        private string GetToolButtonLabel()
        {
            switch (_annotationTool)
            {
                case AnnotationTool.Move:
                    return GetToolLabel(AnnotationTool.Move);
                case AnnotationTool.Arrow:
                    return GetToolLabel(AnnotationTool.Arrow);
                default:
                    return GetToolLabel(AnnotationTool.Rectangle);
            }
        }

        private string GetColorButtonLabel(Color color)
        {
            return GetColorLabel(color);
        }

        private string GetStyleButtonLabel()
        {
            if (_annotationTool == AnnotationTool.Move)
                return "Window";
            if (_annotationTool == AnnotationTool.Rectangle)
                return GetRectangleStyleLabel(_annotationRectangleStyle);

            switch (_annotationArrowStyle)
            {
                case AnnotationArrowStyle.Double:
                    return GetArrowStyleLabel(AnnotationArrowStyle.Double);
                case AnnotationArrowStyle.Line:
                    return GetArrowStyleLabel(AnnotationArrowStyle.Line);
                case AnnotationArrowStyle.Tapered:
                    return GetArrowStyleLabel(AnnotationArrowStyle.Tapered);
                default:
                    return GetArrowStyleLabel(AnnotationArrowStyle.Single);
            }
        }

        private string GetToolLabel(AnnotationTool tool)
        {
            switch (tool)
            {
                case AnnotationTool.Move:
                    return "Move";
                case AnnotationTool.Arrow:
                    return "Arrow";
                default:
                    return "Rect";
            }
        }

        private string GetArrowStyleLabel(AnnotationArrowStyle style)
        {
            switch (style)
            {
                case AnnotationArrowStyle.Double:
                    return "Double";
                case AnnotationArrowStyle.Line:
                    return "Line";
                case AnnotationArrowStyle.Tapered:
                    return "Tapered";
                default:
                    return "Single";
            }
        }

        private string GetRectangleStyleLabel(AnnotationRectangleStyle style)
        {
            return style == AnnotationRectangleStyle.Outline ? "Outline" : "Filled";
        }

        private string GetColorLabel(Color color)
        {
            if (color == Color.FromArgb(255, 255, 138, 61)) return "Orange";
            if (color == Color.FromArgb(88, 166, 255)) return "Blue";
            if (color == Color.FromArgb(78, 201, 140)) return "Green";
            if (color == Color.FromArgb(255, 105, 180)) return "Pink";
            return "Custom";
        }

        private Color CreateFillColorFromStroke(Color strokeColor)
        {
            return Color.FromArgb(48, strokeColor.R, strokeColor.G, strokeColor.B);
        }
        private void PictureBox1_MouseDownAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode || e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            HandleAnnotationMouseDown(sender, e);
        }

        private void HandleAnnotationMouseDown(object sender, MouseEventArgs e)
        {
            Point imagePoint;
            if (!TryClientToImagePoint(e.Location, out imagePoint))
            {
                if (_annotationTool == AnnotationTool.Move)
                    pictureBox1_MouseDown(sender, e);
                return;
            }

            int hitIndex;
            AnnotationHandle hitHandle;
            if (TryHitAnnotation(imagePoint, out hitIndex, out hitHandle))
            {
                BeginAnnotationEdit(hitIndex, hitHandle, imagePoint);
                return;
            }

            if (_annotationTool == AnnotationTool.Move)
            {
                pictureBox1_MouseDown(sender, e);
                return;
            }

            BeginAnnotationCreate(imagePoint);
        }

        private void PictureBox1_MouseMoveAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode) return;

            HandleAnnotationMouseMove(sender, e);
        }

        private void HandleAnnotationMouseMove(object sender, MouseEventArgs e)
        {
            ForwardStandardMoveIfNeeded(sender, e);
            if (_annotationDragging)
            {
                UpdateAnnotationDrag(e.Location);
                return;
            }

            UpdateAnnotationHoverState(e.Location);
        }

        private void PictureBox1_MouseUpAnnotations(object sender, MouseEventArgs e)
        {
            if (!_annotationMode) return;

            HandleAnnotationMouseUp(sender, e);
        }

        private void HandleAnnotationMouseUp(object sender, MouseEventArgs e)
        {
            if (_annotationTool == AnnotationTool.Move && !_annotationDragging)
            {
                pictureBox1_MouseUp(sender, e);
                return;
            }

            if (!_annotationDragging || e.Button != MouseButtons.Left) return;

            FinishAnnotationDrag(e.Location);
        }

        private void BeginAnnotationEdit(int hitIndex, AnnotationHandle hitHandle, Point imagePoint)
        {
            var shape = _annotations[hitIndex];
            _selectedAnnotationIndex = hitIndex;
            SyncAnnotationDefaultsFromShape(shape);
            BeginAnnotationDrag(GetInteractionForHit(shape, hitHandle), hitHandle, imagePoint);
            _annotationEditOriginBounds = shape.Bounds;
            _annotationEditOriginStart = shape.Start;
            _annotationEditOriginEnd = shape.End;
        }

        private void BeginAnnotationCreate(Point imagePoint)
        {
            _selectedAnnotationIndex = -1;
            _annotationStartImage = imagePoint;
            _annotationPreview = CreateAnnotationShape(_annotationTool, imagePoint, imagePoint);
            BeginAnnotationDrag(AnnotationInteraction.Create, AnnotationHandle.None, imagePoint);
        }

        private void BeginAnnotationDrag(AnnotationInteraction interaction, AnnotationHandle handle, Point imagePoint)
        {
            _annotationDragging = true;
            _annotationInteraction = interaction;
            _annotationHandle = handle;
            _annotationDragOriginImage = imagePoint;
            pictureBox1.Capture = true;
            UpdateAnnotationCursor(handle);
            pictureBox1.Invalidate();
        }

        private void CompleteAnnotationCreate()
        {
            if (_annotationPreview != null && IsAnnotationShapeUsable(_annotationPreview))
            {
                FinalizeCreatedAnnotation(_annotationPreview);
            }

            _annotationPreview = null;
        }

        private void FinalizeCreatedAnnotation(AnnotationShape shape)
        {
            _annotations.Add(shape);
            _selectedAnnotationIndex = _annotations.Count - 1;
            SyncAnnotationDefaultsFromShape(shape);
            UpdateAnnotationMenuState();
            ShowOverlay(_annotationTool == AnnotationTool.Arrow ? "ARROW ADDED" : "RECT ADDED");
            Log.Info("Annotation added: " + shape.Kind, "SnapWindow");
        }

        private void UpdateHoveredAnnotation(int annotationIndex)
        {
            if (_hoverAnnotationIndex == annotationIndex) return;
            _hoverAnnotationIndex = annotationIndex;
            pictureBox1?.Invalidate();
        }

        private void ClearHoveredAnnotation()
        {
            if (_hoverAnnotationIndex == -1) return;
            _hoverAnnotationIndex = -1;
            pictureBox1?.Invalidate();
        }

        private void ForwardStandardMoveIfNeeded(object sender, MouseEventArgs e)
        {
            if (_annotationTool == AnnotationTool.Move && !_annotationDragging)
                pictureBox1_MouseMove(sender, e);
        }

        private void UpdateAnnotationDrag(Point clientPoint)
        {
            Point imagePoint;
            if (!TryClientToImagePoint(clientPoint, out imagePoint)) return;

            ApplyAnnotationDrag(imagePoint);
            pictureBox1.Invalidate();
        }

        private void ApplyAnnotationDrag(Point imagePoint)
        {
            if (TryApplyAnnotationCreateDrag(imagePoint)) return;
            if (TryApplyRectangleDrag(imagePoint)) return;

            ApplyArrowDrag(imagePoint);
        }

        private bool TryApplyAnnotationCreateDrag(Point imagePoint)
        {
            if (_annotationInteraction != AnnotationInteraction.Create) return false;
            if (_annotationPreview != null)
                _annotationPreview.End = imagePoint;
            return true;
        }

        private bool TryApplyRectangleDrag(Point imagePoint)
        {
            switch (_annotationInteraction)
            {
                case AnnotationInteraction.MoveRectangle:
                    MoveSelectedRectangle(imagePoint);
                    return true;
                case AnnotationInteraction.ResizeRectangle:
                    ResizeSelectedRectangle(imagePoint);
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyArrowDrag(Point imagePoint)
        {
            switch (_annotationInteraction)
            {
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
        }

        private void UpdateAnnotationHoverState(Point clientPoint)
        {
            int hitIndex;
            AnnotationHandle hitHandle;
            if (!TryResolveAnnotationHover(clientPoint, out hitIndex, out hitHandle))
            {
                ClearAnnotationHoverState();
                return;
            }

            ApplyAnnotationHoverState(hitIndex, hitHandle);
        }

        private bool TryResolveAnnotationHover(Point clientPoint, out int hitIndex, out AnnotationHandle hitHandle)
        {
            hitIndex = -1;
            hitHandle = AnnotationHandle.None;

            Point imagePoint;
            if (!TryClientToImagePoint(clientPoint, out imagePoint)) return false;
            return TryHitAnnotation(imagePoint, out hitIndex, out hitHandle);
        }

        private void ApplyAnnotationHoverState(int hitIndex, AnnotationHandle hitHandle)
        {
            UpdateHoveredAnnotation(hitIndex);
            UpdateAnnotationCursor(hitHandle);
        }

        private void ClearAnnotationHoverState()
        {
            ClearHoveredAnnotation();
            UpdateAnnotationCursor(AnnotationHandle.None);
        }

        private void FinishAnnotationDrag(Point clientPoint)
        {
            CompleteFinishedAnnotationDrag();
            EndAnnotationDragState();
            pictureBox1.Invalidate();
        }

        private void CompleteFinishedAnnotationDrag()
        {
            if (_annotationInteraction == AnnotationInteraction.Create)
            {
                CompleteAnnotationCreate();
                return;
            }

            RemoveInvalidSelectedAnnotation();
        }

        private void EndAnnotationDragState()
        {
            _annotationDragging = false;
            pictureBox1.Capture = false;
            _annotationInteraction = AnnotationInteraction.None;
            _annotationHandle = AnnotationHandle.None;
            UpdateAnnotationCursor(AnnotationHandle.None);
        }

        private void RemoveInvalidSelectedAnnotation()
        {
            if (_selectedAnnotationIndex < 0 || _selectedAnnotationIndex >= _annotations.Count) return;
            if (IsAnnotationShapeUsable(_annotations[_selectedAnnotationIndex])) return;

            _annotations.RemoveAt(_selectedAnnotationIndex);
            _selectedAnnotationIndex = -1;
        }

        private void MoveSelectedRectangle(Point imagePoint)
        {
            if (!TryGetSelectedRectangle(out var shape)) return;

            AnnotationShapeMutator.ApplyRectangleBounds(shape, GetMovedRectangleBounds(imagePoint));
        }

        private void ResizeSelectedRectangle(Point imagePoint)
        {
            if (!TryGetSelectedRectangle(out var shape)) return;

            AnnotationShapeMutator.ApplyRectangleBounds(shape, GetResizedRectangleBounds(imagePoint));
        }

        private Rectangle GetMovedRectangleBounds(Point imagePoint)
        {
            return AnnotationGeometry.GetMovedRectangleBounds(_annotationEditOriginBounds, GetAnnotationDragOffset(imagePoint));
        }

        private Rectangle GetResizedRectangleBounds(Point imagePoint)
        {
            return AnnotationGeometry.GetResizedRectangleBounds(_annotationEditOriginBounds, imagePoint, _annotationHandle);
        }

        private Point GetAnnotationDragOffset(Point imagePoint)
        {
            return AnnotationShapeMutator.GetDragOffset(_annotationDragOriginImage, imagePoint);
        }

        private AnnotationInteraction GetInteractionForHit(AnnotationShape shape, AnnotationHandle handle)
        {
            return AnnotationInteractionResolver.GetInteractionForHit(shape.Kind, handle);
        }

        private void MoveSelectedArrow(Point imagePoint)
        {
            if (!TryGetSelectedArrow(out var shape)) return;

            AnnotationShapeMutator.MoveArrow(shape, _annotationEditOriginStart, _annotationEditOriginEnd, GetAnnotationDragOffset(imagePoint));
        }

        private void EditSelectedArrowEndpoint(Point imagePoint, bool editStart)
        {
            if (!TryGetSelectedArrow(out var shape)) return;
            AnnotationShapeMutator.SetArrowEndpoint(shape, imagePoint, editStart);
        }
        private bool TryGetSelectedArrow(out AnnotationShape shape)
        {
            return TryGetSelectedShapeOfKind(AnnotationShapeKind.Arrow, out shape);
        }

        private bool TryGetSelectedRectangle(out AnnotationShape shape)
        {
            return TryGetSelectedShapeOfKind(AnnotationShapeKind.Rectangle, out shape);
        }

        private bool TryGetSelectedShape(out AnnotationShape shape)
        {
            shape = null;
            if (_selectedAnnotationIndex < 0 || _selectedAnnotationIndex >= _annotations.Count) return false;
            shape = _annotations[_selectedAnnotationIndex];
            return true;
        }

        private bool TryGetSelectedShapeOfKind(AnnotationShapeKind kind, out AnnotationShape shape)
        {
            shape = null;
            if (!TryGetSelectedShape(out shape)) return false;
            return shape.Kind == kind;
        }

        private bool TryHitAnnotation(Point imagePoint, out int index, out AnnotationHandle handle)
        {
            for (int i = _annotations.Count - 1; i >= 0; i--)
            {
                var shape = _annotations[i];
                handle = HitTestAnnotationShape(shape, imagePoint);
                if (handle == AnnotationHandle.None) continue;

                index = i;
                return true;
            }

            index = -1;
            handle = AnnotationHandle.None;
            return false;
        }

        private AnnotationHandle HitTestAnnotationShape(AnnotationShape shape, Point imagePoint)
        {
            return AnnotationShapeHitTester.HitTest(shape, imagePoint, AnnotationArrowHandleRadius, AnnotationRectangleHandleRadius);
        }

        private void UpdateAnnotationCursor(AnnotationHandle handle)
        {
            Cursor = AnnotationInteractionResolver.ResolveCursor(_annotationMode, _annotationTool, handle);
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
            RenderAnnotationSequence(includePreview, shape => DrawAnnotationShape(g, shape, null, Size.Empty));
        }

        private void RenderAnnotationsToClient(Graphics g, Rectangle displayRect, Size sourceSize, bool includePreview)
        {
            RenderAnnotationSequence(includePreview, shape => DrawAnnotationShape(g, shape, displayRect, sourceSize));
        }

        private void RenderAnnotationSequence(bool includePreview, Action<AnnotationShape> renderShape)
        {
            foreach (var shape in _annotations)
                renderShape(shape);

            if (includePreview && _annotationPreview != null)
                renderShape(_annotationPreview);
        }
        private void DrawHoverOverlay(Graphics g, Rectangle displayRect, Size sourceSize)
        {
            if (!_annotationMode) return;
            if (_annotationDragging) return;
            var shape = GetHoveredAnnotation();
            if (shape == null) return;
            using (var pen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f))
            {
                pen.DashStyle = DashStyle.Dot;
                DrawHoverShapeOverlay(g, pen, shape, displayRect, sourceSize);
            }
        }

        private void DrawSelectionOverlay(Graphics g, Rectangle displayRect, Size sourceSize)
        {
            if (!_annotationMode) return;
            var shape = GetSelectedAnnotation();
            if (shape == null) return;

            using (var dashPen = new Pen(Color.FromArgb(255, 255, 255, 255), 1f))
            using (var handleBrush = new SolidBrush(Color.White))
            using (var handlePen = new Pen(Color.FromArgb(255, 255, 138, 61), 1f))
            {
                dashPen.DashStyle = DashStyle.Dash;
                DrawSelectionShapeOverlay(g, dashPen, handleBrush, handlePen, shape, displayRect, sourceSize);
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
            AnnotationShapeRenderer.DrawShape(g, shape, displayRect, sourceSize, DrawRectangleAnnotationShape, DrawArrowAnnotationShape);
        }
        private AnnotationShape GetHoveredAnnotation()
        {
            if (_hoverAnnotationIndex < 0 || _hoverAnnotationIndex >= _annotations.Count) return null;
            if (_hoverAnnotationIndex == _selectedAnnotationIndex) return null;
            return _annotations[_hoverAnnotationIndex];
        }

        private AnnotationShape GetSelectedAnnotation()
        {
            if (_selectedAnnotationIndex < 0 || _selectedAnnotationIndex >= _annotations.Count) return null;
            return _annotations[_selectedAnnotationIndex];
        }

        private void DrawHoverShapeOverlay(Graphics g, Pen pen, AnnotationShape shape, Rectangle displayRect, Size sourceSize)
        {
            AnnotationShapeRenderer.DrawHoverOverlay(g, pen, shape, displayRect, sourceSize, GetShapeRect, GetShapeLine);
        }
        private void DrawSelectionShapeOverlay(Graphics g, Pen dashPen, Brush handleBrush, Pen handlePen, AnnotationShape shape, Rectangle displayRect, Size sourceSize)
        {
            AnnotationShapeRenderer.DrawSelectionOverlay(g, dashPen, handleBrush, handlePen, shape, displayRect, sourceSize, GetShapeRect, GetShapeLine, DrawArrowHandle);
        }
        private void DrawRectangleAnnotationShape(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            AnnotationShapeRenderer.DrawRectangle(g, shape, displayRect, sourceSize, GetShapeRect, IsDrawableRect, CreateShapePen);
        }
        private void DrawArrowAnnotationShape(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            AnnotationShapeRenderer.DrawArrow(g, shape, displayRect, sourceSize, GetShapeLine, DrawTaperedArrow, CreateShapePen);
        }
        private Rectangle GetShapeRect(AnnotationShape shape, Rectangle? displayRect, Size sourceSize)
        {
            return displayRect.HasValue
                ? ImageRectToClientRect(displayRect.Value, sourceSize, shape.Bounds)
                : shape.Bounds;
        }

        private void GetShapeLine(AnnotationShape shape, Rectangle? displayRect, Size sourceSize, out Point start, out Point end)
        {
            start = displayRect.HasValue
                ? ImagePointToClientPoint(displayRect.Value, sourceSize, shape.Start)
                : shape.Start;
            end = displayRect.HasValue
                ? ImagePointToClientPoint(displayRect.Value, sourceSize, shape.End)
                : shape.End;
        }

        private bool IsDrawableRect(Rectangle rect)
        {
            return rect.Width > 0 && rect.Height > 0;
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
                    case AnnotationArrowStyle.Tapered:
                        break;
                }
            }

            return pen;
        }

        private AnnotationShape CreateAnnotationShape(AnnotationTool tool, Point start, Point end)
        {
            var kind = tool == AnnotationTool.Arrow ? AnnotationShapeKind.Arrow : AnnotationShapeKind.Rectangle;
            return new AnnotationShape
            {
                Kind = kind,
                Start = start,
                End = end,
                StrokeColor = _annotationStrokeColor,
                FillColor = kind == AnnotationShapeKind.Rectangle && _annotationRectangleStyle == AnnotationRectangleStyle.Outline ? Color.Transparent : _annotationFillColor,
                ArrowStyle = _annotationArrowStyle,
                RectangleStyle = _annotationRectangleStyle,
                StrokeWidth = Math.Max(2, DeviceDpi / 48)
            };
        }

        private void DrawTaperedArrow(Graphics g, AnnotationShape shape, Point start, Point end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1d) return;

            var ux = dx / length;
            var uy = dy / length;
            var nx = -uy;
            var ny = ux;

            PointF P(double along, double side)
            {
                return new PointF(
                    (float)(start.X + ux * along + nx * side),
                    (float)(start.Y + uy * along + ny * side));
            }

            // -----------------------------
            // パラメータ
            // -----------------------------
            var headLength = Math.Max(length * 0.22d, shape.StrokeWidth * 9.0d);
            headLength = Math.Min(headLength, length * 0.36d);

            var headStart = length - headLength;

            // 胴体前端の半幅
            var bodyHalfWidthFront = Math.Max(length * 0.026d, shape.StrokeWidth * 2.2d);
            bodyHalfWidthFront = Math.Min(bodyHalfWidthFront, headLength * 0.22d);

            // 返しの深さ（大きくしすぎない）
            var notchDepth = Math.Max(length * 0.028d, shape.StrokeWidth * 1.8d);
            notchDepth = Math.Min(notchDepth, headLength * 0.16d);

            // 返しの張り出し
            var barbOuter = Math.Max(length * 0.095d, shape.StrokeWidth * 5.0d);
            barbOuter = Math.Min(barbOuter, headLength * 0.42d);

            var barbAlong = headStart - notchDepth;

            // -----------------------------
            // 単純な外周だけで構成する
            // 6点ポリゴン
            // -----------------------------
            var points = new[]
            {
                P(0, 0),                         // 尾
                P(headStart, -bodyHalfWidthFront), // 胴体上
                P(barbAlong, -barbOuter),        // 上側の返し
                P(length, 0),                    // 先端
                P(barbAlong, barbOuter),         // 下側の返し
                P(headStart, bodyHalfWidthFront) // 胴体下
            };

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(shape.StrokeColor))
            using (var pen = new Pen(shape.StrokeColor, 1.5f))
            using (var path = new System.Drawing.Drawing2D.GraphicsPath(
                System.Drawing.Drawing2D.FillMode.Winding))
            {
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                path.AddPolygon(points);

                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            g.SmoothingMode = oldSmoothing;
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
            return AnnotationGeometry.DistanceSquared(a, b);
        }

        private bool TryGetDisplayImageRect(out Rectangle displayRect, out Bitmap source)
        {
            source = main_image ?? _originalImage as Bitmap;
            return AnnotationDisplayGeometry.TryGetDisplayImageRect(pictureBox1, source, out displayRect);
        }

        private bool TryClientToImagePoint(Point clientPoint, out Point imagePoint)
        {
            imagePoint = Point.Empty;
            Rectangle displayRect;
            Bitmap source;
            if (!TryGetDisplayImageRect(out displayRect, out source)) return false;
            return AnnotationDisplayGeometry.TryClientToImagePoint(displayRect, source.Size, clientPoint, out imagePoint);
        }

        private Point ImagePointToClientPoint(Rectangle displayRect, Size sourceSize, Point imagePoint)
        {
            return AnnotationDisplayGeometry.ImagePointToClientPoint(displayRect, sourceSize, imagePoint);
        }

        private Rectangle ImageRectToClientRect(Rectangle displayRect, Size sourceSize, Rectangle imageRect)
        {
            return AnnotationDisplayGeometry.ImageRectToClientRect(displayRect, sourceSize, imageRect);
        }

    }
}


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private const int AnnotationHistoryLimit = 100;
        private static readonly FieldInfo[] AnnotationHistoryShapeFields = typeof(AnnotationShape).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly List<AnnotationHistorySnapshot> _annotationUndoHistory = new List<AnnotationHistorySnapshot>();
        private readonly List<AnnotationHistorySnapshot> _annotationRedoHistory = new List<AnnotationHistorySnapshot>();
        private AnnotationHistorySnapshot _annotationPendingHistory;
        private bool _annotationPendingHistoryIncludesDefaults;

        private bool CanUndoAnnotations => _annotationUndoHistory.Count > 0;
        private bool CanRedoAnnotations => _annotationRedoHistory.Count > 0;

        private sealed class AnnotationHistorySnapshot
        {
            internal readonly List<AnnotationShape> Shapes;
            internal readonly int SelectedIndex;
            internal readonly int StrokeWidth;
            internal readonly Color StrokeColor;
            internal readonly Color FillColor;
            internal readonly AnnotationArrowStyle ArrowStyle;
            internal readonly AnnotationRectangleStyle RectangleStyle;
            internal readonly int FontSize;
            internal readonly bool TextBackground;
            internal readonly int MosaicBlockSize;

            internal AnnotationHistorySnapshot(SnapWindow window)
            {
                Shapes = new List<AnnotationShape>(window._annotations.Count);
                foreach (var shape in window._annotations) Shapes.Add(shape.Clone());
                SelectedIndex = window._selectedAnnotationIndex;
                StrokeWidth = window._annotationStrokeWidth;
                StrokeColor = window._annotationStrokeColor;
                FillColor = window._annotationFillColor;
                ArrowStyle = window._annotationArrowStyle;
                RectangleStyle = window._annotationRectangleStyle;
                FontSize = window._annotationFontSize;
                TextBackground = window._annotationTextBackground;
                MosaicBlockSize = window._annotationMosaicBlockSize;
            }
        }

        private void BeginAnnotationHistoryChange(bool includeDefaults = true)
        {
            if (_annotationPendingHistory != null) return;
            _annotationPendingHistory = new AnnotationHistorySnapshot(this);
            _annotationPendingHistoryIncludesDefaults = includeDefaults;
        }

        private void CommitAnnotationHistoryChange()
        {
            // A palette change while a drag is active belongs to the drag's
            // single history entry, which is committed after mouse up.
            if (_annotationDragging) return;
            var before = _annotationPendingHistory;
            if (before == null) return;
            _annotationPendingHistory = null;

            if (!AnnotationHistoryChanged(before, _annotationPendingHistoryIncludesDefaults)) return;
            _annotationUndoHistory.Add(before);
            if (_annotationUndoHistory.Count > AnnotationHistoryLimit) _annotationUndoHistory.RemoveAt(0);
            _annotationRedoHistory.Clear();
            UpdateAnnotationMenuState();
        }

        private void CancelAnnotationHistoryChange(bool restore)
        {
            var before = _annotationPendingHistory;
            _annotationPendingHistory = null;
            if (restore && before != null) RestoreAnnotationSnapshot(before);
        }

        private void ClearAnnotationHistory()
        {
            _annotationPendingHistory = null;
            _annotationUndoHistory.Clear();
            _annotationRedoHistory.Clear();
        }

        private bool AnnotationHistoryChanged(AnnotationHistorySnapshot before, bool includeDefaults)
        {
            if (before.Shapes.Count != _annotations.Count) return true;
            for (int i = 0; i < _annotations.Count; i++)
            {
                foreach (var field in AnnotationHistoryShapeFields)
                {
                    if (!Equals(field.GetValue(before.Shapes[i]), field.GetValue(_annotations[i]))) return true;
                }
            }
            return includeDefaults && (before.StrokeWidth != _annotationStrokeWidth ||
                before.StrokeColor != _annotationStrokeColor || before.FillColor != _annotationFillColor ||
                before.ArrowStyle != _annotationArrowStyle || before.RectangleStyle != _annotationRectangleStyle ||
                before.FontSize != _annotationFontSize || before.TextBackground != _annotationTextBackground ||
                before.MosaicBlockSize != _annotationMosaicBlockSize);
        }

        private void RestoreAnnotationSnapshot(AnnotationHistorySnapshot snapshot)
        {
            _annotations.Clear();
            foreach (var shape in snapshot.Shapes) _annotations.Add(shape.Clone());
            _selectedAnnotationIndex = AnnotationSelectionState.NormalizeIndex(snapshot.SelectedIndex, _annotations.Count);
            _hoverAnnotationIndex = -1;
            _annotationPreview = null;
            _annotationStrokeWidth = snapshot.StrokeWidth;
            _annotationStrokeColor = snapshot.StrokeColor;
            _annotationFillColor = snapshot.FillColor;
            _annotationArrowStyle = snapshot.ArrowStyle;
            _annotationRectangleStyle = snapshot.RectangleStyle;
            _annotationFontSize = snapshot.FontSize;
            _annotationTextBackground = snapshot.TextBackground;
            _annotationMosaicBlockSize = snapshot.MosaicBlockSize;
            RefreshAnnotationUi();
        }

        private void UndoLastAnnotation()
        {
            if (_annotationDragging) CancelAnnotationDrag();
            if (!CanUndoAnnotations) return;
            _annotationRedoHistory.Add(new AnnotationHistorySnapshot(this));
            var index = _annotationUndoHistory.Count - 1;
            var snapshot = _annotationUndoHistory[index];
            _annotationUndoHistory.RemoveAt(index);
            RestoreAnnotationSnapshot(snapshot);
            ShowOverlay("ANNOTATION UNDONE");
        }

        private void RedoAnnotation()
        {
            if (_annotationDragging) CancelAnnotationDrag();
            if (!CanRedoAnnotations) return;
            _annotationUndoHistory.Add(new AnnotationHistorySnapshot(this));
            if (_annotationUndoHistory.Count > AnnotationHistoryLimit) _annotationUndoHistory.RemoveAt(0);
            var index = _annotationRedoHistory.Count - 1;
            var snapshot = _annotationRedoHistory[index];
            _annotationRedoHistory.RemoveAt(index);
            RestoreAnnotationSnapshot(snapshot);
            ShowOverlay("ANNOTATION REDONE");
        }
    }
}

using System;
using System.Drawing;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private static class AnnotationShapeRenderer
        {
            public static void DrawHoverOverlay(Graphics g, Pen pen, AnnotationShape shape, Rectangle displayRect, Size sourceSize, Func<AnnotationShape, Rectangle?, Size, Rectangle> getShapeRect, GetShapeLineDelegate getShapeLine)
            {
                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    var rect = getShapeRect(shape, displayRect, sourceSize);
                    if (rect.Width <= 0 || rect.Height <= 0) return;
                    g.DrawRectangle(pen, rect);
                    return;
                }

                getShapeLine(shape, displayRect, sourceSize, out var start, out var end);
                g.DrawLine(pen, start, end);
            }

            public static void DrawSelectionOverlay(Graphics g, Pen dashPen, Brush handleBrush, Pen handlePen, AnnotationShape shape, Rectangle displayRect, Size sourceSize, Func<AnnotationShape, Rectangle?, Size, Rectangle> getShapeRect, GetShapeLineDelegate getShapeLine, DrawArrowHandleDelegate drawArrowHandle)
            {
                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    var rect = getShapeRect(shape, displayRect, sourceSize);
                    if (rect.Width <= 0 || rect.Height <= 0) return;
                    g.DrawRectangle(dashPen, rect);

                    foreach (var pair in AnnotationShapeHitTester.GetRectangleHandleRects(rect, 4))
                    {
                        g.FillRectangle(handleBrush, pair.Value);
                        g.DrawRectangle(handlePen, pair.Value);
                    }
                    return;
                }

                getShapeLine(shape, displayRect, sourceSize, out var start, out var end);
                g.DrawLine(dashPen, start, end);
                drawArrowHandle(g, handleBrush, handlePen, start);
                drawArrowHandle(g, handleBrush, handlePen, end);
            }

            public static void DrawShape(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize, DrawShapeDelegate drawRectangle, DrawShapeDelegate drawArrow)
            {
                if (shape.Kind == AnnotationShapeKind.Rectangle)
                {
                    drawRectangle(g, shape, displayRect, sourceSize);
                    return;
                }

                drawArrow(g, shape, displayRect, sourceSize);
            }

            public static void DrawRectangle(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize, Func<AnnotationShape, Rectangle?, Size, Rectangle> getShapeRect, Func<Rectangle, bool> isDrawableRect, Func<AnnotationShape, Pen> createShapePen)
            {
                using (var pen = createShapePen(shape))
                using (var brush = new SolidBrush(shape.FillColor))
                {
                    var rect = getShapeRect(shape, displayRect, sourceSize);
                    if (!isDrawableRect(rect)) return;
                    if (shape.RectangleStyle != AnnotationRectangleStyle.Outline)
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(pen, rect);
                }
            }

            public static void DrawArrow(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize, GetShapeLineDelegate getShapeLine, DrawTaperedArrowDelegate drawTaperedArrow, Func<AnnotationShape, Pen> createShapePen)
            {
                getShapeLine(shape, displayRect, sourceSize, out var start, out var end);
                if (shape.ArrowStyle == AnnotationArrowStyle.Tapered)
                {
                    drawTaperedArrow(g, shape, start, end);
                    return;
                }

                using (var pen = createShapePen(shape))
                {
                    g.DrawLine(pen, start, end);
                }
            }
        }
    }
}

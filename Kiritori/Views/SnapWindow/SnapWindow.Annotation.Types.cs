using System;
using System.Drawing;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private sealed class AnnotationShape
        {
            public AnnotationShapeKind Kind;
            public Point Start;
            public Point End;
            public Color StrokeColor;
            public int StrokeWidth;
            public Color FillColor;
            public AnnotationArrowStyle ArrowStyle;
            public AnnotationRectangleStyle RectangleStyle;

            public Rectangle Bounds
            {
                get { return Rectangle.FromLTRB(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y), Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)); }
            }
        }

        private delegate void GetShapeLineDelegate(AnnotationShape shape, Rectangle? displayRect, Size sourceSize, out Point start, out Point end);
        private delegate void DrawArrowHandleDelegate(Graphics g, Brush fill, Pen border, Point point);
        private delegate void DrawTaperedArrowDelegate(Graphics g, AnnotationShape shape, Point start, Point end);
        private delegate void DrawShapeDelegate(Graphics g, AnnotationShape shape, Rectangle? displayRect, Size sourceSize);
    }
}

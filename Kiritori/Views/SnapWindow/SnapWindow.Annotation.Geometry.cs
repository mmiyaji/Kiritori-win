using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private static class AnnotationDisplayGeometry
        {
            public static bool TryGetDisplayImageRect(PictureBox pictureBox, Bitmap source, out Rectangle displayRect)
            {
                displayRect = Rectangle.Empty;
                if (pictureBox == null || source == null) return false;

                var image = pictureBox.Image;
                if (image == null || image.Width <= 0 || image.Height <= 0) return false;
                var client = pictureBox.ClientRectangle;
                if (client.Width <= 0 || client.Height <= 0) return false;

                // Match PictureBox's actual image rectangle. After a resize commit,
                // Image is a resampled viewport bitmap in Normal mode; its dimensions
                // intentionally differ from the original annotation coordinate space.
                var padding = pictureBox.Padding;
                displayRect = new Rectangle(client.Left + padding.Left, client.Top + padding.Top,
                    client.Width - padding.Horizontal, client.Height - padding.Vertical);
                switch (pictureBox.SizeMode)
                {
                    case PictureBoxSizeMode.Normal:
                    case PictureBoxSizeMode.AutoSize:
                        displayRect.Size = image.Size;
                        break;
                    case PictureBoxSizeMode.CenterImage:
                        displayRect.X += (displayRect.Width - image.Width) / 2;
                        displayRect.Y += (displayRect.Height - image.Height) / 2;
                        displayRect.Size = image.Size;
                        break;
                    case PictureBoxSizeMode.Zoom:
                        // WinForms Zoom uses the full client rectangle and truncates
                        // the scaled dimensions, rather than rounding to nearest.
                        float scale = Math.Min((float)client.Width / image.Width, (float)client.Height / image.Height);
                        displayRect.Width = (int)(image.Width * scale);
                        displayRect.Height = (int)(image.Height * scale);
                        displayRect.X = (client.Width - displayRect.Width) / 2;
                        displayRect.Y = (client.Height - displayRect.Height) / 2;
                        break;
                    case PictureBoxSizeMode.StretchImage:
                        break;
                }
                return displayRect.Width > 0 && displayRect.Height > 0;
            }

            public static bool TryClientToImagePoint(Rectangle displayRect, Size sourceSize, Point clientPoint, out Point imagePoint)
            {
                imagePoint = Point.Empty;
                if (!displayRect.Contains(clientPoint)) return false;

                imagePoint = ClientToImagePointClamped(displayRect, sourceSize, clientPoint);
                return true;
            }

            public static Point ClientToImagePointClamped(Rectangle displayRect, Size sourceSize, Point clientPoint)
            {
                // Only use this after an in-image mouse down. Capture can deliver
                // continuation events outside the displayed image.
                int clientX = Math.Max(displayRect.Left, Math.Min(displayRect.Right - 1, clientPoint.X));
                int clientY = Math.Max(displayRect.Top, Math.Min(displayRect.Bottom - 1, clientPoint.Y));

                double ratioX = (double)(clientX - displayRect.X) / Math.Max(1, displayRect.Width);
                double ratioY = (double)(clientY - displayRect.Y) / Math.Max(1, displayRect.Height);
                int x = Math.Max(0, Math.Min(sourceSize.Width - 1, (int)Math.Round(ratioX * sourceSize.Width)));
                int y = Math.Max(0, Math.Min(sourceSize.Height - 1, (int)Math.Round(ratioY * sourceSize.Height)));
                return new Point(x, y);
            }

            public static Point ImagePointToClientPoint(Rectangle displayRect, Size sourceSize, Point imagePoint)
            {
                int x = displayRect.X + (int)Math.Round((double)imagePoint.X * displayRect.Width / Math.Max(1, sourceSize.Width));
                int y = displayRect.Y + (int)Math.Round((double)imagePoint.Y * displayRect.Height / Math.Max(1, sourceSize.Height));
                return new Point(x, y);
            }

            public static Rectangle ImageRectToClientRect(Rectangle displayRect, Size sourceSize, Rectangle imageRect)
            {
                int x = displayRect.X + (int)Math.Round((double)imageRect.X * displayRect.Width / Math.Max(1, sourceSize.Width));
                int y = displayRect.Y + (int)Math.Round((double)imageRect.Y * displayRect.Height / Math.Max(1, sourceSize.Height));
                int width = Math.Max(1, (int)Math.Round((double)imageRect.Width * displayRect.Width / Math.Max(1, sourceSize.Width)));
                int height = Math.Max(1, (int)Math.Round((double)imageRect.Height * displayRect.Height / Math.Max(1, sourceSize.Height)));
                return new Rectangle(x, y, width, height);
            }
        }

        private static class AnnotationShapeHitTester
        {
            public static AnnotationHandle HitTest(AnnotationShape shape, Point imagePoint, int arrowHandleRadius, int rectangleHandleRadius)
            {
                return shape.Kind == AnnotationShapeKind.Arrow
                    ? HitTestArrow(shape, imagePoint, arrowHandleRadius)
                    : HitTestRectangle(shape.Bounds, imagePoint, rectangleHandleRadius);
            }

            public static Dictionary<AnnotationHandle, Rectangle> GetRectangleHandleRects(Rectangle rect, int radius)
            {
                var size = radius * 2;
                var center = AnnotationGeometry.GetRectangleCenter(rect);
                return new Dictionary<AnnotationHandle, Rectangle>
                {
                    { AnnotationHandle.TopLeft, AnnotationGeometry.CreateHandleRect(rect.Left, rect.Top, radius, size) },
                    { AnnotationHandle.Top, AnnotationGeometry.CreateHandleRect(center.X, rect.Top, radius, size) },
                    { AnnotationHandle.TopRight, AnnotationGeometry.CreateHandleRect(rect.Right, rect.Top, radius, size) },
                    { AnnotationHandle.Right, AnnotationGeometry.CreateHandleRect(rect.Right, center.Y, radius, size) },
                    { AnnotationHandle.BottomRight, AnnotationGeometry.CreateHandleRect(rect.Right, rect.Bottom, radius, size) },
                    { AnnotationHandle.Bottom, AnnotationGeometry.CreateHandleRect(center.X, rect.Bottom, radius, size) },
                    { AnnotationHandle.BottomLeft, AnnotationGeometry.CreateHandleRect(rect.Left, rect.Bottom, radius, size) },
                    { AnnotationHandle.Left, AnnotationGeometry.CreateHandleRect(rect.Left, center.Y, radius, size) },
                };
            }

            private static AnnotationHandle HitTestArrow(AnnotationShape shape, Point imagePoint, int handleRadius)
            {
                if (AnnotationGeometry.GetHandleRect(shape.Start, handleRadius).Contains(imagePoint)) return AnnotationHandle.ArrowStart;
                if (AnnotationGeometry.GetHandleRect(shape.End, handleRadius).Contains(imagePoint)) return AnnotationHandle.ArrowEnd;
                return AnnotationGeometry.IsPointNearSegment(imagePoint, shape.Start, shape.End, 100) ? AnnotationHandle.Move : AnnotationHandle.None;
            }

            private static AnnotationHandle HitTestRectangle(Rectangle rect, Point imagePoint, int handleRadius)
            {
                foreach (var pair in GetRectangleHandleRects(rect, handleRadius))
                {
                    if (pair.Value.Contains(imagePoint)) return pair.Key;
                }

                return rect.Contains(imagePoint) ? AnnotationHandle.Move : AnnotationHandle.None;
            }
        }

        private static class AnnotationInteractionResolver
        {
            public static AnnotationInteraction GetInteractionForHit(AnnotationShapeKind kind, AnnotationHandle handle)
            {
                if (kind == AnnotationShapeKind.Arrow)
                {
                    if (handle == AnnotationHandle.ArrowStart) return AnnotationInteraction.EditArrowStart;
                    if (handle == AnnotationHandle.ArrowEnd) return AnnotationInteraction.EditArrowEnd;
                    return AnnotationInteraction.MoveArrow;
                }

                return handle == AnnotationHandle.Move ? AnnotationInteraction.MoveRectangle : AnnotationInteraction.ResizeRectangle;
            }

            public static Cursor ResolveCursor(bool annotationMode, AnnotationTool tool, AnnotationHandle handle)
            {
                if (!annotationMode) return Cursors.Default;

                switch (handle)
                {
                    case AnnotationHandle.TopLeft:
                    case AnnotationHandle.BottomRight:
                        return Cursors.SizeNWSE;
                    case AnnotationHandle.TopRight:
                    case AnnotationHandle.BottomLeft:
                        return Cursors.SizeNESW;
                    case AnnotationHandle.Top:
                    case AnnotationHandle.Bottom:
                        return Cursors.SizeNS;
                    case AnnotationHandle.Left:
                    case AnnotationHandle.Right:
                        return Cursors.SizeWE;
                    case AnnotationHandle.Move:
                        return Cursors.SizeAll;
                    case AnnotationHandle.ArrowStart:
                    case AnnotationHandle.ArrowEnd:
                        return Cursors.Hand;
                    default:
                        return tool == AnnotationTool.Move ? Cursors.SizeAll : Cursors.Cross;
                }
            }
        }

        private static class AnnotationShapeMutator
        {
            public static void ApplyRectangleBounds(AnnotationShape shape, Rectangle bounds)
            {
                shape.Start = bounds.Location;
                shape.End = new Point(bounds.Right, bounds.Bottom);
            }

            public static Point GetDragOffset(Point origin, Point current)
            {
                return new Point(current.X - origin.X, current.Y - origin.Y);
            }

            public static void MoveArrow(AnnotationShape shape, Point originStart, Point originEnd, Point dragOffset)
            {
                shape.Start = AnnotationGeometry.OffsetPoint(originStart, dragOffset);
                shape.End = AnnotationGeometry.OffsetPoint(originEnd, dragOffset);
            }

            public static void SetArrowEndpoint(AnnotationShape shape, Point imagePoint, bool editStart)
            {
                if (editStart)
                    shape.Start = imagePoint;
                else
                    shape.End = imagePoint;
            }
        }

        private static class AnnotationSelectionState
        {
            public static int NormalizeIndex(int index, int count)
            {
                if (count <= 0) return -1;
                return index >= count ? count - 1 : index;
            }

            public static bool TryGetShape(IList<AnnotationShape> annotations, int index, out AnnotationShape shape)
            {
                shape = GetShapeOrNull(annotations, index);
                return shape != null;
            }

            public static AnnotationShape GetHoveredShape(IList<AnnotationShape> annotations, int hoverIndex, int selectedIndex)
            {
                if (hoverIndex == selectedIndex) return null;
                return GetShapeOrNull(annotations, hoverIndex);
            }

            public static AnnotationShape GetShapeOrNull(IList<AnnotationShape> annotations, int index)
            {
                if (annotations == null || index < 0 || index >= annotations.Count) return null;
                return annotations[index];
            }
        }

        private static class AnnotationGeometry
        {
            public static Rectangle GetMovedRectangleBounds(Rectangle originBounds, Point dragOffset)
            {
                return new Rectangle(originBounds.X + dragOffset.X, originBounds.Y + dragOffset.Y, originBounds.Width, originBounds.Height);
            }

            public static Rectangle GetResizedRectangleBounds(Rectangle originBounds, Point imagePoint, AnnotationHandle handle)
            {
                var left = originBounds.Left;
                var top = originBounds.Top;
                var right = originBounds.Right;
                var bottom = originBounds.Bottom;

                switch (handle)
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

                return Rectangle.FromLTRB(Math.Min(left, right), Math.Min(top, bottom), Math.Max(left, right), Math.Max(top, bottom));
            }

            public static Point OffsetPoint(Point point, Point offset)
            {
                return new Point(point.X + offset.X, point.Y + offset.Y);
            }

            public static bool IsPointNearSegment(Point point, Point start, Point end, int thresholdSquared)
            {
                return DistancePointToSegmentSquared(point, start, end) <= thresholdSquared;
            }

            public static Rectangle GetHandleRect(Point center, int radius)
            {
                return new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }

            public static Rectangle CreateHandleRect(int centerX, int centerY, int radius, int size)
            {
                return new Rectangle(centerX - radius, centerY - radius, size, size);
            }

            public static Point GetRectangleCenter(Rectangle rect)
            {
                return new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            }

            public static int DistanceSquared(Point a, Point b)
            {
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                return (dx * dx) + (dy * dy);
            }

            private static int DistancePointToSegmentSquared(Point point, Point start, Point end)
            {
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                if (dx == 0 && dy == 0) return DistanceSquared(point, start);

                var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (double)((dx * dx) + (dy * dy));
                t = Math.Max(0d, Math.Min(1d, t));
                var projX = start.X + (t * dx);
                var projY = start.Y + (t * dy);
                var px = point.X - projX;
                var py = point.Y - projY;
                return (int)Math.Round((px * px) + (py * py));
            }
        }
    }
}

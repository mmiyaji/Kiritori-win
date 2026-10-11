using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// Exercises the real SnapWindow event wiring and native PictureBox capture.
internal static class AnnotationEditingProbe
{
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Form _window;
    private static PictureBox _picture;
    private static Type _windowType;
    private static Type _toolType;
    private static Type _geometryType;

    private static object Field(object target, string name) { return target.GetType().GetField(name, Instance).GetValue(target); }
    private static object Call(string name, params object[] args) { return _windowType.GetMethod(name, Instance).Invoke(_window, args); }
    private static bool State(string name) { return (bool)_windowType.GetProperty(name, Instance).GetValue(_window, null); }
    private static IList Shapes { get { return (IList)Field(_window, "_annotations"); } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static Point ShapePoint(string name) { return (Point)Field(Shapes[0], name); }
    private static void Tool(string name) { Call("SetAnnotationTool", Enum.Parse(_toolType, name)); }

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Run(args[0]);
    }

    private static void Run(string assemblyPath)
    {
        _windowType = Assembly.LoadFrom(assemblyPath).GetType("Kiritori.SnapWindow", true);
        _toolType = _windowType.GetNestedType("AnnotationTool", BindingFlags.NonPublic);
        _geometryType = _windowType.GetNestedType("AnnotationDisplayGeometry", BindingFlags.NonPublic);
        using (_window = (Form)Activator.CreateInstance(_windowType, new object[] { null }))
        {
            _window.StartPosition = FormStartPosition.Manual;
            _window.Location = new Point(60, 60);
            _picture = (PictureBox)Field(_window, "pictureBox1");
            _window.Shown += (sender, e) => _window.BeginInvoke((Action)(() =>
            {
                try { VerifyEditing(); }
                catch (Exception error) { Console.Error.WriteLine(error.GetBaseException()); Environment.ExitCode = 1; }
                finally { _window.Close(); }
            }));
            Application.Run(_window);
        }
    }

    private static void VerifyEditing()
    {
            Reset();
            Application.DoEvents();

            Draw(Source(30, 30), Source(130, 110));
            Require(Shapes.Count == 1, "Rectangle creation failed");
            var original = ShapePoint("Start");
            Require(State("CanUndoAnnotations"), "Creation did not enter undo history");
            Call("UndoLastAnnotation");
            Require(Shapes.Count == 0 && State("CanRedoAnnotations"), "Undo did not remove the created rectangle");
            Call("RedoAnnotation");
            Require(Shapes.Count == 1 && ShapePoint("Start") == original, "Redo did not restore the rectangle");
            Console.WriteLine("PASS: creation undo and redo");

            Tool("Move");
            Draw(Source(75, 65), Source(100, 85));
            Require(Shapes.Count == 1 && ShapePoint("Start") != original, "Move did not change the rectangle");
            var moved = ShapePoint("Start");
            Call("UndoLastAnnotation");
            Require(Shapes.Count == 1 && ShapePoint("Start") == original, "Move undo deleted the shape or kept the moved coordinates");
            Call("RedoAnnotation");
            Require(Shapes.Count == 1 && ShapePoint("Start") == moved, "Move redo did not restore the moved coordinates");
            Console.WriteLine("PASS: move undo and redo preserve the shape");

            var oldColor = (Color)Field(Shapes[0], "StrokeColor");
            Call("SetAnnotationColor", Color.Red, Color.FromArgb(48, Color.Red));
            Require((Color)Field(Shapes[0], "StrokeColor") == Color.Red, "Color change did not reach the selected shape");
            Call("UndoLastAnnotation");
            Require((Color)Field(Shapes[0], "StrokeColor") == oldColor, "Color undo failed");
            Call("RedoAnnotation");
            Require((Color)Field(Shapes[0], "StrokeColor") == Color.Red, "Color redo failed");

            var input = (NumericUpDown)Field(_window, "_annotationWidthInput");
            var oldWidth = (int)Field(Shapes[0], "StrokeWidth");
            input.Value = oldWidth + 3;
            Require((int)Field(Shapes[0], "StrokeWidth") == oldWidth + 3, "Palette width control did not edit the selected shape");
            Call("UndoLastAnnotation");
            Require((int)Field(Shapes[0], "StrokeWidth") == oldWidth, "Width undo failed");
            Call("RedoAnnotation");
            Require((int)Field(Shapes[0], "StrokeWidth") == oldWidth + 3, "Width redo failed");

            var styleType = _windowType.GetNestedType("AnnotationRectangleStyle", BindingFlags.NonPublic);
            var oldStyle = Field(Shapes[0], "RectangleStyle");
            var nextStyle = Enum.Parse(styleType, oldStyle.ToString() == "Filled" ? "Outline" : "Filled");
            Call("SetAnnotationRectangleStyle", nextStyle);
            Require(Equals(Field(Shapes[0], "RectangleStyle"), nextStyle), "Style change did not reach the selected shape");
            Call("UndoLastAnnotation");
            Require(Equals(Field(Shapes[0], "RectangleStyle"), oldStyle), "Style undo failed");
            Call("RedoAnnotation");
            Require(Equals(Field(Shapes[0], "RectangleStyle"), nextStyle), "Style redo failed");
            Console.WriteLine("PASS: selected color, palette width and style undo and redo");

            Call("ClearAnnotations");
            Require(Shapes.Count == 0, "Clear failed");
            Call("UndoLastAnnotation");
            Require(Shapes.Count == 1, "Clear undo failed");
            Call("RedoAnnotation");
            Require(Shapes.Count == 0, "Clear redo failed");
            Call("UndoLastAnnotation");
            Call("SetAnnotationColor", Color.Blue, Color.FromArgb(48, Color.Blue));
            Require(!State("CanRedoAnnotations"), "A new edit did not invalidate redo history");
            Console.WriteLine("PASS: clear undo/redo and divergent edit invalidation");

            Call("DeleteSelectedAnnotation");
            Require(Shapes.Count == 0, "Selected-shape deletion failed");
            Call("UndoLastAnnotation");
            Require(Shapes.Count == 1 && (Color)Field(Shapes[0], "StrokeColor") == Color.Blue,
                "Delete undo did not restore the edited shape");
            Console.WriteLine("PASS: selected-shape deletion undo");

            Reset();
            Mouse("Down", Source(20, 20));
            Mouse("Move", Source(70, 60));
            Mouse("Up", Source(150, 100));
            Require(Shapes.Count == 1 && Near(ShapePoint("End"), new Point(150, 100)),
                "Mouse up position was not used as the final endpoint: " + (Shapes.Count == 0 ? "no shape" : ShapePoint("End").ToString()));
            Console.WriteLine("PASS: final release coordinate");

            Reset();
            Mouse("Down", Source(20, 20));
            var outside = new Point(DisplayRect().Right + 80, DisplayRect().Bottom + 80);
            Mouse("Move", outside);
            Mouse("Up", outside);
            Require(Shapes.Count == 1 && ShapePoint("End") == new Point(299, 199),
                "Captured continuation did not clamp to image boundary");
            Console.WriteLine("PASS: captured drag clamps to the image boundary");

            Reset();
            Draw(Source(30, 30), Source(130, 110));
            Tool("Move");
            original = ShapePoint("Start");
            Mouse("Down", Source(75, 65));
            Mouse("Move", Source(100, 85));
            Require(ShapePoint("Start") != original, "Capture-loss setup did not move the shape");
            _picture.Capture = false;
            Application.DoEvents();
            Require(!((bool)Field(_window, "_annotationDragging")) && Shapes.Count == 1 && ShapePoint("Start") == original,
                "Capture loss did not cancel and roll back the edit");
            Mouse("Up", Source(100, 85));
            Require(ShapePoint("Start") == original, "Mouse up after capture loss resumed the cancelled edit");

            Mouse("Down", Source(75, 65));
            Mouse("Move", Source(100, 85));
            Tool("Arrow");
            Require(!((bool)Field(_window, "_annotationDragging")) && Shapes.Count == 1 && ShapePoint("Start") == original,
                "Tool switch did not cancel and roll back the edit");
            Console.WriteLine("PASS: capture loss and tool switch roll back active edits");

            Reset();
            for (var i = 0; i < 105; i++)
                Call("SetAnnotationColor", Color.FromArgb(255, i, 20, 30), Color.FromArgb(48, i, 20, 30));
            Require(((IList)Field(_window, "_annotationUndoHistory")).Count == 100,
                "Undo history did not stay within its 100-entry limit");
            Call("SetImageAndResetZoom", new Bitmap(300, 200), null);
            Require(!State("CanUndoAnnotations") && !State("CanRedoAnnotations"),
                "Replacing the image did not clear annotation history");
            Console.WriteLine("PASS: history is bounded and cleared when the image changes");
    }

    private static bool Near(Point actual, Point expected)
    {
        return Math.Abs(actual.X - expected.X) <= 1 && Math.Abs(actual.Y - expected.Y) <= 1;
    }

    private static void Reset()
    {
        Call("SetImageAndResetZoom", new Bitmap(300, 200), null);
        Call("EnterAnnotationMode");
        Tool("Rectangle");
        Application.DoEvents();
    }

    private static Rectangle DisplayRect()
    {
        var parameters = new object[] { Rectangle.Empty, null };
        Require((bool)Call("TryGetDisplayImageRect", parameters), "No displayed image rectangle");
        return (Rectangle)parameters[0];
    }

    private static Point Source(int x, int y)
    {
        var method = _geometryType.GetMethod("ImagePointToClientPoint", Static);
        return (Point)method.Invoke(null, new object[] { DisplayRect(), new Size(300, 200), new Point(x, y) });
    }

    private static void Mouse(string kind, Point point)
    {
        typeof(Control).GetMethod("OnMouse" + kind, Instance).Invoke(_picture,
            new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });
    }

    private static void Draw(Point start, Point end)
    {
        Mouse("Down", start);
        Mouse("Move", end);
        Mouse("Up", end);
    }
}

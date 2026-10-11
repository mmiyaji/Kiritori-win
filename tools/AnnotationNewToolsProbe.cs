using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class AnnotationNewToolsProbe
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static Type _snapType;
    private static Type _shapeType;
    private static Type _toolType;
    private static Type _kindType;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var assembly = Assembly.LoadFrom(args[0]);
        _snapType = assembly.GetType("Kiritori.SnapWindow", true);
        _shapeType = _snapType.GetNestedType("AnnotationShape", BindingFlags.NonPublic);
        _toolType = _snapType.GetNestedType("AnnotationTool", BindingFlags.NonPublic);
        _kindType = _snapType.GetNestedType("AnnotationShapeKind", BindingFlags.NonPublic);
        using (var context = new ApplicationContext())
        {
            EventHandler start = null;
            start = (sender, e) =>
            {
                Application.Idle -= start;
                try
                {
                    VerifyStepSequence();
                    VerifyMosaicExportAndHistory();
                    VerifyMultilineTextExportAndViewportScale();
                    Console.WriteLine("PASS: step numbering, mosaic pixels/history, multiline text export/view scaling");
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error.GetBaseException());
                    Environment.ExitCode = 1;
                }
                finally { context.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run(context);
        }
    }

    private static Form NewWindow(Bitmap image)
    {
        var window = (Form)Activator.CreateInstance(_snapType, new object[] { null });
        Call(window, "SetImageAndResetZoom", image, null);
        window.Show();
        Application.DoEvents();
        return window;
    }

    private static object Get(object target, string name)
    {
        return target.GetType().GetField(name, Instance).GetValue(target);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Instance).SetValue(target, value);
    }

    private static object Call(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, Instance).Invoke(target, arguments);
    }

    private static object Kind(string name) { return Enum.Parse(_kindType, name); }
    private static object Tool(string name) { return Enum.Parse(_toolType, name); }
    private static IList Shapes(object window) { return (IList)Get(window, "_annotations"); }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Bitmap WhiteImage(int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
        return bitmap;
    }

    private static void VerifyStepSequence()
    {
        using (var window = NewWindow(WhiteImage(120, 100)))
        {
            Set(window, "_annotationTool", Tool("Step"));
            Call(window, "TryHandleSpecialAnnotationMouseDown", new Point(20, 20));
            Call(window, "TryHandleSpecialAnnotationMouseDown", new Point(50, 40));
            Require(Shapes(window).Count == 2, "Step clicks did not create two shapes");
            Require((int)Get(Shapes(window)[0], "StepNumber") == 1, "First step number was not 1");
            Require((int)Get(Shapes(window)[1], "StepNumber") == 2, "Second step number was not 2");
            Call(window, "UndoLastAnnotation");
            Require(Shapes(window).Count == 1, "Undo did not remove the second step");
            Call(window, "RedoAnnotation");
            Require((int)Get(Shapes(window)[1], "StepNumber") == 2, "Redo did not restore step 2");
            Call(window, "UndoLastAnnotation");
            Call(window, "TryHandleSpecialAnnotationMouseDown", new Point(80, 60));
            Require((int)Get(Shapes(window)[1], "StepNumber") == 2, "Step number was not reused after undo");
            Call(window, "ClearAnnotations");
            Call(window, "TryHandleSpecialAnnotationMouseDown", new Point(50, 50));
            Require((int)Get(Shapes(window)[0], "StepNumber") == 1, "Step number did not reset after clear");
        }
    }

    private static void VerifyMosaicExportAndHistory()
    {
        var source = new Bitmap(12, 12);
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 12; x++)
            source.SetPixel(x, y, Color.FromArgb((x * 19 + y * 3) % 256,
                (x * 7 + y * 31) % 256, (x * 13 + y * 17) % 256));
        using (var window = NewWindow(source))
        {
            var shape = Call(window, "CreateSpecialAnnotationShape", Kind("Mosaic"), new Point(2, 2));
            Set(shape, "End", new Point(10, 10));
            Set(shape, "MosaicBlockSize", 4);
            Shapes(window).Add(shape);
            Set(window, "_selectedAnnotationIndex", 0);
            using (var export = (Bitmap)Call(window, "CloneCurrentBitmapWithAnnotations", source))
            {
                for (var y = 0; y < 12; y++)
                for (var x = 0; x < 12; x++)
                {
                    var expected = x >= 2 && x < 10 && y >= 2 && y < 10
                        ? BlockAverage(source, 2 + ((x - 2) / 4) * 4, 2 + ((y - 2) / 4) * 4, 4)
                        : source.GetPixel(x, y);
                    Require(export.GetPixel(x, y).ToArgb() == expected.ToArgb(),
                        "Mosaic pixel mismatch at " + x + "," + y);
                }
            }
            Call(window, "SetAnnotationMosaicBlockSize", 8);
            Require((int)Get(Shapes(window)[0], "MosaicBlockSize") == 8, "Mosaic strength did not change");
            Call(window, "UndoLastAnnotation");
            Require((int)Get(Shapes(window)[0], "MosaicBlockSize") == 4, "Undo did not restore mosaic strength");
            Call(window, "RedoAnnotation");
            Require((int)Get(Shapes(window)[0], "MosaicBlockSize") == 8, "Redo did not restore mosaic strength");
        }
    }

    private static Color BlockAverage(Bitmap source, int left, int top, int blockSize)
    {
        var r = 0; var green = 0; var b = 0;
        for (var y = top; y < top + blockSize; y++)
        for (var x = left; x < left + blockSize; x++)
        {
            var color = source.GetPixel(x, y);
            r += color.R; green += color.G; b += color.B;
        }
        var count = blockSize * blockSize;
        return Color.FromArgb(r / count, green / count, b / count);
    }

    private static void VerifyMultilineTextExportAndViewportScale()
    {
        var source = WhiteImage(200, 100);
        using (var window = NewWindow(source))
        {
            var shape = Call(window, "CreateSpecialAnnotationShape", Kind("Text"), new Point(20, 10));
            Set(shape, "Text", "A\nB");
            Set(shape, "FontSize", 20);
            Set(shape, "StrokeColor", Color.Red);
            Set(shape, "TextBackground", false);
            _snapType.GetMethod("UpdateTextShapeBounds", Static).Invoke(null, new[] { shape });
            Shapes(window).Add(shape);
            using (var export = (Bitmap)Call(window, "CloneCurrentBitmapWithAnnotations", source))
            using (var client = new Bitmap(400, 150))
            {
                using (var g = Graphics.FromImage(client))
                {
                    g.Clear(Color.White);
                    Call(window, "RenderAnnotationsToClient", g, new Rectangle(0, 0, 400, 150), source.Size, false);
                }
                var native = RedBounds(export);
                var stretched = RedBounds(client);
                Require(native.Width > 0 && native.Height > 20, "Multiline text is missing from export");
                Require(Math.Abs(stretched.Left - native.Left * 2) <= 2, "Text X position changed under nonproportional resize");
                Require(Math.Abs(stretched.Top - native.Top * 1.5) <= 2, "Text Y position changed under nonproportional resize");
                Require(Math.Abs(stretched.Width - native.Width * 2) <= 3, "Text width did not scale with viewport");
                Require(Math.Abs(stretched.Height - native.Height * 1.5) <= 3, "Text height did not scale with viewport");
                var origin = (Rectangle)_shapeType.GetProperty("Bounds", Instance).GetValue(shape, null);
                var resized = new Rectangle(origin.Location, new Size(origin.Width + 12, origin.Height + 10));
                Call(window, "ResizeTextAnnotationShape", shape, resized, origin, 20);
                using (var resizedExport = (Bitmap)Call(window, "CloneCurrentBitmapWithAnnotations", source))
                {
                    var resizedPixels = RedBounds(resizedExport);
                    Require(resizedPixels.Width > native.Width + 5 && resizedPixels.Height > native.Height + 5,
                        "Text resize handles did not resize the glyphs in export");
                    Require((int)Get(shape, "FontSize") > 20, "Font-size control did not reflect text resize");
                }
            }
        }
    }

    private static Rectangle RedBounds(Bitmap image)
    {
        var left = image.Width; var top = image.Height; var right = -1; var bottom = -1;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image.GetPixel(x, y);
            if (pixel.R < 160 || pixel.R < pixel.G * 2 || pixel.R < pixel.B * 2) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}

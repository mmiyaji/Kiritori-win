using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class AnnotationBoundsProbe
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    private static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var context = new ApplicationContext())
        {
            EventHandler start = null;
            start = (sender, e) =>
            {
                Application.Idle -= start;
                try { Run(args); }
                catch (Exception error) { Console.Error.WriteLine(error.GetBaseException()); Environment.ExitCode = 1; }
                finally { context.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run(context);
        }
    }
    private static void Run(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        var snapType = assembly.GetType("Kiritori.SnapWindow", true);
        var geometry = snapType.GetNestedType("AnnotationDisplayGeometry", BindingFlags.NonPublic);
        var displayMethod = geometry.GetMethod("TryGetDisplayImageRect", BindingFlags.Public | BindingFlags.Static);
        var nativeRectProperty = typeof(PictureBox).GetProperty("ImageRectangle", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(nativeRectProperty != null, "Framework PictureBox image rectangle is unavailable");
        // Compare against the rectangle used by the actual installed WinForms renderer,
        // including odd sizes that distinguish truncation from rounding.
        using (var original = new Bitmap(301, 203))
        using (var display = new Bitmap(607, 319))
        using (var picture = new PictureBox { Size = new Size(613, 421), Image = display })
        {
            foreach (var mode in new[] { PictureBoxSizeMode.Normal, PictureBoxSizeMode.CenterImage, PictureBoxSizeMode.StretchImage, PictureBoxSizeMode.Zoom, PictureBoxSizeMode.AutoSize })
            foreach (var padding in new[] { Padding.Empty, new Padding(3, 5, 7, 11) })
            foreach (var border in new[] { BorderStyle.None, BorderStyle.FixedSingle })
            {
                picture.SizeMode = mode;
                picture.Padding = padding;
                picture.BorderStyle = border;
                if (mode != PictureBoxSizeMode.AutoSize) picture.Size = new Size(613, 421);
                var parameters = new object[] { picture, original, Rectangle.Empty };
                Require((bool)displayMethod.Invoke(null, parameters), "Image geometry was unavailable");
                var actual = (Rectangle)nativeRectProperty.GetValue(picture, null);
                Require(actual == (Rectangle)parameters[2], "PictureBox geometry mismatch: " + mode + " padding=" + padding + "; renderer=" + actual + "; annotations=" + parameters[2]);
            }
        }
        Console.WriteLine("PASS: all PictureBox modes, padding, borders and fractional dimensions match the framework renderer");
        bool highDpi = false;
        foreach (var screen in Screen.AllScreens)
        using (var snap = (Form)Activator.CreateInstance(snapType, new object[] { null }))
        {
            snap.StartPosition = FormStartPosition.Manual;
            snap.Location = new Point(screen.WorkingArea.Left + 30, screen.WorkingArea.Top + 30);
            var bitmap = new Bitmap(300, 200);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.White);
            Call(snap, "SetImageAndResetZoom", bitmap, null);
            snap.Show();
            Application.DoEvents();
            highDpi |= GetDpiForWindow(snap.Handle) > 96;
            var picture = (PictureBox)Field(snap, "pictureBox1");
            foreach (var size in new[] { new Size(600, 400), new Size(600, 200), new Size(300, 600) })
            {
                Call(snap, "BeginResizePreview");
                snap.ClientSize = size;
                VerifyCornerDrawing(snap, picture, nativeRectProperty, "resize preview " + size);
                Call(snap, "SnapWindow_ResizeEnd", snap, EventArgs.Empty);
                Require(picture.SizeMode == PictureBoxSizeMode.Normal, "Resize commit did not produce the real Normal-mode display bitmap");
                VerifyCornerDrawing(snap, picture, nativeRectProperty, "resize committed " + size);
                VerifyCornerDrawing(snap, picture, nativeRectProperty, "arrow after resize " + size, true);
            }
            // A settled viewport bitmap, temporarily smaller than its client, must not
            // pretend the blank area to its right/bottom is part of the image.
            Call(snap, "BeginResizePreview");
            snap.ClientSize = new Size(300, 200);
            Call(snap, "SnapWindow_ResizeEnd", snap, EventArgs.Empty);
            snap.ClientSize = new Size(500, 400);
            Application.DoEvents();
            Call(snap, "ClearAnnotations");
            Call(snap, "EnterAnnotationMode");
            Draw(picture, new Point(380, 260), new Point(460, 350));
            Require(((IList)Field(snap, "_annotations")).Count == 0, "Blank Normal-mode area accepted an annotation");
            // Real Zoom letterboxing stays excluded, even though committed resize fills
            // the entire viewport with an intentionally non-proportional display bitmap.
            Call(snap, "SetImageAndResetZoom", new Bitmap(bitmap), null);
            snap.ClientSize = new Size(600, 600);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            Application.DoEvents();
            Call(snap, "EnterAnnotationMode");
            Draw(picture, new Point(100, 20), new Point(180, 70));
            Require(((IList)Field(snap, "_annotations")).Count == 0, "Real Zoom letterbox accepted an annotation");
            VerifyCornerDrawing(snap, picture, nativeRectProperty, "letterboxed Zoom");

            Call(snap, "StartZoomAnimationToScale", 2.0F, 160);
            var deadline = DateTime.UtcNow.AddSeconds(2);
            int animationFrames = 0;
            while ((bool)Field(snap, "_isZoomAnimating") && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                var parameters = new object[] { picture, Field(snap, "main_image"), Rectangle.Empty };
                Require((bool)displayMethod.Invoke(null, parameters), "Animation has no image geometry");
                Require((Rectangle)parameters[2] == (Rectangle)nativeRectProperty.GetValue(picture, null), "Animated zoom image and hit region disagree");
                animationFrames++;
                System.Threading.Thread.Sleep(12);
            }
            Require(!(bool)Field(snap, "_isZoomAnimating") && animationFrames > 0, "Zoom animation did not complete");
            VerifyCornerDrawing(snap, picture, nativeRectProperty, "completed animated Zoom");
            Console.WriteLine("PASS: {0}, native DPI {1}: image-space/export positions survive resize, margins and animated zoom", screen.DeviceName, GetDpiForWindow(snap.Handle));
        }
        if (args.Length > 1 && args[1] == "--require-high-dpi") Require(highDpi, "A high-DPI monitor is required");
    }
    private static void VerifyCornerDrawing(Form snap, PictureBox picture, PropertyInfo rectProperty, string scenario, bool arrow = false)
    {
        Call(snap, "ClearAnnotations");
        Call(snap, "EnterAnnotationMode");
        var tool = snap.GetType().GetNestedType("AnnotationTool", BindingFlags.NonPublic);
        Call(snap, "SetAnnotationTool", Enum.Parse(tool, arrow ? "Arrow" : "Rectangle"));
        Call(snap, "SetAnnotationColor", Color.Red, Color.FromArgb(64, Color.Red));
        var rect = (Rectangle)rectProperty.GetValue(picture, null);
        var start = new Point(rect.Left + (int)Math.Round(rect.Width * .8), rect.Top + (int)Math.Round(rect.Height * .8));
        var end = new Point(rect.Left + (int)Math.Round(rect.Width * .96), rect.Top + (int)Math.Round(rect.Height * .96));
        Draw(picture, start, end);
        var shapes = (IList)Field(snap, "_annotations");
        Require(shapes.Count == 1, scenario + ": lower-right image area rejected drawing");
        var shape = shapes[0];
        var shapeStart = (Point)Field(shape, "Start");
        var shapeEnd = (Point)Field(shape, "End");
        Require(Math.Abs(shapeStart.X - 240) <= 1 && Math.Abs(shapeStart.Y - 160) <= 1, scenario + ": wrong source start " + shapeStart);
        Require(Math.Abs(shapeEnd.X - 288) <= 1 && Math.Abs(shapeEnd.Y - 192) <= 1, scenario + ": wrong source end " + shapeEnd);
        using (var exported = (Bitmap)Call(snap, "GetCurrentBitmapClone"))
        {
            Require(exported.Size == new Size(300, 200), scenario + ": export resized the original image");
            var center = exported.GetPixel(264, 176);
            Require(center.R > center.G && center.G == center.B && center.G < 240, scenario + ": export has no annotation at expected source position");
            Require(exported.GetPixel(200, 140).ToArgb() == Color.White.ToArgb(), scenario + ": export moved annotation outside the expected region");
        }
        SetField(snap, "_overlayText", null);
        using (var rendered = new Bitmap(picture.Width, picture.Height))
        {
            picture.DrawToBitmap(rendered, picture.ClientRectangle);
            var center = rendered.GetPixel((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Require(center.R > center.G && center.G == center.B && center.G < 240, scenario + ": annotation is not painted where the mouse drew it");
        }
        Console.WriteLine("  PASS: {0}: display={1}, source={2}..{3}", scenario, rect, shapeStart, shapeEnd);
    }
    private static void Draw(PictureBox picture, Point start, Point end)
    {
        typeof(Control).GetMethod("OnMouseDown", Flags).Invoke(picture, new object[] { new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0) });
        typeof(Control).GetMethod("OnMouseMove", Flags).Invoke(picture, new object[] { new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0) });
        typeof(Control).GetMethod("OnMouseUp", Flags).Invoke(picture, new object[] { new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0) });
    }
}

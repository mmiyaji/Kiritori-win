using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Compile with Kiritori/Properties/app.manifest and copy Kiritori/App.config
// beside the executable to mirror the production WinForms DPI initialization.
internal static class WindowDpiProbe
{
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error)
        {
            var root = error.GetBaseException();
            Console.Error.WriteLine("FAIL: {0}: {1}", root.GetType().Name, root.Message);
            Console.Error.WriteLine(root.StackTrace);
            Environment.ExitCode = 1;
        }
    }
    private static void Run(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var assembly = Assembly.LoadFrom(args[0]);
        var type = assembly.GetType("Kiritori.Views.LiveCapture.LivePreviewWindow", true);
        var flags = Flags;
        foreach (var screen in Screen.AllScreens)
        {
            using (var form = (Form)Activator.CreateInstance(type))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(screen.Bounds.Left + 50, screen.Bounds.Top + 50);
                type.GetProperty("SourceRectPhysical").SetValue(form, new Rectangle(form.Left, form.Top, 600, 300), null);
                var handle = form.Handle;
                var dpi = GetDpiForWindow(handle);
                type.GetProperty("CaptureRect").SetValue(form, new Rectangle(form.Left, form.Top, (int)(600 * 96.0 / dpi), (int)(300 * 96.0 / dpi)), null);
                var size = (Size)type.GetMethod("GetDesiredClientLogical", flags).Invoke(form, null);
                type.GetMethod("ResizeToKeepClient", flags).Invoke(form, new object[] { size });
                Console.WriteLine("{0}: nativeDpi={1}, managedDpi={2}, source=600x300, actualClient={3}", screen.DeviceName, dpi, form.DeviceDpi, form.ClientSize);
                ((ToolStripMenuItem)Field(form, "_miTitlebar")).Checked = false;
                Call(form, "HideCaptionBarTemporarily");
                Call(form, "ResizeToKeepClient", new Size(800, 450));
                var origin = form.PointToScreen(Point.Empty);
                for (int i = 0; i < 2; i++)
                {
                    Call(form, "ToggleTitlebar");
                    Application.DoEvents();
                    Require(form.ClientSize == new Size(800, 450), "Titlebar changed client size: " + form.ClientSize);
                    Require(form.PointToScreen(Point.Empty) == origin, "Titlebar moved client origin");
                }
                Console.WriteLine("  PASS: real titlebar handlers preserve 800x450 and client origin in both directions");
            }
        }
        if (args.Length > 1 && args[1] == "--require-high-dpi")
        {
            bool found = false;
            foreach (var screen in Screen.AllScreens)
                using (var form = new Form())
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(screen.Bounds.Left + 50, screen.Bounds.Top + 50);
                    if (GetDpiForWindow(form.Handle) > 96) found = true;
                }
            Require(found, "High-DPI monitor is required for this run");
        }
        var snapType = assembly.GetType("Kiritori.SnapWindow", true);
        using (var snap = (Form)Activator.CreateInstance(snapType, new object[] { null }))
        {
            var handle = snap.Handle;
            snap.StartPosition = FormStartPosition.Manual;
            snap.Location = new Point(-20000, -20000);
            snap.Show();
            snap.SetBounds(0, 0, 0, 0);
            Require(snap.Width >= 100 && snap.Height >= 50, "Snapshot became unusably small: " + snap.Size);
            Console.WriteLine("PASS: snapshot minimum size = " + snap.Size);
            using (var bitmap = new Bitmap(300, 200))
            {
                SetField(snap, "_originalImage", bitmap);
                Call(snap, "zoomIn");
                Call(snap, "zoomOff");
                Thread.Sleep(180);
                Application.DoEvents();
                Require(Math.Abs((float)Field(snap, "_scale") - 1f) < .001f, "Pending zoom overwrote 100% reset");
                Console.WriteLine("PASS: 100% zoom survives pending timer messages");
                foreach (var size in new[] { new Size(300, 200), new Size(100, 50), new Size(800, 450) })
                {
                    snap.ClientSize = size;
                    Call(snap, "EnterAnnotationMode");
                    var palette = (Panel)Field(snap, "_annotationPalette");
                    Require(snap.ClientRectangle.Contains(palette.Bounds), "Palette is outside snapshot at " + size);
                    foreach (var name in new[] { "_annotationClearButton", "_annotationUndoButton", "_annotationDoneButton" })
                    {
                        var button = (Button)Field(snap, name);
                        palette.ScrollControlIntoView(button);
                        Require(palette.ClientRectangle.IntersectsWith(button.Bounds), "Action is unreachable: " + name + " at " + size);
                    }
                    Console.WriteLine("PALETTE: client={0}, palette={1}", size, palette.Bounds);
                    ((Button)Field(snap, "_annotationDoneButton")).PerformClick();
                    Require(!(bool)Field(snap, "_annotationMode"), "Done button failed to exit annotation mode");
                }
                SetField(snap, "_originalImage", null);
            }
        }
        var settingsType = assembly.GetType("Kiritori.Properties.Settings", true);
        var settings = settingsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
        var setting = settingsType.GetProperty("FixedCropWidth");
        var original = (int)setting.GetValue(settings, null);
        using (var pref = (Form)Activator.CreateInstance(assembly.GetType("Kiritori.PrefForm", true)))
        {
            try
            {
                Call(pref, "BuildAdvancedTab");
                object edited = null;
                foreach (var row in (IEnumerable)Field(pref, "_rows"))
                    if ((string)row.GetType().GetProperty("Name").GetValue(row, null) == "FixedCropWidth") edited = row;
                Require(edited != null, "Advanced row missing");
                var grid = (DataGridView)Field(pref, "_gridSettings");
                var page = (TabPage)Field(pref, "tabAdvanced");
                ((TabControl)page.Parent).SelectedTab = page;
                var prefHandle = pref.Handle;
                var gridHandle = grid.Handle;
                DataGridViewRow editedGridRow = null;
                foreach (DataGridViewRow row in grid.Rows)
                    if (object.ReferenceEquals(row.DataBoundItem, edited)) editedGridRow = row;
                Require(editedGridRow != null, "Advanced grid row missing");
                foreach (DataGridViewColumn column in grid.Columns)
                    if (column.DataPropertyName == "ValueString") grid.CurrentCell = editedGridRow.Cells[column.Index];
                Require(grid.BeginEdit(false), "Advanced grid edit could not begin");
                ((TextBox)grid.EditingControl).Text = "777";
                Call(pref, "SaveSettingsAndUpdateBaseline");
                settingsType.GetMethod("Reload").Invoke(settings, null);
                Require((int)setting.GetValue(settings, null) == 777, "Advanced row was not persisted");
                Console.WriteLine("PASS: shared save commits active Advanced cell and persists it through Settings.Reload");
            }
            finally
            {
                setting.SetValue(settings, original, null);
                settingsType.GetMethod("Save").Invoke(settings, null);
                SetField(pref, "_isDirty", false);
            }
        }
        // Inspect the callback actually compiled into the application. Its async
        // completion must never overwrite the mode of a later selection session.
        var screenType = assembly.GetType("Kiritori.ScreenWindow", true);
        var modeToken = screenType.GetField("_ocr_mode", Flags).MetadataToken;
        int callbacks = 0;
        foreach (var nested in screenType.GetNestedTypes(BindingFlags.NonPublic))
            foreach (var method in nested.GetMethods(Flags))
                if (method.Name.Contains("ScreenWindow_MouseUp"))
                {
                    var async = (AsyncStateMachineAttribute)Attribute.GetCustomAttribute(method, typeof(AsyncStateMachineAttribute));
                    if (async == null) continue;
                    callbacks++;
                    var il = async.StateMachineType.GetMethod("MoveNext", Flags).GetMethodBody().GetILAsByteArray();
                    for (int i = 0; i + 4 < il.Length; i++)
                        Require(!(il[i] == 0x7d && BitConverter.ToInt32(il, i + 1) == modeToken), "OCR completion still writes the shared selection mode");
                }
        Require(callbacks > 0, "OCR async callback was not found");
        Console.WriteLine("PASS: compiled OCR completion callback cannot overwrite selection mode (IL check)");
    }
}

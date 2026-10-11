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
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
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
    private static void Run(string[] args)
    {
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
                // Match startup: the saved style is captured before WS_VISIBLE.
                Call(form, "HideCaptionBarTemporarily");
                form.Show();
                Application.DoEvents();
                Call(form, "ResizeToKeepClient", new Size(800, 450));
                var origin = form.PointToScreen(Point.Empty);
                for (int i = 0; i < 2; i++)
                {
                    Call(form, "ToggleTitlebar");
                    Application.DoEvents();
                    Require((GetWindowLong(form.Handle, -16) & 0x10000000) != 0, "Titlebar restore hid the window (WS_VISIBLE lost)");
                    Require(form.ClientSize == new Size(800, 450), "Titlebar changed client size: " + form.ClientSize);
                    Require(form.PointToScreen(Point.Empty) == origin, "Titlebar moved client origin");
                }
                Console.WriteLine("  PASS: real titlebar handlers preserve visibility, 800x450 and client origin in both directions");
                form.Close();
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
                foreach (var screen in Screen.AllScreens)
                foreach (var size in new[] { new Size(300, 200), new Size(100, 50), new Size(800, 450) })
                {
                    var area = screen.WorkingArea;
                    snap.Location = new Point(area.Left + 40, area.Top + 40);
                    snap.ClientSize = size;
                    Application.DoEvents();
                    var originalClient = snap.ClientSize;
                    Call(snap, "EnterAnnotationMode");
                    Application.DoEvents();
                    var palette = (Form)Field(snap, "_annotationPalette");
                    Require(palette.Visible && palette.Owner == snap && !palette.ShowInTaskbar, "Palette is not a visible owned auxiliary window");
                    Require(snap.ClientSize == originalClient, "Palette resized the captured image");
                    Require(palette.ClientSize.Height <= 50 * GetDpiForWindow(palette.Handle) / 96, "Palette is no longer a compact single row: " + palette.ClientSize);
                    Require(area.Contains(palette.Bounds), "Palette leaves monitor work area at " + size + ": " + palette.Bounds);
                    Require(!snap.Bounds.IntersectsWith(palette.Bounds), "Palette covers the capture at " + size);
                    var nativePaletteDpi = GetDpiForWindow(palette.Handle);
                    Require(nativePaletteDpi == GetDpiForWindow(snap.Handle), "Palette is on a different DPI monitor than owner");
                    var toolButtons = (RadioButton[])Field(snap, "_annotationToolButtons");
                    Require(Math.Abs(toolButtons[0].Width - 30 * nativePaletteDpi / 96.0) <= 1, "Palette icons did not scale to native DPI");
                    var widthInput = (NumericUpDown)Field(snap, "_annotationWidthInput");
                    Require(Math.Abs(widthInput.Font.Size - 12 * nativePaletteDpi / 96.0) <= 1, "Palette numeric text did not scale to native DPI");
                    foreach (var choices in new[] { toolButtons, (RadioButton[])Field(snap, "_annotationColorButtons") })
                    foreach (var choice in choices)
                    {
                        var bounds = palette.RectangleToClient(choice.RectangleToScreen(choice.ClientRectangle));
                        Require(palette.ClientRectangle.Contains(bounds) && choice.Parent.ClientRectangle.Contains(choice.Bounds), "Palette choice clipped at native DPI: " + choice.Name);
                        Require(!string.IsNullOrEmpty(choice.AccessibleName), "Icon has no accessible name: " + choice.Name);
                    }
                    foreach (var name in new[] { "_annotationClearButton", "_annotationUndoButton", "_annotationDoneButton" })
                    {
                        var button = (Button)Field(snap, name);
                        var buttonRect = palette.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                        Require(palette.ClientRectangle.Contains(buttonRect), "Action is clipped: " + name + " at " + size);
                        Require(button.TabStop, "Action is not keyboard accessible: " + name);
                    }
                    var previousPalette = palette.Location;
                    snap.Left += palette.Width / 2;
                    Application.DoEvents();
                    Require(palette.Location != previousPalette, "Palette did not follow capture movement");
                    Require(snap.ClientSize == originalClient, "Palette movement resized capture");
                    Console.WriteLine("PALETTE: monitor={0}, client={1}, palette={2}, dpi={3}", screen.DeviceName, size, palette.Bounds, nativePaletteDpi);
                    ((Button)Field(snap, "_annotationDoneButton")).PerformClick();
                    Require(!(bool)Field(snap, "_annotationMode") && !palette.Visible, "Done failed to finish and hide palette");
                }
                snap.ClientSize = new Size(300, 200);
                var workArea = Screen.FromControl(snap).WorkingArea;
                snap.Location = new Point(workArea.Left + 40, workArea.Bottom - snap.Height - 10);
                Call(snap, "EnterAnnotationMode");
                Application.DoEvents();
                var annotationPalette = (Form)Field(snap, "_annotationPalette");
                Require(annotationPalette.Bottom <= snap.Top, "Palette did not move above capture at bottom edge");
                snap.TopMost = true;
                Application.DoEvents();
                Require((GetWindowLong(annotationPalette.Handle, -20) & 8) != 0, "Owned palette did not follow topmost owner");
                snap.TopMost = false;
                Application.DoEvents();
                Require((GetWindowLong(annotationPalette.Handle, -20) & 8) == 0, "Palette remained topmost after owner changed");
                snap.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                Require(!annotationPalette.Visible, "Palette remained visible for minimized owner");
                snap.WindowState = FormWindowState.Normal;
                Application.DoEvents();
                Require(annotationPalette.Visible, "Palette failed to restore with owner");
                snap.Hide();
                Require(!annotationPalette.Visible, "Palette remained visible for hidden owner");
                snap.Show();
                Application.DoEvents();
                Require(annotationPalette.Visible, "Palette failed to restore with shown owner");

                var toolType = snapType.GetNestedType("AnnotationTool", BindingFlags.NonPublic);
                var rectangleTool = Enum.Parse(toolType, "Rectangle");
                Call(snap, "SetAnnotationTool", rectangleTool);
                var shape = Call(snap, "CreateAnnotationShape", rectangleTool, new Point(10, 10), new Point(100, 80));
                Call(snap, "FinalizeCreatedAnnotation", shape);
                var widths = (NumericUpDown)Field(snap, "_annotationWidthInput");
                widths.Value = 6;
                Require((int)Field(shape, "StrokeWidth") == 6, "Width did not update selected annotation");
                Call(snap, "ShowAnnotationStyleMenu");
                var styles = (ContextMenuStrip)Field(snap, "_annotationStyleMenu");
                ((ToolStripMenuItem)styles.Items[1]).PerformClick();
                styles.Hide();
                Require(Field(shape, "RectangleStyle").ToString() == "Outline", "Style did not update selected annotation");
                var colors = (RadioButton[])Field(snap, "_annotationColorButtons");
                colors[1].Checked = true;
                Require(((Color)Field(shape, "StrokeColor")).ToArgb() == Color.FromArgb(88, 166, 255).ToArgb(), "Color did not update selected annotation");
                Require(((Color)Field(shape, "FillColor")) == Color.Transparent, "Outline color change introduced fill");
                var buttons = (RadioButton[])Field(snap, "_annotationToolButtons");
                buttons[2].Checked = true;
                Call(snap, "ShowAnnotationStyleMenu");
                styles.Hide();
                Require((int)Field(snap, "_selectedAnnotationIndex") == -1 && styles.Items.Count == 4, "Arrow choice did not switch to new arrow settings");
                widths.Value = 8;
                var arrow = Call(snap, "CreateAnnotationShape", Enum.Parse(toolType, "Arrow"), new Point(20, 20), new Point(160, 120));
                Require((int)Field(arrow, "StrokeWidth") == 8, "Width did not apply to new annotation");
                Call(snap, "FinalizeCreatedAnnotation", arrow);
                ((Button)Field(snap, "_annotationUndoButton")).PerformClick();
                Require(((IList)Field(snap, "_annotations")).Count == 1, "Remove last did not remove exactly one annotation");
                ((Button)Field(snap, "_annotationClearButton")).PerformClick();
                Require(((IList)Field(snap, "_annotations")).Count == 0, "Clear did not remove annotations");
                Require(!((Button)Field(snap, "_annotationUndoButton")).Enabled, "Empty undo remains enabled");
                annotationPalette.Close();
                Require(!(bool)Field(snap, "_annotationMode") && !annotationPalette.Visible && !annotationPalette.IsDisposed, "Palette close did not finish editing safely");
                Call(snap, "EnterAnnotationMode");
                Require(annotationPalette.Visible, "Closed palette could not reopen");
                Console.WriteLine("PASS: palette positioning, ownership, lifecycle, selected/new styles, width, color, undo, clear and reopen");
                SetField(snap, "_originalImage", null);
            }
        }
        foreach (var screen in Screen.AllScreens)
        foreach (var borderStyle in new[] { FormBorderStyle.FixedToolWindow, FormBorderStyle.FixedSingle })
        using (var freshSnap = (Form)Activator.CreateInstance(snapType, new object[] { null }))
        {
            freshSnap.StartPosition = FormStartPosition.Manual;
            freshSnap.Location = new Point(screen.WorkingArea.Left + 400, screen.WorkingArea.Top + 250);
            Call(freshSnap, "SetImageAndResetZoom", new Bitmap(300, 200), null);
            var freshPalette = (Form)Field(freshSnap, "_annotationPalette");
            freshPalette.ShowInTaskbar = true;
            freshPalette.FormBorderStyle = borderStyle;
            freshSnap.Show();
            Call(freshSnap, "EnterAnnotationMode");
            Application.DoEvents();
            var palette = (Form)Field(freshSnap, "_annotationPalette");
            palette.Activate();
            foreach (var focusTarget in new Control[] { ((RadioButton[])Field(freshSnap, "_annotationToolButtons"))[0], (Control)Field(freshSnap, "_annotationWidthInput"), (Control)Field(freshSnap, "_annotationDoneButton") })
            {
                focusTarget.Focus();
                Application.DoEvents();
                Require(!palette.HorizontalScroll.Visible && !palette.VerticalScroll.Visible && palette.AutoScrollPosition == Point.Empty, "Palette scrolled when focusing " + focusTarget.Name);
            }
            Console.WriteLine("FRESH PALETTE: dpi={0}, client={1}, scroll={2}, H={3}, V={4}", GetDpiForWindow(palette.Handle), palette.ClientSize, palette.AutoScrollPosition, palette.HorizontalScroll.Visible, palette.VerticalScroll.Visible);
            Require(!palette.HorizontalScroll.Visible && !palette.VerticalScroll.Visible && palette.AutoScrollPosition == Point.Empty, "Fresh palette gained scrollbars after focus");
        }
        var locationMethod = snapType.GetMethod("GetAnnotationPaletteLocation", BindingFlags.Static | BindingFlags.NonPublic);
        var areas = new[] { new Rectangle(0, 0, 1920, 1040), new Rectangle(-1920, -1080, 1920, 1040) };
        foreach (var area in areas)
        {
            var paletteSize = new Size(600, 220);
            foreach (var owner in new[]
            {
                new Rectangle(area.Left + 100, area.Top + 100, 300, 200),
                new Rectangle(area.Left + 100, area.Bottom - 210, 300, 200),
                new Rectangle(area.Left + 100, area.Top, 300, area.Height),
                new Rectangle(area.Left + 1600, area.Top + 100, 300, 200)
            })
            {
                var position = (Point)locationMethod.Invoke(null, new object[] { owner, paletteSize, area, 8 });
                var placed = new Rectangle(position, paletteSize);
                Require(area.Contains(placed) && !owner.IntersectsWith(placed), "Placement cannot find available external space: " + owner);
            }
            var fullScreenPosition = (Point)locationMethod.Invoke(null, new object[] { area, paletteSize, area, 8 });
            Require(area.Contains(new Rectangle(fullScreenPosition, paletteSize)), "Fullscreen fallback leaves monitor work area");
        }
        using (var disposableSnap = (Form)Activator.CreateInstance(snapType, new object[] { null }))
        {
            var ownedPalette = (Form)Field(disposableSnap, "_annotationPalette");
            disposableSnap.Dispose();
            Require(ownedPalette.IsDisposed, "Disposing capture leaked palette");
        }
        Console.WriteLine("PASS: external placement for negative monitor coordinates and no-space fallback; palette disposal");
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

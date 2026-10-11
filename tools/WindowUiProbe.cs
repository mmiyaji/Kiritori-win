using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class WindowUiProbe
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var assembly = Assembly.LoadFrom(args[0]);
        if (args.Length > 1 && args[1] == "--live")
        {
            var liveType = assembly.GetType("Kiritori.Views.LiveCapture.LivePreviewWindow", true);
            using (var live = (Form)Activator.CreateInstance(liveType))
            {
                live.ShowInTaskbar = true;
                live.Text = "Kiritori live UI verification";
                live.StartPosition = FormStartPosition.Manual;
                var rect = new Rectangle(Screen.PrimaryScreen.Bounds.Left + 800, Screen.PrimaryScreen.Bounds.Top + 400, 600, 300);
                liveType.GetProperty("SourceRectPhysical").SetValue(live, rect, null);
                var dpiType = assembly.GetType("Kiritori.Helpers.DpiUtil", true);
                var logical = dpiType.GetMethod("PhysicalToLogical").Invoke(null, new object[] { rect });
                liveType.GetProperty("CaptureRect").SetValue(live, logical, null);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                ((ToolStripMenuItem)liveType.GetField("_miTitlebar", flags).GetValue(live)).Checked = false;
                Application.Run(live);
            }
            return;
        }
        var snapType = assembly.GetType("Kiritori.SnapWindow", true);
        using (var snap = (Form)Activator.CreateInstance(snapType, new object[] { null }))
        {
            // Discovery only: the production form normally hides its taskbar item.
            snap.ShowInTaskbar = true;
            snap.Text = "Kiritori UI verification";
            snap.StartPosition = FormStartPosition.Manual;
            var monitorIndex = -1;
            foreach (var argument in args)
                if (argument.StartsWith("--monitor-index="))
                    monitorIndex = int.Parse(argument.Substring("--monitor-index=".Length));
            var screen = monitorIndex >= 0 ? Screen.AllScreens[monitorIndex] : Screen.PrimaryScreen;
            snap.Location = new Point(screen.WorkingArea.Left + 400, screen.WorkingArea.Top + 250);
            var bitmap = new Bitmap(300, 200);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Arial", 16))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("WINDOW TEST", font, Brushes.Black, 25, 90);
            }
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            snapType.GetMethod("SetImageAndResetZoom", flags).Invoke(snap, new object[] { bitmap, null });
            // Discover the production owned palette through Computer Use as well.
            // This changes only the isolated test host, never the application's UI.
            var palette = snapType.GetField("_annotationPalette", flags).GetValue(snap) as Form;
            if (palette != null)
            {
                palette.ShowInTaskbar = true;
                palette.FormBorderStyle = FormBorderStyle.FixedSingle;
            }
            Application.Run(snap);
        }
    }
}

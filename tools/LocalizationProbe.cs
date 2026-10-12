using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class LocalizationProbe
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static Assembly _assembly;
    private static Type _snapType;
    private static Type _srType;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        _assembly = Assembly.LoadFrom(args[0]);
        _snapType = _assembly.GetType("Kiritori.SnapWindow", true);
        _srType = _assembly.GetType("Kiritori.Helpers.SR", true);
        using (var context = new ApplicationContext())
        {
            EventHandler start = null;
            start = (sender, e) =>
            {
                Application.Idle -= start;
                try
                {
                    VerifyCultureSettingsRoundTrip();
                    VerifyOpenWindowRelocalizes();
                    Console.WriteLine("PASS: isolated UICulture save/reload, EN/JA open-window menus/tooltips, and localized text editor dialogs");
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

    private static object Get(object target, string name)
    {
        return target.GetType().GetField(name, Instance).GetValue(target);
    }

    private static object Call(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, Instance).Invoke(target, arguments);
    }

    private static string Text(string key)
    {
        return (string)_srType.GetMethod("T", Static, null, new[] { typeof(string) }, null).Invoke(null, new object[] { key });
    }

    private static void SetCulture(string culture)
    {
        _srType.GetMethod("SetCulture", Static).Invoke(null, new object[] { culture });
        Application.DoEvents();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyCultureSettingsRoundTrip()
    {
        var settingsType = _assembly.GetType("Kiritori.Properties.Settings", true);
        var defaultSettings = settingsType.GetProperty("Default", Static).GetValue(null, null);
        var culture = settingsType.GetProperty("UICulture");
        var save = settingsType.GetMethod("Save", Instance);
        var reload = settingsType.GetMethod("Reload", Instance);
        var original = (string)culture.GetValue(defaultSettings, null);
        try
        {
            culture.SetValue(defaultSettings, "ja", null);
            save.Invoke(defaultSettings, null);
            reload.Invoke(defaultSettings, null);
            Require((string)culture.GetValue(defaultSettings, null) == "ja", "Japanese UICulture did not survive settings save/reload");
            culture.SetValue(defaultSettings, "en", null);
            save.Invoke(defaultSettings, null);
            reload.Invoke(defaultSettings, null);
            Require((string)culture.GetValue(defaultSettings, null) == "en", "English UICulture did not survive settings save/reload");
        }
        finally
        {
            culture.SetValue(defaultSettings, original, null);
            save.Invoke(defaultSettings, null);
            reload.Invoke(defaultSettings, null);
        }
    }

    private static void VerifyOpenWindowRelocalizes()
    {
        SetCulture("en");
        var window = (Form)Activator.CreateInstance(_snapType, new object[] { null });
        Call(window, "SetImageAndResetZoom", new Bitmap(80, 60), null);
        window.Show();
        Call(window, "EnterAnnotationMode");
        Application.DoEvents();

        try
        {
            VerifyWindowText(window, "en");
            SetCulture("ja");
            VerifyWindowText(window, "ja");
            SetCulture("en");
            VerifyWindowText(window, "en");

            VerifyTextEditorOpensLocalized(window, "en");
            VerifyTextEditorOpensLocalized(window, "ja");
        }
        finally
        {
            SetCulture("en");
            window.Close();
            window.Dispose();
        }
    }

    private static void VerifyWindowText(Form window, string culture)
    {
        var effectMenu = (ToolStripMenuItem)Get(window, "_imageEffectMenu");
        Require(effectMenu.Text == Text("ImageEffect.Menu"), culture + " image effect menu title did not update");
        var effectItems = effectMenu.DropDownItems.OfType<ToolStripMenuItem>().ToArray();
        var effectKeys = new[] { "ImageEffect.None", "ImageEffect.Mosaic", "ImageEffect.Sepia", "ImageEffect.Grayscale" };
        for (int i = 0; i < effectKeys.Length; i++)
            Require(effectItems[i].Text == Text(effectKeys[i]), culture + " image effect menu item did not update: " + effectKeys[i]);
        var strengthKeys = new[] { "ImageEffect.MosaicWeak", "ImageEffect.MosaicMedium", "ImageEffect.MosaicStrong" };
        var strengthItems = effectItems[1].DropDownItems.OfType<ToolStripMenuItem>().ToArray();
        for (int i = 0; i < strengthKeys.Length; i++)
            Require(strengthItems[i].Text == Text(strengthKeys[i]), culture + " mosaic strength did not update: " + strengthKeys[i]);

        var colorMenu = (ContextMenuStrip)Get(window, "_annotationColorMenu");
        var more = colorMenu.Items.OfType<ToolStripMenuItem>().Single(item => item.Name == "Annotation.Color.More");
        Require(more.Text == Text("Annotation.Color.More") && more.AccessibleName == Text("Annotation.Color.More"),
            culture + " color dropdown label/accessibility did not update");
        Require(more.AccessibleDescription == Text("Annotation.Color.MoreHint"), culture + " color dropdown description did not update");
        var tips = (ToolTip)Get(window, "_annotationPaletteTips");
        var colorButton = (Control)Get(window, "_annotationColorMenuButton");
        Require(tips.GetToolTip(colorButton).Contains(Text("Annotation.Color.MoreHint")), culture + " color dropdown tooltip did not update");

        var toolButtons = (RadioButton[])Get(window, "_annotationToolButtons");
        Require(toolButtons[0].AccessibleName == Text("Annotation.Move"), culture + " annotation toolbar label did not update");
        var backgroundAccessible = (string)toolButtons[0].AccessibleDescription;
        Require(backgroundAccessible == Text("Annotation.MoveHint"), culture + " annotation toolbar hint did not update");
        var editAnnotation = (ToolStripMenuItem)Get(window, "_annotationModeMenuItem");
        Require(editAnnotation.Text == Text("Annotation.Done"), culture + " annotation menu command did not update");
        var undoItem = (ToolStripMenuItem)Get(window, "_annotationUndoMenuItem");
        Require(undoItem.Text == Text("Menu.UndoLastAnnotation"), culture + " annotation undo menu did not update");

        Call(window, "SetAnnotationTool", Enum.Parse(_snapType.GetNestedType("AnnotationTool", BindingFlags.NonPublic), "Text"));
        Call(window, "ShowAnnotationStyleMenu");
        var styleMenu = (ContextMenuStrip)Get(window, "_annotationStyleMenu");
        var styleKeys = new[] { "Annotation.Style.TextOutline", "Annotation.Style.TextOutlineBlack",
            "Annotation.Style.TextOutlineWhite", "Annotation.Style.TextBackground" };
        var styleItems = styleMenu.Items.OfType<ToolStripMenuItem>().ToArray();
        for (int i = 0; i < styleKeys.Length; i++)
            Require(styleItems[i].Text == Text(styleKeys[i]), culture + " text style menu did not localize: " + styleKeys[i]);
        styleMenu.Hide();
    }

    private static void VerifyTextEditorOpensLocalized(Form owner, string culture)
    {
        SetCulture(culture);
        Exception failure = null;
        using (var timer = new Timer { Interval = 40 })
        {
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                Form dialog = null;
                try
                {
                    dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                        !ReferenceEquals(form, owner) && form.FormBorderStyle == FormBorderStyle.FixedDialog);
                    Require(dialog != null, "Text editor dialog did not appear");
                    Require(dialog.Text == Text("Annotation.TextEditor.Title"), culture + " text editor title did not localize");
                    var labels = dialog.Controls.OfType<Label>().ToArray();
                    Require(labels.Single(label => label.Top == 130).Text == Text("Annotation.FontFamily"), culture + " font label did not localize");
                    Require(labels.Single(label => label.Top == 163 && label.Left == 12).Text == Text("Annotation.TextEditor.OutlineColor"), culture + " outline label did not localize");
                    Require(labels.Single(label => label.Top == 163 && label.Left == 265).Text == Text("Annotation.FontSize"), culture + " size label did not localize");
                    var outline = dialog.Controls.OfType<ComboBox>().Single(combo => combo.Top == 163);
                    Require(outline.Items[0].ToString() == Text("Annotation.TextEditor.OutlineBlack") &&
                        outline.Items[1].ToString() == Text("Annotation.TextEditor.OutlineWhite"), culture + " outline choices did not localize");
                    var background = dialog.Controls.OfType<CheckBox>().Single();
                    Require(background.Text == Text("Annotation.TextEditor.Background"), culture + " background checkbox did not localize");
                    var buttons = dialog.Controls.OfType<Button>().ToArray();
                    Require(buttons.Any(button => button.Text == Text("Annotation.TextEditor.Ok")) &&
                        buttons.Any(button => button.Text == Text("Annotation.TextEditor.Cancel")), culture + " text editor buttons did not localize");
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    if (dialog != null && !dialog.IsDisposed) dialog.Close();
                }
            };
            timer.Start();
            var method = _snapType.GetMethod("ShowAnnotationTextEditor", Instance);
            var arguments = new object[] { "Localization probe", 24, "Segoe UI", Color.Black, false };
            method.Invoke(owner, arguments);
            timer.Stop();
        }
        if (failure != null) throw failure;
    }
}

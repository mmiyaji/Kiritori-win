using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Standalone .NET Framework 4.8 probe. Compile with:
// %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:OcrModeProbe.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll tools\OcrModeProbe.cs
// Run with: OcrModeProbe.exe <Kiritori.exe> [--expect-old-bug]
internal static class OcrModeProbe
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const int TimeoutMilliseconds = 15000;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            Console.Error.WriteLine("Usage: OcrModeProbe.exe <Kiritori.exe> [--expect-old-bug]");
            return 2;
        }

        bool expectOldBug = args.Length == 2 && args[1] == "--expect-old-bug";
        if (args.Length == 2 && !expectOldBug)
        {
            Console.Error.WriteLine("Unknown argument: " + args[1]);
            return 2;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine("Assembly not found: " + assemblyPath);
            return 2;
        }

        IDataObject originalClipboard = null;
        bool clipboardRead = false;
        Form window = null;
        Bitmap source = null;
        SynchronizationContext previousContext = SynchronizationContext.Current;
        try
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(assemblyPath);

            try
            {
                originalClipboard = Clipboard.GetDataObject();
                clipboardRead = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not snapshot the clipboard before the probe.", ex);
            }

            Assembly app = Assembly.LoadFrom(assemblyPath);
            DisableOcrNotification(app);
            if (!HasInstalledOcrLanguage(app))
            {
                Console.WriteLine("SKIP: Windows OCR has no installed recognizer languages; skipped to avoid triggering the OCR error notification.");
                return 0;
            }

            Type windowType = app.GetType("Kiritori.ScreenWindow", true);
            window = (Form)Activator.CreateInstance(windowType, new object[] { null, null });
            IntPtr hwnd = window.Handle;
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Could not create a ScreenWindow HWND.");

            source = CreateTestBitmap();
            SetField(windowType, window, "bmp", source);
            SetField(windowType, window, "baseBmp", source);
            SetField(windowType, window, "rc", new Rectangle(0, 0, source.Width, source.Height));
            SetField(windowType, window, "isPressed", true);
            SetField(windowType, window, "_ocr_mode", true);

            var trackingContext = new TrackingSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(trackingContext);

            MethodInfo mouseUp = windowType.GetMethod("ScreenWindow_MouseUp", InstancePrivate);
            if (mouseUp == null)
                throw new MissingMethodException(windowType.FullName, "ScreenWindow_MouseUp");

            mouseUp.Invoke(window, new object[]
            {
                window,
                new MouseEventArgs(MouseButtons.Left, 1, source.Width / 2, source.Height / 2, 0)
            });

            // Model a new OCR selection reusing this ScreenWindow while the prior OCR is pending.
            SetField(windowType, window, "_ocr_mode", true);

            if (!PumpUntilAsyncVoidCompletes(trackingContext, TimeoutMilliseconds))
                throw new TimeoutException("The actual OCR async-void operation did not complete within 15 seconds.");

            if (trackingContext.CallbackException != null)
                throw new InvalidOperationException("A posted OCR continuation failed.", trackingContext.CallbackException);

            if (!Clipboard.ContainsText() ||
                Clipboard.GetText().Replace(" ", "").IndexOf("WINDOWTEST", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("The actual Windows OCR did not recognize the fixture text.");

            bool modeRemainedSet = (bool)GetField(windowType, window, "_ocr_mode");
            if (expectOldBug)
            {
                if (modeRemainedSet)
                    throw new InvalidOperationException("Expected the old OCR reset bug, but the mode remained set.");
                Console.WriteLine("PASS: reproduced the old OCR completion reset bug.");
            }
            else
            {
                if (!modeRemainedSet)
                    throw new InvalidOperationException("OCR completion cleared the mode of the new selection.");
                Console.WriteLine("PASS: OCR completion preserved the mode of the new selection.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + DescribeException(ex));
            return 1;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            try { if (window != null) window.Close(); } catch { }
            try { if (window != null) window.Dispose(); } catch { }
            try { if (source != null) source.Dispose(); } catch { }

            if (clipboardRead)
            {
                try
                {
                    if (originalClipboard == null)
                        Clipboard.Clear();
                    else
                        Clipboard.SetDataObject(originalClipboard, true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("WARNING: could not restore the clipboard: " + ex.Message);
                }
            }
        }
    }

    private static bool HasInstalledOcrLanguage(Assembly app)
    {
        Type providerType = app.GetType("Kiritori.Services.Ocr.WindowsOcrProvider", true);
        object provider = Activator.CreateInstance(providerType);
        string[] languages = (string[])providerType.GetMethod("GetSupportedLanguages").Invoke(provider, null);
        return languages != null && languages.Length > 0;
    }

    private static void DisableOcrNotification(Assembly app)
    {
        Type settingsType = app.GetType("Kiritori.Properties.Settings", true);
        object settings = settingsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
        settingsType.GetProperty("ShowNotificationOnOcr").SetValue(settings, false, null);
    }

    private static Bitmap CreateTestBitmap()
    {
        var bitmap = new Bitmap(1000, 400);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Font font = new Font("Arial", 30, FontStyle.Regular, GraphicsUnit.Point))
        using (Brush brush = new SolidBrush(Color.Black))
        {
            graphics.Clear(Color.White);
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.DrawString("KIRITORI WINDOW TEST", font, brush, new PointF(35, 140));
        }
        return bitmap;
    }

    private static void SetField(Type type, object instance, string name, object value)
    {
        FieldInfo field = type.GetField(name, InstancePrivate);
        if (field == null)
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(instance, value);
    }

    private static object GetField(Type type, object instance, string name)
    {
        FieldInfo field = type.GetField(name, InstancePrivate);
        if (field == null)
            throw new MissingFieldException(type.FullName, name);
        return field.GetValue(instance);
    }

    private static bool PumpUntilAsyncVoidCompletes(TrackingSynchronizationContext context, int timeoutMilliseconds)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < timeoutMilliseconds)
        {
            Application.DoEvents();
            context.DrainPostedCallbacks();

            if (context.OperationStartedCount > 0 &&
                context.OperationCompletedCount >= context.OperationStartedCount &&
                context.PendingCallbackCount == 0)
                return true;

            Thread.Sleep(10);
        }

        Application.DoEvents();
        context.DrainPostedCallbacks();
        return context.OperationStartedCount > 0 &&
            context.OperationCompletedCount >= context.OperationStartedCount &&
            context.PendingCallbackCount == 0;
    }

    private static string DescribeException(Exception ex)
    {
        var invocation = ex as TargetInvocationException;
        if (invocation != null && invocation.InnerException != null)
            ex = invocation.InnerException;
        return ex.GetType().Name + ": " + ex.Message;
    }

    private sealed class TrackingSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Tuple<SendOrPostCallback, object>> _posted =
            new ConcurrentQueue<Tuple<SendOrPostCallback, object>>();
        private int _started;
        private int _completed;
        private Exception _callbackException;

        public int OperationStartedCount { get { return Volatile.Read(ref _started); } }
        public int OperationCompletedCount { get { return Volatile.Read(ref _completed); } }
        public int PendingCallbackCount { get { return _posted.Count; } }
        public Exception CallbackException { get { return _callbackException; } }

        public override void OperationStarted()
        {
            Interlocked.Increment(ref _started);
        }

        public override void OperationCompleted()
        {
            Interlocked.Increment(ref _completed);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _posted.Enqueue(Tuple.Create(d, state));
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public void DrainPostedCallbacks()
        {
            Tuple<SendOrPostCallback, object> item;
            while (_posted.TryDequeue(out item))
            {
                try
                {
                    item.Item1(item.Item2);
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref _callbackException, ex, null);
                }
            }
        }
    }
}

using Kiritori.Views.LiveCapture;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori.LiveCaptureBenchmarks
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            EnablePerMonitorDpiAwareness();
            if (args.Length < 1 || (args[0] != "gdi" && args[0] != "gpu"))
            {
                Console.Error.WriteLine("Usage: LiveCaptureBackendBenchmark gdi|gpu [seconds] [width] [height] [fps]");
                return 2;
            }

            string mode = args[0];
            int seconds = ParsePositive(args, 1, 5);
            int width = ParsePositive(args, 2, 1280);
            int height = ParsePositive(args, 3, 720);
            int fps = ParsePositive(args, 4, 30);
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            width = Math.Min(width, screen.Width);
            height = Math.Min(height, screen.Height);

            using (var surface = AnimatedCaptureSurface.Start(screen.Left, screen.Top, width, height))
            {
                return RunBenchmark(mode, seconds, width, height, fps, surface.Bounds);
            }
        }

        private static int RunBenchmark(string mode, int seconds, int width, int height, int fps, Rectangle capture)
        {

            LiveCaptureBackend backend;
            if (mode == "gpu")
            {
                if (!WindowsGraphicsCaptureBackend.CanCapture(capture, out string reason))
                {
                    Console.Error.WriteLine("GPU capture unavailable: " + reason);
                    return 3;
                }
                backend = new WindowsGraphicsCaptureBackend
                {
                    CaptureRect = capture,
                    CaptureRectPhysical = capture,
                    MaxFps = fps,
                };
            }
            else
            {
                backend = new GdiCaptureBackend
                {
                    CaptureRect = capture,
                    CaptureRectPhysical = capture,
                    MaxFps = fps,
                    TransferFrameOwnership = true,
                };
            }

            long frames = 0;
            long pixels = 0;
            int measuring = 0;
            Exception failure = null;
            backend.FrameArrived += frame =>
            {
                try
                {
                    if (frame?.Bitmap == null) return;
                    if (Volatile.Read(ref measuring) == 0) return;
                    Interlocked.Increment(ref frames);
                    Interlocked.Add(ref pixels, (long)frame.Bitmap.Width * frame.Bitmap.Height);
                }
                finally
                {
                    if (frame != null && frame.TransfersOwnership)
                        frame.Bitmap?.Dispose();
                }
            };
            if (backend is WindowsGraphicsCaptureBackend gpu)
                gpu.CaptureFailed += ex => failure = ex;

            var process = Process.GetCurrentProcess();
            try
            {
                backend.Start();
                Thread.Sleep(1000);
                Interlocked.Exchange(ref frames, 0);
                Interlocked.Exchange(ref pixels, 0);
                process.Refresh();
                TimeSpan cpuStart = process.TotalProcessorTime;
                long memoryStart = process.WorkingSet64;
                var watch = Stopwatch.StartNew();
                Volatile.Write(ref measuring, 1);
                Thread.Sleep(seconds * 1000);
                Volatile.Write(ref measuring, 0);
                watch.Stop();
                process.Refresh();
                TimeSpan cpuEnd = process.TotalProcessorTime;
                long memoryEnd = process.WorkingSet64;
                long count = Interlocked.Read(ref frames);
                double actualFps = count / watch.Elapsed.TotalSeconds;
                double cpu = (cpuEnd - cpuStart).TotalMilliseconds /
                             (watch.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;

                Console.WriteLine(
                    "{\"mode\":\"" + mode +
                    "\",\"size\":\"" + width + "x" + height +
                    "\",\"targetFps\":" + fps +
                    ",\"frames\":" + count +
                    ",\"actualFps\":" + actualFps.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"cpuPercent\":" + cpu.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"workingSetDeltaBytes\":" + (memoryEnd - memoryStart) +
                    ",\"megapixelsPerSecond\":" +
                    (Interlocked.Read(ref pixels) / watch.Elapsed.TotalSeconds / 1_000_000.0)
                        .ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"failure\":" + (failure == null ? "null" : "\"" + Escape(failure.Message) + "\"") + "}");
                return failure == null && count > 0 ? 0 : 4;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 5;
            }
            finally
            {
                backend.Dispose();
            }
        }

        private static int ParsePositive(string[] args, int index, int fallback)
        {
            return index < args.Length && int.TryParse(args[index], out int value) && value > 0
                ? value
                : fallback;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnablePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
                try { SetProcessDpiAwareness(2); } catch { }
            }
        }

        private sealed class AnimatedCaptureSurface : IDisposable
        {
            private readonly Thread _thread;
            private readonly ManualResetEventSlim _ready = new ManualResetEventSlim();
            private readonly Rectangle _bounds;
            private AnimationForm _form;
            private Exception _startupError;

            private AnimatedCaptureSurface(int left, int top, int width, int height)
            {
                _bounds = new Rectangle(left, top, width, height);
                _thread = new Thread(() =>
                {
                    try
                    {
                        _form = new AnimationForm(_bounds);
                        _form.Shown += (s, e) => _ready.Set();
                        Application.Run(_form);
                    }
                    catch (Exception ex)
                    {
                        _startupError = ex;
                        _ready.Set();
                    }
                })
                {
                    IsBackground = true,
                    Name = "LiveCaptureBenchmark.Surface",
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            public Rectangle Bounds => _bounds;

            public static AnimatedCaptureSurface Start(int left, int top, int width, int height)
            {
                var surface = new AnimatedCaptureSurface(left, top, width, height);
                if (!surface._ready.Wait(5000))
                {
                    surface.Dispose();
                    throw new TimeoutException("The animated benchmark surface did not start.");
                }
                if (surface._startupError != null)
                {
                    surface.Dispose();
                    throw new InvalidOperationException("The animated benchmark surface failed to start.", surface._startupError);
                }
                return surface;
            }

            public void Dispose()
            {
                try
                {
                    if (_form != null && !_form.IsDisposed)
                        _form.BeginInvoke((Action)(() => _form.Close()));
                }
                catch { }
                try { _thread.Join(2000); } catch { }
                _ready.Dispose();
            }
        }

        private sealed class AnimationForm : Form
        {
            private readonly System.Windows.Forms.Timer _timer;
            private int _phase;

            public AnimationForm(Rectangle bounds)
            {
                Bounds = bounds;
                StartPosition = FormStartPosition.Manual;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                DoubleBuffered = true;
                _timer = new System.Windows.Forms.Timer { Interval = 16 };
                _timer.Tick += (s, e) =>
                {
                    _phase = (_phase + 13) % Math.Max(1, ClientSize.Width);
                    Invalidate();
                };
                _timer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.FromArgb(20, 28, 42));
                int stripe = Math.Max(24, ClientSize.Width / 12);
                using (var accent = new SolidBrush(Color.FromArgb(40, 180, 240)))
                using (var secondary = new SolidBrush(Color.FromArgb(245, 145, 55)))
                {
                    e.Graphics.FillRectangle(accent, _phase - stripe, 0, stripe, ClientSize.Height);
                    int reverse = ClientSize.Width - _phase;
                    e.Graphics.FillRectangle(secondary, reverse, 0, stripe / 2, ClientSize.Height);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _timer.Dispose();
                base.Dispose(disposing);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);
    }
}

using Kiritori.Views.LiveCapture;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori.LiveCaptureBenchmarks
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
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
            var capture = new Rectangle(screen.Left, screen.Top, width, height);

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
    }
}

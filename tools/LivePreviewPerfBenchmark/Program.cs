using System.Diagnostics;
using System.Drawing.Drawing2D;

const int Width = 1920;
const int Height = 1080;
const int SampleRuns = 7;

string mode = args.Length == 1 ? args[0].ToLowerInvariant() : string.Empty;
if (mode != "legacy" && mode != "optimized")
{
    Console.Error.WriteLine("Usage: LivePreviewPerfBenchmark legacy|optimized");
    return 2;
}

bool optimized = mode == "optimized";
using var source = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
using (var g = Graphics.FromImage(source))
{
    g.Clear(Color.CornflowerBlue);
    g.FillEllipse(Brushes.OrangeRed, 100, 100, Width - 200, Height - 200);
}

Console.WriteLine($"mode={mode} size={Width}x{Height} runs={SampleRuns}");
Print("frame-copy", Measure(SampleRuns, () => BenchmarkFrameCopy(source, optimized, 60)), "60 frames");
Print("equal-size-paint", Measure(SampleRuns, () => BenchmarkPaint(source, optimized, 120)), "120 paints");
Print("scaled-paint", Measure(SampleRuns, () => BenchmarkScaledPaint(source, optimized, 120)), "120 paints at 1280x720");
Print("disabled-trace", Measure(SampleRuns, () => BenchmarkDisabledTrace(optimized, 500_000)), "500000 frames");
Print("ui-notify", Measure(SampleRuns, () => BenchmarkUiNotify(optimized, 200_000)), "200000 frames, UI drains every 4");
return 0;

static Sample Measure(int runs, Func<long> action)
{
    action();
    var samples = new List<Sample>(runs);
    for (int i = 0; i < runs; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        long operations = action();
        sw.Stop();
        samples.Add(new Sample(sw.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before, operations));
    }

    samples.Sort((a, b) => a.Milliseconds.CompareTo(b.Milliseconds));
    return samples[samples.Count / 2];
}

static long BenchmarkFrameCopy(Bitmap source, bool optimized, int frames)
{
    long consumed = 0;
    for (int i = 0; i < frames; i++)
    {
        using var backendFrame = (Bitmap)source.Clone();
        if (optimized)
        {
            consumed += backendFrame.Width;
        }
        else
        {
            using var presentationFrame = (Bitmap)backendFrame.Clone();
            consumed += presentationFrame.Width;
        }
    }
    return consumed;
}

static long BenchmarkPaint(Bitmap source, bool optimized, int paints)
{
    using var target = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
    using var g = Graphics.FromImage(target);
    long consumed = 0;
    for (int i = 0; i < paints; i++)
    {
        if (optimized)
        {
            g.DrawImageUnscaled(source, 0, 0);
        }
        else
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, new Rectangle(0, 0, target.Width, target.Height));
        }
        consumed += target.Width;
    }
    return consumed;
}

static long BenchmarkScaledPaint(Bitmap source, bool optimized, int paints)
{
    using var target = new Bitmap(1280, 720, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
    using var g = Graphics.FromImage(target);
    g.InterpolationMode = optimized ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
    long consumed = 0;
    for (int i = 0; i < paints; i++)
    {
        g.DrawImage(source, new Rectangle(0, 0, target.Width, target.Height));
        consumed += target.Width;
    }
    return consumed;
}

static long BenchmarkDisabledTrace(bool optimized, int frames)
{
    long consumed = 0;
    bool traceEnabled = false;
    for (int i = 0; i < frames; i++)
    {
        if (!optimized || traceEnabled)
        {
            string message = $"[LivePreview] FrameArrived: paused=False, maxFps=15, policy=AlwaysDraw, frame={i}";
            consumed += message.Length;
        }
    }
    return consumed;
}

static long BenchmarkUiNotify(bool optimized, int frames)
{
    var queue = new Queue<Action>();
    int pending = 0;
    long posted = 0;
    for (int i = 0; i < frames; i++)
    {
        if (!optimized || Interlocked.Exchange(ref pending, 1) == 0)
        {
            queue.Enqueue(() => Volatile.Write(ref pending, 0));
            posted++;
        }

        if ((i & 3) == 3 && queue.Count > 0)
            queue.Dequeue()();
    }
    while (queue.Count > 0) queue.Dequeue()();
    return posted;
}

static void Print(string name, Sample sample, string work)
{
    Console.WriteLine($"{name,-18} {sample.Milliseconds,10:F3} ms  alloc={sample.AllocatedBytes,12:N0} B  observed={sample.Observed,12:N0}  ({work})");
}

readonly record struct Sample(double Milliseconds, long AllocatedBytes, long Observed);

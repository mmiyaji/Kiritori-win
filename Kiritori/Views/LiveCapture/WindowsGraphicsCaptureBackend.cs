using Kiritori.Helpers;
using Kiritori.Services.Logging;
using SharpGen.Runtime;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Kiritori.Views.LiveCapture
{
    /// <summary>
    /// Captures the monitor containing the selected region through Windows
    /// Graphics Capture, crops on the GPU, and reads back only that crop.
    /// The existing WinForms presentation pipeline continues to own Bitmap
    /// frames, which keeps recording/OCR/snapshot behavior compatible.
    /// </summary>
    internal sealed class WindowsGraphicsCaptureBackend : LiveCaptureBackend
    {
        private const uint MonitorDefaultToNearest = 2;
        private static readonly Guid GraphicsCaptureItemGuid =
            new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        private static readonly Guid Texture2DGuid =
            new Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

        private readonly object _sync = new object();
        private readonly Stopwatch _frameClock = Stopwatch.StartNew();
        private volatile bool _running;
        private volatile int _maxFps = 15;
        private long _nextFrameTicks;
        private int _processingFrame;

        private Rectangle _captureRect;
        private Rectangle _captureRectPhysical;
        private Rectangle _monitorRect;
        private IntPtr _monitor;
        private int _frameWidth;
        private int _frameHeight;
        private ID3D11Device _device;
        private ID3D11DeviceContext _context;
        private IDirect3DDevice _winRtDevice;
        private ID3D11Texture2D _stagingTexture;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private GraphicsCaptureItem _item;

        public event Action<LiveCaptureFrameEventArgs> FrameArrived;
        public event Action<Exception> CaptureFailed;

        public Rectangle CaptureRect
        {
            get { lock (_sync) return _captureRect; }
            set { lock (_sync) _captureRect = value; }
        }

        public Rectangle CaptureRectPhysical
        {
            get { lock (_sync) return _captureRectPhysical; }
            set { lock (_sync) _captureRectPhysical = value; }
        }

        public int MaxFps
        {
            get => _maxFps;
            set => _maxFps = value;
        }

        public static bool IsSupported()
        {
            try
            {
                return GraphicsCaptureSession.IsSupported();
            }
            catch
            {
                return false;
            }
        }

        public static bool CanCapture(Rectangle physicalRect, out string reason)
        {
            reason = null;
            if (!IsSupported())
            {
                reason = "Windows Graphics Capture is unavailable.";
                return false;
            }
            if (physicalRect.Width <= 0 || physicalRect.Height <= 0)
            {
                reason = "The capture rectangle is empty.";
                return false;
            }

            NativeRect nativeRect = ToNativeRect(physicalRect);
            IntPtr monitor = MonitorFromRect(ref nativeRect, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero || !TryGetMonitorRect(monitor, out Rectangle monitorRect))
            {
                reason = "The source monitor could not be resolved.";
                return false;
            }
            if (!monitorRect.Contains(physicalRect))
            {
                reason = "GPU capture currently requires a region contained by one monitor.";
                return false;
            }
            return true;
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_running) return;
                Rectangle physical = !_captureRectPhysical.IsEmpty
                    ? _captureRectPhysical
                    : DpiUtil.LogicalToPhysical(_captureRect);
                if (!CanCapture(physical, out string reason))
                    throw new NotSupportedException(reason);

                NativeRect nativeRect = ToNativeRect(physical);
                _monitor = MonitorFromRect(ref nativeRect, MonitorDefaultToNearest);
                if (!TryGetMonitorRect(_monitor, out _monitorRect))
                    throw new InvalidOperationException("Failed to read monitor bounds.");

                CreateDevice();
                _item = CreateItemForMonitor(_monitor);
                _item.Closed += OnCaptureItemClosed;
                _frameWidth = _item.Size.Width;
                _frameHeight = _item.Size.Height;
                _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _winRtDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _item.Size);
                _framePool.FrameArrived += OnFrameArrived;
                _session = _framePool.CreateCaptureSession(_item);
                try { _session.IsCursorCaptureEnabled = false; } catch { }
                _running = true;
                _nextFrameTicks = 0;
                _session.StartCapture();
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (!_running && _framePool == null && _device == null) return;
                _running = false;
                if (_framePool != null)
                    _framePool.FrameArrived -= OnFrameArrived;
                if (_item != null)
                    _item.Closed -= OnCaptureItemClosed;
                SafeDispose(ref _session);
                SafeDispose(ref _framePool);
                _item = null;
                SafeDispose(ref _stagingTexture);
                SafeDispose(ref _winRtDevice);
                SafeDispose(ref _context);
                SafeDispose(ref _device);
                _monitorRect = Rectangle.Empty;
                _monitor = IntPtr.Zero;
                _frameWidth = _frameHeight = 0;
            }
        }

        private void CreateDevice()
        {
            Result result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                null,
                out _device,
                out _context);
            result.CheckError();

            using (IDXGIDevice dxgiDevice = _device.QueryInterface<IDXGIDevice>())
            {
                int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr inspectable);
                Marshal.ThrowExceptionForHR(hr);
                try
                {
                    _winRtDevice = (IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable);
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            if (!_running) return;
            using (Direct3D11CaptureFrame frame = sender.TryGetNextFrame())
            {
                if (frame == null || !_running || !ShouldProcessFrame()) return;
                if (Interlocked.Exchange(ref _processingFrame, 1) != 0) return;
                try
                {
                    Bitmap bitmap;
                    lock (_sync)
                    {
                        if (!_running) return;
                        if (frame.ContentSize.Width != _frameWidth || frame.ContentSize.Height != _frameHeight)
                        {
                            _frameWidth = frame.ContentSize.Width;
                            _frameHeight = frame.ContentSize.Height;
                            _framePool.Recreate(
                                _winRtDevice,
                                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                2,
                                frame.ContentSize);
                            TryGetMonitorRect(_monitor, out _monitorRect);
                        }
                        bitmap = CopyCropToBitmap(frame);
                    }
                    if (bitmap == null) return;

                    var handler = FrameArrived;
                    if (handler == null)
                    {
                        bitmap.Dispose();
                        return;
                    }

                    try
                    {
                        handler(new LiveCaptureFrameEventArgs(bitmap, true));
                        bitmap = null;
                    }
                    finally
                    {
                        bitmap?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Log.Warn("GPU capture frame failed: " + ex.Message, "LivePreview");
                        CaptureFailed?.Invoke(ex);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _processingFrame, 0);
                }
            }
        }

        private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
        {
            if (!_running) return;
            var error = new InvalidOperationException("The GPU capture source was closed.");
            Log.Warn(error.Message, "LivePreview");
            CaptureFailed?.Invoke(error);
        }

        private bool ShouldProcessFrame()
        {
            int fps = MaxFps;
            if (fps <= 0) return true;
            long now = _frameClock.ElapsedTicks;
            long interval = Math.Max(1L, Stopwatch.Frequency / fps);
            while (true)
            {
                long next = Interlocked.Read(ref _nextFrameTicks);
                if (next != 0 && now < next) return false;

                long following = next == 0 ? now + interval : next + interval;
                if (following <= now) following = now + interval;
                if (Interlocked.CompareExchange(ref _nextFrameTicks, following, next) == next)
                    return true;
            }
        }

        private Bitmap CopyCropToBitmap(Direct3D11CaptureFrame frame)
        {
            Rectangle physical = !_captureRectPhysical.IsEmpty
                ? _captureRectPhysical
                : DpiUtil.LogicalToPhysical(_captureRect);
            Rectangle crop = new Rectangle(
                physical.Left - _monitorRect.Left,
                physical.Top - _monitorRect.Top,
                physical.Width,
                physical.Height);
            if (crop.Left < 0 || crop.Top < 0 || crop.Right > frame.ContentSize.Width || crop.Bottom > frame.ContentSize.Height)
                return null;

            IntPtr texturePointer = GetDXGIInterface(frame.Surface, Texture2DGuid);
            using (ID3D11Texture2D source = MarshallingHelpers.FromPointer<ID3D11Texture2D>(texturePointer))
            {
                EnsureStagingTexture(crop.Size);
                var sourceBox = new Box(crop.Left, crop.Top, 0, crop.Right, crop.Bottom, 1);
                _context.CopySubresourceRegion(_stagingTexture, 0, 0, 0, 0, source, 0, sourceBox);
                MappedSubresource mapped = _context.Map(
                    _stagingTexture,
                    0,
                    MapMode.Read,
                    Vortice.Direct3D11.MapFlags.None);
                try
                {
                    return CopyMappedBitmap(mapped, crop.Width, crop.Height);
                }
                finally
                {
                    _context.Unmap(_stagingTexture, 0);
                }
            }
        }

        private void EnsureStagingTexture(Size size)
        {
            if (_stagingTexture != null)
            {
                Texture2DDescription current = _stagingTexture.Description;
                if (current.Width == size.Width && current.Height == size.Height) return;
                _stagingTexture.Dispose();
                _stagingTexture = null;
            }

            var description = new Texture2DDescription(
                Format.B8G8R8A8_UNorm,
                size.Width,
                size.Height,
                bindFlags: BindFlags.None,
                usage: ResourceUsage.Staging,
                cpuAccessFlags: CpuAccessFlags.Read);
            _stagingTexture = _device.CreateTexture2D(description);
        }

        private static unsafe Bitmap CopyMappedBitmap(MappedSubresource mapped, int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            try
            {
                BitmapData data = null;
                try
                {
                    data = bitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppPArgb);
                    int bytesPerRow = checked(width * 4);
                    for (int y = 0; y < height; y++)
                    {
                        byte* source = (byte*)mapped.DataPointer + y * mapped.RowPitch;
                        byte* destination = (byte*)data.Scan0 + y * data.Stride;
                        Buffer.MemoryCopy(source, destination, Math.Abs(data.Stride), bytesPerRow);
                    }
                }
                finally
                {
                    if (data != null) bitmap.UnlockBits(data);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private static NativeRect ToNativeRect(Rectangle rect)
        {
            return new NativeRect
            {
                Left = rect.Left,
                Top = rect.Top,
                Right = rect.Right,
                Bottom = rect.Bottom,
            };
        }

        private static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
        {
            object factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
            var interop = (IGraphicsCaptureItemInterop)factory;
            Guid iid = GraphicsCaptureItemGuid;
            IntPtr pointer = interop.CreateForMonitor(monitor, ref iid);
            try
            {
                return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(pointer);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        private static IntPtr GetDXGIInterface(IDirect3DSurface surface, Guid iid)
        {
            var access = (IDirect3DDxgiInterfaceAccess)(object)surface;
            int hr = access.GetInterface(ref iid, out IntPtr result);
            Marshal.ThrowExceptionForHR(hr);
            return result;
        }

        private static bool TryGetMonitorRect(IntPtr monitor, out Rectangle rect)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info))
            {
                rect = Rectangle.Empty;
                return false;
            }
            rect = Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom);
            return true;
        }

        private static void SafeDispose<T>(ref T value) where T : class, IDisposable
        {
            T current = value;
            value = null;
            try { current?.Dispose(); } catch { }
        }

        public void Dispose()
        {
            Stop();
        }

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow(IntPtr window, ref Guid iid);
            IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
        }

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDxgiInterfaceAccess
        {
            [PreserveSig]
            int GetInterface(ref Guid iid, out IntPtr result);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
    }
}

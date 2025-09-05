using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kiritori.Interop;
using static Kiritori.Interop.Magnification; // MagInitialize / MagUninitialize / WC_MAGNIFIER

namespace Kiritori.Views.LiveCapture
{
    internal sealed class MagPreviewHost : IDisposable
    {
        private readonly IntPtr _parent;
        private IntPtr _magHwnd = IntPtr.Zero;
        private bool _initialized;
        private float _scale = 1.0f;
        private Rectangle _hostBounds;

        // ----- user32 -----
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateWindowExW")]
        static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr hWnd);

        // ----- flags (user32 style/exstyle) -----
        const int WS_CHILD = 0x40000000;
        const int WS_VISIBLE = 0x10000000;
        const int WS_CLIPSIBLINGS = 0x04000000;
        const int WS_CLIPCHILDREN = 0x02000000;

        const int WS_EX_NOPARENTNOTIFY = 0x00000004;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_NOACTIVATE = 0x08000000;

        // ----- magnification -----
        [StructLayout(LayoutKind.Sequential)]
        struct MAGTRANSFORM
        {
            public float m00, m01, m02;
            public float m10, m11, m12;
            public float m20, m21, m22;

            public static MAGTRANSFORM Identity() => new MAGTRANSFORM
            {
                m00 = 1, m01 = 0, m02 = 0,
                m10 = 0, m11 = 1, m12 = 0,
                m20 = 0, m21 = 0, m22 = 1
            };
        }

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM m);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

        public MagPreviewHost(IntPtr parentHandle, Rectangle initialBounds, bool mouseThrough = false, bool noActivate = true)
        {
            _parent = parentHandle;
            _hostBounds = initialBounds;

            if (!MagInitialize())
                throw new InvalidOperationException("MagInitialize failed.");
            _initialized = true;

            // int ex = WS_EX_NOPARENTNOTIFY | WS_EX_LAYERED;
            int ex = WS_EX_NOPARENTNOTIFY;
            if (mouseThrough) ex |= WS_EX_TRANSPARENT;
            if (noActivate)   ex |= WS_EX_NOACTIVATE;

            int style = WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN;

            _magHwnd = CreateWindowEx(
                ex, WC_MAGNIFIER, null, style,
                _hostBounds.X, _hostBounds.Y, _hostBounds.Width, _hostBounds.Height,
                _parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_magHwnd == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx(WC_MAGNIFIER) failed.");

            // 初期スケール適用
            ApplyScaleTransform(_scale);
        }

        public void Dispose()
        {
            try
            {
                if (_magHwnd != IntPtr.Zero)
                {
                    try { DestroyWindow(_magHwnd); } catch { /* ignore */ }
                    _magHwnd = IntPtr.Zero;
                }
            }
            finally
            {
                if (_initialized)
                {
                    try { MagUninitialize(); } catch { /* ignore */ }
                    _initialized = false;
                }
            }
        }

        // --------- 公開API ---------

        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr hWnd);

        public void SetHostBounds(Rectangle r)
        {
            _hostBounds = r;
            if (_magHwnd != IntPtr.Zero)
            {
                MoveWindow(_magHwnd, r.X, r.Y, r.Width, r.Height, true);
                InvalidateRect(_magHwnd, IntPtr.Zero, true);   // ★縮小方向も確実に再描画
                UpdateWindow(_magHwnd);
            }
        }

        public void SetScale(float scale)
        {
            _scale = Math.Max(0.1f, Math.Min(16f, scale));
            var m = MAGTRANSFORM.Identity();   // ← Identity() 使うと他成分が確実に0/1で初期化される
            m.m00 = _scale; m.m11 = _scale;

            if (_magHwnd != IntPtr.Zero)
            {
                MagSetWindowTransform(_magHwnd, ref m);
                InvalidateRect(_magHwnd, IntPtr.Zero, true);   // ★変換更新後に再描画
                UpdateWindow(_magHwnd);
            }
        }

        public void SetSourceDesktopPx(Rectangle desktopPx)
        {
            if (_magHwnd == IntPtr.Zero) return;
            var rc = new RECT { Left = desktopPx.Left, Top = desktopPx.Top, Right = desktopPx.Right, Bottom = desktopPx.Bottom };
            if (!MagSetWindowSource(_magHwnd, rc))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "MagSetWindowSource failed");
        }

        // --------- 内部 ---------

        private void ApplyScaleTransform(float scale)
        {
            if (_magHwnd == IntPtr.Zero) return;

            var m = MAGTRANSFORM.Identity();
            m.m00 = scale;
            m.m11 = scale;
            // m22 は常に 1.0f のまま

            if (!MagSetWindowTransform(_magHwnd, ref m))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "MagSetWindowTransform failed");
        }
    }
}

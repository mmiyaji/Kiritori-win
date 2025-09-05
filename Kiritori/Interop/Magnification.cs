using System;
using System.Runtime.InteropServices;

namespace Kiritori.Interop
{
    internal static class Magnification
    {
        public const string WC_MAGNIFIER = "Magnifier";
        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_CLIPCHILDREN = 0x02000000;

        public const int WS_EX_NOPARENTNOTIFY = 0x00000004;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MAGTRANSFORM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
            public float[] v;
            public static MAGTRANSFORM Identity()
            {
                return new MAGTRANSFORM
                {
                    v = new float[9] {
                    1,0,0,
                    0,1,0,
                    0,0,1
                }
                };
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool MagImageScalingCallback(IntPtr hwnd, IntPtr srcdata, MAGIMAGEHEADER srcheader,
                                                     IntPtr destdata, MAGIMAGEHEADER destheader,
                                                     RECT src, RECT dest, IntPtr clip, IntPtr reserved);

        [StructLayout(LayoutKind.Sequential)]
        public struct MAGIMAGEHEADER
        {
            public uint width;
            public uint height;
            public Guid format;
            public uint stride;
            public uint offset;
            public uint cbSize;
        }

        [DllImport("Magnification.dll", ExactSpelling = true)]
        public static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true)]
        public static extern bool MagUninitialize();

        // [DllImport("Magnification.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [DllImport("user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true /* ExactSpelling は付けない */)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        public static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        public static extern bool MagSetImageScalingCallback(IntPtr hwnd, MagImageScalingCallback callback);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Helpers
{
    internal static class DpiUtil
    {
        // Win8.1+ Monitor DPI 取得
        [System.Runtime.InteropServices.DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        private const int MDT_EFFECTIVE_DPI = 0;    // スケール設定に相当
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private static uint GetEffectiveDpiAt(int x, int y)
        {
            var mon = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
            uint dx, dy;
            if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out dx, out dy) == 0 && dx != 0) return dx;
            return 96; // フォールバック
        }

        // 論理px → 物理px（左上で決め打ち。幅高は同一DPI前提。矩形が別モニタを跨ぐ場合は要分割）
        public static Rectangle LogicalToPhysical(Rectangle logical)
        {
            var dpi = GetEffectiveDpiAt(logical.Left, logical.Top);
            float s = dpi / 96f;
            return new Rectangle(
                (int)Math.Round(logical.Left * s),
                (int)Math.Round(logical.Top * s),
                (int)Math.Round(logical.Width * s),
                (int)Math.Round(logical.Height * s)
            );
        }

        // 物理px → 論理px
        public static Rectangle PhysicalToLogical(Rectangle physical)
        {
            var dpi = GetEffectiveDpiAt(physical.Left, physical.Top);
            float s = dpi / 96f;
            return new Rectangle(
                (int)Math.Round(physical.Left / s),
                (int)Math.Round(physical.Top / s),
                (int)Math.Round(physical.Width / s),
                (int)Math.Round(physical.Height / s)
            );
        }
    }
}

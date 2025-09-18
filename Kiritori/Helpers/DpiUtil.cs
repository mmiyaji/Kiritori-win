using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]

        private struct POINT { public int X; public int Y; public POINT(int x,int y){X=x;Y=y;} }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private enum MONITOR_DPI_TYPE : int
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI   = 1,
            MDT_RAW_DPI       = 2,
            MDT_DEFAULT       = MDT_EFFECTIVE_DPI
        }


        [DllImport("Shcore.dll", ExactSpelling = true)]
        private static extern int GetDpiForMonitor(
            IntPtr hmonitor,
            MONITOR_DPI_TYPE dpiType,
            out uint dpiX,
            out uint dpiY);

        /// <summary>
        /// 画面上の“物理ピクセル座標”に対する GDI スケール（= 実効DPI/96f）を返す。
        /// 例: 150% のモニタなら 1.5f。
        /// </summary>
        public static float GdiScaleAtScreenPoint(Point screenPointPhysical)
        {
            try
            {
                var hmon = MonitorFromPoint(new POINT(screenPointPhysical.X, screenPointPhysical.Y), MONITOR_DEFAULTTONEAREST);
                if (hmon != IntPtr.Zero)
                {
                    if (GetDpiForMonitor(hmon, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0 && dx > 0)
                        return dx / 96f;
                }
            }
            catch (DllNotFoundException) { /* Shcore.dll が無い古いOS */ }
            catch (EntryPointNotFoundException) { /* API未サポート */ }
            catch { /* 何かあってもフォールバック */ }

            // フォールバック：システムDPI（概ねメインモニタ）/ 最終手段は 1.0
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX / 96f;
            }
            catch { return 1.0f; }
        }
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
        public static Rectangle LogicalToPhysicalAtSource(Rectangle logical)
        {
            // 変換基準の点（矩形内であれば左上で十分）
            var srcPt = new System.Drawing.Point(logical.X, logical.Y);

            // ソース座標の拡大率（例: 150% => 1.5）
            // 既存のユーティリティがあるならそれを使う：
            //   var sSrc = GdiCaptureBackend.DpiUtil.ScaleAtScreenPoint(srcPt);
            // なければ自前で GetDpiForMonitor(MDT_EFFECTIVE_DPI) → sSrc = dpi/96f
            float sSrc = GdiScaleAtScreenPoint(srcPt); // 実装は既存 DpiUtil に合わせて

            int L = (int)Math.Round(logical.Left   * sSrc);
            int T = (int)Math.Round(logical.Top    * sSrc);
            int W = (int)Math.Round(logical.Width  * sSrc);
            int H = (int)Math.Round(logical.Height * sSrc);

            return new Rectangle(L, T, W, H);
        }
        
    }
}

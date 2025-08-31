using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    internal static class WindowAligner
    {
        public static void MoveFormToMatchClient(Form f, Rectangle clientScreenRect, bool topMost)
        {
            if (f == null) return;
            if (clientScreenRect.Width <= 0 || clientScreenRect.Height <= 0) return;

            // 現在のスタイル取得
            var style = GetWindowLong(f.Handle, GWL_STYLE);
            var ex = GetWindowLong(f.Handle, GWL_EXSTYLE);

            // クライアント(0,0)-(w,h)に対して必要なウィンドウ矩形を算出
            var r = new RECT { left = 0, top = 0, right = clientScreenRect.Width, bottom = clientScreenRect.Height };

            // DPI対応版が使えるならそちらを優先（Per-Monitor DPIの枠太さを正確に計算）
            if (!AdjustWindowRectExForDpiSafe(ref r, (uint)style, HasMenu(f), (uint)ex, GetDpiForWindowSafe(f.Handle)))
            {
                AdjustWindowRectEx(ref r, (uint)style, HasMenu(f), (uint)ex);
            }

            int winW = r.right - r.left;
            int winH = r.bottom - r.top;

            // r.left/top は通常負値（枠分の余白）。これを使って「クライアント左上＝指定座標」になるように調整
            int winX = clientScreenRect.Left + r.left;
            int winY = clientScreenRect.Top + r.top;

            // 実配置
            f.TopMost = topMost;
            f.SetBounds(winX, winY, winW, winH); // StartPosition=Manual 推奨
        }

        private static bool HasMenu(Form f) => f.MainMenuStrip != null;

        // ---- P/Invoke ----
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        // Win10 1607+ （存在しない環境でも安全にフォールバック）
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "AdjustWindowRectExForDpi")]
        private static extern bool AdjustWindowRectExForDpi_Native(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle, uint dpi);

        private static bool AdjustWindowRectExForDpiSafe(ref RECT r, uint style, bool menu, uint ex, uint dpi)
        {
            try { return AdjustWindowRectExForDpi_Native(ref r, style, menu, ex, dpi); }
            catch { return false; } // 関数が存在しないOS → 失敗しフォールバック
        }

        // Win10 1607+
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);

        private static uint GetDpiForWindowSafe(IntPtr hWnd)
        {
            try { var v = GetDpiForWindow(hWnd); return v != 0 ? v : 96u; }
            catch { return 96u; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }
    }
}

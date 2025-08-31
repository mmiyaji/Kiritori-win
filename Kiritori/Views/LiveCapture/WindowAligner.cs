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

            var style = GetWindowLong(f.Handle, GWL_STYLE);
            var ex    = GetWindowLong(f.Handle, GWL_EXSTYLE);

            var r = new RECT { left = 0, top = 0, right = clientScreenRect.Width, bottom = clientScreenRect.Height };

            // DPI対応版が使えるなら優先
            if (!AdjustWindowRectExForDpiSafe(ref r, (uint)style, HasMenu(f), (uint)ex, GetDpiForWindowSafe(f.Handle)))
            {
                AdjustWindowRectEx(ref r, (uint)style, HasMenu(f), (uint)ex);
            }

            int winW = r.right - r.left;
            int winH = r.bottom - r.top;
            int winX = clientScreenRect.Left + r.left; // r.left/top は負値(枠ぶん)
            int winY = clientScreenRect.Top  + r.top;

            f.TopMost = topMost;
            f.SetBounds(winX, winY, winW, winH);
        }

        public static void ResizeFormClientAt(Form f, Point clientTopLeftScreenLogical, Size clientSizeLogical, bool topMost)
        {
            var style = GetWindowLong(f.Handle, GWL_STYLE);
            var ex    = GetWindowLong(f.Handle, GWL_EXSTYLE);

            var r = new RECT { left = 0, top = 0, right = clientSizeLogical.Width, bottom = clientSizeLogical.Height };
            var ok = AdjustWindowRectForFormSafe(ref r, (uint)style, f.MainMenuStrip != null, (uint)ex, GetDpiForWindowSafe(f.Handle));

            int w = r.right - r.left;
            int h = r.bottom - r.top;
            int x = clientTopLeftScreenLogical.X + r.left;
            int y = clientTopLeftScreenLogical.Y + r.top;

            f.TopMost = topMost;
            f.SetBounds(x, y, w, h);
        }
        public static void ResizeFormClientKeepTopLeft(Form f, Size newClientSizeLogical, bool topMost)
        {
            var style = GetWindowLong(f.Handle, GWL_STYLE);
            var ex    = GetWindowLong(f.Handle, GWL_EXSTYLE);

            // 新しいクライアントサイズに必要なウィンドウ外形（枠込み）を算出
            var r = new RECT { left = 0, top = 0, right = newClientSizeLogical.Width, bottom = newClientSizeLogical.Height };
            AdjustWindowRectForFormSafe(ref r, (uint)style, f.MainMenuStrip != null, (uint)ex, GetDpiForWindowSafe(f.Handle));

            int w = r.right - r.left;
            int h = r.bottom - r.top;

            // 今のクライアント左上（スクリーン論理座標）を取得して“その場”
            var tl = f.PointToScreen(Point.Empty);
            int x = tl.X + r.left;  // r.left/top は通常マイナス（枠ぶん）
            int y = tl.Y + r.top;

            f.TopMost = topMost;
            f.SetBounds(x, y, w, h);
        }

        // （オプション）中心を固定してズームしたい場合
        public static void ResizeFormClientKeepCenter(Form f, Size newClientSizeLogical, bool topMost)
        {
            var style = GetWindowLong(f.Handle, GWL_STYLE);
            var ex    = GetWindowLong(f.Handle, GWL_EXSTYLE);

            var r = new RECT { left = 0, top = 0, right = newClientSizeLogical.Width, bottom = newClientSizeLogical.Height };
            AdjustWindowRectForFormSafe(ref r, (uint)style, f.MainMenuStrip != null, (uint)ex, GetDpiForWindowSafe(f.Handle));

            int w = r.right - r.left;
            int h = r.bottom - r.top;

            // 現在のクライアントの中心を基準に
            var tl = f.PointToScreen(Point.Empty);
            var curClient = new Rectangle(tl, f.ClientSize);
            var curCenter = new Point(curClient.Left + curClient.Width/2, curClient.Top + curClient.Height/2);

            int x = curCenter.X - (w - (r.left)) / 2 - (-r.left);
            int y = curCenter.Y - (h - (r.top))  / 2 - (-r.top);

            f.TopMost = topMost;
            f.SetBounds(x, y, w, h);
        }

        // ---- ここが欠けていた安全ラッパー ----
        public static bool AdjustWindowRectForFormSafe(ref RECT r, uint style, bool hasMenu, uint ex, uint dpi)
        {
            // Windows 10 1607+ なら DPI 対応版を試す
            if (AdjustWindowRectExForDpiSafe(ref r, style, hasMenu, ex, dpi))
                return true;

            // フォールバック（DPI非対応だが広く使える）
            return AdjustWindowRectEx(ref r, style, hasMenu, ex);
        }

        private static bool HasMenu(Form f) => f.MainMenuStrip != null;

        // ---- P/Invoke ----
        private const int GWL_STYLE   = -16;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        // Win10 1607+（存在しないOSでも呼び出し例外を握りつぶしてフォールバック）
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "AdjustWindowRectExForDpi")]
        private static extern bool AdjustWindowRectExForDpi_Native(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle, uint dpi);

        private static bool AdjustWindowRectExForDpiSafe(ref RECT r, uint style, bool menu, uint ex, uint dpi)
        {
            try { return AdjustWindowRectExForDpi_Native(ref r, style, menu, ex, dpi); }
            catch { return false; }
        }

        // Win10 1607+
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);

        public static uint GetDpiForWindowSafe(IntPtr hWnd)
        {
            try { var v = GetDpiForWindow(hWnd); return v != 0 ? v : 96u; }
            catch { return 96u; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }
    }
}

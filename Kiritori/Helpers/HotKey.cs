// Kiritori.Helpers.HotKey
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori.Helpers
{
    public class HotKey : IDisposable
    {
        HotKeyForm form;
        public event EventHandler HotKeyPush;

        public HotKey(MOD_KEY modKey, Keys key)
        {
            form = new HotKeyForm(modKey, key, raiseHotKeyPush);
        }

        private void raiseHotKeyPush()
        {
            var h = HotKeyPush;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Dispose() { if (form != null) form.Dispose(); }

        private class HotKeyForm : Form
        {
            [DllImport("user32.dll", SetLastError = true)]
            private static extern int RegisterHotKey(IntPtr hWnd, int id, MOD_KEY fsModifiers, Keys vk);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int UnregisterHotKey(IntPtr hWnd, int id);

            const int WM_HOTKEY = 0x0312;
            int id = -1;
            ThreadStart proc;

            public HotKeyForm(MOD_KEY modKey, Keys key, ThreadStart proc)
            {
                this.proc = proc;

                // フォームのハンドルを“必ず”作ってから登録
                if (!this.IsHandleCreated) this.CreateHandle();

                // id を探しつつ登録（この HWND 上で一意）
                for (int i = 0x0001; i <= 0xBFFF; i++)
                {
                    if (RegisterHotKey(this.Handle, i, modKey, key) != 0)
                    {
                        id = i;
                        break;
                    }
                }

                if (id < 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        $"RegisterHotKey failed. modifiers={modKey}, key={key}, lastError=0x{err:X8}");
                }

                // 表示不要
                this.ShowInTaskbar = false;
                this.Visible = false;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && id >= 0 && (int)m.WParam == id)
                {
                    try { proc(); } catch { /* swallow */ }
                    return;
                }
                base.WndProc(ref m);
            }

            protected override void Dispose(bool disposing)
            {
                if (id >= 0 && this.IsHandleCreated)
                {
                    UnregisterHotKey(this.Handle, id);
                    id = -1;
                }
                base.Dispose(disposing);
            }
        }
    }

    [Flags]
    public enum MOD_KEY : int
    {
        ALT     = 0x0001,
        CONTROL = 0x0002,
        SHIFT   = 0x0004,
        // 任意: NOREPEAT も使えます（チャタリング防止）
        // NOREPEAT = 0x4000,
        WIN     = 0x0008, // 使う場合は HotkeyPicker 側も対応を
    }
}

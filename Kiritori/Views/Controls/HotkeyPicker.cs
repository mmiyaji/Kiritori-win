using System;
using System.Windows.Forms;
using Kiritori.Helpers;

namespace Kiritori
{
    internal class HotkeyPicker : TextBox
    {
        public HotkeySpec Value { get; private set; }
        public event EventHandler HotkeyPicked;

        public HotkeyPicker()
        {
            this.ReadOnly = true;
            this.ShortcutsEnabled = false; // Ctrl+C 等のショートカットを無効に
            this.TabStop = true;
            this.Dock = DockStyle.Fill;
        }

        public void SetFromText(string text, HotkeySpec fallback)
        {
            Value = HotkeyUtil.ParseOrDefault(text, fallback);
            this.Text = HotkeyUtil.ToText(Value);
        }

        protected override bool IsInputKey(Keys keyData) => true; // Tab/矢印も制御に渡す

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            e.SuppressKeyPress = true;

            var m = ModMask.None;
            if (e.Control) m |= ModMask.Ctrl;
            if (e.Shift)   m |= ModMask.Shift;
            if (e.Alt)     m |= ModMask.Alt;

            var key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu) return;
            if (!HotkeyUtil.IsValidKeyboardKey(key)) return;

            Value = new HotkeySpec { Mods = m, Key = key };
            this.Text = HotkeyUtil.ToText(Value);
            var h = HotkeyPicked; if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            var h = HotkeyPicked; if (h != null) h(this, EventArgs.Empty);
        }
    }
}

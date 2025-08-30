using System.Windows.Forms;
using Kiritori.Helpers;

namespace Kiritori
{
    internal class HotkeyPicker : TextBox
    {
        public HotkeySpec Value { get; private set; }

        public void SetFromText(string text, HotkeySpec fallback)
        {
            Value = HotkeyUtil.ParseOrDefault(text, fallback);
            this.Text = HotkeyUtil.ToText(Value);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            e.SuppressKeyPress = true;

            ModMask m = ModMask.None;
            if (e.Control) m |= ModMask.Ctrl;
            if (e.Shift) m |= ModMask.Shift;
            if (e.Alt) m |= ModMask.Alt;

            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
                return;

            Value = new HotkeySpec { Mods = m, Key = key };
            this.Text = HotkeyUtil.ToText(Value);
        }
    }
}

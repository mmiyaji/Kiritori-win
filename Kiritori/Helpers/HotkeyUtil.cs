using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Kiritori.Helpers
{
    [Flags]
    public enum ModMask
    {
        None = 0,
        Ctrl = 1 << 0,
        Shift = 1 << 1,
        Alt = 1 << 2,
        // Win を使いたい場合は既存 MOD_KEY 拡張とセットで追加してください
    }

    public sealed class HotkeySpec
    {
        public ModMask Mods { get; set; }
        public Keys Key { get; set; }
    }

    public static class HotkeyUtil
    {
        // "Ctrl+Shift+5" → HotkeySpec
        public static bool TryParse(string text, out HotkeySpec hk)
        {
            hk = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(new[] { '+', '＋' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim()).ToArray();
            if (parts.Length == 0) return false;

            ModMask mods = ModMask.None;
            Keys key = Keys.None;

            for (int i = 0; i < parts.Length; i++)
            {
                var up = parts[i].ToUpperInvariant();
                if (up == "CTRL" || up == "CONTROL") mods |= ModMask.Ctrl;
                else if (up == "SHIFT") mods |= ModMask.Shift;
                else if (up == "ALT") mods |= ModMask.Alt;
                else
                {
                    Keys parsed;
                    if (Enum.TryParse(up, true, out parsed))
                    {
                        key = parsed;
                    }
                    else if (up.Length == 1 && char.IsDigit(up[0]))
                    {
                        key = (Keys)Enum.Parse(typeof(Keys), "D" + up);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (key == Keys.None) return false;
            hk = new HotkeySpec { Mods = mods, Key = key };
            return true;
        }

        // HotkeySpec → "Ctrl+Shift+5"
        public static string ToText(HotkeySpec hk)
        {
            if (hk == null) return "";
            var list = new List<string>();
            if ((hk.Mods & ModMask.Ctrl) != 0) list.Add("Ctrl");
            if ((hk.Mods & ModMask.Shift) != 0) list.Add("Shift");
            if ((hk.Mods & ModMask.Alt) != 0) list.Add("Alt");

            string keyText;
            if (hk.Key >= Keys.D0 && hk.Key <= Keys.D9)
            {
                keyText = ((int)(hk.Key - Keys.D0)).ToString();
            }
            else
            {
                keyText = hk.Key.ToString();
            }

            list.Add(keyText);
            return string.Join("+", list.ToArray());
        }

        // HotkeySpec → 既存の MOD_KEY と Keys
        public static Tuple<MOD_KEY, Keys> ToModAndKey(HotkeySpec hk)
        {
            MOD_KEY m = 0;
            if ((hk.Mods & ModMask.Ctrl) != 0) m |= MOD_KEY.CONTROL;
            if ((hk.Mods & ModMask.Shift) != 0) m |= MOD_KEY.SHIFT;
            if ((hk.Mods & ModMask.Alt) != 0) m |= MOD_KEY.ALT;
            return Tuple.Create(m, hk.Key);
        }

        public static HotkeySpec ParseOrDefault(string text, HotkeySpec fallback)
        {
            HotkeySpec hk;
            if (TryParse(text, out hk) && IsValidKeyboardKey(hk.Key))
                return hk;
            return fallback;
        }
        public static bool IsValidKeyboardKey(Keys k)
        {
            // マウス系は不可
            if (k == Keys.LButton || k == Keys.RButton || k == Keys.MButton ||
                k == Keys.XButton1 || k == Keys.XButton2)
                return false;

            // 修飾単体も不可
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu)
                return false;

            // よく使う許可範囲（必要に応じて拡張してください）
            if ((k >= Keys.A && k <= Keys.Z) ||
                (k >= Keys.D0 && k <= Keys.D9) ||
                (k >= Keys.NumPad0 && k <= Keys.NumPad9) ||
                (k >= Keys.F1 && k <= Keys.F24) ||
                k == Keys.PrintScreen || k == Keys.Insert || k == Keys.Delete ||
                k == Keys.Home || k == Keys.End ||
                k == Keys.PageUp || k == Keys.PageDown ||
                k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right ||
                k == Keys.Space || k == Keys.Tab || k == Keys.Escape || k == Keys.Back)
                return true;

            // それ以外は念のため不可
            return false;
        }


    }
}

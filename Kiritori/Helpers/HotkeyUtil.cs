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
        public static bool TryParse(string text, out HotkeySpec spec)
        {
            spec = default(HotkeySpec);
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 正規化：空白/全角＋を除去、大小無視で処理
            var t = text.Trim()
                        .Replace(" ", "")
                        .Replace("　", "")
                        .Replace("＋", "+");

            var parts = t.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            ModMask mods = ModMask.None;
            Keys key = Keys.None;

            foreach (var raw in parts)
            {
                var tok = raw.ToUpperInvariant();

                switch (tok)
                {
                    case "CTRL":
                    case "CONTROL":
                        mods |= ModMask.Ctrl;  continue;
                    case "SHIFT":
                        mods |= ModMask.Shift; continue;
                    case "ALT":
                        mods |= ModMask.Alt;   continue;
                    case "WIN":
                    case "WINDOWS":
                        // 必要なら ModMask.Win を定義して対応
                        continue;
                }

                // 数字 → D0..D9
                if (tok.Length == 1 && tok[0] >= '0' && tok[0] <= '9')
                {
                    key = Keys.D0 + (tok[0] - '0');
                    continue;
                }

                // F1..F24
                if (tok.Length >= 2 && tok[0] == 'F' &&
                    int.TryParse(tok.Substring(1), out var f) && f >= 1 && f <= 24)
                {
                    key = Keys.F1 + (f - 1);
                    continue;
                }

                // NUMPAD0..9
                if (tok.StartsWith("NUMPAD") &&
                    int.TryParse(tok.Substring(6), out var n) && n >= 0 && n <= 9)
                {
                    key = Keys.NumPad0 + n;
                    continue;
                }

                // その他のキー名（Enum名）—Arrow, Home, End, Insert, Delete, etc.
                if (Enum.TryParse(tok, ignoreCase: true, out Keys k))
                {
                    // マウス系は不可
                    if (k == Keys.LButton || k == Keys.RButton || k == Keys.MButton ||
                        k == Keys.XButton1 || k == Keys.XButton2)
                        return false;

                    // 修飾単体は不可
                    if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu)
                        return false;

                    key = k;
                    continue;
                }

                // ここまで来たら不明なトークン
                return false;
            }

            if (key == Keys.None) return false;

            spec = new HotkeySpec { Mods = mods, Key = key };
            return true;
        }

        public static HotkeySpec ParseOrDefault(string text, HotkeySpec fallback)
            => TryParse(text, out var s) ? s : fallback;

        public static string ToText(HotkeySpec s)
        {
            // 表示は統一書式に（スペースなし/ありどちらでもOKだが、ここではなし）
            var sb = new System.Text.StringBuilder();
            if ((s.Mods & ModMask.Ctrl)  != 0) sb.Append("Ctrl+");
            if ((s.Mods & ModMask.Shift) != 0) sb.Append("Shift+");
            if ((s.Mods & ModMask.Alt)   != 0) sb.Append("Alt+");

            // D0..D9 は数字で表示
            if (s.Key >= Keys.D0 && s.Key <= Keys.D9)
                sb.Append(((int)s.Key - (int)Keys.D0).ToString());
            else
                sb.Append(s.Key.ToString());

            return sb.ToString();
        }

        public static Tuple<MOD_KEY, Keys> ToModAndKey(HotkeySpec s)
        {
            MOD_KEY mods = 0;
            if ((s.Mods & ModMask.Ctrl)  != 0) mods |= MOD_KEY.CONTROL;
            if ((s.Mods & ModMask.Shift) != 0) mods |= MOD_KEY.SHIFT;
            if ((s.Mods & ModMask.Alt)   != 0) mods |= MOD_KEY.ALT;
            return Tuple.Create(mods, s.Key);
        }

        public static bool IsValidKeyboardKey(Keys k)
        {
            if (k == Keys.None) return false;
            if (k == Keys.LButton || k == Keys.RButton || k == Keys.MButton ||
                k == Keys.XButton1 || k == Keys.XButton2) return false;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu) return false;
            return true;
        }
    }
}

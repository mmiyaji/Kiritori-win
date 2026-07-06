using System.Windows.Forms;
using Kiritori.Helpers;
using Xunit;

namespace Kiritori.Tests.Helpers
{
    public sealed class HotkeyUtilTests
    {
        [Theory]
        [InlineData("Ctrl+Shift+6", ModMask.Ctrl | ModMask.Shift, Keys.D6)]
        [InlineData(" control + alt + f1 ", ModMask.Ctrl | ModMask.Alt, Keys.F1)]
        [InlineData("CTRL+SHIFT+ALT+A", ModMask.Ctrl | ModMask.Shift | ModMask.Alt, Keys.A)]
        [InlineData("0", ModMask.None, Keys.D0)]
        [InlineData("F24", ModMask.None, Keys.F24)]
        [InlineData("NUMPAD9", ModMask.None, Keys.NumPad9)]
        [InlineData("Delete", ModMask.None, Keys.Delete)]
        [InlineData("Win+5", ModMask.None, Keys.D5)]
        [InlineData("Windows+F2", ModMask.None, Keys.F2)]
        [InlineData("Ctrl+Numpad1", ModMask.Ctrl, Keys.NumPad1)]
        public void TryParse_accepts_supported_hotkeys(string text, ModMask expectedMods, Keys expectedKey)
        {
            Assert.True(HotkeyUtil.TryParse(text, out var spec));
            Assert.Equal(expectedMods, spec.Mods);
            Assert.Equal(expectedKey, spec.Key);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("++")]
        [InlineData("Ctrl+Shift")]
        [InlineData("Win")]
        [InlineData("F0")]
        [InlineData("F25")]
        [InlineData("FX")]
        [InlineData("Numpad10")]
        [InlineData("NotAKey")]
        [InlineData("LButton")]
        [InlineData("RButton")]
        [InlineData("MButton")]
        [InlineData("XButton1")]
        [InlineData("XButton2")]
        [InlineData("ControlKey")]
        [InlineData("ShiftKey")]
        [InlineData("Menu")]
        public void TryParse_rejects_invalid_hotkeys(string text)
        {
            Assert.False(HotkeyUtil.TryParse(text, out var spec));
            Assert.Null(spec);
        }

        [Fact]
        public void ParseOrDefault_returns_parsed_value_when_valid()
        {
            var fallback = new HotkeySpec { Mods = ModMask.Alt, Key = Keys.F12 };

            var spec = HotkeyUtil.ParseOrDefault("Ctrl+7", fallback);

            Assert.Equal(ModMask.Ctrl, spec.Mods);
            Assert.Equal(Keys.D7, spec.Key);
        }

        [Fact]
        public void ParseOrDefault_returns_fallback_when_invalid()
        {
            var fallback = new HotkeySpec { Mods = ModMask.Alt, Key = Keys.F12 };

            var spec = HotkeyUtil.ParseOrDefault("Ctrl+Shift", fallback);

            Assert.Same(fallback, spec);
        }

        [Theory]
        [InlineData(ModMask.Ctrl | ModMask.Shift | ModMask.Alt, Keys.D9, "Ctrl+Shift+Alt+9")]
        [InlineData(ModMask.None, Keys.F5, "F5")]
        [InlineData(ModMask.Shift, Keys.NumPad4, "Shift+NumPad4")]
        public void ToText_formats_hotkeys(ModMask mods, Keys key, string expected)
        {
            var spec = new HotkeySpec { Mods = mods, Key = key };

            Assert.Equal(expected, HotkeyUtil.ToText(spec));
        }

        [Theory]
        [InlineData(ModMask.Ctrl | ModMask.Shift | ModMask.Alt, Keys.F10, MOD_KEY.CONTROL | MOD_KEY.SHIFT | MOD_KEY.ALT)]
        [InlineData(ModMask.None, Keys.F10, (MOD_KEY)0)]
        public void ToModAndKey_maps_modifier_mask(ModMask mods, Keys key, MOD_KEY expectedMods)
        {
            var spec = new HotkeySpec { Mods = mods, Key = key };

            var result = HotkeyUtil.ToModAndKey(spec);

            Assert.Equal(expectedMods, result.Item1);
            Assert.Equal(key, result.Item2);
        }

        [Theory]
        [InlineData(Keys.A, true)]
        [InlineData(Keys.F12, true)]
        [InlineData(Keys.None, false)]
        [InlineData(Keys.LButton, false)]
        [InlineData(Keys.RButton, false)]
        [InlineData(Keys.MButton, false)]
        [InlineData(Keys.XButton1, false)]
        [InlineData(Keys.XButton2, false)]
        [InlineData(Keys.ControlKey, false)]
        [InlineData(Keys.ShiftKey, false)]
        [InlineData(Keys.Menu, false)]
        public void IsValidKeyboardKey_filters_non_keyboard_or_modifier_keys(Keys key, bool expected)
        {
            Assert.Equal(expected, HotkeyUtil.IsValidKeyboardKey(key));
        }
    }
}

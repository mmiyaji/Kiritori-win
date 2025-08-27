using System.Windows.Forms;

namespace Kiritori
{
    public static class Localizer
    {
        /// <summary>
        /// フォーム全体に対してローカライズを再適用する。
        /// Tag に "loc:キー名" を指定したコントロールに SR.T(key) を適用。
        /// </summary>
        public static void Apply(Form form)
        {
            if (form == null) return;

            // フォームタイトル
            if (form.Tag is string tag && tag.StartsWith("loc:"))
                form.Text = SR.T(tag.Substring(4));

            ApplyControls(form.Controls);
        }

        private static void ApplyControls(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.Tag is string tag && tag.StartsWith("loc:"))
                    c.Text = SR.T(tag.Substring(4));

                // メニューやタブにも対応
                if (c is TabControl tc)
                {
                    foreach (TabPage tp in tc.TabPages)
                    {
                        if (tp.Tag is string ttag && ttag.StartsWith("loc:"))
                            tp.Text = SR.T(ttag.Substring(4));
                    }
                }
                else if (c is MenuStrip ms)
                {
                    foreach (ToolStripMenuItem item in ms.Items)
                        ApplyMenu(item);
                }

                if (c.HasChildren)
                    ApplyControls(c.Controls);
            }
        }

        private static void ApplyMenu(ToolStripMenuItem menu)
        {
            if (menu.Tag is string tag && tag.StartsWith("loc:"))
                menu.Text = SR.T(tag.Substring(4));
            foreach (ToolStripItem sub in menu.DropDownItems)
            {
                if (sub is ToolStripMenuItem sm)
                    ApplyMenu(sm);
            }
        }
    }
}

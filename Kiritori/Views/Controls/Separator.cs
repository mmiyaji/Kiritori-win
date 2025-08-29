using System.Drawing;
using System.Windows.Forms;

namespace Kiritori.Views.Controls
{
    public class Separator : Control
    {
        public Separator()
        {
            this.Height = 1;
            this.Dock = DockStyle.Top;
            this.Margin = new Padding(0, 6, 0, 6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(SystemColors.ControlLight))
            {
                e.Graphics.DrawLine(p, 0, this.Height / 2, this.Width, this.Height / 2);
            }
        }
    }
}

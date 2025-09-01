using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    sealed class GlassPanel : Panel
    {
        public int Alpha { get; set; } = 0; // 0..200 程度
        public int CornerRadius { get; set; } = 14;

        public GlassPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent; // 背景は描かない
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            /* 何もしない：親の背景塗りを抑制してフリッカ低減 */
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Alpha <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var path = RoundedRect(ClientRectangle, CornerRadius))
            using (var br = new SolidBrush(Color.FromArgb(Math.Min(Alpha, 200), 0, 0, 0)))
            {
                g.FillPath(br, path);
            }
        }

        // Alpha=0 のときはヒットテスト透過（操作を邪魔しない）
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST && Alpha <= 0)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var r = Math.Max(1, radius);
            int d = r * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    sealed class PauseOverlayPanel : Panel
    {
        public int Alpha { get; set; } = 0; // 0..255 だが 200 くらいまで使う
        public bool Paused { get; private set; } = false;
        public event EventHandler ClickPause;

        private bool _hot, _down;
        private Rectangle _btnRect;

        public PauseOverlayPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            Size = new Size(160, 56);
        }

        // protected override CreateParams CreateParams {
        //     get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; } // WS_EX_TRANSPARENT
        // }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 何も描かない（親の背景塗りを抑制）
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Alpha <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 背景の黒ガラス
            using (var br = new SolidBrush(Color.FromArgb(Math.Min(Alpha, 200), 0, 0, 0)))
            using (var path = RoundRect(ClientRectangle, 16))
                g.FillPath(br, path);

            // 中央ボタン（文字含む）
            var pad = 8;
            _btnRect = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);

            // ボタンのホバー／押下トーン
            int over = _down ? 80 : (_hot ? 40 : 24);
            using (var brBtn = new SolidBrush(Color.FromArgb(Math.Min(Alpha, 200) * over / 100, 255, 255, 255)))
            using (var pathBtn = RoundRect(_btnRect, 12))
                g.FillPath(brBtn, pathBtn);

            // ラベル
            string text = Paused ? "▶  Resume" : "⏸  Pause";
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var f = new Font(Font.FontFamily, 9f, FontStyle.Bold))
            using (var brText = new SolidBrush(Color.White))
                g.DrawString(text, f, brText, _btnRect, fmt);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = _btnRect.Contains(e.Location) && Alpha > 0;
            if (hot != _hot) { _hot = hot; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hot || _down) { _hot = _down = false; Invalidate(); }
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _btnRect.Contains(e.Location) && Alpha > 0)
            { _down = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_down && e.Button == MouseButtons.Left)
            {
                _down = false; Invalidate();
                if (_btnRect.Contains(e.Location) && Alpha > 0)
                    ClickPause?.Invoke(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        // Alpha==0 の時はヒットテスト透過（操作の邪魔をしない）
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST && Alpha <= 0) { m.Result = (IntPtr)HTTRANSPARENT; return; }
            base.WndProc(ref m);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}

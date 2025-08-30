using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kiritori.Helpers
{
#if DEBUG
    internal sealed class DebugUiOptions
    {
        public bool ShowBanner  { get; set; } = true;
        public bool PrefixTitle { get; set; } = true;
        public bool PatchIcon   { get; set; } = true;
        public bool Watermark   { get; set; } = false;
    }

    internal static class DebugUiDecorator
    {
        public static void Apply(Form f, DebugUiOptions opt = null)
        {
            if (opt == null) opt = new DebugUiOptions();

            // 1) タイトルに [DEBUG]
            if (opt.PrefixTitle && !f.Text.Contains("[DEBUG]"))
                f.Text = "[DEBUG] " + f.Text;

            // 2) バナー
            if (opt.ShowBanner)
                AddBanner(f);

            // 3) アイコンにバッジ
            if (opt.PatchIcon && f.Icon != null)
            {
                try
                {
                    var theme = GetTheme();
                    f.Icon = CreateDebugBadgedIcon(f.Icon, theme.BadgeColor);
                }
                catch { /* ignore */ }
            }

            // 4) 透かし
            if (opt.Watermark)
            {
                f.Paint  += (s, e) => DrawWatermark(e.Graphics, f.ClientRectangle);
                f.Resize += (s, e) => f.Invalidate();
            }
        }

        // --- Theme: EXE = Blue, MSIX = Red ---
        private struct DebugTheme
        {
            public Color BannerColor;
            public Color BadgeColor;
            public Color WatermarkColor;
            public string BannerText;
        }

        private static DebugTheme GetTheme()
        {
            var isPackaged = PackagedHelper.IsPackaged();

            if (isPackaged)
            {
                // MSIX パッケージ版（赤系）
                return new DebugTheme
                {
                    BannerColor    = Color.FromArgb(230, 200, 30, 30),
                    BadgeColor     = Color.FromArgb(255, 210, 30, 30),
                    WatermarkColor = Color.FromArgb(40, 200, 0, 0),
                    BannerText     = "⚠ DEBUG BUILD (MSIX パッケージ版)"
                };
            }
            else
            {
                // 通常 EXE 版（青系）
                return new DebugTheme
                {
                    BannerColor    = Color.FromArgb(230, 30, 80, 200),
                    BadgeColor     = Color.FromArgb(255, 30, 80, 200),
                    WatermarkColor = Color.FromArgb(40, 0, 40, 200),
                    BannerText     = "⚠ DEBUG BUILD (EXE 版)"
                };
            }
        }

        private static void AddBanner(Form f)
        {
            var theme = GetTheme();

            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = theme.BannerColor
            };

            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font(SystemFonts.CaptionFont, FontStyle.Bold),
                Padding = new Padding(10, 0, 10, 0),
                Text = theme.BannerText
            };

            var right = new Label
            {
                Dock = DockStyle.Right,
                Width = 220,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White,
                Font = SystemFonts.CaptionFont,
                Padding = new Padding(0, 0, 10, 0),
                Text = string.Format("PID {0} • {1:yyyy-MM-dd HH:mm}",
                                    System.Diagnostics.Process.GetCurrentProcess().Id,
                                    DateTime.Now)
            };

            panel.Controls.Add(lbl);
            panel.Controls.Add(right);
            f.SuspendLayout();
            f.Controls.Add(panel);
            f.Controls.SetChildIndex(panel, f.Controls.Count - 1); // Dock レイアウト先行（タブに被らない）
            f.Size = new Size(f.Width, f.Height + panel.Height);
            f.ResumeLayout(true);
        }

        private static void DrawWatermark(Graphics g, Rectangle bounds)
        {
            var theme = GetTheme();

            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                var size = Math.Max(36, bounds.Width / 12f);
                using (var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var path = new GraphicsPath())
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TranslateTransform(bounds.Width / 2f, bounds.Height / 2f);
                    g.RotateTransform(-30f);

                    path.AddString("DEBUG", font.FontFamily, (int)FontStyle.Bold, font.Size,
                                   new PointF(-bounds.Width, -font.Size), sf);

                    using (var brush = new SolidBrush(theme.WatermarkColor))
                        g.FillPath(brush, path);

                    g.ResetTransform();
                }
            }
        }

        private static Icon CreateDebugBadgedIcon(Icon baseIcon, Color badgeColor)
        {
            var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawIcon(baseIcon, new Rectangle(0, 0, 32, 32));

                var r = new Rectangle(32 - 14, 32 - 14, 14, 14);
                using (var bg = new SolidBrush(badgeColor))
                using (var pen = new Pen(Color.White, 1f))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(bg, r);
                    g.DrawEllipse(pen, r);

                    using (var fnt = new Font("Segoe UI", 8, FontStyle.Bold))
                    using (var white = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("D", fnt, white, new RectangleF(r.X, r.Y - 1, r.Width, r.Height), sf);
                    }
                }
            }

            // メモ: FromHandle は GC で破棄されないので、必要なら DestroyIcon を使って自前で後始末してください。
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
#endif
}

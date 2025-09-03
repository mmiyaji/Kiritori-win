using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== クリックイベントから呼ばれる関数（イベントハンドラ群） =====
        private void captureToolStripMenuItem_Click(object sender, EventArgs e) { this.ma.openScreen(); }
        private void closeESCToolStripMenuItem_Click(object sender, EventArgs e) { this.Close(); }
        private void cutCtrlXToolStripMenuItem_Click(object sender, EventArgs e) { Clipboard.SetImage(this.pictureBox1.Image); this.Close(); }
        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e) { Clipboard.SetImage(this.pictureBox1.Image); ShowOverlay("Copy"); }
        private void ocrCtrlTToolStripMenuItem_Click(object sender, EventArgs e) { RunOcrOnCurrentImage(); }
        private void keepAfloatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            ShowOverlay("Keep Afloat: " + this.TopMost);
            this.keepAfloatToolStripMenuItem.Checked = this.TopMost;
            this.isAfloatWindow = this.TopMost;
        }
        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e) { saveImage(); }
        private void openImageToolStripMenuItem_Click(object sender, EventArgs e) { openImage(); }
        private void originalLocationToolStripMenuItem_Click(object sender, EventArgs e) { initLocation(); }
        private void originalSizeToolStripMenuItem_Click(object sender, EventArgs e) { zoomOff(); }
        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e) { zoomIn(); }
        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e) { zoomOut(); }
        private void size10ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(10); }
        private void size50ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(50); }
        private void size100ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(100); }
        private void size150ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(150); }
        private void size200ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(200); }
        private void size500ToolStripMenuItem_Click(object sender, EventArgs e) { ZoomToPercent(500); }
        private void dropShadowToolStripMenuItem_Click(object sender, EventArgs e) { ToggleShadow(!this.isWindowShadow); }
        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e) { PrefForm.ShowSingleton(this.ma); }
        private void printToolStripMenuItem_Click(object sender, EventArgs e) { printImage(); }
        private void opacity100toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(1.0); ShowOverlay("Opacity: 100%"); }
        private void opacity90toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.9); ShowOverlay("Opacity: 90%"); }
        private void opacity80toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.8); ShowOverlay("Opacity: 80%"); }
        private void opacity50toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.5); ShowOverlay("Opacity: 50%"); }
        private void opacity30toolStripMenuItem_Click(object sender, EventArgs e) { setAlpha(0.3); ShowOverlay("Opacity: 30%"); }
        private void minimizeToolStripMenuItem_Click(object sender, EventArgs e) { this.minimizeWindow(); }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e) { Application.Exit(); }

        // MSPaint（起動→終了で再読込）
        private void editPaintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (pictureBox1.Image == null)
                {
                    MessageBox.Show(this, "No image to edit.", "Kiritori",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string preferredSrcPath = pictureBox1.Image?.Tag as string;

                if (!string.IsNullOrEmpty(preferredSrcPath) && File.Exists(preferredSrcPath))
                {
                    _paintEditPath = preferredSrcPath;
                }
                else
                {
                    _paintEditPath = Path.Combine(
                        Path.GetTempPath(),
                        $"Kiritori_Edit_{DateTime.Now:yyyyMMdd_HHmmssfff}.png"
                    );
                    using (var bmp = new Bitmap(pictureBox1.Image))
                    {
                        bmp.Save(_paintEditPath, ImageFormat.Png);
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "mspaint.exe",
                    Arguments = $"\"{_paintEditPath}\"",
                    UseShellExecute = true
                };

                var proc = Process.Start(psi);
                if (proc == null) return;

                proc.EnableRaisingEvents = true;
                proc.Exited += (s, ev) =>
                {
                    try
                    {
                        if (File.Exists(_paintEditPath))
                        {
                            using (var fs = new FileStream(_paintEditPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var img = Image.FromStream(fs))
                            {
                                var updated = new Bitmap(img);
                                this.BeginInvoke((Action)(() =>
                                {
                                    var old = pictureBox1.Image;
                                    pictureBox1.Image = updated;
                                    if (string.IsNullOrEmpty(preferredSrcPath))
                                        pictureBox1.Image.Tag = _paintEditPath;
                                    old?.Dispose();
                                }));
                            }
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Failed to load edited image: " + _paintEditPath);
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open in Paint.\r\n" + ex.Message,
                    "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // オーバーレイ
            if (!string.IsNullOrEmpty(_overlayText))
            {
                double elapsed = (DateTime.Now - _overlayStart).TotalMilliseconds;
                int alpha = 200;
                double remain = _overlayDurationMs - elapsed;
                if (remain < _overlayFadeMs)
                {
                    alpha = (int)(alpha * (remain / _overlayFadeMs));
                    if (alpha < 0) alpha = 0;
                }

                int padding = (int)(10 * (_dpi / 96f));
                SizeF ts = g.MeasureString(_overlayText, _overlayFont);
                int w = (int)Math.Ceiling(ts.Width) + padding * 2;
                int h = (int)Math.Ceiling(ts.Height) + padding * 2;

                int margin = (int)(12 * (_dpi / 96f));
                int x = pictureBox1.ClientSize.Width - w - margin;
                int y = pictureBox1.ClientSize.Height - h - margin;

                using (var path = RoundedRect(new Rectangle(x, y, w, h), (int)(8 * (_dpi / 96f))))
                using (var bg = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                using (var pen = new Pen(Color.FromArgb(Math.Max(80, alpha), 255, 255, 255), 1f))
                using (var txt = new SolidBrush(Color.FromArgb(Math.Max(180, alpha), 255, 255, 255)))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(pen, path);
                    g.DrawString(_overlayText, _overlayFont, txt, x + padding, y + padding);
                }
            }

            // ホバー枠
            if (isHighlightOnHover && _hoverWindow && _hoverAlphaPercent > 0 && _hoverThicknessPx > 0)
            {
                using (var pen = MakeHoverPen())
                {
                    var r = pictureBox1.ClientRectangle;
                    if (r.Width > 0 && r.Height > 0)
                    {
                        pen.Alignment = PenAlignment.Center;
                        g.DrawRectangle(pen, r);
                    }
                }
            }

            // 右上クローズ
            if (_hoverWindow &&
                pictureBox1.ClientSize.Width >= 100 &&
                pictureBox1.ClientSize.Height >= 50)
            {
                _closeBtnRect = GetCloseRect();

                // 常時うっすら背景（白地でも視認）＋ ホバー時は濃く
                int baseAlpha  = 40;  // 非ホバー時
                int hoverAlpha = 90;  // ホバー時
                int alpha = _hoverClose ? hoverAlpha : baseAlpha;

                using (var bg = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillEllipse(bg, _closeBtnRect);

                // X（ホバー時は太め）
                int inset = Math.Max(2, DpiScale(4));
                var r = Rectangle.Inflate(_closeBtnRect, -inset, -inset);

                float w = _hoverClose
                    ? Math.Max(2f, (float)DpiScale(2))
                    : Math.Max(1.5f, (float)DpiScale(1));   // ← int キャスト注意

                using (var pen = new Pen(Color.White, w))
                {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(pen, r.Left,  r.Top,    r.Right, r.Bottom);
                    g.DrawLine(pen, r.Right, r.Top,    r.Left,  r.Bottom);
                }

                // うっすら外周（ガイド）
                using (var guide = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                    g.DrawEllipse(guide, _closeBtnRect);
            }
            else
            {
                _closeBtnRect = Rectangle.Empty;
                _hoverClose = false;
            }
        }

        private void PictureBox1_MouseMove_Icon(object sender, MouseEventArgs e)
        {
            var rect = GetCloseRect();       // ここで毎回最新を計算
            _closeBtnRect = rect;            // フィールドも更新
            bool now = rect.Contains(e.Location);
            if (now != _hoverClose)
            {
                _hoverClose = now;
                pictureBox1.Invalidate(rect);
                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
            }
        }

        private void PictureBox1_MouseClick_Icon(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (GetCloseRect().Contains(e.Location)) this.Close();
        }

        public void printImage()
        {
            using (var printDialog1 = new PrintDialog
            {
                PrinterSettings = new System.Drawing.Printing.PrinterSettings()
            })
            {
                if (printDialog1.ShowDialog() == DialogResult.OK)
                {
                    using (var pd = new System.Drawing.Printing.PrintDocument())
                    {
                        pd.PrintPage += pd_PrintPage;
                        pd.Print();
                    }
                    ShowOverlay("Printed");
                }
            }
        }

        private void pd_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            e.Graphics.DrawImage(this.pictureBox1.Image, e.MarginBounds);
            e.HasMorePages = false;
        }

        public void minimizeWindow()
        {
            this.WindowState = FormWindowState.Minimized;
            ShowOverlay("Minimized");
        }
        public void showWindow()
        {
            this.WindowState = FormWindowState.Normal;
            ShowOverlay("Show");
        }
        public void closeWindow()
        {
            ShowOverlay("Close");
            this.Close();
        }

        public void copyImage(object sender) => copyCtrlCToolStripMenuItem_Click(sender, EventArgs.Empty);
        public void closeImage(object sender) => closeESCToolStripMenuItem_Click(sender, EventArgs.Empty);
        public void afloatImage(object sender) => keepAfloatToolStripMenuItem_Click(sender, EventArgs.Empty);
        public void editInMSPaint(object sender)
        {
            editPaintToolStripMenuItem_Click(sender, EventArgs.Empty);
            ShowOverlay("Edit");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_settingsHandler != null)
            {
                try { Properties.Settings.Default.PropertyChanged -= _settingsHandler; } catch { }
                _settingsHandler = null;
            }

            if (this.Icon != null) { this.Icon.Dispose(); this.Icon = null; }
            base.OnFormClosed(e);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this._originalLocation = this.Location;
            Debug.WriteLine($"Original Location: {this._originalLocation}");
        }
        private int DpiScale(int px) => (int)Math.Round(px * this.DeviceDpi / 96.0);

        private Rectangle GetCloseRect()
        {
            int pad = DpiScale(6);
            int sz  = DpiScale(20);
            return new Rectangle(pictureBox1.ClientSize.Width - pad - sz, pad, sz, sz);
        }


        #endregion

    }
}
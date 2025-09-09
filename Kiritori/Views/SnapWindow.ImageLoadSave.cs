using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Kiritori.Services.Logging;
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
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== 画像キャプチャ / ロード・セーブ / 適用 =====
        public void CaptureFromBitmap(Bitmap source, Rectangle crop, Point desiredScreenPos, LoadMethod loadMethod = LoadMethod.Capture)
        {
            Rectangle safe = Rectangle.Intersect(crop, new Rectangle(0, 0, source.Width, source.Height));
            if (safe.Width <= 0 || safe.Height <= 0) return;

            Bitmap cropped = source.Clone(safe, source.PixelFormat);

            SetLoadMethod(loadMethod);
            ApplyBitmap(cropped);
            AlignClientTopLeftToScreen(desiredScreenPos);
        }

        private void ApplyBitmap(Bitmap bmp)
        {
            if (pictureBox1.Image != null && !ReferenceEquals(pictureBox1.Image, bmp))
            {
                try { pictureBox1.Image.Dispose(); } catch { }
            }
            if (main_image != null && !ReferenceEquals(main_image, bmp))
            {
                try { main_image.Dispose(); } catch { }
            }

            this.Size = bmp.Size;
            pictureBox1.Size = bmp.Size;

            SetImageAndResetZoom(bmp);
            pictureBox1.Image = bmp;

            date = DateTime.Now;
            this.Text = date.ToString("yyyyMMdd-HHmmss") + ".png";
            this.TopMost = this.AlwaysOnTop;
            this.Opacity = this.WindowOpacityPercent;

            this.main_image = bmp;
            this.setThumbnail(bmp);
            if (!SuppressHistory) ma.setHistory(this);
            ShowOverlay("KIRITORI");
        }

        public void openImage() => this.ma.openImage();
        public void openClipboard() => this.ma.pasteFromClipboard();

        public void saveImage()
        {
            using (var sfd = new SaveFileDialog
            {
                FileName = this.Text,
                Filter = "Image Files(*.png;*.PNG)|*.png;*.PNG|All Files(*.*)|*.*",
                FilterIndex = 1,
                Title = "Select a path to save the image",
                RestoreDirectory = true,
                OverwritePrompt = true,
                CheckPathExists = true
            })
            {
                try
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        this.pictureBox1.Image.Save(sfd.FileName);
                        Log.Info("Image saved: " + sfd.FileName, "SnapWindow");
                        ShowOverlay("SAVED");
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("Image save failed: " + ex.Message, "SnapWindow");
                    throw;
                }
            }
        }

        public void loadImage()
        {
            try
            {
                using (var ofd = new OpenFileDialog
                {
                    Title = "Load Image",
                    Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*",
                    FilterIndex = 1,
                    ValidateNames = true,
                    RestoreDirectory = true
                })
                {
                    try
                    {
                        if (ofd.ShowDialog() != DialogResult.OK) return;
                        Log.Info("Image loaded: " + ofd.FileName, "SnapWindow");

                        var bmp = LoadBitmapClone(ofd.FileName);
                        ApplyImage(bmp, ofd.FileName, addHistory: !SuppressHistory, showOverlay: true);
                        SetLoadMethod(LoadMethod.Path);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Image load failed: " + ex.Message, "SnapWindow");
                        throw;
                    }
                }
            }
            catch
            {
                // 必要なら通知
            }
        }

        public void setImageFromPath(string fname)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fname) || !File.Exists(fname)) return;
                SetLoadMethod(LoadMethod.Path);

                var bmp = LoadBitmapClone(fname);
                ApplyImage(bmp, fname, addHistory: !SuppressHistory, showOverlay: true);
            }
            catch
            {
                // 無視
            }
        }

        public void setImageFromBMP(Bitmap bmp, LoadMethod method = LoadMethod.History)
        {
            if (bmp == null) return;
            using (var clone = new Bitmap(bmp))
            {
                ApplyImage(clone, titlePath: null, addHistory: false, showOverlay: false);
                SetLoadMethod(method);
            }
        }

        private void ApplyImage(Bitmap bmp, string titlePath, bool addHistory, bool showOverlay)
        {
            var wa = Screen.FromControl(this).WorkingArea;
            var target = FitInto(bmp.Size, wa.Size);

            this.SuspendLayout();
            try
            {
                this.Size = target;
                var old = pictureBox1.Image;
                pictureBox1.Image = null;

                if (pictureBox1.Size != target)
                    pictureBox1.Size = target;

                SetImageAndResetZoom(bmp);
                pictureBox1.Image = bmp;
                old?.Dispose();

                if (!string.IsNullOrEmpty(titlePath))
                    this.Text = titlePath;

                this.TopMost = this.AlwaysOnTop;
                this.Opacity = this.WindowOpacityPercent;
                this.StartPosition = FormStartPosition.Manual;

                var loc = new Point(
                    wa.Left + (wa.Width - this.Width) / 2,
                    wa.Top + (wa.Height - this.Height) / 2
                );
                this.Location = loc;

                this.main_image = bmp;
                this.setThumbnail(bmp);
                ApplyInitialDisplayZoomIfNeeded();

                if (addHistory) ma.setHistory(this);
                if (showOverlay) ShowOverlay("LOADED");
            }
            finally
            {
                this.ResumeLayout();
            }
        }

        private static Size FitInto(Size src, Size box)
        {
            if (src.Width <= box.Width && src.Height <= box.Height) return src;

            double rw = (double)box.Width / src.Width;
            double rh = (double)box.Height / src.Height;
            double r = Math.Min(rw, rh);

            int w = Math.Max(1, (int)Math.Round(src.Width * r));
            int h = Math.Max(1, (int)Math.Round(src.Height * r));
            return new Size(w, h);
        }

        private static Bitmap LoadBitmapClone(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false))
            {
                return new Bitmap(img);
            }
        }

        private void setThumbnail(Bitmap bmp)
        {
            this.main_image = bmp;
            if (bmp.Size.Width > THUMB_WIDTH)
            {
                int resizeWidth = THUMB_WIDTH;
                int resizeHeight = (int)(bmp.Height * ((double)resizeWidth / (double)bmp.Width));
                Bitmap resizeBmp = new Bitmap(resizeWidth, resizeHeight);
                using (Graphics g = Graphics.FromImage(resizeBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, resizeWidth, resizeHeight);
                }
                this.thumbnail_image = resizeBmp;
            }
            else
            {
                this.thumbnail_image = bmp;
            }
        }

        public void AlignClientTopLeftToScreen(Point targetScreenPoint)
        {
            if (!this.IsHandleCreated) this.CreateControl();
            if (!this.Visible) this.Show();

            var clientTopLeftOnScreen = this.PointToScreen(Point.Empty);
            var dx = targetScreenPoint.X - clientTopLeftOnScreen.X;
            var dy = targetScreenPoint.Y - clientTopLeftOnScreen.Y;
            if (dx != 0 || dy != 0)
            {
                this.Location = new Point(this.Location.X + dx, this.Location.Y + dy);
            }
        }
        private void RefreshFromOriginalHiQ()
        {
            if (_originalImage == null) return;

            var vw = Math.Max(1, pictureBox1.ClientSize.Width);
            var vh = Math.Max(1, pictureBox1.ClientSize.Height);

            var bmp = new Bitmap(vw, vh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // ウィンドウにフィット（縦横比は固定しない＝以前の挙動）
                g.DrawImage(_originalImage, new Rectangle(0, 0, vw, vh));
            }

            var old = pictureBox1.Image;
            // pictureBox1.SizeMode = PictureBoxSizeMode.Normal; // ← もう描画済みBitmapなのでストレッチ不要
            pictureBox1.Image = bmp;
            old?.Dispose();
            Log.Debug($"Refreshed from original hi-q: {bmp.Width}x{bmp.Height}", "SnapWindow");
        }


        #endregion

    }
}
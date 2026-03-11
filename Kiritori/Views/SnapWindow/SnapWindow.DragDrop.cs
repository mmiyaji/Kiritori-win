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
        #region ===== DnD =====
        public void InitializeResizePreviewPipeline()
        {
            if (_resizeHooksSet) return;

            // Formの標準イベントで“サイズ変更ドラッグ”の開始・終了を拾う
            this.ResizeBegin += SnapWindow_ResizeBegin;
            this.ResizeEnd += SnapWindow_ResizeEnd;
            this.SizeChanged += SnapWindow_SizeChanged;

            _resizeCommitTimer = new Timer { Interval = _resizeCommitDelayMs };
            _resizeCommitTimer.Tick += (s, e) =>
            {
                _resizeCommitTimer.Stop();
                CommitResizeToHighQuality();
            };

            _resizeHooksSet = true;
        }

        private void SnapWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Any(IsValidImageFile))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void SnapWindow_DragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            var img = paths.FirstOrDefault(IsValidImageFile);
            if (string.IsNullOrEmpty(img)) return;

            try
            {
                this.setImageFromPath(img);
            }
            catch (Exception ex)
            {
                MessageBox.Show(SR.T("Text.DragDropFailed", "Failed open image") + ":\n" + ex.Message, "Kiritori", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (Directory.Exists(path)) return false;
            if (!File.Exists(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExts.Contains(ext);
        }

 // ====== イベント ======
        private void SnapWindow_ResizeBegin(object sender, EventArgs e)
        {
            if (_originalImage == null) return;
            _isResizeInteractive = true;
            EnsureOriginalStill();                 // 非アニメ静止ビットマップを用意
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            // プレビューは軽量に：一旦“1段ストレッチ表示”へ
            if (!ReferenceEquals(pictureBox1.Image, _originalStill))
            {
                var prev = pictureBox1.Image;
                pictureBox1.Image = _originalStill;
                if (prev != null &&
                    !ReferenceEquals(prev, _originalImage) &&
                    !ReferenceEquals(prev, _originalStill))
                {
                    try { prev.Dispose(); } catch { }
                }
            }
        }

        private void SnapWindow_SizeChanged(object sender, EventArgs e)
        {
            if (_originalImage == null) return;

            if (_isResizeInteractive)
            {
                // プレビュー：Bitmap 再生成はしない。PictureBox をウィンドウサイズに追随させるだけ
                pictureBox1.Size = this.ClientSize;

                // 現在の幅から一旦 _scale を更新（ゆがみは許容。確定時に正しい比率で再描画）
                var targetScale = (float)this.ClientSize.Width / Math.Max(1, (float)_originalImage.Width);
                _scale = Clamp(targetScale, MIN_SCALE, MAX_SCALE);
                _zoomStep = (int)Math.Round((_scale - 1.0f) / STEP_LINEAR);

                // 連続ドラッグでも「少し手を止めた瞬間」に高品質へ確定させられるようコマ送り
                RestartResizeCommitTimer();
            }
        }

        private void SnapWindow_ResizeEnd(object sender, EventArgs e)
        {
            if (_originalImage == null) return;
            _isResizeInteractive = false;
            _resizeCommitTimer.Stop();
            CommitResizeToHighQuality();           // 終了時に必ず高品質で確定
        }

        // ====== コミット処理（高品質再サンプル） ======
        private void CommitResizeToHighQuality()
        {
            if (_originalImage == null) return;

            // いまのウィンドウ幅・高さから最終スケールを決める
            // ※ アスペクトは“幅基準”で確定（高さは ApplyZoom で正される）
            float targetScale = (float)this.ClientSize.Width / Math.Max(1, (float)_originalImage.Width);
            _scale = Clamp(targetScale, MIN_SCALE, MAX_SCALE);
            _zoomStep = (int)Math.Round((_scale - 1.0f) / STEP_LINEAR);

            // 高品質で一度だけ再サンプル描画（ApplyZoom は pictureBox1.Image を新規Bitmapへ）
            ApplyZoom(redrawOnly: false, interactive: false);

            // 表示は新しいBitmapに切り替わったので、静止ビットマップは再利用可（破棄は任意）
            // _originalStill は次回リサイズ用に保持してOK。メモリを気にするなら Dispose しても良い。
        }

        // ====== ヘルパー ======
        private void RestartResizeCommitTimer()
        {
            if (_resizeCommitTimer == null) return;
            _resizeCommitTimer.Stop();
            _resizeCommitTimer.Interval = _resizeCommitDelayMs;
            _resizeCommitTimer.Start();
        }

        private void EnsureOriginalStill()
        {
            if (_originalImage == null) return;

            if (_originalStill != null &&
                _originalStill.Width  == _originalImage.Width &&
                _originalStill.Height == _originalImage.Height)
            {
                return; // 既に最新
            }

            try { _originalStill?.Dispose(); } catch { }

            _originalStill = new Bitmap(_originalImage.Width, _originalImage.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(_originalStill))
            {
                // ただのピクセルに落とす（アニメ情報を持ち込まない）
                g.CompositingMode    = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.InterpolationMode  = InterpolationMode.NearestNeighbor;
                g.SmoothingMode      = SmoothingMode.None;
                g.PixelOffsetMode    = PixelOffsetMode.HighSpeed;
                g.DrawImageUnscaled(_originalImage, 0, 0);
            }
        }
        #endregion

    }
}
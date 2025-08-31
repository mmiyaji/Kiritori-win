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
        #region ===== ズーム関連 =====
        public void zoomIn()
        {
            if (_smoothZoomEnabled)
            {
                // 直近入力が短時間なら“ゴール基準”、そうでなければ見かけ基準
                bool inBurst = (DateTime.UtcNow - _lastZoomInputUtc).TotalMilliseconds <= INPUT_BURST_MS;
                float baseGoal = (inBurst && _isZoomAnimating) ? _animToScale : _scale;

                // 1ステップ先へ
                float targetScale = baseGoal + STEP_LINEAR;

                // 先行しすぎ防止：現在の見かけ位置から MAX_PENDING_STEPS までしか先行させない
                float maxAhead = _scale + STEP_LINEAR * MAX_PENDING_STEPS;
                targetScale = Math.Min(targetScale, maxAhead);

                // クランプして発車
                targetScale = Clamp(targetScale, MIN_SCALE, MAX_SCALE);
                _lastZoomInputUtc = DateTime.UtcNow;

                StartZoomAnimationToScale(targetScale, 140);
                ShowOverlay("Zoom " + (int)Math.Round(targetScale * 100) + "%");
            }
            else
            {
                _zoomStep++;
                UpdateScaleFromStep();
                ApplyZoom(false);
                ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
            }
        }

        public void zoomOut()
        {
            if (_smoothZoomEnabled)
            {
                bool inBurst = (DateTime.UtcNow - _lastZoomInputUtc).TotalMilliseconds <= INPUT_BURST_MS;
                float baseGoal = (inBurst && _isZoomAnimating) ? _animToScale : _scale;

                float targetScale = baseGoal - STEP_LINEAR;

                float minBehind = _scale - STEP_LINEAR * MAX_PENDING_STEPS;
                targetScale = Math.Max(targetScale, minBehind);

                targetScale = Clamp(targetScale, MIN_SCALE, MAX_SCALE);
                _lastZoomInputUtc = DateTime.UtcNow;

                StartZoomAnimationToScale(targetScale, 140);
                ShowOverlay("Zoom " + (int)Math.Round(targetScale * 100) + "%");
            }
            else
            {
                _zoomStep--;
                UpdateScaleFromStep();
                ApplyZoom(false);
                ShowOverlay("Zoom " + (int)Math.Round(_scale * 100) + "%");
            }
        }

        public void zoomOff()
        {
            // if (_smoothZoomEnabled)
            // {
            //     StartZoomAnimationToScale(1.0f, 140);
            //     ShowOverlay("Zoom 100%");
            // }
            // else
            // {
                _zoomStep = 0;
                UpdateScaleFromStep();
                ApplyZoom(false);
                ShowOverlay("Zoom 100%");
            // }
        }

        public void ZoomToPercent(int percent)
        {
            if (_smoothZoomEnabled)
            {
                float targetScale = Clamp(percent / 100f, MIN_SCALE, MAX_SCALE);
                StartZoomAnimationToScale(targetScale, 160);
                ShowOverlay("Zoom " + percent + "%");
            }
            else
            {
                _zoomStep = (int)Math.Round((percent - 100) / (STEP_LINEAR * 100f));
                UpdateScaleFromStep();
                ApplyZoom(false);
                ShowOverlay("Zoom " + percent + "%");
            }
        }

        private void UpdateScaleFromStep()
        {
            _scale = 1.0f + (_zoomStep * STEP_LINEAR);
            if (_scale < MIN_SCALE)
            {
                _scale = MIN_SCALE;
                _zoomStep = (int)Math.Round((MIN_SCALE - 1.0f) / STEP_LINEAR);
            }
            else if (_scale > MAX_SCALE)
            {
                _scale = MAX_SCALE;
                _zoomStep = (int)Math.Round((MAX_SCALE - 1.0f) / STEP_LINEAR);
            }
        }

        private void SetImageAndResetZoom(Image img)
        {
            _originalImage?.Dispose();
            _originalImage = (Image)img.Clone();

            _scale = 1f;
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage; // ストレッチ採用
            ApplyZoom(false);
        }
        private bool _pinWindowLocationDuringZoom = true;
        private void ApplyZoom(bool redrawOnly) { ApplyZoom(redrawOnly, false); }
        private void ApplyZoom(bool redrawOnly, bool interactive)
        {
            if (_originalImage == null) return;

            int newW = Math.Max(1, (int)Math.Round(_originalImage.Width * _scale));
            int newH = Math.Max(1, (int)Math.Round(_originalImage.Height * _scale));

            Size oldClient = this.ClientSize;

            Bitmap bmp = new Bitmap(newW, newH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.CompositingQuality = interactive ? CompositingQuality.HighSpeed : CompositingQuality.HighQuality;
                g.InterpolationMode = interactive ? InterpolationMode.Low : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = interactive ? PixelOffsetMode.None : PixelOffsetMode.HighQuality;

                g.DrawImage(_originalImage, 0, 0, newW, newH);
            }

            var oldImg = pictureBox1.Image;
            pictureBox1.Image = bmp;
            oldImg?.Dispose();

            pictureBox1.Width = newW;
            pictureBox1.Height = newH;

            // 既存仕様どおりフォームも変える場合は残す：
            this.ClientSize = new Size(newW, newH);

            if (!redrawOnly && !_pinWindowLocationDuringZoom)
            {
                int dx = (this.ClientSize.Width - oldClient.Width) / 2;
                int dy = (this.ClientSize.Height - oldClient.Height) / 2;
                this.Left -= dx;
                this.Top  -= dy;
            }
        }

        private void ApplyInitialDisplayZoomIfNeeded()
        {
            if (_originalImage == null) return;

            var wa = Screen.FromControl(this).WorkingArea;

            double capByHalfWidth = (wa.Width * 0.5) / (double)_originalImage.Width;
            double capByHeight = (wa.Height * 1.0) / (double)_originalImage.Height;
            double desired = Math.Min(1.0, Math.Min(capByHalfWidth, capByHeight));

            if (desired >= 1.0) return;

            double clamped = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, desired));
            int step = (int)Math.Round((clamped - 1.0) / STEP_LINEAR);

            _zoomStep = step;
            UpdateScaleFromStep();

            ApplyZoom(redrawOnly: false);
        }

        // スケール→ステップへ逆変換して整合性を取るヘルパー
        private void UpdateStepFromScale()
        {
            // クランプ
            if (_scale < MIN_SCALE) _scale = MIN_SCALE;
            if (_scale > MAX_SCALE) _scale = MAX_SCALE;

            // 既存ロジックの逆計算： step = round((scale - 1.0) / STEP_LINEAR)
            _zoomStep = (int)Math.Round((_scale - 1.0f) / STEP_LINEAR);
        }

        // 横幅を絶対ピクセルで指定（アスペクト比維持）
        public void ZoomToWidth(int targetWidthPx)
        {
            if (_originalImage == null) return;
            if (targetWidthPx <= 0) return;

            // scale = 目標幅 / 元画像幅
            _scale = (float)targetWidthPx / (float)_originalImage.Width;
            UpdateStepFromScale();
            ApplyZoom(false);
            ShowOverlay("Width " + targetWidthPx + "px  (" + (int)Math.Round(_scale * 100) + "%)");
        }

        // 高さを絶対ピクセルで指定（アスペクト比維持）
        public void ZoomToHeight(int targetHeightPx)
        {
            if (_originalImage == null) return;
            if (targetHeightPx <= 0) return;

            // scale = 目標高 / 元画像高
            _scale = (float)targetHeightPx / (float)_originalImage.Height;
            UpdateStepFromScale();
            ApplyZoom(false);
            ShowOverlay("Height " + targetHeightPx + "px  (" + (int)Math.Round(_scale * 100) + "%)");
        }

        // 指定ボックスに“収まる最大サイズ”でフィット（アスペクト比維持）
        public void ZoomToFitBox(int maxWidthPx, int maxHeightPx)
        {
            if (_originalImage == null) return;
            if (maxWidthPx <= 0 || maxHeightPx <= 0) return;

            // それぞれの制約から許容スケールを求め、より厳しい方（小さい方）を採用
            float sw = (float)maxWidthPx / (float)_originalImage.Width;
            float sh = (float)maxHeightPx / (float)_originalImage.Height;
            _scale = Math.Min(sw, sh);

            // 1.0 を超える指定でも OK（MAX_SCALE 側で最終クランプ）
            UpdateStepFromScale();
            ApplyZoom(false);

            int outW = Math.Max(1, (int)Math.Round(_originalImage.Width * _scale));
            int outH = Math.Max(1, (int)Math.Round(_originalImage.Height * _scale));
            ShowOverlay($"Fit {outW}x{outH}px  ({(int)Math.Round(_scale * 100)}%)");
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
        // ほどよい減速感（EaseOutCubic）
        private static float EaseOutCubic(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return 1f - (float)Math.Pow(1f - t, 3);
        }

        // scale -> step へ同期（既存の UpdateScaleFromStep の逆）
        private void SyncZoomStepToScale()
        {
            _scale = Clamp(_scale, MIN_SCALE, MAX_SCALE);
            _zoomStep = (int)Math.Round((_scale - 1.0f) / STEP_LINEAR);
        }
        private void EnsureAnimTimer()
        {
            if (_zoomAnimTimer != null) return;
            _zoomAnimTimer = new System.Windows.Forms.Timer();
            _zoomAnimTimer.Interval = 16; // ほぼ60fps
            _zoomAnimTimer.Tick += ZoomAnimTimer_Tick;
        }

        private void StartZoomAnimationToScale(float targetScale, int baseDurationPerStepMs)
        {
            if (_originalImage == null) return;
            EnsureAnimTimer();

            float newTo   = Clamp(targetScale, MIN_SCALE, MAX_SCALE);
            float current = _scale; // Tickで常に更新される“見かけの現在値”

            // 残距離→ステップ数
            float steps = Math.Abs(newTo - current) / STEP_LINEAR;

            // 距離に対する時間の伸びは sqrt にしてダラダラ化を抑制、さらに絶対上限/下限でクランプ
            int duration = (int)Math.Round(baseDurationPerStepMs * Math.Sqrt(Math.Max(1f, steps)));
            duration = Math.Max(MIN_ANIM_MS, Math.Min(MAX_ANIM_MS, duration));

            _animFromScale   = current;
            _animToScale     = newTo;
            _animDurationMs  = duration;
            _animStartUtc    = DateTime.UtcNow;
            _isZoomAnimating = true;

            _zoomAnimTimer.Start();
        }

        private void StopZoomAnimation(bool finalizeHighQuality)
        {
            if (_zoomAnimTimer != null) _zoomAnimTimer.Stop();
            _isZoomAnimating = false;

            // 終了時に高品質で最終描画（確定）
            if (finalizeHighQuality)
            {
                _scale = Clamp(_animToScale, MIN_SCALE, MAX_SCALE);
                SyncZoomStepToScale();
                ApplyZoom(false, false); // 高品質
            }
        }

        private void ZoomAnimTimer_Tick(object sender, EventArgs e)
        {
            if (!_isZoomAnimating) { _zoomAnimTimer.Stop(); return; }

            double ms = (DateTime.UtcNow - _animStartUtc).TotalMilliseconds;
            float t = (float)(ms / _animDurationMs);
            if (t >= 1f) t = 1f;

            float eased = EaseOutCubic(t);
            float s = Lerp(_animFromScale, _animToScale, eased);

            _scale = Clamp(s, MIN_SCALE, MAX_SCALE);
            SyncZoomStepToScale();

            // 操作中（低品質）で描画
            ApplyZoom(false, true);

            if (t >= 1f)
            {
                StopZoomAnimation(true);
            }
        }

        #endregion

    }
}
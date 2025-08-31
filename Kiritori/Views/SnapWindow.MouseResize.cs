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
        #region ===== マウス入力（移動・リサイズ） =====
        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location)) return;

            // リサイズ優先
            var hit = HitTestAnchor(e.Location);
            if (hit != ResizeAnchor.None)
            {
                _anchor = hit;
                _isResizing = true;
                _isDragging = false;
                _dragStartScreen = Cursor.Position;
                _startSize = this.Size;
                _startLocation = this.Location;

                _imgAspect = (pictureBox1.Image != null)
                    ? (float)pictureBox1.Image.Width / pictureBox1.Image.Height
                    : (float)Math.Max(1, this.ClientSize.Width) / Math.Max(1, this.ClientSize.Height);

                pictureBox1.Capture = true;
                this.Cursor = GetCursorForAnchor(_anchor);
                return;
            }

            // 移動開始
            mousePoint = new Point(e.X, e.Y);
            _isDragging = true;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location))
            {
                this.Cursor = Cursors.Hand;
                return;
            }

            if (_isResizing)
            {
                var now = Cursor.Position;
                int dx = now.X - _dragStartScreen.X;
                int dy = now.Y - _dragStartScreen.Y;

                // 基準値
                int newW = _startSize.Width;
                int newH = _startSize.Height;
                int newLeft = _startLocation.X;
                int newTop = _startLocation.Y;

                switch (_anchor)
                {
                    // 角４つ（既存と同じ：Left/Top 側は位置も動かす）
                    case ResizeAnchor.BottomRight:
                        newW = _startSize.Width + dx;
                        newH = _startSize.Height + dy;
                        break;
                    case ResizeAnchor.BottomLeft:
                        newW = _startSize.Width - dx;
                        newH = _startSize.Height + dy;
                        newLeft = _startLocation.X + dx;
                        break;
                    case ResizeAnchor.TopRight:
                        newW = _startSize.Width + dx;
                        newH = _startSize.Height - dy;
                        newTop = _startLocation.Y + dy;
                        break;
                    case ResizeAnchor.TopLeft:
                        newW = _startSize.Width - dx;
                        newH = _startSize.Height - dy;
                        newLeft = _startLocation.X + dx;
                        newTop = _startLocation.Y + dy;
                        break;

                    // 辺４つ（横だけ/縦だけ、Left/Top は位置も動かす）
                    case ResizeAnchor.Left:
                        newW = _startSize.Width - dx;
                        newLeft = _startLocation.X + dx;
                        break;
                    case ResizeAnchor.Right:
                        newW = _startSize.Width + dx;
                        break;
                    case ResizeAnchor.Top:
                        newH = _startSize.Height - dy;
                        newTop = _startLocation.Y + dy;
                        break;
                    case ResizeAnchor.Bottom:
                        newH = _startSize.Height + dy;
                        break;
                }

                // 角のみアスペクト固定を適用（Shift）
                bool isCorner =
                    _anchor == ResizeAnchor.TopLeft || _anchor == ResizeAnchor.TopRight ||
                    _anchor == ResizeAnchor.BottomLeft || _anchor == ResizeAnchor.BottomRight;

                if (isCorner && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && _imgAspect != null)
                {
                    float ar = _imgAspect.Value;
                    if (Math.Abs(newW - _startSize.Width) >= Math.Abs(newH - _startSize.Height))
                        newH = (int)Math.Round(newW / ar);
                    else
                        newW = (int)Math.Round(newH * ar);

                    // 左/上の場合は位置補正
                    if (_anchor == ResizeAnchor.BottomLeft || _anchor == ResizeAnchor.TopLeft)
                        newLeft = _startLocation.X + (_startSize.Width - newW);
                    if (_anchor == ResizeAnchor.TopLeft || _anchor == ResizeAnchor.TopRight)
                        newTop = _startLocation.Y + (_startSize.Height - newH);
                }

                // 最小サイズクリップ＋位置補正（共通ヘルパーを再利用）
                ClampAndAdjustForAnchor(ref newW, ref newH, ref newLeft, ref newTop, _anchor);
                this.SuspendLayout();
                try
                {
                    this.Location = new Point(newLeft, newTop);
                    this.ClientSize = new Size(newW, newH);
                    pictureBox1.Size = this.ClientSize;
                }
                finally
                {
                    this.ResumeLayout();
                }

                // 最小サイズ
                newW = Math.Max(this.MinimumSize.Width, newW);
                newH = Math.Max(this.MinimumSize.Height, newH);

                // 反映
                this.SuspendLayout();
                try
                {
                    this.Location = new Point(newLeft, newTop);
                    this.ClientSize = new Size(newW, newH);
                    pictureBox1.Size = this.ClientSize;
                }
                finally
                {
                    this.ResumeLayout();
                }

                this.Cursor = GetCursorForAnchor(_anchor);

                int nowTick = Environment.TickCount;
                if (nowTick - _lastPaintTick >= 15)
                {
                    pictureBox1.Invalidate();
                    pictureBox1.Update();
                    _lastPaintTick = nowTick;
                }
                return;
            }

            // 非リサイズ時のカーソル表示
            var hoverAnchor = HitTestAnchor(e.Location);

            this.Cursor = GetCursorForAnchor(hoverAnchor);

            // 移動（既存）
            if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                this.Left += e.X - mousePoint.X;
                this.Top += e.Y - mousePoint.Y;
                this.Opacity = this.WindowAlphaPercent * DRAG_ALPHA;
            }
        }


        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _isDragging = false;
            this.Opacity = this.WindowAlphaPercent;

            if (_isResizing)
            {
                _isResizing = false;
                _anchor = ResizeAnchor.None;
                _imgAspect = null;
                pictureBox1.Capture = false;

                // ホバー位置のカーソルに戻す
                var hoverAnchor = HitTestCorner(e.Location);
                this.Cursor = GetCursorForAnchor(hoverAnchor);
                pictureBox1.Invalidate();
                RefreshFromOriginalHiQ();
            }
        }

        private void PictureBox1_CaptureChanged(object sender, EventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                _anchor = ResizeAnchor.None;
                _imgAspect = null;
                pictureBox1.Cursor = Cursors.Default;
            }
            _isDragging = false;
            this.Opacity = this.WindowAlphaPercent;
        }

        private Dictionary<ResizeAnchor, Rectangle> GripRectsOnPB()
        {
            float scale = this.DeviceDpi / 96f;
            int grip = (int)Math.Max(12, GRIP_PX * scale);

            var w = pictureBox1.ClientSize.Width;
            var h = pictureBox1.ClientSize.Height;

            var dict = new Dictionary<ResizeAnchor, Rectangle>();
            dict[ResizeAnchor.BottomRight] = new Rectangle(w - grip, h - grip, grip, grip);
            dict[ResizeAnchor.BottomLeft] = new Rectangle(0, h - grip, grip, grip);
            dict[ResizeAnchor.TopRight] = new Rectangle(w - grip, 0, grip, grip);
            dict[ResizeAnchor.TopLeft] = new Rectangle(0, 0, grip, grip);
            return dict;
        }
        private (Dictionary<ResizeAnchor, Rectangle> corners, Dictionary<ResizeAnchor, Rectangle> edges) BuildGripRects()
        {
            float scale = this.DeviceDpi / 96f;
            int grip = (int)Math.Max(12, GRIP_PX * scale);   // 角の四角
            int band = (int)Math.Max(6, 6 * scale);         // 辺の帯幅

            int w = pictureBox1.ClientSize.Width;
            int h = pictureBox1.ClientSize.Height;

            var corners = new Dictionary<ResizeAnchor, Rectangle>
            {
                [ResizeAnchor.BottomRight] = new Rectangle(w - grip, h - grip, grip, grip),
                [ResizeAnchor.BottomLeft] = new Rectangle(0, h - grip, grip, grip),
                [ResizeAnchor.TopRight] = new Rectangle(w - grip, 0, grip, grip),
                [ResizeAnchor.TopLeft] = new Rectangle(0, 0, grip, grip),
            };

            // 角を除いた帯（上下は左右の角を避ける、左右は上下の角を避ける）
            var edges = new Dictionary<ResizeAnchor, Rectangle>
            {
                [ResizeAnchor.Top] = new Rectangle(grip, 0, w - grip * 2, band),
                [ResizeAnchor.Bottom] = new Rectangle(grip, h - band, w - grip * 2, band),
                [ResizeAnchor.Left] = new Rectangle(0, grip, band, h - grip * 2),
                [ResizeAnchor.Right] = new Rectangle(w - band, grip, band, h - grip * 2),
            };

            return (corners, edges);
        }

        private ResizeAnchor HitTestAnchor(Point p)
        {
            var (corners, edges) = BuildGripRects();
            foreach (var kv in corners) if (kv.Value.Contains(p)) return kv.Key; // 角優先
            foreach (var kv in edges) if (kv.Value.Contains(p)) return kv.Key;
            return ResizeAnchor.None;
        }

        private void ClampAndAdjustForAnchor(ref int newW, ref int newH, ref int newLeft, ref int newTop, ResizeAnchor anchor)
        {
            int minW = this.MinimumSize.Width;
            int minH = this.MinimumSize.Height;

            // 幅クリップ
            if (newW < minW)
            {
                // 左側が動くアンカーなら、Leftを「右方向へ補正」して見た目の反転を防ぐ
                if (anchor == ResizeAnchor.TopLeft || anchor == ResizeAnchor.BottomLeft)
                {
                    int delta = minW - newW;     // 不足分
                    newLeft -= delta;            // 左に出た分を戻す（== 右へ寄せる）
                }
                newW = minW;
            }

            // 高さクリップ
            if (newH < minH)
            {
                // 上側が動くアンカーなら、Topを「下方向へ補正」して見た目の反転を防ぐ
                if (anchor == ResizeAnchor.TopLeft || anchor == ResizeAnchor.TopRight)
                {
                    int delta = minH - newH;
                    newTop -= delta;             // 上に出た分を戻す（== 下へ寄せる）
                }
                newH = minH;
            }
        }

        private ResizeAnchor HitTestCorner(Point p)
        {
            foreach (var kv in GripRectsOnPB())
                if (kv.Value.Contains(p)) return kv.Key;
            return ResizeAnchor.None;
        }

        private static Cursor GetCursorForAnchor(ResizeAnchor a)
        {
            switch (a)
            {
                case ResizeAnchor.TopLeft:
                case ResizeAnchor.BottomRight: return Cursors.SizeNWSE;
                case ResizeAnchor.TopRight:
                case ResizeAnchor.BottomLeft: return Cursors.SizeNESW;
                case ResizeAnchor.Left:
                case ResizeAnchor.Right: return Cursors.SizeWE;
                case ResizeAnchor.Top:
                case ResizeAnchor.Bottom: return Cursors.SizeNS;
                default: return Cursors.Default;
            }
        }

        #endregion

    }
}
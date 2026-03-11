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
        private ResizeAnchor _lastHoverAnchor = ResizeAnchor.None;
        private Cursor _lastCursor = null;

        private void SetCursorIfChanged(Cursor c)
        {
            if (!ReferenceEquals(_lastCursor, c))
            {
                this.Cursor = c;
                _lastCursor = c;
            }
        }

        // ← ResizeAnchor.None のときは必ず SizeAll を返すようにしておく
        private Cursor GetCursorForAnchor(ResizeAnchor a)
        {
            switch (a)
            {
                case ResizeAnchor.Left: 
                case ResizeAnchor.Right:  return Cursors.SizeWE;
                case ResizeAnchor.Top: 
                case ResizeAnchor.Bottom: return Cursors.SizeNS;
                case ResizeAnchor.TopLeft: 
                case ResizeAnchor.BottomRight: return Cursors.SizeNWSE;
                case ResizeAnchor.TopRight: 
                case ResizeAnchor.BottomLeft: return Cursors.SizeNESW;
                case ResizeAnchor.None:
                default: return Cursors.SizeAll;   // ★ 通常ホバーは SizeAll
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            // === 非リサイズ時：ここでカーソルを確定して return ===
            if (!_isResizing)
            {
                // 1) クローズボタン上なら Hand にして即 return（上書き防止）
                if (!_closeBtnRect.IsEmpty && _closeBtnRect.Contains(e.Location))
                {
                    SetCursorIfChanged(Cursors.Hand);
                    return;
                }

                // 2) リサイズの“縁”かどうかヒットテスト（縁ならサイズカーソル、そうでなければ SizeAll）
                var hoverAnchor = HitTestAnchor(e.Location);   // ← ここで判定
                _lastHoverAnchor = hoverAnchor;
                SetCursorIfChanged(GetCursorForAnchor(hoverAnchor));

                // 3) ドラッグ移動（必要なら）
                if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
                {
                    this.Left += e.X - mousePoint.X;
                    this.Top  += e.Y - mousePoint.Y;
                    this.Opacity = this.WindowOpacityPercent * DRAG_ALPHA;
                }
                return; // ★ ここで終わる
            }

            // === ここからリサイズ中 ===
            var now = Cursor.Position;
            int dx = now.X - _dragStartScreen.X;
            int dy = now.Y - _dragStartScreen.Y;

            int newW = _startSize.Width, newH = _startSize.Height;
            int newLeft = _startLocation.X, newTop = _startLocation.Y;

            switch (_anchor)
            {
                case ResizeAnchor.BottomRight: newW += dx; newH += dy; break;
                case ResizeAnchor.BottomLeft:  newW -= dx; newH += dy; newLeft += dx; break;
                case ResizeAnchor.TopRight:    newW += dx; newH -= dy; newTop  += dy; break;
                case ResizeAnchor.TopLeft:     newW -= dx; newH -= dy; newLeft += dx; newTop += dy; break;
                case ResizeAnchor.Left:        newW -= dx; newLeft += dx; break;
                case ResizeAnchor.Right:       newW += dx; break;
                case ResizeAnchor.Top:         newH -= dy; newTop  += dy; break;
                case ResizeAnchor.Bottom:      newH += dy; break;
            }

            bool isCorner = (_anchor == ResizeAnchor.TopLeft || _anchor == ResizeAnchor.TopRight ||
                            _anchor == ResizeAnchor.BottomLeft || _anchor == ResizeAnchor.BottomRight);

            if (isCorner && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && _imgAspect != null)
            {
                float ar = _imgAspect.Value;
                if (Math.Abs(newW - _startSize.Width) >= Math.Abs(newH - _startSize.Height))
                    newH = (int)Math.Round(newW / ar);
                else
                    newW = (int)Math.Round(newH * ar);

                if (_anchor == ResizeAnchor.BottomLeft || _anchor == ResizeAnchor.TopLeft)
                    newLeft = _startLocation.X + (_startSize.Width - newW);
                if (_anchor == ResizeAnchor.TopLeft || _anchor == ResizeAnchor.TopRight)
                    newTop  = _startLocation.Y + (_startSize.Height - newH);
            }

            ClampAndAdjustForAnchor(ref newW, ref newH, ref newLeft, ref newTop, _anchor);
            newW = Math.Max(this.MinimumSize.Width,  newW);
            newH = Math.Max(this.MinimumSize.Height, newH);

            this.SuspendLayout();
            try
            {
                this.SetBounds(newLeft, newTop, newW, newH, BoundsSpecified.Location | BoundsSpecified.Size);
            }
            finally { this.ResumeLayout(); }

            // リサイズ中は“そのアンカーに対応する”カーソル
            SetCursorIfChanged(GetCursorForAnchor(_anchor));

            int nowTick = Environment.TickCount;
            if (nowTick - _lastPaintTick >= 15)
            {
                // pictureBox1.Invalidate();
                pictureBox1.Refresh();
                _lastPaintTick = nowTick;
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _isDragging = false;
            this.Opacity = this.WindowOpacityPercent;

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
            this.Opacity = this.WindowOpacityPercent;
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

        #endregion

    }
}
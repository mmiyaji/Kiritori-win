using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Collections;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Kiritori
{
    public partial class ScreenWindow : Form
    {
        private readonly Func<int> getHostDpi;
        private Graphics g;
        private Bitmap bmp;
        private Bitmap baseBmp; // キャプチャ原本
        private Boolean isOpen;
        private ArrayList captureArray;
        private Font fnt = new Font("Segoe UI", 10);
        private MainApplication ma;
        private int currentDpi = 96;
        private int snapGrid = 50;      // グリッド間隔(px)
        private int edgeSnapTol = 6;    // 端スナップの許容距離(px)
        private bool showSnapGuides = true; // Alt中にガイド線を描くか
        public ScreenWindow(MainApplication mainapp, Func<int> getHostDpi = null)
        {
            this.ma = mainapp;
            this.getHostDpi = getHostDpi ?? (() =>
            {
                try { return GetDpiForWindow(this.Handle); } catch { return 96; }
            });
            captureArray = new ArrayList();
            isOpen = true;
            InitializeComponent();
            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            try { OnHostDpiChanged(this.getHostDpi()); } catch { }
        }
        public Boolean isScreenOpen()
        {
            return this.isOpen;
        }
        private void Screen_Load(object sender, EventArgs e)
        {
            pictureBox1.MouseDown +=
                    new MouseEventHandler(ScreenWindow_MouseDown);
            pictureBox1.MouseMove +=
                    new MouseEventHandler(ScreenWindow_MouseMove);
            pictureBox1.MouseUp +=
                    new MouseEventHandler(ScreenWindow_MouseUp);
        }
        private bool IsAltDown() => (ModifierKeys & Keys.Alt) == Keys.Alt;
        private bool IsGuidesEnabled()
        {
            // Altで反転
            bool baseVal = Properties.Settings.Default.isScreenGuide;
            if ((ModifierKeys & Keys.Alt) != 0) return !baseVal;
            return baseVal;
        }

        private bool IsSnapEnabled()
        {
            // Ctrlで反転
            bool baseVal = false;
            if ((ModifierKeys & Keys.Control) != 0) return !baseVal;
            return baseVal;
        }

        private bool IsSquareConstraint()
        {
            return (ModifierKeys & Keys.Shift) != 0;
        }
        private Point SnapPoint(Point p)
        {
            if (bmp == null) return p;

            // 1) グリッドにスナップ（四捨五入）
            int sx = (int)Math.Round(p.X / (double)snapGrid) * snapGrid;
            int sy = (int)Math.Round(p.Y / (double)snapGrid) * snapGrid;
            p = new Point(sx, sy);

            // 2) 画像端にスナップ（許容距離内なら吸着）
            int W = bmp.Width, H = bmp.Height;
            if (Math.Abs(p.X - 0) <= edgeSnapTol) p.X = 0;
            if (Math.Abs(p.Y - 0) <= edgeSnapTol) p.Y = 0;
            if (Math.Abs(p.X - (W - 1)) <= edgeSnapTol) p.X = W - 1;
            if (Math.Abs(p.Y - (H - 1)) <= edgeSnapTol) p.Y = H - 1;

            return p;
        }
        public void openImage()
        {
            try
            {
                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                openFileDialog1.Title = "Open Image";

                // ファイルのフィルタを設定する
                openFileDialog1.Filter = "Image|*.png;*.PNG;*.jpg;*.JPG;*.jpeg;*.JPEG;*.gif;*.GIF;*.bmp;*.BMP|すべてのファイル|*.*";
                openFileDialog1.FilterIndex = 1;

                // 有効な Win32 ファイル名だけを受け入れるようにする (初期値 true)
                openFileDialog1.ValidateNames = false;

                // ダイアログを表示し、戻り値が [OK] の場合は、選択したファイルを表示する
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    // 不要になった時点で破棄する (正しくは オブジェクトの破棄を保証する を参照)
                    openFileDialog1.Dispose();

                    SnapWindow sw = new SnapWindow(this.ma);
                    sw.StartPosition = FormStartPosition.CenterScreen;
                    sw.setImageFromPath(openFileDialog1.FileName);
                    sw.FormClosing +=
                        new FormClosingEventHandler(SW_FormClosing);
                    captureArray.Add(sw);
                    sw.Show();
                }
            }
            catch
            {
            }
        }
        public SnapWindow getSW(int i)
        {
            return (SnapWindow)captureArray[i];
        }
        public void openImageFromHistory(ToolStripMenuItem item)
        {
            try
            {
                SnapWindow sw = new SnapWindow(this.ma);
                sw.StartPosition = FormStartPosition.CenterScreen;
                sw.setImageFromBMP((Bitmap)((item.Tag as SnapWindow).main_image));
                sw.Text = (item.Tag as SnapWindow).Text;
                sw.FormClosing +=
                    new FormClosingEventHandler(SW_FormClosing);
                captureArray.Add(sw);
                sw.Show();
            }
            catch
            {
            }
        }

        public void showScreen()
        {
            // this.Opacity = 0.61;
            this.Opacity = 1.0;
            // int h, w;
            // //ディスプレイの高さ
            // h = System.Windows.Forms.Screen.GetBounds(this).Height;
            // //ディスプレイの幅
            // w = System.Windows.Forms.Screen.GetBounds(this).Width;
            // this.SetBounds(0, 0, w, h);
            // bmp = new Bitmap(w, h);
            var vs = GetVirtualScreenPhysical();
            int w = vs.W, h = vs.H;
            this.SetBounds(vs.X, vs.Y, w, h);
            bmp = new Bitmap(w, h);
            using (g = Graphics.FromImage(bmp))
            {
                // g.Clear(Color.White);
                // g.CopyFromScreen(
                //     new Point(0, 0),
                //     new Point(w, h), bmp.Size
                // );
                g.CopyFromScreen(new Point(vs.X, vs.Y), new Point(0, 0), bmp.Size);
            }
            baseBmp = (Bitmap)bmp.Clone();
            // 全体をうっすら白く
            using (g = Graphics.FromImage(bmp))
            using (var mask = new SolidBrush(Color.FromArgb(80, Color.White))) // 濃さは80～120で調整
                g.FillRectangle(mask, new Rectangle(0, 0, w, h));

            pictureBox1.SetBounds(0, 0, w, h);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = bmp;
            pictureBox1.Refresh();
            this.TopLevel = true;
            this.Show();
            // Console.WriteLine(h + "," + w);
        }
        int x = 0, y = 0, h = 0, w = 0;
        public void showScreenAll()
        {
            // this.Opacity = 0.61;
            this.Opacity = 1.0;
            this.StartPosition = FormStartPosition.Manual;
            // this.showSnapGuides = Properties.Settings.Default.isScreenGuide;

            // int index;
            // int upperBound;
            // x = 0;
            // y = 0;
            // h = 0;
            // w = 0;
            // // 接続しているすべてのディスプレイを取得
            // Screen[] screens = Screen.AllScreens;
            // upperBound = screens.GetUpperBound(0);
            // // すべてのディスプレイにおける基準点（左上）とサイズを計算
            // for (index = 0; index <= upperBound; index++)
            // {
            //     if (x > screens[index].Bounds.X)
            //     {
            //         x = screens[index].Bounds.X;
            //     }
            //     if (y > screens[index].Bounds.Y)
            //     {
            //         y = screens[index].Bounds.Y;
            //     }
            //     if (w < screens[index].Bounds.Width + screens[index].Bounds.X)
            //     {
            //         w = screens[index].Bounds.Width + screens[index].Bounds.X;
            //     }
            //     if (h < screens[index].Bounds.Height + screens[index].Bounds.Y)
            //     {
            //         h = screens[index].Bounds.Height + screens[index].Bounds.Y;
            //     }
            // }
            // // 複数ディスプレイ時にメインより左上のディスプレイの基準点がマイナスになるため、座標系を補正
            // w = Math.Abs(x) + Math.Abs(w);
            // h = Math.Abs(y) + Math.Abs(h);
            // 物理ピクセルで仮想スクリーンを取得（負座標も含む）
            var vs = GetVirtualScreenPhysical();
            x = vs.X; y = vs.Y; w = vs.W; h = vs.H;

            // ディスプレイ全体に白幕（スクリーン）を描画
            this.SetBounds(x, y, w, h);
            bmp = new Bitmap(w, h);
            using (g = Graphics.FromImage(bmp))
            {
                // g.Clear(Color.White);
                // g.CopyFromScreen(
                //     new Point(x, y),
                //     new Point(w, h), bmp.Size
                // );
                g.CopyFromScreen(new Point(x, y), new Point(0, 0), bmp.Size);
            }
            baseBmp = (Bitmap)bmp.Clone();
            using (g = Graphics.FromImage(bmp))
            using (var mask = new SolidBrush(Color.FromArgb(40, Color.White)))
                g.FillRectangle(mask, new Rectangle(0, 0, w, h));

            pictureBox1.SetBounds(0, 0, w, h);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = bmp;
            pictureBox1.Refresh();
            this.TopLevel = true;
            this.Show();

            //Console.WriteLine(x + ":" + y + " " + h + "," + w + "@" + upperBound);
        }
        //マウスのクリック位置を記憶
        private Point startPoint;
        private Point startPointPhys;
        private Point hoverPoint = Point.Empty;
        private bool showHover = true;
        private Point endPoint;
        private Rectangle rc;
        private Boolean isPressed = false;
        //マウスのボタンが押されたとき
        private void ScreenWindow_MouseDown(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                //位置を記憶する
                startPoint = new Point(e.X, e.Y);
                startPointPhys = new Point(e.X + x, e.Y + y);
                isPressed = true;
            }
        }
        //マウスが動いたとき
        private void ScreenWindow_MouseMove(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            hoverPoint = new Point(e.X, e.Y);

            if (!isPressed)
            {
                if (IsGuidesEnabled())
                {
                    RedrawHoverOnly();
                }
                return;
            }
            Point cur = new Point(e.X, e.Y);
            if (IsSnapEnabled())
            {
                cur = SnapPoint(cur);
            }
            rc.X = Math.Min(startPoint.X, cur.X);
            rc.Y = Math.Min(startPoint.Y, cur.Y);
            rc.Width  = Math.Abs(cur.X - startPoint.X);
            rc.Height = Math.Abs(cur.Y - startPoint.Y);
            // rc = new Rectangle();
            // // Pen p = new Pen(Color.Black, 10);
            // if (startPoint.X < e.X)
            // {
            //     rc.X = startPoint.X;
            //     rc.Width = e.X - startPoint.X;
            // }
            // else
            // {
            //     rc.X = e.X;
            //     rc.Width = startPoint.X - e.X;
            // }
            // if (startPoint.Y < e.Y)
            // {
            //     rc.Y = startPoint.Y;
            //     rc.Height = e.Y - startPoint.Y;
            // }
            // else
            // {
            //     rc.Y = e.Y;
            //     rc.Height = startPoint.Y - e.Y;
            // }
            // {
            //     Pen blackPen = new Pen(Color.Black);
            //     using (g = Graphics.FromImage(bmp))
            //     {
            //         blackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            //         blackPen.Width = 1;
            //         g.Clear(SystemColors.Control);
            //         g.DrawRectangle(blackPen, rc);
            //         g.DrawString(rc.Width.ToString() + "x" + rc.Height.ToString(),
            //             fnt, Brushes.Black, e.X + 5, e.Y + 10);
            //     }
            //     pictureBox1.Refresh();
            // }
            if (IsSquareConstraint())
            {
                int size = Math.Min(rc.Width, rc.Height);
                rc.Width = rc.Height = size;
            }
            using (g = Graphics.FromImage(bmp))
            {
                // 原本から毎回描き直し
                g.DrawImage(baseBmp, Point.Empty);

                // 外側だけに半透明白マスクをかける
                using (var mask = new SolidBrush(Color.FromArgb(40, Color.White)))
                using (var outside = new Region(new Rectangle(0, 0, bmp.Width, bmp.Height)))
                {
                    outside.Exclude(rc);           // 選択範囲は除外（＝透けない）
                    g.FillRegion(mask, outside);   // 外側だけ白っぽく
                }

                using (var blackPen = new Pen(Color.Black) { Width = 1, DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawRectangle(blackPen, rc); // 破線枠
                }

                if (IsGuidesEnabled())
                {
                    // 1) 選択サイズ
                    DrawLabel(g, $"{rc.Width} x {rc.Height}", new Point(e.X + 12, e.Y + 12));

                    // 2) 開始点の十字マーカー
                    DrawCrosshair(g, startPoint);

                    // 3) 開始点の“物理座標”ラベル（開始点のすぐ右下に表示）
                    DrawLabel(g, $"{startPointPhys.X}, {startPointPhys.Y}", new Point(startPoint.X + 10, startPoint.Y + 10));
                    using (var pen = new Pen(Color.FromArgb(120, Color.Black)))
                    {
                        g.DrawLine(pen, 0, cur.Y, bmp.Width, cur.Y);
                        g.DrawLine(pen, cur.X, 0, cur.X, bmp.Height);
                    }
                }

                // g.DrawString($"{rc.Width}x{rc.Height}", fnt, Brushes.Black, e.X + 5, e.Y + 10);
                pictureBox1.Refresh();
            }
        }
        //マウスのボタンが離されたとき
        private void ScreenWindow_MouseUp(object sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if (isPressed)
            {
                endPoint = new Point(e.X, e.Y);
                isPressed = false;
                this.CloseScreen();
                if (rc.Width != 0 || rc.Height != 0)
                {
                    SnapWindow sw = new SnapWindow(this.ma);
                    sw.StartPosition = FormStartPosition.Manual;
                    sw.capture(new Rectangle(rc.X + x, rc.Y + y, rc.Width, rc.Height));
                    sw.SetBounds(rc.X + x, rc.Y + y, 0, 0);
                    sw.FormClosing +=
                        new FormClosingEventHandler(SW_FormClosing);
                    captureArray.Add(sw);
                    sw.Show();
                    //                    Console.WriteLine(rc.X +";"+ x);
                }
            }
        }
        private void RedrawHoverOnly()
        {
            if (!showHover || bmp == null || baseBmp == null) return;

            var pt = hoverPoint;
            if (IsAltDown())
                pt = SnapPoint(pt);

            using (g = Graphics.FromImage(bmp))
            {
                // 原本から描き直し
                g.DrawImage(baseBmp, Point.Empty);

                // 全体を薄く白（選択前のスクリーン状態）
                using (var mask = new SolidBrush(Color.FromArgb(40, Color.White)))
                    g.FillRectangle(mask, new Rectangle(0, 0, bmp.Width, bmp.Height));

                if (showSnapGuides && IsAltDown())
                {
                    using (var pen = new Pen(Color.FromArgb(120, Color.Black)))
                    {
                        g.DrawLine(pen, 0, pt.Y, bmp.Width, pt.Y);
                        g.DrawLine(pen, pt.X, 0, pt.X, bmp.Height);
                    }
                }
                // 開始候補点に十字
                DrawCrosshair(g, hoverPoint);

                // 物理座標（仮想スクリーン基準）ラベル
                var phys = new Point(hoverPoint.X + x, hoverPoint.Y + y);
                if (showSnapGuides)
                {
                    DrawLabel(g, $"{phys.X}, {phys.Y}", new Point(hoverPoint.X + 10, hoverPoint.Y + 10));
                }
            }
            pictureBox1.Refresh();
        }

        public void hideWindows()
        {
            foreach (SnapWindow sw in captureArray)
            {
                sw.minimizeWindow();
            }
        }
        public void showWindows()
        {
            foreach (SnapWindow sw in captureArray)
            {
                sw.showWindow();
            }
        }
        public void closeWindows()
        {
            // 現在の内容で固定した配列を作ってから閉じる
            var snapshot = captureArray.Cast<SnapWindow>().ToArray();
            foreach (var sw in snapshot)
            {
                try
                {
                    if (sw != null && !sw.IsDisposed)
                        sw.closeWindow();
                }
                catch { /* 必要ならログ */ }
            }
        }
        private void CloseScreen()
        {
            this.isOpen = false;
            this.Hide();
        }
        void SW_FormClosing(object sender, FormClosingEventArgs e)
        {
            captureArray.Remove(sender);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch ((int)keyData)
            {
                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    this.CloseScreen();
                    break;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        // --- DPI/スクリーン関連 ---
        [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hWnd); // Win10+
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi); // Win10+
        private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

        private struct PhysVS { public int X, Y, W, H; }
        private PhysVS GetVirtualScreenPhysical()
        {
            try
            {
                int dpi = GetDpiForWindow(this.Handle); // 例: 96,120,144...
                int x = GetSystemMetricsForDpi(SM_XVIRTUALSCREEN, (uint)dpi);
                int y = GetSystemMetricsForDpi(SM_YVIRTUALSCREEN, (uint)dpi);
                int cx = GetSystemMetricsForDpi(SM_CXVIRTUALSCREEN, (uint)dpi);
                int cy = GetSystemMetricsForDpi(SM_CYVIRTUALSCREEN, (uint)dpi);
                return new PhysVS { X = x, Y = y, W = cx, H = cy };
            }
            catch
            {
                // フォールバック（プロセスが PM 対応ならこれも物理ピクセル）
                var vs = SystemInformation.VirtualScreen;
                return new PhysVS { X = vs.X, Y = vs.Y, W = vs.Width, H = vs.Height };
            }
        }
        /// <summary>
        /// ホスト側(MainApplication)や自分自身で DPI が変わったときに呼び出す。
        /// フォント・線幅など DPI 依存のリソースを作り直す。
        /// </summary>
        public void OnHostDpiChanged(int newDpi)
        {
            if (newDpi <= 0) newDpi = 96;
            if (newDpi == currentDpi) return;
            currentDpi = newDpi;

            // フォントを DPI に合わせて再生成
            fnt?.Dispose();
            int basePt = 10; // 元が 96dpi のとき 10pt 相当
            float scaledPt = basePt * (currentDpi / 96f);
            fnt = new Font("Segoe UI", scaledPt, GraphicsUnit.Point);

            // 必要に応じて他の DPI 依存リソースも調整
            // 例: ペンの太さ, PictureBox のサイズモード etc.
        }
        private void DrawLabel(Graphics g, string text, Point anchor, int pad = 4)
        {
            // 文字サイズ計測
            var sz = g.MeasureString(text, fnt);
            var rect = new RectangleF(anchor.X, anchor.Y, sz.Width + pad * 2, sz.Height + pad * 2);

            // 背景（半透明白）＋枠線
            using (var bg = new SolidBrush(Color.FromArgb(180, Color.White)))
            using (var pen = new Pen(Color.Black))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, Rectangle.Round(rect));
            }

            // 文字
            g.DrawString(text, fnt, Brushes.Black, rect.X + pad, rect.Y + pad);
        }

        private void DrawCrosshair(Graphics g, Point p)
        {
            using (var pen = new Pen(Color.Black, 1))
            {
                g.DrawLine(pen, p.X - 6, p.Y, p.X + 6, p.Y);
                g.DrawLine(pen, p.X, p.Y - 6, p.X, p.Y + 6);
            }
        }

    }
    
}

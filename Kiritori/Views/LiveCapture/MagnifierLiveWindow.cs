using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kiritori.Views.LiveCapture
{
    /// <summary>
    /// Magnification API を使って、指定領域の画面をライブで映し続けるフォーム
    /// </summary>
    public sealed class MagnifierLiveWindow : Form
    {
        // --- P/Invoke ---

        // Magnification
        [DllImport("Magnification.dll", ExactSpelling = true)]
        private static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true)]
        private static extern bool MagUninitialize();

        // [DllImport("Magnification.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        // private static extern IntPtr CreateWindowExW(
        //     int dwExStyle, string lpClassName, string lpWindowName,
        //     int dwStyle, int X, int Y, int nWidth, int nHeight,
        //     IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        private static extern bool MagSetWindowSource(IntPtr hwndMag, RECT rect);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        private static extern bool MagSetWindowTransform(IntPtr hwndMag, ref MAGTRANSFORM transform);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        private static extern bool MagSetWindowFilterList(IntPtr hwndMag, int dwFilterMode, int count, IntPtr pHWND);

        // user32
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        // [DllImport("user32.dll", ExactSpelling = true)]
        // private static extern IntPtr GetModuleHandleW(string lpModuleName);
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle, string lpClassName, string lpWindowName,
            int dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);
        // 構造体
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MAGTRANSFORM
        {
            // 3x3 行列（列優先でOK、単位行列だけ使う）
            public float _11, _12, _13;
            public float _21, _22, _23;
            public float _31, _32, _33;
        }

        // 定数
        private const string WC_MAGNIFIER = "Magnifier";
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020; // クリック透過 ON にする場合に付与

        private const int SW_SHOW = 5;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOMOVE     = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // フィールド
        private static int _magInitRef;          // MagInitialize/Uninitialize の参照カウント
        private IntPtr _hwndMag = IntPtr.Zero;   // 子ウィンドウ（拡大鏡本体）
        private Rectangle _sourceRect;           // 画面座標のキャプチャ元

        // オプション
        public bool ClickThrough { get; set; } = false;   // クリック透過
        public new float Scale { get; private set; } = 1.0f;  // 倍率（= 1.0 で原寸）

        public MagnifierLiveWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            // ❌ これらは使わない（レイヤード化の原因になる）
            this.Opacity = 0.8;            // ←削除
            // this.TransparencyKey = ...;    // ←削除

            CreateHandle();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style = WS_POPUP | WS_VISIBLE;
                cp.ExStyle = WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                if (ClickThrough) cp.ExStyle |= WS_EX_TRANSPARENT; // 透過クリックはこれだけでOK
                return cp;
                // var cp = base.CreateParams;
                // cp.Style = WS_POPUP | WS_VISIBLE;
                // cp.ExStyle = WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
                // if (ClickThrough) cp.ExStyle |= WS_EX_TRANSPARENT;
                // return cp;
            }
        }


        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnsureMagInitialized();

            // このフォームのクライアント領域一杯に拡大鏡コントロール（子ウィンドウ）を作る
            // _hwndMag = CreateWindowExW(
            //     0, WC_MAGNIFIER, null,
            //     WS_CHILD | WS_VISIBLE,
            //     0, 0, this.ClientSize.Width, this.ClientSize.Height,
            //     this.Handle, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            // 親フォームの画面座標とサイズをそのまま使う
            var r = this.Bounds; // System.Drawing.Rectangle

            _hwndMag = CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE /* | WS_EX_TRANSPARENT (必要なら) */,
                WC_MAGNIFIER, null,
                WS_POPUP | WS_VISIBLE,
                r.Left, r.Top, r.Width, r.Height,
                this.Handle /* ← owner。子ではない */,
                IntPtr.Zero,
                GetModuleHandleW(null),
                IntPtr.Zero);

            if (_hwndMag == IntPtr.Zero)
            {
                Debug.WriteLine($"CreateWindowExW(Magnifier) failed: {Marshal.GetLastWin32Error()}");
                return;
            }

            SetWindowPos(_hwndMag, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);


            if (_hwndMag == IntPtr.Zero)
            {
                // 失敗時は安全に閉じる
                BeginInvoke((Action)(() => { Close(); }));
                return;
            }

            // 単位行列（= 拡大率 1.0）
            var m = new MAGTRANSFORM
            {
                _11 = 1, _22 = 1, _33 = 1
            };
            MagSetWindowTransform(_hwndMag, ref m);

            ShowWindow(_hwndMag, SW_SHOW);
            // 最前面維持（非アクティブで）
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_hwndMag != IntPtr.Zero)
            {
                // 子ウィンドウをクライアントにフィット
                SetWindowPos(_hwndMag, IntPtr.Zero, 0, 0, this.ClientSize.Width, this.ClientSize.Height,
                    SWP_NOACTIVATE | SWP_SHOWWINDOW);
                // 必要に応じて倍率再設定
                ApplyScaleTransform();
                // 同じソースでも再適用しておくと確実
                ApplySourceRect();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 初回表示時にも念のため最前面化
            SetWindowPos(this.Handle, HWND_TOPMOST, Left, Top, Width, Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_hwndMag != IntPtr.Zero)
                {
                    DestroyWindow(_hwndMag);
                    _hwndMag = IntPtr.Zero;
                }
            }
            finally
            {
                base.Dispose(disposing);
                ReleaseMagInitialized();
            }
        }

        // --- 公開API ---

        /// <summary>
        /// 指定した画面ソース矩形（スクリーン座標）をライブ表示する。
        /// </summary>
        public void StartLive(Rectangle sourceRect, Point viewerLocation, Size? viewerSize = null, float scale = 1.0f, bool clickThrough = false)
        {
            ClickThrough = clickThrough;
            Scale = Math.Max(0.1f, scale);

            _sourceRect = sourceRect;
            var vs = viewerSize ?? sourceRect.Size;

            Bounds = new Rectangle(viewerLocation, vs);
            Show(); // OnHandleCreated → magnifier 作成→適用

            // 初期適用
            ApplyScaleTransform();
            ApplySourceRect();
        }

        /// <summary>
        /// 倍率を変更（1.0 = 原寸、2.0 = 2倍）
        /// </summary>
        public void SetScale(float scale)
        {
            Scale = Math.Max(0.1f, scale);
            ApplyScaleTransform();
        }

        /// <summary>
        /// クリック透過（ONで背後のアプリにクリックを通す）
        /// </summary>
        public void SetClickThrough(bool on)
        {
            ClickThrough = on;
            // ExStyle の再適用（簡易対応：作り直さず SetWindowLong を使うなら別途P/InvokeしてもOK）
            RecreateHandle();
        }

        /// <summary>
        /// 映す元の矩形を変更したいとき（ドラッグ移動や再選択）
        /// </summary>
        public void UpdateSource(Rectangle newSourceRect)
        {
            _sourceRect = newSourceRect;
            ApplySourceRect();
        }

        // --- 内部 ---

        private void ApplySourceRect()
        {
            if (_hwndMag == IntPtr.Zero) return;
            var r = new RECT
            {
                Left = _sourceRect.Left,
                Top = _sourceRect.Top,
                Right = _sourceRect.Right,
                Bottom = _sourceRect.Bottom
            };
            MagSetWindowSource(_hwndMag, r);
        }

        private void ApplyScaleTransform()
        {
            if (_hwndMag == IntPtr.Zero) return;

            // 倍率を掛ける（単位行列の対角のみ使用）
            var m = new MAGTRANSFORM
            {
                _11 = Scale,
                _22 = Scale,
                _33 = 1f
            };
            MagSetWindowTransform(_hwndMag, ref m);
        }

        private static void EnsureMagInitialized()
        {
            if (_magInitRef == 0)
            {
                // DPI認識はアプリ側で既に設定済みでOK（Per-MonitorV2 推奨）
                MagInitialize();
            }
            _magInitRef++;
        }

        private static void ReleaseMagInitialized()
        {
            _magInitRef = Math.Max(0, _magInitRef - 1);
            if (_magInitRef == 0)
            {
                MagUninitialize();
            }
        }
    }
}

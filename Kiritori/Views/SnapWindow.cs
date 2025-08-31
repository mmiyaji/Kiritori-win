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
#region ===== フィールド/定数 =====
public DateTime date;

        // 基本フラグ
        private bool isWindowShadow = true;
        private bool isAfloatWindow = true;
        private bool isOverlay = true;
        private bool isHighlightOnHover = true;

        // 親参照
        private MainApplication ma;

        // マウスドラッグ関連
        private Point mousePoint;
        private bool _isDragging = false;
        private bool _isResizing = false;
        private Point _dragStartScreen;
        private Size _startSize;
        private float? _imgAspect = null;

        // 透明度関連
        private double WindowAlphaPercent;
        private const double DRAG_ALPHA = 0.3;
        private const double MIN_ALPHA = 0.1;

        // 移動関連
        private const int MOVE_STEP = 3;
        private const int SHIFT_MOVE_STEP = MOVE_STEP * 10;

        // オーバーレイ
        private string _overlayText = null;
        private DateTime _overlayStart;
        private readonly int _overlayDurationMs = 2000;
        private readonly int _overlayFadeMs = 300;
        private readonly Timer _overlayTimer;
        private Font _overlayFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private int _dpi = 96;

        // 画像・ズーム関連
        private Image _originalImage;
        private Point _originalLocation;
        private float _scale = 1f;
        private int _zoomStep = 0;
        private const float STEP_LINEAR = 0.10f;
        private const float MIN_SCALE = 0.10f;
        private const float MAX_SCALE = 8.00f;
        // ---- Smooth Zoom（短時間アニメーション）用 ----
        private bool _smoothZoomEnabled = true;
        private System.Windows.Forms.Timer _zoomAnimTimer;
        private bool _isZoomAnimating;
        private float _animFromScale;
        private float _animToScale;
        private int _animDurationMs = 80;   // 120～180ms 推奨
        private DateTime _animStartUtc;
        private const int   INPUT_BURST_MS       = 180; // この時間内の連打は同一バースト扱い
        private const int   MAX_PENDING_STEPS    = 6;   // 現在表示位置から“先行できる”最大ステップ
        private const int   MIN_ANIM_MS          = 80;  // どんなに短くてもこの時間
        private const int   MAX_ANIM_MS          = 220; // どんなに長くてもこの時間
        private DateTime    _lastZoomInputUtc;          // 最終ズーム入力時刻


        // サムネイル
        private const int THUMB_WIDTH = 250;
        public Bitmap main_image;
        public Bitmap thumbnail_image;

        // クローズボタン
        private const int MIN_WIDTH = 100;
        private const int MIN_HEIGHT = 50;
        private readonly Dictionary<int, (Bitmap normal, Bitmap hover)> _closeIconCache
            = new Dictionary<int, (Bitmap normal, Bitmap hover)>();
        private bool _hoverWindow = false;
        private bool _hoverClose = false;
        private Rectangle _closeBtnRect = Rectangle.Empty;

        // ドロップシャドウ
        private const int CS_DROPSHADOW = 0x00020000;

        // 設定関連
        private Color _hoverColor;
        private int _hoverAlphaPercent;
        private int _hoverThicknessPx;
        private PropertyChangedEventHandler _settingsHandler;
        private bool _isApplyingSettings = false;

        // リサイズ用
        private const int GRIP_PX = 18;
        private int _lastPaintTick;
        private ResizeAnchor _anchor = ResizeAnchor.None;
        private Point _startLocation; // フォームの開始位置
        private bool _isResizeInteractive;
        private bool _resizeHooksSet;
        private Timer _resizeCommitTimer;   // 一瞬手を離しただけでも高品質確定へ
        private int _resizeCommitDelayMs = 120;

        // PictureBox に渡す「非アニメ・静止」複製（ImageAnimator回避用）
        private Bitmap _originalStill;

        // MSPaint 編集
        private string _paintEditPath;
        public bool SuppressHistory { get; set; } = false;

        // OCR
        private bool _ocrBusy = false;
        private static readonly string[] ImageExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

        internal LoadMethod CurrentLoadMethod { get; private set; } = LoadMethod.Path;

        #endregion

        #region ===== Win32/プロパティ =====
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (this.isWindowShadow)
                {
                    cp.ClassStyle |= CS_DROPSHADOW;
                }
                return cp;
            }
        }

        #endregion

        #region ===== コンストラクタ/ライフサイクル =====
        public SnapWindow(MainApplication mainapp)
        {
            this.ma = mainapp;

            ReadSettingsIntoFieldsWithFallback();

            _overlayTimer = new Timer { Interval = 33 };
            _overlayTimer.Tick += (s, e) =>
            {
                if (_overlayText == null) { _overlayTimer.Stop(); return; }
                if ((DateTime.Now - _overlayStart).TotalMilliseconds > _overlayDurationMs)
                {
                    _overlayText = null;
                    pictureBox1.Invalidate();
                    _overlayTimer.Stop();
                    return;
                }
                pictureBox1.Invalidate(new Rectangle(
                    pictureBox1.ClientSize.Width - 300,
                    pictureBox1.ClientSize.Height - 120, 300, 120));
            };

            InitializeComponent();
            Localizer.Apply(this);
            ApplyAllContextMenusLocalization();

            SR.CultureChanged += () =>
            {
                if (this.IsDisposed) return;
                Localizer.Apply(this);
                ApplyAllContextMenusLocalization();
            };

            // フォーム
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // PictureBox（反射）
            var pi = typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pi != null) pi.SetValue(pictureBox1, true, null);

            ApplyUiFromFields();
            HookSettingsChanged();
            InitializeResizePreviewPipeline();

            this.pictureBox1.Paint += PictureBox1_Paint;

            this.DoubleBuffered = true;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            this.AllowDrop = true;
            this.DragEnter += SnapWindow_DragEnter;
            this.DragDrop += SnapWindow_DragDrop;
            _originalLocation = this.Location;
        }

        private void SnapWindow_Load(object sender, EventArgs e)
        {
            this.pictureBox1.MouseMove += PictureBox1_MouseMove_Icon;
            this.pictureBox1.MouseClick += PictureBox1_MouseClick_Icon;
            this.pictureBox1.MouseEnter += delegate { _hoverWindow = true; pictureBox1.Invalidate(); };
            this.pictureBox1.MouseLeave += delegate { _hoverWindow = false; _hoverClose = false; pictureBox1.Invalidate(); };
            this.ClientSizeChanged += (_, __) => pictureBox1.Size = this.ClientSize;

            this.pictureBox1.MouseDown += pictureBox1_MouseDown;
            this.pictureBox1.MouseMove += pictureBox1_MouseMove;
            this.pictureBox1.MouseUp += pictureBox1_MouseUp;
            this.pictureBox1.MouseCaptureChanged += PictureBox1_CaptureChanged;
            this.pictureBox1.MouseLeave += (_, __) => { if (!_isResizing) this.Cursor = Cursors.Default; };
        }

        public Bitmap GetMainImage() => (Bitmap)pictureBox1.Image;
        public string GetImageSourcePath() => pictureBox1.Image?.Tag as string;
        public void SetLoadMethod(LoadMethod m)
        {
            CurrentLoadMethod = m;
        }

        #endregion
    }
}
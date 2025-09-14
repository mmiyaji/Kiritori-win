using Kiritori.Helpers;
using Kiritori.Services.Logging; 
using Kiritori.Services.History;
using Kiritori.Services.Ocr;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori
{
    public partial class PrefForm
    {
        // History タブ用
        private ListView _lvHistory;
        private ImageList _imgThumbs;
        private bool _historyUiInitialized;
        private bool _thumbLoadingActive;
        private Queue<HistoryEntry> _thumbQueue;
        private EventHandler _idleHandler;

        // サムネ設定
        private const int THUMB_W = 192 / 2;
        private const int THUMB_H = 108 / 2;
        private const int THUMB_PER_IDLE = 3;    // 1回のIdleで処理する枚数上限
        private const int IDLE_TIME_BUDGET_MS = 12; // Idle 1回で使う最大時間
        private Bitmap _placeholder;
        // タブが今表示中かどうか（通知の最適化に使える）
        internal bool IsHistoryTabActive =>
            this.tabControl?.SelectedTab == this.tabHistory;

        // データ
        private List<HistoryEntry> _allHistory = new List<HistoryEntry>();
        private List<HistoryEntry> _viewHistory = new List<HistoryEntry>();

        // UI
        private Panel _historyToolbar;
        private TextBox _txtSearch;
        private ComboBox _cboSort;
        private ComboBox _cboOrder;
        private Button _btnClearHistory;
        private Button _btnDelete;
        private ContextMenuStrip _ctxHistory;
        private ToolStripMenuItem _miOpen, _miOpenFolder, _miCopyPath, _miCopyOcr, _miCopyImage, _miDelete;
        private ToolStripMenuItem _miRunOcr;

        private enum SortKey { LoadedAt, FileName, Width, Height }
        private SortKey _sortKey = SortKey.LoadedAt;
        private bool _sortAsc = false;

        // 空表示用
        private Panel _emptyState;
        private Label _emptyTitle;
        private Label _emptyBody;
        private Button _btnEnableHistory;
        
        public void BuildHistoryTab()
        {
            EnsureHistoryTabUi();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;
        private void SetCueBanner(TextBox tb, string text, bool showWhenFocused = true)
        {
            if (tb == null || tb.IsDisposed) return;

            Action apply = () =>
            {
                try
                {
                    // wParam=1 でフォーカス時も表示（好みで false に）
                    SendMessage(tb.Handle, EM_SETCUEBANNER, showWhenFocused ? new IntPtr(1) : IntPtr.Zero, text ?? "");
                }
                catch { /* noop */ }
            };

            if (tb.IsHandleCreated) apply();
            else
            {
                // ハンドル作成後に一度だけ適用
                EventHandler h = null;
                h = (s, e) =>
                {
                    tb.HandleCreated -= h;
                    apply();
                };
                tb.HandleCreated += h;
            }
        }
        private void EnsureHistoryEmptyStateUi()
        {
            if (_emptyState != null) return;

            _emptyState = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Window,
                Visible = false  // 初期は非表示
            };

            _emptyTitle = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 36
            };

            _emptyBody = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Dock = DockStyle.Top,
                Height = 48
            };

            _btnEnableHistory = new Button
            {
                AutoSize = false,
                // Anchor = AnchorStyles.Top,
                Width = 200,
                Dock = DockStyle.Top,
                Visible = false // 無効化時のみ表示
            };
            _btnEnableHistory.Click += (s, e) =>
            {
                try
                {
                    // 上限を1件にして有効化（好みで既定値を変更可）
                    Properties.Settings.Default.HistoryLimit = Math.Max(10, Properties.Settings.Default.HistoryLimit); // 既に値があれば尊重
                    if (Properties.Settings.Default.HistoryLimit <= 0)
                        Properties.Settings.Default.HistoryLimit = 10;
                    Properties.Settings.Default.Save();

                    // 画面をリロード
                    _txtSearch.Text = "";
                    HistoryBridge.RaiseChanged(this); // Main 側がスナップショットを投げてきてくれます
                    UpdateHistoryEmptyState();        // 念のため更新
                }
                catch { /* noop */ }
            };

            // 中央寄せのためのレイアウト
            var host = new Panel { Dock = DockStyle.Fill };
            var spacerTop = new Panel { Dock = DockStyle.Top, Height = 40 };
            var center = new Panel { Dock = DockStyle.Top, Height = 140 };
            var spacerBottom = new Panel { Dock = DockStyle.Fill };

            center.Controls.Add(_btnEnableHistory);
            center.Controls.Add(_emptyBody);
            center.Controls.Add(_emptyTitle);

            _emptyState.Controls.Add(spacerBottom);
            _emptyState.Controls.Add(center);
            _emptyState.Controls.Add(spacerTop);

            this.tabHistory.Controls.Add(_emptyState);
            _emptyState.BringToFront();

            UpdateHistoryEmptyTexts(); // 初回文言設定
        }

        private void UpdateHistoryEmptyTexts()
        {
            // ローカライズ
            _emptyTitle.Text = SR.T("History.Empty.Title", "No history yet");
            _emptyBody.Text  = SR.T("History.Empty.Body", "Your captures will appear here.\nUse the capture shortcut or toolbar to get started.");

            _btnEnableHistory.Text = SR.T("History.Empty.EnableButton", "Enable history");
        }
        private void UpdateHistoryEmptyState()
        {
            EnsureHistoryEmptyStateUi();

            int limit = Properties.Settings.Default.HistoryLimit;
            bool disabled = (limit <= 0);
            bool empty = (_viewHistory == null || _viewHistory.Count == 0);

            // 空状態にするか？
            bool show = disabled || empty;

            _emptyState.Visible = show;
            if (!show) return;

            // 文言とボタンの出し分け
            if (disabled)
            {
                _emptyTitle.Text = SR.T("History.Empty.Disabled.Title", "History is turned off");
                _emptyBody.Text  = SR.T("History.Empty.Disabled.Body", "Set the history limit above 0 to save new captures.");
                _btnEnableHistory.Visible = true;
            }
            else // 件数0
            {
                UpdateHistoryEmptyTexts();
                _btnEnableHistory.Visible = false;
            }

            // オーバーレイを最前面に
            _emptyState.BringToFront();
        }

        private void BuildHistoryToolbar()
        {
            if (_historyToolbar != null) return;

            _historyToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = SystemColors.Control
            };

            _txtSearch = new TextBox { Left = 8, Top = 6, Width = 260 };
            // プレースホルダー（ハンドル生成後に設定）
            // _txtSearch.HandleCreated += (s, e) => { try { SendMessage(_txtSearch.Handle, EM_SETCUEBANNER, 1, "検索（ファイル名 / パス）"); } catch { } };
            _txtSearch.TextChanged += (s, e) => ApplyFilterAndRefresh();

            _cboSort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = _txtSearch.Right + 8, Top = 6, Width = 140 };
            _cboSort.Items.AddRange(new object[] {
                SR.T("History.Toolbar.SortByDate", "Captured Time"),
                SR.T("History.Toolbar.SortByName", "File Name"),
                SR.T("History.Toolbar.SortByWidth", "Width"),
                SR.T("History.Toolbar.SortByHeight", "Height"),
                });
            _cboSort.SelectedIndex = 0;
            _cboSort.SelectedIndexChanged += (s, e) =>
            {
                switch (_cboSort.SelectedIndex)
                {
                    case 1: _sortKey = SortKey.FileName; break;
                    case 2: _sortKey = SortKey.Width; break;
                    case 3: _sortKey = SortKey.Height; break;
                    default: _sortKey = SortKey.LoadedAt; break;
                }
                ApplySortAndRefresh();
            };

            _cboOrder = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = _cboSort.Right + 6, Top = 6, Width = 100 };
            _cboOrder.Items.AddRange(new object[] {
                SR.T("History.Toolbar.OrderDescending", "Descending"),
                SR.T("History.Toolbar.OrderAscending", "Ascending")
                });
            _cboOrder.SelectedIndex = 0; // 既定：降順
            _cboOrder.SelectedIndexChanged += (s, e) => { _sortAsc = (_cboOrder.SelectedIndex == 1); ApplySortAndRefresh(); };

            _btnClearHistory = new Button { Text = "クリア", Left = _cboOrder.Right + 8, Top = 5, Width = 70 };
            _btnClearHistory.Click += (s, e) =>
            {
                _txtSearch.Text = "";
                try
                {
                    var snap = HistoryBridge.GetSnapshot();
                    RefreshAllHistory(snap);
                }
                catch
                {
                    ApplyFilterAndRefresh();
                }
            };
            _btnDelete = new Button
            {
                Text = SR.T("History.Toolbar.DeleteSelected", "Delete Selected"),
                Left = _btnClearHistory.Right + 8,
                Top = 5,
                Width = 110
            };
            _btnDelete.Click += (s, e) => DeleteSelected();

            _historyToolbar.Controls.AddRange(new Control[] { _txtSearch, _cboSort, _cboOrder, _btnClearHistory, _btnDelete });

            this.tabHistory.Controls.Add(_lvHistory);
            this.tabHistory.Controls.Add(_historyToolbar);
            _lvHistory.BringToFront();
            UpdateHistoryToolbarTexts();
        }

        private Bitmap BuildPlaceholder()
        {
            var bmp = new Bitmap(THUMB_W, THUMB_H);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(240, 240, 240));
                using (var p = new Pen(Color.Silver)) g.DrawRectangle(p, 0, 0, THUMB_W - 1, THUMB_H - 1);

                // ローカライズされた文字列を使用（英語フォールバック付き）
                var s = SR.T("History.Thumb.Placeholder", "No preview");
                using (var f = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point))
                using (var br = new SolidBrush(Color.Gray))
                {
                    var sz = g.MeasureString(s, f);
                    g.DrawString(s, f, br, (THUMB_W - sz.Width) / 2f, (THUMB_H - sz.Height) / 2f);
                }
            }
            return bmp;
        }


        private static bool IsUsableImage(Image img)
        {
            if (img == null) return false;
            try
            {
                // Width/Height アクセスで ObjectDisposed や Argument 例外が出るケースを吸収
                return img.Width > 0 && img.Height > 0;
            }
            catch { return false; }
        }

        // he.Thumb を安全に取り出す（使えなければ placeholder）
        private Image GetSafeThumb(HistoryEntry he)
        {
            try
            {
                if (he != null && IsUsableImage(he.Thumb))
                    return he.Thumb;

                // まだ Thumb が無いが Path から作れそうなら軽量生成して返す（失敗時は placeholder）
                if (he != null && !string.IsNullOrEmpty(he.Path) && File.Exists(he.Path))
                {
                    // 読めるが重いなら LazyLoad に任せるため、ここでは作らず placeholder を返してOK
                    // （即時生成したいなら RenderThumb を呼ぶ）
                }
            }
            catch { /* no-op */ }

            return _placeholder;
        }

        bool _subscribedToHistory = false;
        private void EnsureHistoryTabUi()
        {
            if (_historyUiInitialized) return;

            _lvHistory = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Tile,                 // ← Tile 表示
                OwnerDraw = true,                 // ← 自前描画ON
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                FullRowSelect = true,
                MultiSelect = true,
                BackColor = SystemColors.Window,
                UseCompatibleStateImageBehavior = false,
                ShowItemToolTips = true
            };
            // タイル寸法（DPI対応）
            UpdateHistoryTileMetrics();
            this.FontChanged += (s, e) => UpdateHistoryTileMetrics();
            this.HandleCreated += (s, e) => UpdateHistoryTileMetrics();

            // OwnerDraw フック
            _lvHistory.DrawItem += LvHistory_DrawItem;
            _lvHistory.MouseMove += LvHistory_MouseMove;
            _lvHistory.MouseLeave += (s, e) => { _historyHotIndex = -1; _lvHistory.Invalidate(); };

            _lvHistory.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_lvHistory, true, null);

            // _imgThumbs = new ImageList { ImageSize = new Size(THUMB_W, THUMB_H), ColorDepth = ColorDepth.Depth32Bit };
            // _lvHistory.LargeImageList = _imgThumbs;
            _imgThumbs = new ImageList
            {
                ImageSize = new Size(THUMB_W, THUMB_H),
                ColorDepth = ColorDepth.Depth32Bit
            };
            _lvHistory.LargeImageList = _imgThumbs;
            _placeholder = BuildPlaceholder();
            _imgThumbs.Images.Add("__placeholder__", _placeholder);
            _thumbQueue = new Queue<HistoryEntry>();


            BuildHistoryToolbar();
            BuildHistoryContextMenu();
            
            SR.CultureChanged += () =>
            {
                try
                {
                    UpdateHistoryToolbarTexts();
                    UpdateHistoryEmptyTexts();
                    UpdateHistoryEmptyState();
                    _lvHistory?.Invalidate();
                }
                catch { /* noop */ }
            };
            _historyUiInitialized = true;

            _lvHistory.ItemActivate += (s, e) => OpenSelected();
            _lvHistory.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; }
            };

            if (!_subscribedToHistory)
            {
                Kiritori.Services.History.HistoryBridge.HistoryChanged += (s, e) =>
                {
                    var snap = Kiritori.Services.History.HistoryBridge.GetSnapshot();
                    RefreshAllHistory(snap);
                };
                _subscribedToHistory = true;
            }
        }
        private void UpdateHistoryTileMetrics()
        {
            float scale = this.DeviceDpi / 96f;
            int textAreaW = (int)Math.Round(180 * scale); // ← 右の文字エリア幅（必要なら増やす）
            int gap = (int)Math.Round(12 * scale);
            int tileW = THUMB_W + gap + textAreaW;
            int tileH = Math.Max(THUMB_H + (int)Math.Round(12 * scale), (int)Math.Round(96 * scale));
            _lvHistory.TileSize = new Size(tileW, tileH);
        }
        private void UpdateHistoryToolbarTexts()
        {
            if (_historyToolbar == null) return;

            // 検索プレースホルダー
            var placeholder = SR.T("History.Toolbar.SearchPlaceholder", "Search (filename / path / OCR)");
            SetCueBanner(_txtSearch, placeholder, showWhenFocused: true);

            // ソートキー（順番は固定、表示だけ差し替え）
            var itemsSort = new[]
            {
                SR.T("History.Toolbar.SortByDate",   "Captured Time"),
                SR.T("History.Toolbar.SortByName",   "File Name"),
                SR.T("History.Toolbar.SortByWidth",  "Width"),
                SR.T("History.Toolbar.SortByHeight", "Height")
            };
            if (_cboSort != null)
            {
                int keep = _cboSort.SelectedIndex;
                _cboSort.BeginUpdate();
                _cboSort.Items.Clear();
                _cboSort.Items.AddRange(itemsSort);
                _cboSort.EndUpdate();
                _cboSort.SelectedIndex = (keep >= 0 && keep < _cboSort.Items.Count) ? keep : 0;
            }

            // 昇順/降順
            if (_cboOrder != null)
            {
                int keep = _cboOrder.SelectedIndex;
                _cboOrder.BeginUpdate();
                _cboOrder.Items.Clear();
                _cboOrder.Items.AddRange(new object[] {
                    SR.T("History.Toolbar.OrderDesc", "Descending"),
                    SR.T("History.Toolbar.OrderAsc",  "Ascending")
                });
                _cboOrder.EndUpdate();
                _cboOrder.SelectedIndex = (keep == 0 || keep == 1) ? keep : 0;
            }

            if (_btnClearHistory != null)
                _btnClearHistory.Text = SR.T("History.Toolbar.Clear", "Clear");

            if (_btnDelete != null)
                _btnDelete.Text = SR.T("History.Toolbar.DeleteSelected", "Delete Selected");
        }

        private void PopulateHistoryItems(IEnumerable<HistoryEntry> entries)
        {
            if (entries == null) return;

            // ListView 項目を即時構築（表示を先に速く）
            var items = new List<ListViewItem>();

            // 1) まず ListView を空にしてから追加（ちらつき抑制）
            _lvHistory.BeginUpdate();
            _lvHistory.Items.Clear();

            // 2) 各エントリごとにプレースホルダー or 既存Thumbで即表示
            foreach (var he in entries)
            {
                if (he == null) continue;

                // ▼ ImageList のキーはできるだけ安定 & 衝突回避
                //    Path が無い（クリップボード）場合や同ファイルの複数履歴も区別できるよう LoadedAt.Ticks を混ぜます
                string keyStable = (he.Path ?? "clipboard")
                                + "|" + he.LoadedAt.Ticks.ToString()
                                + "|" + he.Resolution.Width + "x" + he.Resolution.Height;

                var name = Path.GetFileName(he.Path) ?? "(clipboard)";
                var tip = he.Path ?? "(clipboard)";
                if (!string.IsNullOrEmpty(he.Description))
                    tip += "\r\n" + he.Description;   // OCR 結果を追記

                var it = new ListViewItem(name)
                {
                    Tag = he,
                    ImageKey = keyStable,
                    ToolTipText = tip
                };
                items.Add(it);
                Log.Debug($"History: Add ListViewItem for '{name}' with key '{keyStable}'");
                // ▼ ImageList 登録
                if (!_imgThumbs.Images.ContainsKey(keyStable))
                {
                    if (he.Thumb != null)
                    {
                        // 既存のトレイ用サムネをそのまま使うとサイズが小さい/比率が合わない場合があるので、
                        // 履歴タブ用に一度フィット変換してから入れます。
                        try
                        {
                            using (var fit = RenderThumb(he.Thumb, THUMB_W, THUMB_H))
                                SafeAddThumb(keyStable, fit);
                        }
                        catch
                        {
                            // 壊れ画像などはプレースホルダーにフォールバック
                            SafeAddThumb(keyStable, _placeholder);
                        }
                    }
                    else
                    {
                        // まだサムネが無い → ひとまずプレースホルダー
                        SafeAddThumb(keyStable, _placeholder);
                    }
                }
            }

            // 3) 一括追加して描画更新
            if (items.Count > 0)
                _lvHistory.Items.AddRange(items.ToArray());
            _lvHistory.EndUpdate();

            // 4) LazyLoad の生成キューを組み立て（Thumb が無いものだけを積む）
            var needs = new List<HistoryEntry>();
            foreach (var he in entries)
                if (he != null && he.Thumb == null) needs.Add(he);

            _thumbQueue = new Queue<HistoryEntry>(needs);
        }
        private int _historyHotIndex = -1;

        private void LvHistory_MouseMove(object sender, MouseEventArgs e)
        {
            var it = _lvHistory.GetItemAt(e.X, e.Y);
            int idx = (it != null) ? it.Index : -1;
            if (idx != _historyHotIndex)
            {
                _historyHotIndex = idx;
                _lvHistory.Invalidate();
            }
        }

        private void LvHistory_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            Rectangle r = e.Bounds;
            int pad = 6;
            int gap = 10;

            bool selected = e.Item.Selected;
            Color bg = selected ? SystemColors.Highlight : _lvHistory.BackColor;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, r);

            var inner = Rectangle.Inflate(r, -pad, -pad);

            if (!selected && e.ItemIndex == _historyHotIndex)
            {
                using (var pen = new Pen(Color.FromArgb(90, 0, 120, 215), 2f))
                    e.Graphics.DrawRectangle(pen, Rectangle.Inflate(inner, -1, -1));
            }

            var thumbRect = new Rectangle(inner.Left, inner.Top, THUMB_W, THUMB_H);
            Image img = null;
            try { img = _imgThumbs.Images[e.Item.ImageKey]; } catch { }
            if (img != null) e.Graphics.DrawImage(img, thumbRect);
            else { using (var p = new Pen(Color.Silver)) e.Graphics.DrawRectangle(p, thumbRect); }

            int textX = thumbRect.Right + gap;
            int textW = inner.Right - textX;
            int y = inner.Top;

            Color cMain = selected ? SystemColors.HighlightText : SystemColors.ControlText;
            Color cSub = selected ? Color.FromArgb(230, 230, 230) : Color.Gray;

            var he = e.Item.Tag as HistoryEntry;
            string name = e.Item.Text;
            string date = (he != null) ? he.LoadedAt.ToString("yyyy/MM/dd HH:mm") : "";
            string res = (he != null) ? (he.Resolution.Width + "×" + he.Resolution.Height) : "";
            string folder = (he != null && !string.IsNullOrEmpty(he.Path)) ? Path.GetDirectoryName(he.Path) : "";
            string desc = (he != null) ? he.Description : null;
            if (!string.IsNullOrEmpty(he?.Description))
            {
                desc = desc.Replace("\r", " ").Replace("\n", " ").Trim();
            }

            using (var fBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var f = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var fDesc = new Font("Segoe UI", 8f, FontStyle.Regular))
            {
                var r1 = new Rectangle(textX, y, textW, 20);
                TextRenderer.DrawText(e.Graphics, date, fBold, r1, cMain, TextFormatFlags.EndEllipsis);
                y += r1.Height;

                if (!string.IsNullOrEmpty(res))
                {
                    var r2 = new Rectangle(textX, y, textW, 18);
                    TextRenderer.DrawText(e.Graphics, res, fBold, r2, cSub, TextFormatFlags.EndEllipsis);
                    y += r2.Height;
                }

                // ② 説明（OCR） 最大2行・折返し省略
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var rDesc = new Rectangle(textX, y, textW, (int)Math.Ceiling(fDesc.GetHeight(e.Graphics) * 2 + 2));
                    DrawMultilineEllipsis(e.Graphics, desc, fDesc, rDesc, selected ? SystemColors.HighlightText : Color.DimGray, 2);
                    y += rDesc.Height;
                }
                // // ④ フォルダ
                // if (!string.IsNullOrEmpty(folder))　
                // {
                //     var r3 = new Rectangle(textX, y, textW, 18);
                //     TextRenderer.DrawText(e.Graphics, folder, f, r3, cSub, TextFormatFlags.EndEllipsis);
                //     // y += r3.Height; // 高さが足りないときは下行は描けなくてもOK
                // }
            }

            if (e.Item.Focused)
            {
                var focusRect = Rectangle.Inflate(inner, -1, -1);
                ControlPaint.DrawFocusRectangle(e.Graphics, focusRect,
                    selected ? SystemColors.HighlightText : SystemColors.ControlText, Color.Transparent);
            }
        }

        private static void DrawMultilineEllipsis(Graphics g, string text, Font font, Rectangle rect, Color color, int maxLines)
        {
            if (string.IsNullOrEmpty(text)) return;

            StringFormat sf = new StringFormat();
            try
            {
                // 文字詰めの見た目を整えたい場合は GenericTypographic でもOK
                sf.Trimming = StringTrimming.EllipsisWord;
                sf.FormatFlags |= StringFormatFlags.LineLimit;

                int lineH = (int)Math.Ceiling(font.GetHeight(g));
                int h = Math.Min(rect.Height, lineH * Math.Max(1, maxLines));
                Rectangle r = new Rectangle(rect.X, rect.Y, rect.Width, h);

                using (var br = new SolidBrush(color))
                {
                    g.DrawString(text, font, br, r, sf);
                }
            }
            finally { sf.Dispose(); }
        }



        private void StartLazyThumbLoad()
        {
            if (_thumbLoadingActive) return;
            if (_thumbQueue == null || _thumbQueue.Count == 0)
                RefillThumbQueueFromListView();

            _idleHandler = OnAppIdle_GenerateThumbs;
            Application.Idle += _idleHandler;
            _thumbLoadingActive = true;
        }
        private static string GetStableKey(HistoryEntry he) =>
            (he.Path ?? "clipboard")
            + "|" + he.LoadedAt.Ticks.ToString()
            + "|" + he.Resolution.Width + "x" + he.Resolution.Height;

        private void StopLazyThumbLoad()
        {
            if (!_thumbLoadingActive) return;
            Application.Idle -= _idleHandler;
            _idleHandler = null;
            _thumbLoadingActive = false;
        }

        private void RefillThumbQueueFromListView()
        {
            var needs = new List<HistoryEntry>(_lvHistory.Items.Count);
            foreach (ListViewItem it in _lvHistory.Items)
                if (it.Tag is HistoryEntry he && he.Thumb == null) needs.Add(he);
            _thumbQueue = new Queue<HistoryEntry>(needs);
        }


        private void OnAppIdle_GenerateThumbs(object sender, EventArgs e)
        {
            if (_thumbQueue == null || _thumbQueue.Count == 0)
            {
                StopLazyThumbLoad();
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int processed = 0;

            while (_thumbQueue.Count > 0)
            {
                var he = _thumbQueue.Dequeue();
                TryRenderOneThumb(he);   // 失敗しても飲み込む

                processed++;
                if (processed >= THUMB_PER_IDLE) break;
                if (sw.ElapsedMilliseconds >= IDLE_TIME_BUDGET_MS) break;
            }
        }

        private void TryRenderOneThumb(HistoryEntry he)
        {
            try
            {
                // 既に作っていればスキップ
                if (he.Thumb != null) { AssignThumbToItem(he, he.Thumb); return; }

                Bitmap bmp = null;

                if (!string.IsNullOrEmpty(he.Path) && File.Exists(he.Path))
                {
                    // ファイルロックを避けるため全読み→MemoryStream
                    byte[] bytes = File.ReadAllBytes(he.Path);
                    using (var ms = new MemoryStream(bytes))
                    using (var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false))
                    {
                        using (var src = new Bitmap(img)) // ← 独立ビットマップに
                            bmp = RenderThumb(src, THUMB_W, THUMB_H);
                    }
                }
                else if (he.Thumb != null)
                {
                    bmp = RenderThumb(he.Thumb, THUMB_W, THUMB_H);
                }

                if (bmp != null)
                {
                    he.Thumb = bmp; // キャッシュ（以後は再利用）
                    AssignThumbToItem(he, bmp);
                }
            }
            catch { /* 壊れた画像などは無視 */ }
        }

        private static Bitmap RenderThumb(Bitmap src, int w, int h)
        {
            // フィット計算（letterbox）
            float sx = (float)w / src.Width;
            float sy = (float)h / src.Height;
            float s = Math.Min(sx, sy);
            int rw = Math.Max(1, (int)(src.Width * s));
            int rh = Math.Max(1, (int)(src.Height * s));

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                int dx = (w - rw) / 2;
                int dy = (h - rh) / 2;
                g.DrawImage(src, new Rectangle(dx, dy, rw, rh));
                using (var pen = new Pen(Color.Silver))
                    g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
            }
            return bmp;
        }

        private void AssignThumbToItem(HistoryEntry he, Bitmap bmp)
        {
            // ImageList へ登録 → 対応 ListViewItem に割当
            string key = GetStableKey(he);
            SafeReplaceThumb(key, bmp, _placeholder);
            
            // 対応するListViewItemを探して更新
            foreach (ListViewItem it in _lvHistory.Items)
            {
                if (ReferenceEquals(it.Tag, he))
                {
                    it.ImageKey = key;
                    break;
                }
            }
        }

        internal void SetupHistoryTabIfNeededAndShow(IEnumerable<HistoryEntry> entries)
        {
            EnsureHistoryTabUi();
            _allHistory = (entries == null) ? new List<HistoryEntry>() : new List<HistoryEntry>(entries);
            ApplyFilterAndSort();
            RebuildListView(_viewHistory);
            StartLazyThumbLoad();
            UpdateHistoryEmptyState();
        }

        private void ApplyFilterAndSort()
        {
            string q = (_txtSearch != null ? _txtSearch.Text : null);
            q = string.IsNullOrWhiteSpace(q) ? "" : q.Trim();

            IEnumerable<HistoryEntry> filtered = _allHistory;

            if (!string.IsNullOrEmpty(q))
            {
                filtered = filtered.Where(he =>
                {
                    // ファイル名・フルパス
                    string name = Path.GetFileName(he?.Path ?? "") ?? "";
                    string path = he?.Path ?? "";

                    // OCR（改行をスペース化して検索性UP）
                    string desc = (he?.Description ?? "").Replace("\r", " ").Replace("\n", " ");

                    return
                        name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        desc.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            IEnumerable<HistoryEntry> sorted;
            switch (_sortKey)
            {
                case SortKey.FileName:
                    sorted = _sortAsc
                        ? filtered.OrderBy(he => Path.GetFileName(he.Path ?? ""), StringComparer.OrdinalIgnoreCase)
                        : filtered.OrderByDescending(he => Path.GetFileName(he.Path ?? ""), StringComparer.OrdinalIgnoreCase);
                    break;
                case SortKey.Width:
                    sorted = _sortAsc
                        ? filtered.OrderBy(he => he.Resolution.Width)
                        : filtered.OrderByDescending(he => he.Resolution.Width);
                    break;
                case SortKey.Height:
                    sorted = _sortAsc
                        ? filtered.OrderBy(he => he.Resolution.Height)
                        : filtered.OrderByDescending(he => he.Resolution.Height);
                    break;
                default: // LoadedAt
                    sorted = _sortAsc
                        ? filtered.OrderBy(he => he.LoadedAt)
                        : filtered.OrderByDescending(he => he.LoadedAt);
                    break;
            }

            _viewHistory = sorted.ToList();
        }



        private void ApplyFilterAndRefresh()
        {
            string q = (_txtSearch != null ? _txtSearch.Text : null);
            q = string.IsNullOrWhiteSpace(q) ? "" : q.Trim();

            if (string.IsNullOrEmpty(q))
            {
                // 検索が空になったら最新スナップショットを再読み込み
                try
                {
                    var snap = HistoryBridge.GetSnapshot(); // 既存の仕組みに合わせてください
                    _allHistory = snap?.ToList() ?? new List<HistoryEntry>();
                }
                catch
                {
                    // 取得できなければ現状維持
                }
            }

            ApplyFilterAndSort();
            RebuildListView(_viewHistory);
            StartLazyThumbLoad();
            UpdateHistoryEmptyState();
        }

        internal void RefreshAllHistory(IEnumerable<HistoryEntry> fresh)
        {
            _allHistory = (fresh == null) ? new List<HistoryEntry>() : fresh.ToList();
            ApplyFilterAndRefresh();
        }

        private void ApplySortAndRefresh()
        {
            ApplyFilterAndSort();
            RebuildListView(_viewHistory);
            StartLazyThumbLoad();
            UpdateHistoryEmptyState();
        }
        private void SafeAddThumb(string key, Image img)
        {
            if (_imgThumbs == null || img == null) return;
            if (_imgThumbs.Images.ContainsKey(key)) return;

            try
            {
                if (img.Width <= 0 || img.Height <= 0) return;

                // 必ずクローンを渡す → ImageList が独立コピーを持てる
                using (var clone = new Bitmap(img))
                {
                    _imgThumbs.Images.Add(key, (Bitmap)clone.Clone());
                }
            }
            catch
            {
                // 壊れた画像は placeholder にフォールバック
                if (!_imgThumbs.Images.ContainsKey("__placeholder__") && _placeholder != null)
                    SafeAddThumb("__placeholder__", _placeholder);
            }
        }

        private void SafeReplaceThumb(string key, Image img, Image fallback)
        {
            if (_imgThumbs == null || img == null) return;
            try
            {
                if (_imgThumbs.Images.ContainsKey(key)) _imgThumbs.Images.RemoveByKey(key);
                SafeAddThumb(key, img);
            }
            catch
            {
                if (_imgThumbs.Images.ContainsKey(key)) _imgThumbs.Images.RemoveByKey(key);
                if (fallback != null) SafeAddThumb(key, fallback);
            }
        }
        private void RebuildListView(IEnumerable<HistoryEntry> entries)
        {
            _lvHistory.BeginUpdate();
            _lvHistory.Items.Clear();

            foreach (var he in entries)
            {
                string key = GetStableKey(he);

                // 画像は必ず検証してから
                var img = GetSafeThumb(he);
                try
                {
                    SafeAddThumb(key, img);
                }
                catch
                {
                    // 万一ここで落ちる場合も placeholder にフォールバック
                    try
                    {
                        if (_imgThumbs.Images.ContainsKey(key))
                            _imgThumbs.Images.RemoveByKey(key);
                        SafeAddThumb("__placeholder__", _placeholder);
                        var _ = _imgThumbs.Handle;
                    }
                    catch { /* ここでの失敗は無視 */ }
                }

                string name = Path.GetFileName(he.Path) ?? "(clipboard)";
                string sub1 = he.LoadedAt.ToString("yyyy/MM/dd HH:mm");
                string sub2 = he.Resolution.Width + "×" + he.Resolution.Height;

                var item = new ListViewItem(name) { Tag = he, ImageKey = key };
                item.SubItems.Add(sub1);
                item.SubItems.Add(sub2);
                // ツールチップ（OCR 含む）
                var tip = he.Path ?? "(clipboard)";
                if (!string.IsNullOrEmpty(he.Description)) tip += "\r\n" + he.Description;
                item.ToolTipText = tip;

                _lvHistory.Items.Add(item);
            }

            _lvHistory.EndUpdate();
        }

        private void DeleteSelected()
        {
            Log.Debug("History: DeleteSelected invoked", "History");
            if (_lvHistory.SelectedItems.Count == 0) return;

            var list = _lvHistory.SelectedItems
                .Cast<ListViewItem>()
                .Select(it => it.Tag as HistoryEntry)
                .Where(he => he != null)
                .ToList();
            if (list.Count == 0) return;

            var names = list
                .Take(5)
                .Select(he => System.IO.Path.GetFileName(he.Path) ?? "(clipboard)");

            var title = SR.T("History.Dialog.DeleteTitle", "Delete History");
            string msg;
            if (list.Count <= 5)
            {
                // e.g. "Delete these 3 item(s)?"
                msg = SR.F("History.Dialog.DeleteConfirmFew",
                        "Delete these {0} item(s)?", list.Count)
                    + "\n\n- " + string.Join("\n- ", names);
            }
            else
            {
                // e.g. "Delete the selected 12 item(s)?"
                msg = SR.F("History.Dialog.DeleteConfirmMany",
                        "Delete the selected {0} item(s)?", list.Count);
            }

            if (MessageBox.Show(this, msg, title,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            try
            {
                Log.Debug("Pref->RequestDelete: " + string.Join(", ",
                    list.Select(h => (h.Path ?? "clipboard") + "|" + h.LoadedAt.Ticks + "|" + h.Resolution.Width + "x" + h.Resolution.Height)), "History");
                // 1) 実ファイル削除（存在するものだけ）
                foreach (var he in list)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(he.Path) && File.Exists(he.Path))
                            File.Delete(he.Path);
                        Log.Debug($"History: Deleted file '{he.Path}'", "History");
                    }
                    catch
                    {
                        Log.Debug($"History: Failed to delete file '{he.Path}'", "History");
                    }
                }


                // 2) UI側データから除去
                var set = new HashSet<HistoryEntry>(list);
                _allHistory = _allHistory.Where(h => !set.Contains(h)).ToList();

                // 3) 画面更新
                ApplyFilterAndRefresh();

                // 4) 他画面へ通知（トレイ履歴など）
                Kiritori.Services.History.HistoryBridge.RequestDelete(list);
                Kiritori.Services.History.HistoryBridge.RaiseChanged(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    SR.T("History.Dialog.DeleteFailed", "Failed to delete history.")
                    + "\n" + ex.Message,
                    title,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildHistoryContextMenu()
        {
            // ListView がまだ無ければ何もしない（後で EnsureHistoryTabUi から再呼びされる）
            if (_lvHistory == null || _lvHistory.IsDisposed) return;

            // すでに作ってあればテキストだけ更新して終了
            if (_ctxHistory != null)
            {
                UpdateHistoryContextMenuTexts(); // 多言語化ヘルパ（なければ削除可）
                // 念のため再アタッチ（ListView を作り直したケース）
                _lvHistory.ContextMenuStrip = _ctxHistory;
                return;
            }

            _ctxHistory = new ContextMenuStrip();

            _miOpen       = new ToolStripMenuItem("Open");
            _miOpenFolder = new ToolStripMenuItem("Open Containing Folder");
            _miCopyPath   = new ToolStripMenuItem("Copy Path");
            _miCopyOcr    = new ToolStripMenuItem("Copy OCR Text");
            _miCopyImage  = new ToolStripMenuItem("Copy Image to Clipboard");
            _miRunOcr     = new ToolStripMenuItem("Run OCR");
            _miDelete     = new ToolStripMenuItem("Delete");

            _miOpen.Click       += (s, e) => OpenSelected();
            _miOpenFolder.Click += (s, e) => RevealInExplorerSelected();
            _miCopyPath.Click   += (s, e) => CopyPathSelected();
            _miCopyOcr.Click    += (s, e) => CopyOcrSelected();
            _miCopyImage.Click  += (s, e) => CopyImageSelected();
            _miRunOcr.Click     += async (s, e) => await RunOcrForSelectedAsync();
            _miDelete.Click     += (s, e) => DeleteSelected();

            _ctxHistory.Items.AddRange(new ToolStripItem[]
            {
                _miOpen,
                _miOpenFolder,
                new ToolStripSeparator(),
                _miCopyPath,
                _miCopyOcr,
                _miCopyImage,
                new ToolStripSeparator(),
                _miRunOcr,
                new ToolStripSeparator(),
                _miDelete
            });

            _ctxHistory.Opening += (s, e) =>
            {
                // _lvHistory が null でないことを再確認（保険）
                if (_lvHistory == null || _lvHistory.IsDisposed) { e.Cancel = true; return; }

                var sel = GetSelectedEntries();
                bool single  = sel.Count == 1;
                bool has     = sel.Count > 0;
                bool hasPath = single && !string.IsNullOrEmpty(sel[0].Path) && File.Exists(sel[0].Path);
                bool canRunOcr = hasPath && single && string.IsNullOrWhiteSpace(sel[0].Description);

                _miOpen.Enabled       = hasPath;
                _miOpenFolder.Enabled = hasPath;
                _miCopyPath.Enabled   = has;
                _miCopyOcr.Enabled    = has;
                _miCopyImage.Enabled  = hasPath && single;
                _miRunOcr.Enabled     = canRunOcr;
                _miDelete.Enabled     = has;
            };

            // 多言語化（SR がある場合）
            try { UpdateHistoryContextMenuTexts(); } catch { /* SR 未用意なら無視 */ }
            _lvHistory.ContextMenuStrip = _ctxHistory;

            // ListView のハンドル再作成時にもメニューが外れないように保険
            _lvHistory.HandleCreated -= LvHistory_HandleCreated_AttachCtx;
            _lvHistory.HandleCreated += LvHistory_HandleCreated_AttachCtx;
        }

        private void LvHistory_HandleCreated_AttachCtx(object sender, EventArgs e)
        {
            if (_lvHistory != null && !_lvHistory.IsDisposed && _ctxHistory != null)
                _lvHistory.ContextMenuStrip = _ctxHistory;
        }

        private void UpdateHistoryContextMenuTexts()
        {
            if (_ctxHistory == null) return;

            _miOpen.Text       = SR.T("History.Menu.Open",        "Open");
            _miOpenFolder.Text = SR.T("History.Menu.OpenFolder",  "Open Containing Folder");
            _miCopyPath.Text   = SR.T("History.Menu.CopyPath",    "Copy Path");
            _miCopyOcr.Text    = SR.T("History.Menu.CopyOcr",     "Copy OCR Text");
            _miCopyImage.Text  = SR.T("History.Menu.CopyImage",   "Copy Image to Clipboard");
            _miRunOcr.Text     = SR.T("History.Menu.RunOcr",      "Run OCR");
            _miDelete.Text     = SR.T("History.Menu.Delete",      "Delete");
        }

        private async Task RunOcrForSelectedAsync()
        {
            var sel = GetSelectedEntries();
            if (sel.Count != 1) return;
            var he = sel[0];
            if (string.IsNullOrEmpty(he.Path) || !File.Exists(he.Path)) return;
            if (!string.IsNullOrWhiteSpace(he.Description)) return; // Descが既にある場合は実行しない仕様のまま

            Cursor prev = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                // ロック回避: フル読み→MS→Bitmap
                byte[] bytes = File.ReadAllBytes(he.Path);
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false))
                using (var bmp = new Bitmap(img))
                {
                    var text = await OcrFacade.RunAsync(
                        bmp,
                        copyToClipboard: true,   // ← クリップボードへコピー
                        preprocess: true
                    ).ConfigureAwait(true);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        he.Description = text;

                        // 念のための保険（ランタイム環境によっては OcrFacade がコピーに失敗した時）
                        try { Clipboard.SetText(text); } catch { /* noop */ }

                        // ツールチップ更新
                        foreach (ListViewItem it in _lvHistory.Items)
                        {
                            if (ReferenceEquals(it.Tag, he))
                            {
                                var tip = he.Path ?? "(clipboard)";
                                tip += "\r\n" + he.Description;
                                it.ToolTipText = tip;
                                break;
                            }
                        }

                        _lvHistory.Invalidate();

                        // トレイの履歴メニューの表示テキストも更新（前回追加したメソッド）
                        var main = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
                        try { main?.RefreshHistoryMenuText(he); } catch { /* no-op */ }

                        // 変更通知（任意）
                        Kiritori.Services.History.HistoryBridge.RaiseChanged(this);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, SR.T("History.Dialog.OcrFailed", "Failed to run OCR.") + "\n" + ex.Message, "OCR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = prev;
            }
        }


        private List<HistoryEntry> GetSelectedEntries()
        {
            var list = new List<HistoryEntry>(_lvHistory.SelectedItems.Count);
            foreach (ListViewItem it in _lvHistory.SelectedItems)
                if (it?.Tag is HistoryEntry he) list.Add(he);
            return list;
        }

        private void OpenSelected()
        {
            var sel = GetSelectedEntries();
            if (sel.Count == 0) return;
            var he = sel[0];
            if (string.IsNullOrEmpty(he.Path) || !File.Exists(he.Path)) return;

            // MainApplication の流儀に合わせて SnapWindow を開く（履歴には重複追加しない）
            var sw = new SnapWindow(Application.OpenForms.OfType<MainApplication>().FirstOrDefault())
            {
                StartPosition = FormStartPosition.CenterScreen,
                SuppressHistory = true
            };
            sw.setImageFromPath(he.Path);
            sw.Show();
        }

        private void RevealInExplorerSelected()
        {
            var sel = GetSelectedEntries();
            if (sel.Count == 0) return;
            var he = sel[0];
            if (string.IsNullOrEmpty(he.Path) || !File.Exists(he.Path)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + he.Path + "\"",
                    UseShellExecute = true
                });
            }
            catch { /* noop */ }
        }

        private void CopyPathSelected()
        {
            var sel = GetSelectedEntries();
            if (sel.Count == 0) return;

            try
            {
                var sb = new StringBuilder();
                foreach (var he in sel)
                {
                    if (!string.IsNullOrEmpty(he.Path)) sb.AppendLine(he.Path);
                }
                var text = sb.ToString();
                if (text.Length > 0) Clipboard.SetText(text);
            }
            catch { /* noop */ }
        }

        private void CopyOcrSelected()
        {
            var sel = GetSelectedEntries();
            if (sel.Count == 0) return;

            try
            {
                var sb = new StringBuilder();
                foreach (var he in sel)
                {
                    var t = he.Description ?? "";
                    if (t.Length > 0) sb.AppendLine(t);
                }
                var text = sb.ToString();
                if (text.Length > 0) Clipboard.SetText(text);
            }
            catch { /* noop */ }
        }

        private void CopyImageSelected()
        {
            var sel = GetSelectedEntries();
            if (sel.Count != 1) return;
            var he = sel[0];
            if (string.IsNullOrEmpty(he.Path) || !File.Exists(he.Path)) return;

            try
            {
                // ロック回避のためフル読み→MS→Bitmap→Clipboard
                byte[] bytes = File.ReadAllBytes(he.Path);
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false))
                using (var bmp = new Bitmap(img))
                {
                    Clipboard.SetImage(bmp);
                }
            }
            catch { /* noop */ }
        }

    }
    
}

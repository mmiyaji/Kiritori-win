using Kiritori.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Svg;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private static readonly object AnnotationSvgLock = new object();
        private readonly Dictionary<string, Bitmap> _annotationSvgBitmaps = new Dictionary<string, Bitmap>();
        private Button _annotationRedoButton;
        private ToolStripMenuItem _annotationRedoMenuItem;
        private static readonly Color[] AnnotationColors =
        {
            Color.FromArgb(255, 138, 61), Color.FromArgb(88, 166, 255),
            Color.FromArgb(78, 201, 140), Color.FromArgb(255, 105, 180)
        };
        private static readonly string[] AnnotationColorKeys = { "Orange", "Blue", "Green", "Pink" };

        private void InitializeAnnotationFeature()
        {
            if (_annotationFeatureInitialized || pictureBox1 == null) return;
            _annotationFeatureInitialized = true;
            pictureBox1.Paint += PictureBox1_PaintAnnotations;
            pictureBox1.MouseDown += PictureBox1_MouseDownAnnotations;
            pictureBox1.MouseMove += PictureBox1_MouseMoveAnnotations;
            pictureBox1.MouseUp += PictureBox1_MouseUpAnnotations;
            pictureBox1.MouseDoubleClick += (s, e) =>
            {
                if (_annotationMode && e.Button == MouseButtons.Left) EditSelectedAnnotationText();
            };
            pictureBox1.Disposed += (s, e) => RestoreStandardMouseHandlers();
            CreateAnnotationPalette();
            CreateAnnotationMenuItems();
            LocationChanged += (s, e) => RepositionAnnotationPalette();
            Resize += (s, e) => SyncAnnotationPaletteVisibility();
            VisibleChanged += (s, e) => SyncAnnotationPaletteVisibility();
            DpiChanged += (s, e) => QueueAnnotationPaletteLayout();
        }

        private void CreateAnnotationMenuItems()
        {
            _annotationModeMenuItem = editPaintToolStripMenuItem ?? new ToolStripMenuItem();
            _annotationModeMenuItem.Tag = "loc:Menu.EditAnnotations";
            _annotationModeMenuItem.ShortcutKeys = (Keys)HOTS.EDIT_MSPAINT;
            _annotationUndoMenuItem = new ToolStripMenuItem(SR.T("Menu.UndoLastAnnotation"));
            _annotationUndoMenuItem.Tag = "loc:Menu.UndoLastAnnotation";
            _annotationUndoMenuItem.Click += (s, e) => UndoLastAnnotation();
            _annotationRedoMenuItem = new ToolStripMenuItem(SR.T("Annotation.Redo")) { Tag = "loc:Annotation.Redo" };
            _annotationRedoMenuItem.Click += (s, e) => RedoAnnotation();
            _annotationClearMenuItem = new ToolStripMenuItem(SR.T("Menu.ClearAnnotations"));
            _annotationClearMenuItem.Tag = "loc:Menu.ClearAnnotations";
            _annotationClearMenuItem.Click += (s, e) => ClearAnnotations();
            if (editParentMenu != null)
            {
                editParentMenu.DropDownItems.Add(new ToolStripSeparator());
                editParentMenu.DropDownItems.Add(_annotationUndoMenuItem);
                editParentMenu.DropDownItems.Add(_annotationRedoMenuItem);
                editParentMenu.DropDownItems.Add(_annotationClearMenuItem);
            }
            UpdateAnnotationMenuState();
        }

        // The palette is an owned top-level window: it never consumes image pixels,
        // appears with this capture, and is omitted from Alt+Tab and the taskbar.
        private void CreateAnnotationPalette()
        {
            if (_annotationPalette != null) return;
            _annotationPalette = new AnnotationPaletteWindow(this)
            {
                Name = "AnnotationPalette",
                Tag = "loc:Annotation.Title",
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                ShowInTaskbar = false,
                MaximizeBox = false,
                MinimizeBox = false,
                AutoScaleMode = AutoScaleMode.None,
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.ControlText,
                AutoScroll = false
            };
            _annotationPaletteTips = new ToolTip { ShowAlways = true, AutoPopDelay = 10000 };
            var toolbar = new FlowLayoutPanel { WrapContents = true, TabIndex = 0 };
            _annotationPaletteRows = new[] { toolbar };
            _annotationPalette.Controls.Add(toolbar);
            var toolsRow = new FlowLayoutPanel { WrapContents = false, Margin = Padding.Empty };
            toolbar.Controls.Add(toolsRow);
            var toolKeys = new[] { "Move", "Rectangle", "Arrow", "Text", "Step", "Mosaic" };
            _annotationToolButtons = new RadioButton[toolKeys.Length];
            for (var i = 0; i < toolKeys.Length; i++)
            {
                var tool = (AnnotationTool)i;
                var glyph = toolKeys[i];
                var button = CreateAnnotationChoice("Annotation." + glyph, 30);
                button.Paint += (s, e) => DrawAnnotationGlyph(e.Graphics, button.ClientRectangle, glyph, button.ForeColor, _annotationPalette.UiDpi);
                button.CheckedChanged += (s, e) =>
                {
                    if (button.Checked && !_updatingAnnotationPalette) SetAnnotationTool(tool);
                };
                _annotationToolButtons[i] = button;
                toolsRow.Controls.Add(button);
            }
            toolbar.Controls.Add(CreateAnnotationDivider());
            var colorRow = new FlowLayoutPanel { WrapContents = false, Margin = Padding.Empty };
            toolbar.Controls.Add(colorRow);
            _annotationColorButtons = new RadioButton[AnnotationColors.Length];
            for (var i = 0; i < AnnotationColors.Length; i++)
            {
                var color = AnnotationColors[i];
                var button = CreateAnnotationChoice("Annotation.Color." + AnnotationColorKeys[i], 20);
                button.Paint += (s, e) =>
                {
                    var size = Math.Max(8, (int)Math.Round(12 * _annotationPalette.UiDpi / 96f));
                    var rect = new Rectangle((button.Width - size) / 2, (button.Height - size) / 2, size, size);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(button.Enabled ? color : SystemColors.ControlDark))
                    using (var pen = new Pen(button.Checked ? SystemColors.HighlightText : SystemColors.ControlDarkDark))
                    {
                        e.Graphics.FillEllipse(brush, rect);
                        e.Graphics.DrawEllipse(pen, rect);
                    }
                };
                button.CheckedChanged += (s, e) =>
                {
                    if (button.Checked && !_updatingAnnotationPalette)
                        SetAnnotationColor(color, CreateFillColorFromStroke(color));
                };
                _annotationColorButtons[i] = button;
                colorRow.Controls.Add(button);
            }
            toolbar.Controls.Add(CreateAnnotationDivider());
            _annotationStyleMenu = new ContextMenuStrip();
            _annotationStyleButton = CreateAnnotationAction("Annotation.Style", 32, (s, e) => ShowAnnotationStyleMenu());
            _annotationStyleButton.AccessibleRole = AccessibleRole.ButtonDropDown;
            _annotationStyleButton.Paint += (s, e) =>
            {
                var selected = GetSelectedAnnotation();
                var tool = GetAnnotationStyleTool();
                var glyph = tool == AnnotationTool.Rectangle
                    ? (selected == null ? _annotationRectangleStyle : selected.RectangleStyle).ToString()
                    : tool == AnnotationTool.Arrow ? (selected == null ? _annotationArrowStyle : selected.ArrowStyle).ToString()
                    : tool.ToString();
                DrawAnnotationGlyph(e.Graphics, _annotationStyleButton.ClientRectangle, glyph,
                    _annotationStyleButton.Enabled ? SystemColors.ControlText : SystemColors.GrayText, _annotationPalette.UiDpi);
                var dpiScale = _annotationPalette.UiDpi / 96f;
                var x = _annotationStyleButton.Width - 6 * dpiScale;
                var y = _annotationStyleButton.Height - 5 * dpiScale;
                using (var brush = new SolidBrush(_annotationStyleButton.Enabled ? SystemColors.ControlText : SystemColors.GrayText))
                    e.Graphics.FillPolygon(brush, new[] { new PointF(x - 2 * dpiScale, y), new PointF(x + 2 * dpiScale, y), new PointF(x, y + 2 * dpiScale) });
            };
            toolbar.Controls.Add(_annotationStyleButton);
            _annotationWidthInput = new NumericUpDown { Minimum = 2, Maximum = 16, Value = 2, Width = 40 };
            _annotationWidthInput.ValueChanged += (s, e) =>
            {
                if (_updatingAnnotationPalette) return;
                var width = (int)_annotationWidthInput.Value;
                var tool = GetAnnotationStyleTool();
                if (tool == AnnotationTool.Text || tool == AnnotationTool.Step) SetAnnotationFontSize(width);
                else
                {
                    BeginAnnotationHistoryChange();
                    ApplyToSelectedShapeOrDefaults(shape => shape.StrokeWidth = width, () => _annotationStrokeWidth = width);
                    ApplyToPreviewShape(shape => shape.StrokeWidth = width);
                    CommitAnnotationHistoryChange();
                }
                RefreshAnnotationUi();
            };
            toolbar.Controls.Add(_annotationWidthInput);
            toolbar.Controls.Add(CreateAnnotationDivider());
            _annotationUndoButton = CreateAnnotationAction("Annotation.Undo", 30, (s, e) => UndoLastAnnotation());
            _annotationRedoButton = CreateAnnotationAction("Annotation.Redo", 30, (s, e) => RedoAnnotation());
            _annotationClearButton = CreateAnnotationAction("Annotation.Clear", 30, (s, e) => ClearAnnotations());
            _annotationDoneButton = CreateAnnotationAction("Annotation.Done", 34, (s, e) => FinishAnnotationsFromPalette());
            _annotationUndoButton.Paint += (s, e) => DrawAnnotationGlyph(e.Graphics, _annotationUndoButton.ClientRectangle, "Undo", _annotationUndoButton.Enabled ? SystemColors.ControlText : SystemColors.GrayText, _annotationPalette.UiDpi);
            _annotationRedoButton.Paint += (s, e) => DrawAnnotationGlyph(e.Graphics, _annotationRedoButton.ClientRectangle, "Redo", _annotationRedoButton.Enabled ? SystemColors.ControlText : SystemColors.GrayText, _annotationPalette.UiDpi);
            _annotationClearButton.Paint += (s, e) => DrawAnnotationGlyph(e.Graphics, _annotationClearButton.ClientRectangle, "Clear", _annotationClearButton.Enabled ? SystemColors.ControlText : SystemColors.GrayText, _annotationPalette.UiDpi);
            _annotationDoneButton.Paint += (s, e) => DrawAnnotationGlyph(e.Graphics, _annotationDoneButton.ClientRectangle, "Done", SystemColors.HighlightText, _annotationPalette.UiDpi);
            _annotationDoneButton.BackColor = SystemColors.Highlight;
            _annotationDoneButton.ForeColor = SystemColors.HighlightText;
            _annotationDoneButton.FlatAppearance.BorderColor = SystemColors.Highlight;
            toolbar.Controls.AddRange(new Control[] { _annotationUndoButton, _annotationRedoButton, _annotationClearButton, _annotationDoneButton });
            _annotationPalette.DpiChanged += (s, e) => QueueAnnotationPaletteLayout();
            ApplyAnnotationPaletteLocalization();
        }

        private RadioButton CreateAnnotationChoice(string key, int width)
        {
            return new RadioButton
            {
                Appearance = Appearance.Button, AutoCheck = true, TextAlign = ContentAlignment.MiddleCenter,
                Name = key, Width = width, Height = 30, FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, CheckedBackColor = SystemColors.Highlight },
                UseVisualStyleBackColor = false
            };
        }

        private Control CreateAnnotationDivider()
        {
            var divider = new Panel { Width = 8, Height = 30, TabStop = false };
            divider.Paint += (s, e) =>
            {
                using (var pen = new Pen(SystemColors.ControlLight))
                    e.Graphics.DrawLine(pen, divider.Width / 2, divider.Height / 5, divider.Width / 2, divider.Height * 4 / 5);
            };
            return divider;
        }

        private Button CreateAnnotationAction(string key, int width, EventHandler action)
        {
            var button = new Button { Name = key, Width = width, Height = 30, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = true };
            button.FlatAppearance.BorderSize = 0;
            button.Click += action;
            return button;
        }

        private void ApplyAnnotationPaletteLocalization()
        {
            if (_annotationPalette == null || _annotationPalette.IsDisposed) return;
            Localizer.Apply(_annotationPalette);
            _annotationWidthInput.AccessibleName = SR.T("Annotation.Width");
            _annotationPaletteTips.SetToolTip(_annotationWidthInput, SR.T("Annotation.WidthHint"));
            var keys = new[] { "Move", "Rectangle", "Arrow", "Text", "Step", "Mosaic" };
            for (var i = 0; i < keys.Length; i++)
                SetAnnotationControlDescription(_annotationToolButtons[i], "Annotation." + keys[i], "Annotation." + keys[i] + "Hint");
            for (var i = 0; i < _annotationColorButtons.Length; i++)
                SetAnnotationControlDescription(_annotationColorButtons[i], "Annotation.Color." + AnnotationColorKeys[i], null);
            SetAnnotationControlDescription(_annotationUndoButton, "Annotation.Undo", "Annotation.UndoHint");
            SetAnnotationControlDescription(_annotationRedoButton, "Annotation.Redo", "Annotation.RedoHint");
            SetAnnotationControlDescription(_annotationClearButton, "Annotation.Clear", "Annotation.ClearHint");
            SetAnnotationControlDescription(_annotationDoneButton, "Annotation.Done", "Annotation.DoneHint");
            UpdateAnnotationMenuState();
            RepositionAnnotationPalette();
        }

        private void SetAnnotationControlDescription(Control control, string nameKey, string hintKey)
        {
            control.AccessibleName = SR.T(nameKey);
            control.AccessibleDescription = hintKey == null ? null : SR.T(hintKey);
            _annotationPaletteTips.SetToolTip(control, control.AccessibleName + (hintKey == null ? "" : "\n" + control.AccessibleDescription));
        }

        private void ShowAnnotationStyleMenu()
        {
            while (_annotationStyleMenu.Items.Count > 0) _annotationStyleMenu.Items[0].Dispose();
            var selected = GetSelectedAnnotation();
            var tool = GetAnnotationStyleTool();
            if (tool == AnnotationTool.Text)
            {
                var background = selected == null ? _annotationTextBackground : selected.TextBackground;
                AddAnnotationStyleItem("TextOutline", !background, () => SetAnnotationTextBackground(false));
                AddAnnotationStyleItem("TextBackground", background, () => SetAnnotationTextBackground(true));
                if (selected != null) AddAnnotationStyleItem("EditText", false, EditSelectedAnnotationText);
                _annotationStyleMenu.Show(_annotationStyleButton, new Point(0, _annotationStyleButton.Height));
                return;
            }
            if (tool == AnnotationTool.Mosaic)
            {
                var block = selected == null ? _annotationMosaicBlockSize : selected.MosaicBlockSize;
                AddAnnotationStyleItem("MosaicWeak", block == 8, () => SetAnnotationMosaicBlockSize(8));
                AddAnnotationStyleItem("MosaicMedium", block == 16, () => SetAnnotationMosaicBlockSize(16));
                AddAnnotationStyleItem("MosaicStrong", block == 24, () => SetAnnotationMosaicBlockSize(24));
                _annotationStyleMenu.Show(_annotationStyleButton, new Point(0, _annotationStyleButton.Height));
                return;
            }
            var keys = tool == AnnotationTool.Rectangle ? new[] { "Filled", "Outline" } : new[] { "Single", "Double", "Line", "Tapered" };
            var current = tool == AnnotationTool.Rectangle ? (int)(selected == null ? _annotationRectangleStyle : selected.RectangleStyle) : (int)(selected == null ? _annotationArrowStyle : selected.ArrowStyle);
            for (var i = 0; i < keys.Length; i++)
            {
                var index = i;
                var item = new ToolStripMenuItem(SR.T("Annotation.Style." + keys[i])) { Checked = i == current };
                item.Click += (s, e) =>
                {
                    if (tool == AnnotationTool.Rectangle) SetAnnotationRectangleStyle((AnnotationRectangleStyle)index);
                    else SetAnnotationArrowStyle((AnnotationArrowStyle)index);
                };
                _annotationStyleMenu.Items.Add(item);
            }
            _annotationStyleMenu.Show(_annotationStyleButton, new Point(0, _annotationStyleButton.Height));
        }

        private void AddAnnotationStyleItem(string key, bool selected, Action action)
        {
            var item = new ToolStripMenuItem(SR.T("Annotation.Style." + key)) { Checked = selected };
            item.Click += (s, e) => action();
            _annotationStyleMenu.Items.Add(item);
        }

        private AnnotationTool GetAnnotationStyleTool()
        {
            var selected = GetSelectedAnnotation();
            if (selected == null) return _annotationTool;
            switch (selected.Kind)
            {
                case AnnotationShapeKind.Rectangle: return AnnotationTool.Rectangle;
                case AnnotationShapeKind.Text: return AnnotationTool.Text;
                case AnnotationShapeKind.Step: return AnnotationTool.Step;
                case AnnotationShapeKind.Mosaic: return AnnotationTool.Mosaic;
                default: return AnnotationTool.Arrow;
            }
        }

        private void UpdateAnnotationPaletteState()
        {
            if (_annotationPalette == null || _annotationPalette.IsDisposed) return;
            _updatingAnnotationPalette = true;
            try
            {
                for (var i = 0; i < _annotationToolButtons.Length; i++)
                    SetAnnotationChoiceState(_annotationToolButtons[i], i == (int)_annotationTool);
                var selected = GetSelectedAnnotation();
                var color = selected == null ? _annotationStrokeColor : selected.StrokeColor;
                for (var i = 0; i < _annotationColorButtons.Length; i++)
                    SetAnnotationChoiceState(_annotationColorButtons[i], color.ToArgb() == AnnotationColors[i].ToArgb());
                var tool = GetAnnotationStyleTool();
                var enabled = tool != AnnotationTool.Move;
                _annotationStyleButton.Enabled = enabled && tool != AnnotationTool.Step;
                var style = tool == AnnotationTool.Rectangle
                    ? (selected == null ? _annotationRectangleStyle : selected.RectangleStyle).ToString()
                    : tool == AnnotationTool.Arrow ? (selected == null ? _annotationArrowStyle : selected.ArrowStyle).ToString()
                    : tool == AnnotationTool.Text ? ((selected == null ? _annotationTextBackground : selected.TextBackground) ? "TextBackground" : "TextOutline")
                    : tool == AnnotationTool.Mosaic ? ((selected == null ? _annotationMosaicBlockSize : selected.MosaicBlockSize) == 8 ? "MosaicWeak" : (selected == null ? _annotationMosaicBlockSize : selected.MosaicBlockSize) == 24 ? "MosaicStrong" : "MosaicMedium") : "Single";
                _annotationStyleButton.AccessibleName = SR.T("Annotation.Style") + ": " + SR.T("Annotation.Style." + style);
                _annotationPaletteTips.SetToolTip(_annotationStyleButton, _annotationStyleButton.AccessibleName + " ▾");
                _annotationStyleButton.Invalidate();
                var fontTool = tool == AnnotationTool.Text || tool == AnnotationTool.Step;
                _annotationWidthInput.Enabled = enabled && tool != AnnotationTool.Mosaic;
                foreach (var button in _annotationColorButtons) button.Enabled = enabled && tool != AnnotationTool.Mosaic;
                _annotationWidthInput.Maximum = fontTool ? 72 : 16;
                _annotationWidthInput.Minimum = fontTool ? 12 : 2;
                _annotationWidthInput.AccessibleName = SR.T(fontTool ? "Annotation.FontSize" : "Annotation.Width");
                _annotationPaletteTips.SetToolTip(_annotationWidthInput, SR.T(fontTool ? "Annotation.FontSize" : "Annotation.WidthHint"));
                var width = fontTool ? (selected == null ? _annotationFontSize : selected.FontSize) : selected == null ? _annotationStrokeWidth : selected.StrokeWidth;
                _annotationWidthInput.Value = Math.Max(_annotationWidthInput.Minimum, Math.Min(_annotationWidthInput.Maximum, width));
                _annotationUndoButton.Enabled = CanUndoAnnotations;
                _annotationRedoButton.Enabled = CanRedoAnnotations;
                if (_annotationUndoMenuItem != null) _annotationUndoMenuItem.Enabled = CanUndoAnnotations;
                if (_annotationRedoMenuItem != null) _annotationRedoMenuItem.Enabled = CanRedoAnnotations;
                _annotationClearButton.Enabled = _annotations.Count > 0;
                var hintKey = selected != null ? "Annotation.SelectedHint" : tool == AnnotationTool.Move ? "Annotation.MoveHint" : "Annotation.DrawHint";
                _annotationPalette.AccessibleDescription = string.Format(SR.T("Annotation.Status"), _annotations.Count, SR.T(hintKey));
            }
            finally { _updatingAnnotationPalette = false; }
        }

        private static void SetAnnotationChoiceState(RadioButton button, bool selected)
        {
            button.Checked = selected;
            button.ForeColor = selected ? SystemColors.HighlightText : SystemColors.ControlText;
            button.BackColor = selected ? SystemColors.Highlight : SystemColors.ControlLightLight;
            button.FlatAppearance.BorderColor = selected ? SystemColors.Highlight : SystemColors.ControlDark;
        }

        private void SetAnnotationPaletteVisible(bool visible)
        {
            if (_annotationPalette == null || _annotationPalette.IsDisposed) return;
            if (!visible || !Visible || WindowState == FormWindowState.Minimized)
            {
                _annotationPalette.Hide();
                return;
            }
            RepositionAnnotationPalette();
            if (!_annotationPalette.Visible) _annotationPalette.Show(this);
            // Showing on another DPI monitor can change the final non-client size.
            RepositionAnnotationPalette();
        }

        private void SyncAnnotationPaletteVisibility()
        {
            if (IsDisposed || Disposing) return;
            SetAnnotationPaletteVisible(_annotationMode);
        }

        private void FinishAnnotationsFromPalette()
        {
            ExitAnnotationMode();
            if (Visible && !IsDisposed) Activate();
        }

        private void QueueAnnotationPaletteLayout()
        {
            if (!IsHandleCreated || IsDisposed || Disposing) return;
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing) RepositionAnnotationPalette();
            }));
        }

        private void LayoutAnnotationPalette()
        {
            if (_annotationPalette == null || _annotationPalette.IsDisposed) return;
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            // .NET Framework may report DeviceDpi=96 even with a per-monitor
            // manifest. Scale this small window explicitly from the native owner DPI.
            _annotationPalette.SetUiDpi(IsHandleCreated ? GetAnnotationWindowDpi(Handle) : 96);
            var scale = _annotationPalette.UiDpi / 96f;
            Func<int, int> px = value => Math.Max(1, (int)Math.Round(value * scale));
            var border = new Size(_annotationPalette.Width - _annotationPalette.ClientSize.Width, _annotationPalette.Height - _annotationPalette.ClientSize.Height);
            var availableWidth = Math.Max(px(200), area.Width - border.Width - px(16));
            var width = Math.Min(px(544), availableWidth);
            _annotationPalette.SuspendLayout();
            _annotationPalette.AutoScroll = false;
            _annotationPalette.AutoScrollMinSize = Size.Empty;
            _annotationPalette.AutoScrollPosition = Point.Empty;
            var toolbar = _annotationPaletteRows[0];
            toolbar.SuspendLayout();
            toolbar.SetBounds(px(8), px(7), width - px(16), px(30));
            foreach (Control control in toolbar.Controls)
            {
                control.Margin = new Padding(0, 0, px(2), 0);
                var group = control as FlowLayoutPanel;
                if (group != null)
                {
                    var tools = group.Controls[0] == _annotationToolButtons[0];
                    foreach (Control button in group.Controls)
                    {
                        button.Size = new Size(px(tools ? 30 : 20), px(30));
                        button.Margin = new Padding(0, 0, px(2), 0);
                    }
                    var groupWidth = 0;
                    foreach (Control button in group.Controls) groupWidth += button.Width + button.Margin.Horizontal;
                    group.Size = new Size(groupWidth, px(30));
                }
                else
                {
                    var logicalWidth = control == _annotationStyleButton ? 32 : control == _annotationWidthInput ? 40
                        : control == _annotationDoneButton ? 34 : control is Button ? 30 : 8;
                    control.Size = new Size(px(logicalWidth), px(30));
                    if (control == _annotationWidthInput) control.Margin = new Padding(0, px(4), px(2), 0);
                }
            }
            var preferredWidth = px(16);
            foreach (Control control in toolbar.Controls) preferredWidth += control.Width + control.Margin.Horizontal;
            width = Math.Min(preferredWidth, availableWidth);
            toolbar.Width = width - px(16);
            toolbar.ResumeLayout(true);
            toolbar.Height = toolbar.GetPreferredSize(new Size(toolbar.Width, 0)).Height;
            var contentHeight = toolbar.Height + px(14);
            var visibleHeight = Math.Min(contentHeight, Math.Max(px(44), area.Height - border.Height - px(16)));
            _annotationPalette.SetPhysicalClientSize(new Size(width, visibleHeight));
            // A normal toolbar needs no scrolling. Enable it only when an unusually
            // small work area cannot accommodate even the wrapped toolbar vertically.
            if (visibleHeight < contentHeight)
            {
                _annotationPalette.AutoScrollMinSize = new Size(0, contentHeight);
                _annotationPalette.AutoScroll = true;
            }
            _annotationPalette.ResumeLayout(true);
        }

        private void RepositionAnnotationPalette()
        {
            if (_annotationPalette == null || _annotationPalette.IsDisposed || _positioningAnnotationPalette || WindowState == FormWindowState.Minimized) return;
            _positioningAnnotationPalette = true;
            try
            {
                LayoutAnnotationPalette();
                var area = Screen.FromRectangle(Bounds).WorkingArea;
                var gap = Math.Max(6, _annotationPalette.UiDpi / 12);
                _annotationPalette.Location = GetAnnotationPaletteLocation(Bounds, _annotationPalette.Size, area, gap);
            }
            finally { _positioningAnnotationPalette = false; }
        }

        // Screen coordinates support monitors left/above the primary display. Prefer
        // below, then above, then either side; if space is exhausted minimize overlap.
        private static Point GetAnnotationPaletteLocation(Rectangle owner, Size palette, Rectangle area, int gap)
        {
            var centeredX = owner.Left + (owner.Width - palette.Width) / 2;
            var centeredY = owner.Top + (owner.Height - palette.Height) / 2;
            var candidates = new[]
            {
                new Point(centeredX, owner.Bottom + gap), new Point(centeredX, owner.Top - palette.Height - gap),
                new Point(owner.Right + gap, centeredY), new Point(owner.Left - palette.Width - gap, centeredY)
            };
            var best = Point.Empty;
            long bestOverlap = long.MaxValue;
            foreach (var candidate in candidates)
            {
                var point = new Point(Math.Max(area.Left, Math.Min(area.Right - palette.Width, candidate.X)),
                    Math.Max(area.Top, Math.Min(area.Bottom - palette.Height, candidate.Y)));
                var overlap = Rectangle.Intersect(owner, new Rectangle(point, palette));
                var overlapArea = (long)overlap.Width * overlap.Height;
                if (overlapArea < bestOverlap) { bestOverlap = overlapArea; best = point; }
                if (overlapArea == 0) return point;
            }
            return best;
        }

        private void DisposeAnnotationPalette()
        {
            lock (AnnotationSvgLock)
            {
                foreach (var bitmap in _annotationSvgBitmaps.Values) bitmap.Dispose();
                _annotationSvgBitmaps.Clear();
            }
            _annotationStyleMenu?.Dispose();
            _annotationStyleMenu = null;
            _annotationPaletteTips?.Dispose();
            _annotationPaletteTips = null;
            _annotationPalette?.Dispose();
            _annotationPalette = null;
        }

        private void DrawAnnotationGlyph(Graphics graphics, Rectangle bounds, string glyph, Color color, int dpi)
        {
            if (glyph == "Undo" || glyph == "Redo" || glyph == "Text" || glyph == "Step" || glyph == "Mosaic")
            {
                DrawAnnotationSvg(graphics, bounds, color, dpi, glyph);
                return;
            }

            var state = graphics.Save();
            var scale = dpi / 96f;
            graphics.TranslateTransform(bounds.Left + bounds.Width / 2f - 9 * scale, bounds.Top + bounds.Height / 2f - 9 * scale);
            graphics.ScaleTransform(scale, scale);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                switch (glyph)
                {
                    case "Move":
                        graphics.DrawLine(pen, 2, 9, 16, 9); graphics.DrawLine(pen, 9, 2, 9, 16);
                        graphics.DrawLines(pen, new[] { new Point(5, 6), new Point(2, 9), new Point(5, 12) });
                        graphics.DrawLines(pen, new[] { new Point(13, 6), new Point(16, 9), new Point(13, 12) });
                        graphics.DrawLines(pen, new[] { new Point(6, 5), new Point(9, 2), new Point(12, 5) });
                        graphics.DrawLines(pen, new[] { new Point(6, 13), new Point(9, 16), new Point(12, 13) });
                        break;
                    case "Rectangle": case "Outline": case "Filled":
                        if (glyph == "Filled")
                            using (var fill = new SolidBrush(Color.FromArgb(65, color))) graphics.FillRectangle(fill, 2, 3, 14, 12);
                        graphics.DrawRectangle(pen, 2, 3, 14, 12);
                        break;
                    case "Clear":
                        graphics.DrawLine(pen, 3, 4, 15, 4);
                        graphics.DrawLines(pen, new[] { new Point(6, 4), new Point(6, 2), new Point(12, 2), new Point(12, 4) });
                        graphics.DrawLines(pen, new[] { new Point(4, 6), new Point(5, 16), new Point(13, 16), new Point(14, 6) });
                        graphics.DrawLine(pen, 7, 7, 7, 13); graphics.DrawLine(pen, 11, 7, 11, 13);
                        break;
                    case "Done":
                        graphics.DrawLines(pen, new[] { new Point(3, 9), new Point(7, 13), new Point(15, 5) });
                        break;
                    case "Tapered":
                        graphics.FillPolygon(brush, new[] { new Point(2, 16), new Point(9, 6), new Point(6, 5), new Point(16, 2), new Point(13, 12), new Point(12, 9) });
                        break;
                    default:
                        graphics.DrawLine(pen, 3, 15, 15, 3);
                        if (glyph != "Line") graphics.DrawLines(pen, new[] { new Point(7, 3), new Point(15, 3), new Point(15, 11) });
                        if (glyph == "Double") graphics.DrawLines(pen, new[] { new Point(3, 7), new Point(3, 15), new Point(11, 15) });
                        break;
                }
            }
            graphics.Restore(state);
        }

        private static SvgDocument LoadAnnotationSvg(string glyph)
        {
            var resource = glyph == "Undo" ? "undo_2_svg" : glyph == "Redo" ? "redo_2_svg" : glyph == "Text" ? "type_svg" : glyph == "Step" ? "list_ordered_svg" : "grid_2x2_svg";
            var markup = Properties.Resources.ResourceManager.GetString(resource);
            if (string.IsNullOrEmpty(markup)) throw new FileNotFoundException("Embedded annotation SVG resource was not found.");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(markup)))
            {
                return SvgDocument.Open<SvgDocument>(stream);
            }
        }

        private void DrawAnnotationSvg(Graphics graphics, Rectangle bounds, Color color, int dpi, string glyph)
        {
            var pixelSize = Math.Max(1, (int)Math.Round(18f * dpi / 96f));
            var cacheKey = glyph + ":" + color.ToArgb().ToString("X8") + ":" + pixelSize;
            Bitmap bitmap;
            lock (AnnotationSvgLock)
            {
                if (!_annotationSvgBitmaps.TryGetValue(cacheKey, out bitmap))
                {
                    if (_annotationSvgBitmaps.Count >= 16)
                    {
                        foreach (var cached in _annotationSvgBitmaps.Values) cached.Dispose();
                        _annotationSvgBitmaps.Clear();
                    }
                    // SVG.NET resolves inherited currentColor while rendering. Use
                    // a fresh document for each cached color to avoid stale paint.
                    var svg = LoadAnnotationSvg(glyph);
                    svg.Color = new SvgColourServer(color);
                    bitmap = svg.Draw(pixelSize, pixelSize);
                    _annotationSvgBitmaps.Add(cacheKey, bitmap);
                }
            }
            var state = graphics.Save();
            try
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImageUnscaled(bitmap,
                    bounds.Left + (bounds.Width - pixelSize) / 2,
                    bounds.Top + (bounds.Height - pixelSize) / 2);
            }
            finally { graphics.Restore(state); }
        }

        [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
        private static extern uint GetAnnotationWindowDpiNative(IntPtr window);

        private static int GetAnnotationWindowDpi(IntPtr window)
        {
            try { return Math.Max(96, (int)GetAnnotationWindowDpiNative(window)); }
            catch (EntryPointNotFoundException) { return 96; }
        }

        private sealed class AnnotationPaletteWindow : Form
        {
            private readonly SnapWindow _capture;
            private Font _uiFont;
            internal int UiDpi { get; private set; } = 96;
            internal AnnotationPaletteWindow(SnapWindow capture) { _capture = capture; }
            [StructLayout(LayoutKind.Sequential)]
            private struct ClientRect { public int Left, Top, Right, Bottom; }
            [DllImport("user32.dll")]
            private static extern bool GetClientRect(IntPtr window, out ClientRect rect);
            internal void SetPhysicalClientSize(Size size)
            {
                ClientSize = size;
                // WinForms' cached DeviceDpi can differ from the native window DPI.
                // Correct the outer size against the actual client pixels in that case.
                if (IsHandleCreated && GetClientRect(Handle, out var actual))
                {
                    var delta = new Size(size.Width - actual.Right, size.Height - actual.Bottom);
                    if (delta != Size.Empty) Size = new Size(Width + delta.Width, Height + delta.Height);
                }
            }
            internal void SetUiDpi(int dpi)
            {
                if (_uiFont != null && UiDpi == dpi) return;
                UiDpi = dpi;
                var previous = _uiFont;
                _uiFont = new Font("Segoe UI", 12F * dpi / 96F, FontStyle.Regular, GraphicsUnit.Pixel);
                Font = _uiFont;
                if (_capture._annotationStyleMenu != null) _capture._annotationStyleMenu.Font = _uiFont;
                previous?.Dispose();
                Invalidate(true);
            }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing) { _uiFont?.Dispose(); _uiFont = null; }
            }
            protected override bool ShowWithoutActivation => true;
            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                // Work-area/taskbar and monitor changes are broadcast to top-level windows.
                if (message.Msg == 0x001A || message.Msg == 0x007E || message.Msg == 0x02E0) _capture.QueueAnnotationPaletteLayout();
            }
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape)
                {
                    if (_capture._annotationStyleMenu.Visible) return base.ProcessCmdKey(ref msg, keyData);
                    _capture.FinishAnnotationsFromPalette();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.Z)) { _capture.UndoLastAnnotation(); return true; }
                if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { _capture.RedoAnnotation(); return true; }
                if (keyData == Keys.M || keyData == Keys.R || keyData == Keys.A)
                {
                    _capture.SetAnnotationTool(keyData == Keys.M ? AnnotationTool.Move : keyData == Keys.R ? AnnotationTool.Rectangle : AnnotationTool.Arrow);
                    return true;
                }
                if (keyData == Keys.F6) { _capture.Activate(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing && !_capture.Disposing && !_capture.IsDisposed)
                {
                    e.Cancel = true;
                    _capture.FinishAnnotationsFromPalette();
                }
                base.OnFormClosing(e);
            }
        }
    }
}

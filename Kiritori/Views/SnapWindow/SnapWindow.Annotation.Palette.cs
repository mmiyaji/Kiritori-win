using Kiritori.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private void InitializeAnnotationFeature()
        {
            if (_annotationFeatureInitialized || pictureBox1 == null) return;
            _annotationFeatureInitialized = true;

            pictureBox1.Paint += PictureBox1_PaintAnnotations;
            pictureBox1.MouseDown += PictureBox1_MouseDownAnnotations;
            pictureBox1.MouseMove += PictureBox1_MouseMoveAnnotations;
            pictureBox1.MouseUp += PictureBox1_MouseUpAnnotations;
            pictureBox1.Disposed += (s, e) => RestoreStandardMouseHandlers();

            CreateAnnotationPalette();
            CreateAnnotationMenuItems();
            Resize += (s, e) => RepositionAnnotationPalette();
        }

        private void CreateAnnotationMenuItems()
        {
            _annotationModeMenuItem = editPaintToolStripMenuItem ?? new ToolStripMenuItem("Edit annotations");
            _annotationModeMenuItem.Text = "Edit annotations";
            _annotationModeMenuItem.Tag = "loc:Menu.EditAnnotations";
            _annotationModeMenuItem.ShortcutKeys = (Keys)HOTS.EDIT_MSPAINT;

            _annotationUndoMenuItem = new ToolStripMenuItem("Undo last annotation");
            _annotationUndoMenuItem.Tag = "loc:Menu.UndoLastAnnotation";
            _annotationUndoMenuItem.Click += (s, e) => UndoLastAnnotation();

            _annotationClearMenuItem = new ToolStripMenuItem("Clear annotations");
            _annotationClearMenuItem.Tag = "loc:Menu.ClearAnnotations";
            _annotationClearMenuItem.Click += (s, e) => ClearAnnotations();

            if (editParentMenu != null)
            {
                editParentMenu.DropDownItems.Add(new ToolStripSeparator());
                editParentMenu.DropDownItems.Add(_annotationUndoMenuItem);
                editParentMenu.DropDownItems.Add(_annotationClearMenuItem);
            }

            UpdateAnnotationMenuState();
        }

        private void CreateAnnotationPalette()
        {
            if (_annotationPalette != null) return;

            _annotationPalette = new Panel
            {
                Visible = false,
                Size = AnnotationPaletteSize,
                BackColor = Color.FromArgb(232, 26, 29, 34),
                Padding = AnnotationPalettePadding
            };

            _annotationPaletteLabel = new Label
            {
                AutoSize = false,
                Text = "≡",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = AnnotationPaletteDragHandleSize,
                Location = AnnotationPaletteDragHandleLocation,
                Cursor = Cursors.SizeAll
            };

            _annotationClearButton = CreatePaletteButton(string.Empty, AnnotationPaletteClearButtonX, AnnotationPaletteButtonSize.Width, (s, e) => ClearAnnotations());
            _annotationToolButton = CreatePaletteButton("Tool", AnnotationPaletteToolButtonX, AnnotationPaletteToolButtonWidth, (s, e) => ShowAnnotationMenu(_annotationToolMenu, _annotationToolButton));
            SetDoubleBuffered(_annotationPalette);
            _annotationColorButton = CreatePaletteButton("Color", AnnotationPaletteColorButtonX, AnnotationPaletteColorButtonWidth, (s, e) => ShowAnnotationMenu(_annotationColorMenu, _annotationColorButton));
            _annotationStyleButton = CreatePaletteButton("Style", AnnotationPaletteStyleButtonX, AnnotationPaletteStyleButtonWidth, (s, e) => ShowAnnotationMenu(_annotationStyleMenu, _annotationStyleButton));
            _annotationUndoButton = CreatePaletteButton(string.Empty, AnnotationPaletteUndoButtonX, AnnotationPaletteButtonSize.Width, (s, e) => UndoLastAnnotation());
            _annotationDoneButton = CreatePaletteButton(string.Empty, AnnotationPaletteDoneButtonX, AnnotationPaletteButtonSize.Width, (s, e) => ExitAnnotationMode());

            CreateAnnotationPaletteMenus();
            HookPaletteDrag(_annotationPaletteLabel);

            _annotationPalette.Controls.Add(_annotationPaletteLabel);
            _annotationPalette.Controls.Add(_annotationClearButton);
            _annotationPalette.Controls.Add(_annotationToolButton);
            _annotationPalette.Controls.Add(_annotationColorButton);
            _annotationPalette.Controls.Add(_annotationStyleButton);
            _annotationPalette.Controls.Add(_annotationUndoButton);
            _annotationPalette.Controls.Add(_annotationDoneButton);

            Controls.Add(_annotationPalette);
            _annotationPalette.BringToFront();
            RepositionAnnotationPalette();
        }

        private void CreateAnnotationPaletteMenus()
        {
            if (_annotationToolMenu != null) return;

            _annotationToolMenu = new ContextMenuStrip();
            AddToolMenuItems(_annotationToolMenu);

            _annotationColorMenu = new ContextMenuStrip();
            AddColorMenuItems(_annotationColorMenu);

            _annotationStyleMenu = new ContextMenuStrip();
            AddStyleMenuItems(_annotationStyleMenu);
        }

        private void AddToolMenuItems(ContextMenuStrip menu)
        {
            menu.Items.Add(CreateAnnotationMenuItem("✥ Move window", (s, e) => SetAnnotationTool(AnnotationTool.Move)));
            menu.Items.Add(CreateAnnotationMenuItem("▭ Rectangle", (s, e) => SetAnnotationTool(AnnotationTool.Rectangle)));
            menu.Items.Add(CreateAnnotationMenuItem("➜ Arrow", (s, e) => SetAnnotationTool(AnnotationTool.Arrow)));
        }

        private void AddColorMenuItems(ContextMenuStrip menu)
        {
            menu.Items.Add(CreateAnnotationColorMenuItem("Orange", Color.FromArgb(255, 138, 61), Color.FromArgb(48, 255, 138, 61)));
            menu.Items.Add(CreateAnnotationColorMenuItem("Blue", Color.FromArgb(88, 166, 255), Color.FromArgb(48, 88, 166, 255)));
            menu.Items.Add(CreateAnnotationColorMenuItem("Green", Color.FromArgb(78, 201, 140), Color.FromArgb(48, 78, 201, 140)));
            menu.Items.Add(CreateAnnotationColorMenuItem("Pink", Color.FromArgb(255, 105, 180), Color.FromArgb(48, 255, 105, 180)));
        }

        private void AddStyleMenuItems(ContextMenuStrip menu)
        {
            menu.Items.Add(CreateAnnotationMenuItem("▣ Rect: Filled", (s, e) => SetAnnotationRectangleStyle(AnnotationRectangleStyle.Filled)));
            menu.Items.Add(CreateAnnotationMenuItem("▭ Rect: Outline", (s, e) => SetAnnotationRectangleStyle(AnnotationRectangleStyle.Outline)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateAnnotationMenuItem("➜ Arrow: Single", (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Single)));
            menu.Items.Add(CreateAnnotationMenuItem("⟷ Arrow: Double", (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Double)));
            menu.Items.Add(CreateAnnotationMenuItem("╱ Arrow: Line", (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Line)));
            menu.Items.Add(CreateAnnotationMenuItem("➤ Arrow: Tapered", (s, e) => SetAnnotationArrowStyle(AnnotationArrowStyle.Tapered)));
        }

        private ToolStripMenuItem CreateAnnotationMenuItem(string text, EventHandler click)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += click;
            return item;
        }

        private ToolStripMenuItem CreateAnnotationColorMenuItem(string text, Color strokeColor, Color fillColor)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag = Tuple.Create(strokeColor, fillColor)
            };
            item.Click += (s, e) =>
            {
                var pair = (Tuple<Color, Color>)item.Tag;
                SetAnnotationColor(pair.Item1, pair.Item2);
            };
            return item;
        }

        private void ShowAnnotationMenu(ContextMenuStrip menu, Control anchor)
        {
            if (menu == null || anchor == null) return;
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void SetDoubleBuffered(Control control)
        {
            if (control == null) return;
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (property != null) property.SetValue(control, true, null);
        }

        private void HookPaletteDrag(Control control)
        {
            if (control == null) return;
            control.MouseDown += AnnotationPalette_MouseDown;
            control.MouseMove += AnnotationPalette_MouseMove;
            control.MouseUp += AnnotationPalette_MouseUp;
        }

        private void AnnotationPalette_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _annotationPaletteDragging = true;
            _annotationPaletteDragOrigin = Cursor.Position;
            _annotationPaletteOrigin = _annotationPaletteLocation;
        }

        private void AnnotationPalette_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_annotationPaletteDragging) return;
            var current = Cursor.Position;
            var offset = new Size(current.X - _annotationPaletteDragOrigin.X, current.Y - _annotationPaletteDragOrigin.Y);
            var next = new Point(_annotationPaletteOrigin.X + offset.Width, _annotationPaletteOrigin.Y + offset.Height);
            if (next == _annotationPaletteLocation) return;
            _annotationPaletteLocation = next;
            RepositionAnnotationPalette();
        }

        private void AnnotationPalette_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _annotationPaletteDragging = false;
        }

        private Button CreatePaletteButton(string text, int x, int width, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(width, AnnotationPaletteButtonSize.Height),
                Location = new Point(x, AnnotationPaletteButtonY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 49, 57),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                TabStop = false,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(6, 0, 8, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 62, 72);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 71, 82);
            button.Click += click;
            return button;
        }

        private Image GetAnnotationPaletteIcon(string key)
        {
            var image = Properties.Resources.ResourceManager.GetObject(key) as Image;
            return image == null ? null : CreatePaletteIconBitmap(image);
        }

        private string GetToolIconKey()
        {
            switch (_annotationTool)
            {
                case AnnotationTool.Move:
                    return "annotation_move";
                case AnnotationTool.Arrow:
                    return "annotation_arrow_single";
                default:
                    return "annotation_rect_outline";
            }
        }

        private string GetStyleIconKey()
        {
            if (_annotationTool == AnnotationTool.Move) return "annotation_move";
            if (_annotationTool == AnnotationTool.Rectangle)
                return _annotationRectangleStyle == AnnotationRectangleStyle.Outline ? "annotation_rect_outline" : "annotation_rect_filled";

            switch (_annotationArrowStyle)
            {
                case AnnotationArrowStyle.Double:
                    return "annotation_arrow_double";
                case AnnotationArrowStyle.Line:
                    return "annotation_arrow_line";
                case AnnotationArrowStyle.Tapered:
                    return "annotation_arrow_tapered";
                default:
                    return "annotation_arrow_single";
            }
        }

        private Image CreateAnnotationColorSwatch(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.White, 1.2f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.FillEllipse(brush, 2, 2, 12, 12);
                g.DrawEllipse(pen, 2, 2, 12, 12);
            }
            return bmp;
        }

        private Image CreatePaletteIconBitmap(Image source)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, 16, 16));
            }

            return bmp;
        }

        private void RepositionAnnotationPalette()
        {
            if (_annotationPalette == null) return;
            var maxX = Math.Max(8, ClientSize.Width - _annotationPalette.Width - 8);
            var maxY = Math.Max(8, ClientSize.Height - _annotationPalette.Height - 8);
            var x = Math.Max(8, Math.Min(maxX, _annotationPaletteLocation.X));
            var y = Math.Max(8, Math.Min(maxY, _annotationPaletteLocation.Y));
            var clamped = new Point(x, y);
            _annotationPaletteLocation = clamped;
            if (_annotationPalette.Location != clamped)
                _annotationPalette.Location = clamped;
        }
    }
}

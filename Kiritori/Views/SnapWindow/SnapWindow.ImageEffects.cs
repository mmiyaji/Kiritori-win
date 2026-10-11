using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Kiritori.Helpers;
using Kiritori.Views.LiveCapture;

namespace Kiritori
{
    public partial class SnapWindow
    {
        private LiveFrameEffect _wholeImageEffect = LiveFrameEffect.None;
        private int _wholeImageMosaicBlockSize = 16;
        private Bitmap _processedImageCache;
        private Bitmap _processedImageCacheSource;
        private LiveFrameEffect _processedImageCacheEffect;
        private int _processedImageCacheMosaicBlockSize;
        private ToolStripMenuItem _imageEffectMenu;
        private ToolStripMenuItem _imageEffectNoneMenu;
        private ToolStripMenuItem _imageEffectMosaicMenu;
        private ToolStripMenuItem _imageEffectSepiaMenu;
        private ToolStripMenuItem _imageEffectGrayscaleMenu;
        private ToolStripMenuItem[] _imageEffectMosaicStrengthItems;

        private Bitmap GetEffectedBaseImage()
        {
            var source = main_image ?? _originalImage as Bitmap;
            if (source == null) return null;
            if (_wholeImageEffect == LiveFrameEffect.None) return source;
            if (_processedImageCache != null && ReferenceEquals(_processedImageCacheSource, source) &&
                _processedImageCacheEffect == _wholeImageEffect &&
                _processedImageCacheMosaicBlockSize == _wholeImageMosaicBlockSize)
                return _processedImageCache;

            ReleaseProcessedImageCache();
            _processedImageCache = LiveFrameEffects.Apply(source, _wholeImageEffect, _wholeImageMosaicBlockSize);
            _processedImageCacheSource = source;
            _processedImageCacheEffect = _wholeImageEffect;
            _processedImageCacheMosaicBlockSize = _wholeImageMosaicBlockSize;
            return _processedImageCache;
        }

        private void ReleaseProcessedImageCache()
        {
            if (_processedImageCache == null) return;
            if (pictureBox1 != null && ReferenceEquals(pictureBox1.Image, _processedImageCache))
                pictureBox1.Image = null;
            try { _processedImageCache.Dispose(); } catch { }
            _processedImageCache = null;
            _processedImageCacheSource = null;
        }

        private void ReleaseOriginalStill()
        {
            if (_originalStill == null) return;
            if (pictureBox1 != null && ReferenceEquals(pictureBox1.Image, _originalStill))
                pictureBox1.Image = null;
            DisposeBitmap(ref _originalStill);
        }

        private void SetWholeImageEffect(LiveFrameEffect effect, int mosaicBlockSize = 16)
        {
            if (_originalImage == null ||
                (_wholeImageEffect == effect && (effect != LiveFrameEffect.Mosaic || _wholeImageMosaicBlockSize == mosaicBlockSize))) return;

            if (_annotationDragging) CancelAnnotationDrag();
            BeginAnnotationHistoryChange();
            _wholeImageEffect = effect;
            if (effect == LiveFrameEffect.Mosaic) _wholeImageMosaicBlockSize = mosaicBlockSize;
            ReleaseOriginalStill();
            ReleaseProcessedImageCache();
            UpdateImageEffectMenuState();
            RefreshFromOriginalHiQ();
            pictureBox1?.Invalidate();
            CommitAnnotationHistoryChange();
        }

        private void InitializeImageEffectMenu()
        {
            if (editParentMenu == null || _imageEffectMenu != null) return;
            _imageEffectMenu = new ToolStripMenuItem { Name = "imageEffectMenu", Tag = "loc:ImageEffect.Menu" };
            _imageEffectNoneMenu = CreateImageEffectItem("None", LiveFrameEffect.None);
            _imageEffectMosaicMenu = new ToolStripMenuItem { Name = "imageEffectMosaicMenu", Tag = "loc:ImageEffect.Mosaic" };
            _imageEffectMosaicMenu.Click += (s, e) => SetWholeImageEffect(LiveFrameEffect.Mosaic, _wholeImageMosaicBlockSize);
            string[] strengthKeys = { "Weak", "Medium", "Strong" };
            int[] blockSizes = { 8, 16, 32 };
            _imageEffectMosaicStrengthItems = new ToolStripMenuItem[strengthKeys.Length];
            for (int i = 0; i < strengthKeys.Length; i++)
            {
                int blockSize = blockSizes[i];
                var item = new ToolStripMenuItem { Tag = "loc:ImageEffect.Mosaic" + strengthKeys[i], CheckOnClick = false };
                item.Click += (s, e) => SetWholeImageEffect(LiveFrameEffect.Mosaic, blockSize);
                _imageEffectMosaicMenu.DropDownItems.Add(item);
                _imageEffectMosaicStrengthItems[i] = item;
            }
            _imageEffectSepiaMenu = CreateImageEffectItem("Sepia", LiveFrameEffect.Sepia);
            _imageEffectGrayscaleMenu = CreateImageEffectItem("Grayscale", LiveFrameEffect.Grayscale);
            _imageEffectMenu.DropDownItems.AddRange(new ToolStripItem[] {
                _imageEffectNoneMenu, _imageEffectMosaicMenu, _imageEffectSepiaMenu, _imageEffectGrayscaleMenu
            });
            editParentMenu.DropDownItems.Add(new ToolStripSeparator());
            editParentMenu.DropDownItems.Add(_imageEffectMenu);
            ApplyImageEffectMenuLocalization();
            UpdateImageEffectMenuState();
        }

        private void ApplyImageEffectMenuLocalization()
        {
            if (_imageEffectMenu == null) return;
            if (_imageEffectMenu.Tag is string tag && tag.StartsWith("loc:", StringComparison.Ordinal))
                _imageEffectMenu.Text = SR.T(tag.Substring(4));
            ApplyToolStripLocalization(_imageEffectMenu.DropDownItems);
        }

        private ToolStripMenuItem CreateImageEffectItem(string key, LiveFrameEffect effect)
        {
            var item = new ToolStripMenuItem { Name = "imageEffect" + key + "Menu", Tag = "loc:ImageEffect." + key };
            item.Click += (s, e) => SetWholeImageEffect(effect);
            return item;
        }

        private void UpdateImageEffectMenuState()
        {
            if (_imageEffectMenu == null) return;
            _imageEffectMenu.Enabled = _originalImage != null;
            if (_imageEffectNoneMenu != null) _imageEffectNoneMenu.Checked = _wholeImageEffect == LiveFrameEffect.None;
            if (_imageEffectMosaicMenu != null) _imageEffectMosaicMenu.Checked = _wholeImageEffect == LiveFrameEffect.Mosaic;
            if (_imageEffectSepiaMenu != null) _imageEffectSepiaMenu.Checked = _wholeImageEffect == LiveFrameEffect.Sepia;
            if (_imageEffectGrayscaleMenu != null) _imageEffectGrayscaleMenu.Checked = _wholeImageEffect == LiveFrameEffect.Grayscale;
            if (_imageEffectMosaicStrengthItems == null) return;
            int selected = _wholeImageMosaicBlockSize <= 8 ? 0 : (_wholeImageMosaicBlockSize <= 16 ? 1 : 2);
            for (int i = 0; i < _imageEffectMosaicStrengthItems.Length; i++)
                _imageEffectMosaicStrengthItems[i].Checked = _wholeImageEffect == LiveFrameEffect.Mosaic && i == selected;
        }
    }
}

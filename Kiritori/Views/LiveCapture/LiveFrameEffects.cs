using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Kiritori.Views.LiveCapture
{
    internal enum LiveFrameEffect { None, Mosaic, Sepia, Grayscale }

    internal static class LiveFrameEffects
    {
        // Always return a separately owned image; never modify a backend's bitmap.
        internal static Bitmap Apply(Bitmap source, LiveFrameEffect effect, int mosaicBlockSize = 12)
        {
            if (mosaicBlockSize <= 0) throw new ArgumentOutOfRangeException(nameof(mosaicBlockSize));
            if (effect == LiveFrameEffect.None) return (Bitmap)source.Clone();
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(result))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    if (effect == LiveFrameEffect.Mosaic)
                    {
                        int blockSize = mosaicBlockSize;
                        int width = Math.Max(1, (source.Width + blockSize - 1) / blockSize);
                        int height = Math.Max(1, (source.Height + blockSize - 1) / blockSize);
                        using (var small = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                        {
                            using (var sg = Graphics.FromImage(small))
                            using (var attributes = new ImageAttributes())
                            {
                                sg.CompositingMode = CompositingMode.SourceCopy;
                                sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                                attributes.SetWrapMode(WrapMode.TileFlipXY);
                                sg.DrawImage(source, new Rectangle(0, 0, width, height),
                                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                            }
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = PixelOffsetMode.Half;
                            g.DrawImage(small, new Rectangle(0, 0, result.Width, result.Height),
                                0, 0, width, height, GraphicsUnit.Pixel);
                        }
                    }
                    else if (effect == LiveFrameEffect.Sepia)
                    {
                        using (var attributes = new ImageAttributes())
                        {
                            attributes.SetColorMatrix(new ColorMatrix(new[] {
                                new[] { .393f, .349f, .272f, 0f, 0f },
                                new[] { .769f, .686f, .534f, 0f, 0f },
                                new[] { .189f, .168f, .131f, 0f, 0f },
                                new[] { 0f, 0f, 0f, 1f, 0f },
                                new[] { 0f, 0f, 0f, 0f, 1f }
                            }));
                            g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height),
                                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                        }
                    }
                    else if (effect == LiveFrameEffect.Grayscale)
                    {
                        using (var attributes = new ImageAttributes())
                        {
                            attributes.SetColorMatrix(new ColorMatrix(new[] {
                                new[] { .299f, .299f, .299f, 0f, 0f },
                                new[] { .587f, .587f, .587f, 0f, 0f },
                                new[] { .114f, .114f, .114f, 0f, 0f },
                                new[] { 0f, 0f, 0f, 1f, 0f },
                                new[] { 0f, 0f, 0f, 0f, 1f }
                            }));
                            g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height),
                                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                        }
                    }
                    else throw new ArgumentOutOfRangeException(nameof(effect));
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }
}

using System.Drawing;
using Kiritori.Views.LiveCapture;
using Xunit;

namespace Kiritori.Tests.Helpers
{
    public class LiveFrameEffectsTests
    {
        [Fact]
        public void NoneProducesIndependentUnmodifiedImage()
        {
            using (var source = new Bitmap(2, 2))
            {
                source.SetPixel(0, 0, Color.Red);
                using (var output = LiveFrameEffects.Apply(source, LiveFrameEffect.None))
                {
                    Assert.NotSame(source, output);
                    Assert.Equal(Color.Red.ToArgb(), output.GetPixel(0, 0).ToArgb());
                    output.SetPixel(0, 0, Color.Blue);
                    Assert.Equal(Color.Red.ToArgb(), source.GetPixel(0, 0).ToArgb());
                }
            }
        }

        [Fact]
        public void SepiaUsesExpectedChannelWeightsWithoutChangingSource()
        {
            using (var source = new Bitmap(4, 4))
            {
                using (var g = Graphics.FromImage(source)) g.Clear(Color.Red);
                using (var output = LiveFrameEffects.Apply(source, LiveFrameEffect.Sepia))
                {
                    var pixel = output.GetPixel(2, 2);
                    Assert.InRange(pixel.R, 99, 101);
                    Assert.InRange(pixel.G, 88, 90);
                    Assert.InRange(pixel.B, 68, 70);
                    Assert.Equal(255, pixel.A);
                    Assert.Equal(Color.Red.ToArgb(), source.GetPixel(2, 2).ToArgb());
                }
            }
        }

        [Fact]
        public void GrayscaleUsesWeightedLuminanceAndPreservesAlpha()
        {
            using (var source = new Bitmap(4, 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                source.SetPixel(1, 1, Color.FromArgb(117, 255, 0, 0));
                using (var output = LiveFrameEffects.Apply(source, LiveFrameEffect.Grayscale))
                {
                    var pixel = output.GetPixel(1, 1);
                    Assert.InRange(pixel.R, 75, 77);
                    Assert.Equal(pixel.R, pixel.G);
                    Assert.Equal(pixel.R, pixel.B);
                    Assert.Equal(117, pixel.A);
                    Assert.Equal(Color.FromArgb(117, 255, 0, 0).ToArgb(), source.GetPixel(1, 1).ToArgb());
                }
            }
        }

        [Fact]
        public void MosaicBlockSizeChangesOutputAndMustBePositive()
        {
            using (var source = new Bitmap(48, 48))
            {
                for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                        source.SetPixel(x, y, x < 18 ? Color.Red : Color.Blue);
                using (var weak = LiveFrameEffects.Apply(source, LiveFrameEffect.Mosaic, 6))
                using (var strong = LiveFrameEffects.Apply(source, LiveFrameEffect.Mosaic, 24))
                {
                    Assert.NotEqual(weak.GetPixel(20, 12).ToArgb(), strong.GetPixel(20, 12).ToArgb());
                }
                Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                    LiveFrameEffects.Apply(source, LiveFrameEffect.Mosaic, 0));
            }
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(24, 24)]
        [InlineData(25, 13)]
        public void MosaicPreservesSizeAndProducesUniformBlocks(int width, int height)
        {
            using (var source = new Bitmap(width, height))
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        source.SetPixel(x, y, (x + y) % 2 == 0 ? Color.Red : Color.Blue);
                using (var output = LiveFrameEffects.Apply(source, LiveFrameEffect.Mosaic))
                {
                    Assert.Equal(source.Size, output.Size);
                    Assert.Equal(255, output.GetPixel(0, 0).A);
                    Assert.Equal(255, output.GetPixel(width - 1, height - 1).A);
                    Assert.Equal(output.GetPixel(0, 0), output.GetPixel(System.Math.Min(3, width - 1), 0));
                    Assert.Equal(Color.Red.ToArgb(), source.GetPixel(0, 0).ToArgb());
                }
            }
        }
    }
}

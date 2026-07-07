using Kiritori.Helpers;
using Xunit;

namespace Kiritori.Tests.Helpers
{
    public sealed class ImageFileSupportTests
    {
        [Theory]
        [InlineData(@"C:\tmp\a.png")]
        [InlineData(@"C:\tmp\a.JPG")]
        [InlineData(@"C:\tmp\a.jpeg")]
        [InlineData(@"C:\tmp\a.bmp")]
        [InlineData(@"C:\tmp\a.gif")]
        [InlineData(@"C:\tmp\a.tif")]
        [InlineData(@"C:\tmp\a.tiff")]
        [InlineData(@"C:\tmp\a.webp")]
        public void IsSupportedImagePath_accepts_supported_extensions(string path)
        {
            Assert.True(ImageFileSupport.IsSupportedImagePath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"C:\tmp\a.txt")]
        [InlineData(@"C:\tmp\a")]
        public void IsSupportedImagePath_rejects_unsupported_paths(string path)
        {
            Assert.False(ImageFileSupport.IsSupportedImagePath(path));
        }
    }
}

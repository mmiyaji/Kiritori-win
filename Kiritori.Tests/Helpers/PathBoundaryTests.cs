using System;
using System.IO;
using Kiritori.Helpers;
using Xunit;

namespace Kiritori.Tests.Helpers
{
    public sealed class PathBoundaryTests
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "KiritoriPathBoundary", "root");

        [Fact]
        public void ResolveUnder_accepts_normal_descendant()
        {
            var actual = PathBoundary.ResolveUnder(Root, Path.Combine("bin", "ffmpeg", "8.0.3"));

            Assert.Equal(
                Path.Combine(Root, "bin", "ffmpeg", "8.0.3"),
                actual,
                ignoreCase: true);
        }

        [Theory]
        [InlineData("..\\outside")]
        [InlineData("child\\..\\..\\outside")]
        public void ResolveUnder_rejects_parent_escape(string relative)
        {
            Assert.Throws<InvalidOperationException>(() => PathBoundary.ResolveUnder(Root, relative));
        }

        [Fact]
        public void ResolveUnder_rejects_absolute_path()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PathBoundary.ResolveUnder(Root, Path.Combine(Path.GetPathRoot(Root), "outside")));
        }

        [Fact]
        public void ResolveUnder_rejects_unc_path()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PathBoundary.ResolveUnder(Root, @"\\server\share\extension"));
        }

        [Fact]
        public void IsStrictlyUnder_rejects_root_and_sibling_prefix()
        {
            Assert.False(PathBoundary.IsStrictlyUnder(Root, Root));
            Assert.False(PathBoundary.IsStrictlyUnder(Root, Root + "-sibling"));
            Assert.True(PathBoundary.IsStrictlyUnder(Root, Path.Combine(Root, "child")));
        }
    }
}

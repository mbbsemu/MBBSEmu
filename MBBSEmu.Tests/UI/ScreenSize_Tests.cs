using FluentAssertions;
using MBBSEmu.UI;
using Xunit;

namespace MBBSEmu.Tests.UI
{
    public class ScreenSize_Tests
    {
        [Theory]
        [InlineData(79, 24, true)]
        [InlineData(80, 23, true)]
        [InlineData(80, 24, false)]
        [InlineData(200, 60, false)]
        public void IsBelowMinimum(int width, int height, bool expected)
        {
            ScreenSize.IsBelowMinimum(width, height).Should().Be(expected);
        }

        [Fact]
        public void TooSmallMessage_MatchesSpec()
        {
            ScreenSize.TooSmallMessage(70, 20, "Ctrl+Q still shuts down MBBSEmu.").Should().Equal(
                "Terminal too small",
                "",
                "Currently 70×20, need at least 80×24",
                "",
                "Resize the window to continue.",
                "Ctrl+Q still shuts down MBBSEmu.");
        }
    }
}

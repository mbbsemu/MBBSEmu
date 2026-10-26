using FluentAssertions;
using MBBSEmu.UI.Logging;
using Xunit;

namespace MBBSEmu.Tests.UI.Logging
{
    public class FollowSelection_Tests
    {
        [Fact]
        public void Adjust_EmptyLog_Follows()
        {
            FollowSelection.Adjust(-1, true, 0, 0, 10).Should().Be(new FollowResult(9, true));
        }

        [Fact]
        public void Adjust_SelectedLastAndVisible_Follows()
        {
            FollowSelection.Adjust(9, true, 10, 0, 15).Should().Be(new FollowResult(14, true));
        }

        [Fact]
        public void Adjust_SelectedOlderRow_StaysOnSameEntry()
        {
            FollowSelection.Adjust(4, true, 10, 0, 15).Should().Be(new FollowResult(4, false));
        }

        [Fact]
        public void Adjust_SelectedOlderRow_ShiftsByDropped()
        {
            FollowSelection.Adjust(4, true, 10, 3, 12).Should().Be(new FollowResult(1, false));
        }

        [Fact]
        public void Adjust_SelectedRowDropped_ClampsToZero()
        {
            FollowSelection.Adjust(1, true, 10, 3, 12).Should().Be(new FollowResult(0, false));
        }

        [Fact]
        public void Adjust_LastRowScrolledOutOfView_DoesNotFollow()
        {
            FollowSelection.Adjust(9, false, 10, 0, 15).Should().Be(new FollowResult(9, false));
        }

        [Fact]
        public void Adjust_NoSelectionButBottomVisible_Follows()
        {
            FollowSelection.Adjust(-1, true, 10, 0, 11).Should().Be(new FollowResult(10, true));
        }
    }
}

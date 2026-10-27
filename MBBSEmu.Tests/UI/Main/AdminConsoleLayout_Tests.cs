using FluentAssertions;
using MBBSEmu.UI.Main;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class AdminConsoleLayout_Tests
    {
        [Fact]
        public void Calculate_80x24()
        {
            AdminConsoleLayout.Calculate(80, 24, 16).Should().Be(
                new AdminConsoleLayout(SidebarWidth: 20, LeftWidth: 60, AuditHeight: 8, ApplicationLogHeight: 14,
                    AuditSummaryWidth: 10, ShowSource: false, SourceWidth: 14, ShowMilliseconds: false));
        }

        [Fact]
        public void Calculate_100x30()
        {
            AdminConsoleLayout.Calculate(100, 30, 16).Should().Be(
                new AdminConsoleLayout(SidebarWidth: 20, LeftWidth: 80, AuditHeight: 10, ApplicationLogHeight: 18,
                    AuditSummaryWidth: 14, ShowSource: true, SourceWidth: 14, ShowMilliseconds: true));
        }

        [Fact]
        public void Calculate_132x40_LongName()
        {
            AdminConsoleLayout.Calculate(132, 40, 25).Should().Be(
                new AdminConsoleLayout(SidebarWidth: 27, LeftWidth: 105, AuditHeight: 13, ApplicationLogHeight: 25,
                    AuditSummaryWidth: 32, ShowSource: true, SourceWidth: 20, ShowMilliseconds: true));
        }

        /// <summary>
        ///     Detail keeps room for a classic 63-character audit detail first; width beyond that widens Summary from
        ///     10 up to the 32 characters a summary can be
        /// </summary>
        [Theory]
        [InlineData(80, 10)]
        [InlineData(94, 10)]
        [InlineData(98, 12)]
        [InlineData(100, 14)]
        [InlineData(110, 24)]
        [InlineData(118, 32)]
        [InlineData(200, 32)]
        public void Calculate_AuditSummaryWidensOnceDetailHasRoom(int screenWidth, int expectedSummaryWidth)
        {
            var layout = AdminConsoleLayout.Calculate(screenWidth, 30, 16);

            layout.AuditSummaryWidth.Should().Be(expectedSummaryWidth);
            if (expectedSummaryWidth > 10)
                (layout.LeftWidth - 2 - layout.AuditSummaryWidth - 1).Should().BeGreaterThanOrEqualTo(63, "Detail keeps its 63 characters");
        }

        [Fact]
        public void Calculate_SidebarCappedAtQuarterWidth()
        {
            AdminConsoleLayout.Calculate(132, 40, 34).SidebarWidth.Should().Be(33);
        }

        [Fact]
        public void Calculate_AuditHeightNeverBelowEight()
        {
            AdminConsoleLayout.Calculate(80, 24, 16).AuditHeight.Should().Be(8);
            AdminConsoleLayout.Calculate(200, 60, 16).AuditHeight.Should().Be(20);
        }
    }
}

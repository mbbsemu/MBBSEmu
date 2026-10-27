using FluentAssertions;
using MBBSEmu.UI.Main;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class StatusLine_Tests
    {
        private const string RightText = "Telnet: ON  Rlogin: OFF  Build 2026.10.26.1";

        [Fact]
        public void RightText_Format()
        {
            StatusLine.RightText(true, false, "2026.10.26.1").Should().Be(RightText);
        }

        [Fact]
        public void AdminConsoleHints_ShutdownThenLoadModuleFirst()
        {
            StatusLine.AdminConsoleHints.Should().Equal(new StatusHint("^Q", "Shutdown"), new StatusHint("^O", "Load module"),
                new StatusHint("Enter", "View entry"), new StatusHint("Tab", "Next panel"));
        }

        [Fact]
        public void FitHints_80Columns_KeepsShutdownAndLoadModule()
        {
            StatusLine.FitHints(80, StatusLine.AdminConsoleHints, RightText)
                .Select(x => x.Key).Should().Equal("^Q", "^O");
            StatusLine.FitHints(80, StatusLine.AdminConsoleHints, StatusLine.RightText(true, true, "1234"))
                .Select(x => x.Key).Should().Equal("^Q", "^O");
        }

        [Fact]
        public void FitHints_132Columns_KeepsAll()
        {
            StatusLine.FitHints(132, StatusLine.AdminConsoleHints, RightText)
                .Should().Equal(StatusLine.AdminConsoleHints);
        }

        [Fact]
        public void FitHints_DevelopmentBuildAt80_DropsAllHints()
        {
            var rightText = StatusLine.RightText(true, true, "Development (Overwritten in Github Actions)");

            StatusLine.FitHints(80, StatusLine.AdminConsoleHints, rightText).Should().BeEmpty();
            rightText.Length.Should().Be(73).And.BeLessThanOrEqualTo(80 - 2);
        }
    }
}

using FluentAssertions;
using MBBSEmu.UI;
using Xunit;

namespace MBBSEmu.Tests.UI
{
    public class UserInterfaceModeSelector_Tests
    {
        [Theory]
        [InlineData(false, false, false, true, EnumUserInterfaceMode.AdminConsole)]
        [InlineData(true, false, false, true, EnumUserInterfaceMode.Cli)]
        [InlineData(false, true, false, true, EnumUserInterfaceMode.Cli)]
        [InlineData(false, false, true, true, EnumUserInterfaceMode.Cli)]
        [InlineData(false, false, false, false, EnumUserInterfaceMode.Cli)]
        public void Select_AdminConsoleOnlyForInteractiveNormalRun(bool cli, bool console, bool apiReport, bool interactive,
            EnumUserInterfaceMode expected)
        {
            UserInterfaceModeSelector.Select(cli, console, apiReport, interactive).Should().Be(expected);
        }

        [Theory]
        [InlineData(false, false, false, false, true)]
        [InlineData(true, false, false, false, false)]
        [InlineData(false, true, false, false, false)]
        [InlineData(false, false, true, false, false)]
        [InlineData(false, false, false, true, false)]
        public void IsHeadlessFallback_OnlyWhenNoFlagAndNoTerminal(bool cli, bool console, bool apiReport, bool interactive,
            bool expected)
        {
            UserInterfaceModeSelector.IsHeadlessFallback(cli, console, apiReport, interactive).Should().Be(expected);
        }
    }
}

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

        /// <summary>
        ///     Only a run with no arguments in a terminal opens the admin console from MBBSEmu's own folder; every
        ///     command line keeps today's behavior
        /// </summary>
        [Theory]
        [InlineData(0, true, true)]
        [InlineData(0, false, false)]
        [InlineData(1, true, false)]
        [InlineData(4, true, false)]
        public void IsGuiMode_OnlyWithNoArgumentsInATerminal(int argumentCount, bool interactive, bool expected)
        {
            UserInterfaceModeSelector.IsGuiMode(argumentCount, interactive).Should().Be(expected);
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

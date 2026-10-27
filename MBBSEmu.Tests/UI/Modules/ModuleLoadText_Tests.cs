using FluentAssertions;
using MBBSEmu.UI.Modules;
using Xunit;

namespace MBBSEmu.Tests.UI.Modules
{
    public class ModuleLoadText_Tests
    {
        [Fact]
        public void PlayHint_TelnetOn_PointsAtTelnet()
        {
            ModuleLoadText.PlayHint(true, 23, true, 513)
                .Should().Be("Connect a telnet client to localhost, port 23, to play.");
        }

        [Fact]
        public void PlayHint_OnlyRloginOn_PointsAtRlogin()
        {
            ModuleLoadText.PlayHint(false, 23, true, 513)
                .Should().Be("Telnet is turned off, so connect with Rlogin to localhost, port 513, to play.");
        }

        [Fact]
        public void PlayHint_BothOff_SaysNobodyCanConnect()
        {
            ModuleLoadText.PlayHint(false, 23, false, 513)
                .Should().Be("Telnet and Rlogin are both turned off, so nobody can connect to play yet.");
        }
    }
}

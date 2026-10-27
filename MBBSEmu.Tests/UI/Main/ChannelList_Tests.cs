using FluentAssertions;
using MBBSEmu.UI.Main;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class ChannelList_Tests
    {
        [Fact]
        public void Build_FormatsEachChannel()
        {
            var lines = ChannelList.Build(4, new[] { new ChannelSession(0, "sysop"), new ChannelSession(2, "") });

            lines.Select(x => x.Text).Should().Equal("0: SYSOP", "1: DISCONNECTED", "2: CONNECTING", "3: DISCONNECTED");
            lines.Select(x => x.IsDisconnected).Should().Equal(false, true, false, true);
            lines.Select(x => x.IsConnecting).Should().Equal(false, false, true, false);
        }

        [Fact]
        public void Build_NullUsernameIsConnecting()
        {
            ChannelList.Build(1, new[] { new ChannelSession(0, null) })[0].Text.Should().Be("0: CONNECTING");
        }

        [Fact]
        public void Build_IgnoresSessionsOutsideChannelCount()
        {
            var lines = ChannelList.Build(2, new[] { new ChannelSession(5, "SYSOP") });

            lines.Select(x => x.Text).Should().Equal("0: DISCONNECTED", "1: DISCONNECTED");
        }

        [Fact]
        public void CountOnline_CountsOnlyNamedSessionsInRange()
        {
            ChannelList.CountOnline(4, new[]
            {
                new ChannelSession(0, "A"),
                new ChannelSession(1, ""),
                new ChannelSession(9, "B")
            }).Should().Be(1);
        }

        [Fact]
        public void Title_Format()
        {
            ChannelList.Title(5, 32).Should().Be("Channels (5/32)");
        }
    }
}

using FluentAssertions;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    /// <summary>
    ///     Exercises the raw-byte TestSession APIs (ReadUntilPattern, GetCapturedOutput)
    ///     used for byte-exact assertions against golden captures.
    /// </summary>
    [Collection("Non-Parallel")]
    public class TestSessionRawBytes_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void ReadUntilPattern_ReturnsRawBytesAndCapturesTranscript()
        {
            ExecuteTest((session, host) =>
            {
                var menu = session.ReadUntilPattern(Encoding.ASCII.GetBytes("Make your selection"), TimeSpan.FromSeconds(5));

                menu.Length.Should().BeGreaterThan("Make your selection".Length);
                Encoding.ASCII.GetString(menu).Should().EndWith("Make your selection");

                //The transcript sees every byte regardless of what reads consumed
                var transcript = session.GetCapturedOutput();
                transcript.Should().StartWith(menu);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));
                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void ReadUntilPattern_PatternNeverArrives_ThrowsTimeout()
        {
            ExecuteTest((session, host) =>
            {
                Assert.Throws<TimeoutException>(() =>
                    session.ReadUntilPattern(Encoding.ASCII.GetBytes("never printed"), TimeSpan.FromMilliseconds(500)));

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));
                WaitUntil('.', "Have a nice day");
            });
        }
    }
}

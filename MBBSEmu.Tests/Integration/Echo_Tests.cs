using FluentAssertions;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    [Collection("Non-Parallel")]
    public class Echo_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void DoEchoTestAndLogOff()
        {
            ExecuteTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));

                WaitUntil(':', "Type something");

                session.SendToModule(Encoding.ASCII.GetBytes("This is really cool!\r\n"));

                WaitUntil(':', "You entered");
                WaitUntil('\n', "This is really cool!");

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        /// <summary>
        ///     Tests to ensure Parsin maintains spaces end-to-end when processing input
        /// </summary>
        [Fact]
        public void ParsinMaintainSpaces()
        {
            ExecuteTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));

                WaitUntil(':', "Type something");

                session.SendToModule(Encoding.ASCII.GetBytes("Test    Spaces\r\n"));

                WaitUntil(':', "You entered");
                WaitUntil('\n', "Test    Spaces");

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        /// <summary>
        ///     Input passes through GSBL line editing on its way to the module: control characters
        ///     are dropped, DEL erases the previous character, and the echo reflects the edited line
        /// </summary>
        [Fact]
        public void InputIsEditedAndEchoedByGsbl()
        {
            ExecuteTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));

                WaitUntil(':', "Type something");

                session.SendToModule(Encoding.ASCII.GetBytes("ab\u0001\tc\u007fd\r\n"));

                WaitUntil(':', "You entered");
                WaitUntil('\n', "abd");

                Encoding.ASCII.GetString(session.GetCapturedOutput()).Should().Contain("abc\b \bd\r\n");

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }
    }
}

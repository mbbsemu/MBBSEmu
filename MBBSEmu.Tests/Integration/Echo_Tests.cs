using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class Echo_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void DoEchoTestAndLogOff()
        {
            ExecuteTest(session => {
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
            ExecuteTest(session => {
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
    }
}

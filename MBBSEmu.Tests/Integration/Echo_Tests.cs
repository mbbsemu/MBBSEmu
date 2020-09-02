using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class Echo_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void doEchoTestAndLogOff()
        {
            ExecuteTest(session => {
                WaitUntil(':', "Make your selection");

                _session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));

                WaitUntil(':', "Type something");

                _session.SendToModule(Encoding.ASCII.GetBytes("This is really cool!\r\n"));

                WaitUntil(':', "You entered");
                WaitUntil('\n', "This is really cool!");

                _session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }
    }
}

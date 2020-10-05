using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class BtrievePrint_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void DoEchoTestAndLogOff()
        {
            ExecuteTest(session => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));

                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " P\r\nPrinting all the btrieve rows.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 23923 svalue: \r\n" +
                    "uid: Sysop lvalue: 0 svalue: Whatever\r\n" +
                    "uid: Sysop lvalue: 23556 svalue: 3553088\r\n" +
                    "uid: Sysop lvalue: 3774400 svalue: hahah\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }
    }
}

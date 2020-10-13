using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class BtrievePrint_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void DoPrintTestAndLogOff()
        {
            ExecuteTest((session, host) => {
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

        private static int mbbsemuHash(string s)
        {
            int hash = 7;
            foreach (char c in s)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }

        [Fact]
        public void AddItemsAndPrintTestAndThenLogoff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));

                WaitUntil(':', "Make your selection");

                // add - by id - 31337
                session.SendToModule(Encoding.ASCII.GetBytes("AI31337\r\n"));
                WaitUntil(':', "Make your selection");

                // add - by string - mbbs4evr
                session.SendToModule(Encoding.ASCII.GetBytes("ASmbbs4evr\r\n"));
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
                    "uid: Sysop lvalue: 31337 svalue: 31337\r\n" +
                    $"uid: Sysop lvalue: {mbbsemuHash("mbbs4evr")} svalue: mbbs4evr\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        // TODO(paladine): Add a test that inserts the same key to verify key restraints
    }
}

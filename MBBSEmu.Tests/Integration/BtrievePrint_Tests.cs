using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class BtrievePrint_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void PhysicalOrderTestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));

                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " P\r\nPrinting all the btrieve rows, key 0 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 3444 svalue: 3444 incvalue: 1\r\n" +
                    "uid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
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
        public void AddItemsAndPhysicalOrderTestAndThenLogoff()
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
                    " P\r\nPrinting all the btrieve rows, key 0 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 3444 svalue: 3444 incvalue: 1\r\n" +
                    "uid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "uid: Sysop lvalue: 31337 svalue: 31337 incvalue: 5\r\n" +
                    $"uid: Sysop lvalue: {mbbsemuHash("mbbs4evr")} svalue: mbbs4evr incvalue: 6\r\n" +
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

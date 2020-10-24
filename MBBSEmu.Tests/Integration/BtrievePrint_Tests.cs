using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class BtrievePrint_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void PhysicalOrder_TestAndLogOff()
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

        [Fact]
        public void Key1_Equality_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("2P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 2P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_Equality_NotFound_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7777\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("2P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 2P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nPress X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_GreaterThan_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("3P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 3P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_GreaterThanOrEqual_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("4P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 4P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_Less_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("5P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 5P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 3444 svalue: 3444 incvalue: 1\r\n" +
                    "uid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_LessOrEqual_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("6P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 6P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_Lowest_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("7P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 7P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "uid: Sysop lvalue: 3444 svalue: 3444 incvalue: 1\r\n" +
                    "uid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key1_Highest_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K1\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("I7776\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("8P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 8P\r\nPrinting all the btrieve rows, key 1 and length 4.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_Equality_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("2P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 2P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "Press X to exit.",
                };

                foreach (var line in lines) host.Logger.Error(line);

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_Equality_NotFound_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SNope\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("2P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 2P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\n" +
                    "Press X to exit.",
                };

                foreach (var line in lines) host.Logger.Error(line);

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_GreaterThan_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("3P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 3P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_GreaterThanOrEqual_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("4P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 4P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_Less_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("5P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 5P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 7776 svalue: 7776 incvalue: 2\r\n" +
                    "uid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_LessOrEqual_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("6P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 6P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: 1052234073 svalue: StringValue incvalue: 3\r\n" +
                    "uid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
                    "Press X to exit.",
                };

                Assert.Equal(expected, lines);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }

        [Fact]
        public void Key2_Lowest_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SStringValue\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("7P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 7P\r\nPrinting all the btrieve rows, key 2 and length 32.",
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

        [Fact]
        public void Key2_Highest_TestAndLogOff()
        {
            ExecuteTest((session, host) => {
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("B\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("K2\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("SWhatever\r\n"));
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("8P\r\n"));

                var lines = WaitUntil('.', "Press X to exit");
                var expected = new List<string>() {
                    " 8P\r\nPrinting all the btrieve rows, key 2 and length 32.",
                    ".",
                    ".",
                    "\r\nuid: Sysop lvalue: -615634567 svalue: stringValue incvalue: 4\r\n" +
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
        public void AddItems_PhysicalOrder_TestAndThenLogoff()
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

using MBBSEmu.HostProcess.Structs;
using System;
using Xunit;

namespace MBBSEmu.Tests.Structs
{
    public class UserAccount_Tests
    {
        [Fact]
        public void NewAccountsAreBornTheYearTheMajorBBSWasReleased()
        {
            Assert.Equal(DateTime.Now.Year - 1986, new UserAccount().age);
            Assert.Equal(DateTime.Now.Year - 1986, new UserAccount("sysop").age);
        }
    }
}

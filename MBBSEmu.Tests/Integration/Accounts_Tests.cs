using MBBSEmu.Database.Repositories.Account;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class Accounts_Tests : MBBSEmuIntegrationTestBase
    {
        [Theory]
        [InlineData("SYSOP", true)]
        [InlineData("sysop", true)]
        [InlineData("Sysop", true)]
        [InlineData("derp", false)]
        public void Username_Lookup(string userName, bool shouldExist)
        {
            var account = _serviceResolver.GetService<IAccountRepository>().GetAccountByUsername(userName);

            if(shouldExist)
                Assert.NotNull(account);
            else
                Assert.Null(account);
        }
    }
}

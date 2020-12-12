using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class echsec_Tests : ExportedModuleTestBase
    {
        private const ushort ECHESC = 811;

        [Fact]
        public void echesc_Test()
        {
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ECHESC, new List<ushort> { 'X', 10 });

            Assert.True(testSessions[0].EchoSecureEnabled);
            Assert.Equal(10, testSessions[0].ExtUsrAcc.wid);
            Assert.Equal((byte)'X', testSessions[0].ExtUsrAcc.ech);
        }
    }
}

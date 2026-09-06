using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    public class btuech_Tests : ExportedModuleTestBase
    {
        private const ushort BTUECH_ORDINAL = 11;

        [Fact]
        public void btuech_on_clears_secure_echo()
        {
            Reset();
            testSessions[0].EchoSecureEnabled = true;
            testSessions[0].ExtUsrAcc.ech = (byte)'*';
            testSessions[0].TransparentMode = true;

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUECH_ORDINAL,
                new List<ushort> { 0, 1 });

            Assert.False(testSessions[0].EchoSecureEnabled);
            Assert.False(testSessions[0].TransparentMode);
        }

        [Fact]
        public void btuech_off_is_transparent_and_leaves_secure_flag()
        {
            Reset();
            testSessions[0].EchoSecureEnabled = true;

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUECH_ORDINAL,
                new List<ushort> { 0, 0 });

            Assert.True(testSessions[0].TransparentMode);
            Assert.True(testSessions[0].EchoSecureEnabled);
        }
    }
}

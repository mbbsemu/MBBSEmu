using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class echon_Tests : ExportedModuleTestBase
    {
        private const ushort ECHON = 181;
        private const ushort ECHONU = 182;

        [Fact]
        public void echon_clears_secure_echo()
        {
            Reset();
            testSessions[0].EchoSecureEnabled = true;
            testSessions[0].ExtUsrAcc.ech = (byte)'*';
            testSessions[0].TransparentMode = true;

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ECHON, new List<ushort>());

            Assert.False(testSessions[0].EchoSecureEnabled);
            Assert.False(testSessions[0].TransparentMode);
        }

        [Fact]
        public void echonu_clears_secure_echo()
        {
            Reset();
            testSessions[0].EchoSecureEnabled = true;
            testSessions[0].ExtUsrAcc.ech = (byte)'*';

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ECHONU, new List<ushort> { 0 });

            Assert.False(testSessions[0].EchoSecureEnabled);
            Assert.False(testSessions[0].TransparentMode);
        }
    }
}

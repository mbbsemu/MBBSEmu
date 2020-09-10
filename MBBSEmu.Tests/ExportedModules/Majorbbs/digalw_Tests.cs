using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class digalw_Tests : ExportedModuleTestBase
    {
        private const int DIGALW_ORDINAL = 169;

        [Fact]
        public void daytoday_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DIGALW_ORDINAL, new List<IntPtr16>());

            //Verify Results
            ExecutePropertyTest(DIGALW_ORDINAL);

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }
    }
}

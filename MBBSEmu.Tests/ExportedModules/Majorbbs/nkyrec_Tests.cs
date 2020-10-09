using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class nkyrec_Tests : ExportedModuleTestBase
    {
        private const int NKYREC_ORDINAL = 432;

        [Fact]
        public void nkyrec_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NKYREC_ORDINAL, new List<IntPtr16>());

            //Verify Results
            ExecutePropertyTest(NKYREC_ORDINAL);

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }
    }
}

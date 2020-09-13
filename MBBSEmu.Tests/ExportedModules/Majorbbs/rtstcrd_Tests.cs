using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rtstcrd_Tests : ExportedModuleTestBase
    {
        private const int RTSTCRD_ORDINAL = 517;

        [Fact]
        public void rtstcrd_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RTSTCRD_ORDINAL, new List<IntPtr16>());

            //Verify Results
            ExecutePropertyTest(RTSTCRD_ORDINAL);

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }
    }
}
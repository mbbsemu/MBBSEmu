using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class genrdn_Tests : ExportedModuleTestBase
    {
        private const int GENRDN_ORDINAL = 315;

        [Theory]
        [InlineData(1, 3)]
        [InlineData(5, 20)]
        [InlineData(2500, 10000)]
        [InlineData(10000, 65000)]
        [InlineData(1, 6000)]
        public void genrdnTest(ushort valueMin, ushort valueMax)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GENRDN_ORDINAL, new List<ushort> { valueMin, valueMax });
            
            //Verify Results
            Assert.InRange(mbbsEmuCpuRegisters.AX,valueMin, valueMax);
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class clock_Tests : ExportedModuleTestBase
    {
        private const int CLOCK_ORDINAL = 109;

        [Fact]
        public void clock_Test()
        {
            //Reset State
            Reset();
            Thread.Sleep(20);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CLOCK_ORDINAL, new List<ushort>());

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            Assert.InRange(mbbsEmuCpuRegisters.AX, (ushort)20, ushort.MaxValue);
        }
    }
}

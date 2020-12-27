using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class farmalloc_Tests : ExportedModuleTestBase
    {
        private const int FARMALLOC_ORDINAL = 203;
        private const int FARFREE_ORDINAL = 202;

        [Fact]
        public void FARMALLOC_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARMALLOC_ORDINAL, new List<ushort> { 10, 10 });

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.DX);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARFREE_ORDINAL, new List<FarPtr> { mbbsEmuCpuRegisters.GetPointer() });
        }
    }
}

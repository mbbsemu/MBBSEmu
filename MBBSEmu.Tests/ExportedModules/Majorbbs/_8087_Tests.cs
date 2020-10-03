using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class _8087_Tests : ExportedModuleTestBase
    {
        private const int _8087_ORDINAL = 737;

        [Fact]
        public void _8087_Test()
        {
            //Reset State
            Reset();


            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, _8087_ORDINAL, new List<IntPtr16>());

            //Verify Results
            Assert.Equal(3, mbbsEmuCpuRegisters.AX);
        }
    }
}

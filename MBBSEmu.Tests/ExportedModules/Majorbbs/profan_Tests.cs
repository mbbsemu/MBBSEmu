using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class profan_Tests : ExportedModuleTestBase
    {
        private const int PROFAN_ORDINAL = 480;

        [Fact]
        public void profanTests()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PROFAN_ORDINAL, new List<FarPtr>());

            //Verify Results
            ExecutePropertyTest(PROFAN_ORDINAL);

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}

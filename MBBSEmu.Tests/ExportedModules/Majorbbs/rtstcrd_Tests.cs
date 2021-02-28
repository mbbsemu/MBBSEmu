using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int RTSTCRD_ORDINAL = 517;

        [Fact]
        public void rtstcrd_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RTSTCRD_ORDINAL, new List<FarPtr>());

            //Verify Results
            ExecutePropertyTest(RTSTCRD_ORDINAL);

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }
    }
}

using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class paccit_Tests : ExportedModuleTestBase
    {
        private const int PACCIT_ORDINAL = 465;

        [Fact]
        public void paccitTests()
        {
            //Reset State
            Reset();

            mbbsModule.Memory.SetWord("PFNLVL", 3);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PACCIT_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.Equal(0, mbbsModule.Memory.GetWord("PFNLVL"));
        }
    }
}

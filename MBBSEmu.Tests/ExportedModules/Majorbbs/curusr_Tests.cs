using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class curusr_Tests : ExportedModuleTestBase
    {
        private const int CURUSR_ORDINAL = 151;

        [Fact]
        public void CURUSR_Test()
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 0xFF });

            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetWord("USRNUM"));
        }
    }
}

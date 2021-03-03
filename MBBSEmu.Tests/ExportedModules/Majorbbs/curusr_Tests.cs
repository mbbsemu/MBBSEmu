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

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });

            Assert.Equal(1, mbbsEmuMemoryCore.GetWord("USRNUM"));
        }

        [Fact]
        public void CURUSR_invalid_Test()
        {
            //Reset State
            Reset();

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1000 });

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));
        }
    }
}

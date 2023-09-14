using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class srand_Tests : ExportedModuleTestBase
    {
        private const int SRAND_ORDINAL = 561;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(ushort.MaxValue)]
        public void srandTest(ushort seed)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SRAND_ORDINAL, new List<ushort> { seed });

            //Verify Results
            Assert.Equal(seed, mbbsEmuMemoryCore.GetDWord("RANDSEED"));
        }
    }
}

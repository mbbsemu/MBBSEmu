using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    /// <summary>
    ///     Tests verified against results from Borland C++ 4.5 for DOS
    /// </summary>
    public class rand_Tests : ExportedModuleTestBase
    {
        private const int RAND_ORDINAL = 486;

        [Theory]
        [InlineData(1234, 1356, 2236414843)]
        [InlineData(2236414843, 29133, 4056779384)]
        [InlineData(4056779384, 21998, 3589159641)]
        [InlineData(3589159641, 27018, 1770671342)]
        [InlineData(1770671342, 30398, 4139675975)]
        [InlineData(4139675975, 13799, 904336820)]
        [InlineData(904336820, 26300, 3871102533)]
        [InlineData(3871102533, 16520, 3230196298)]
        [InlineData(3230196298, 26957, 1766679891)]
        [InlineData(1766679891, 21523, 1410548784)]
        public void srandTest(uint seed, ushort expectedRandom, uint newSeed)
        {
            Reset();

            mbbsEmuMemoryCore.SetDWord("RANDSEED", seed);
            
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RAND_ORDINAL, new List<ushort>());

            //Verify Results
            Assert.Equal(expectedRandom, mbbsEmuCpuRegisters.AX);
            Assert.Equal(newSeed, mbbsEmuMemoryCore.GetDWord("RANDSEED"));
        }
    }
}

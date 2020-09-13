using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class digalw_Tests : ExportedModuleTestBase
    {
        private const int DIGALW_ORDINAL = 169;

        [Fact]
        public void digalw_Test()
        {
            //Reset State
            Reset();

            var actual = mbbsEmuMemoryCore.GetWord("DIGALW");

            Assert.Equal(1, actual);
        }
    }
}

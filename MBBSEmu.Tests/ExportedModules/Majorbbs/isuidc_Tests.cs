using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class isuidc_Tests : ExportedModuleTestBase
    {
        private const int ISUIDC_ORDINAL = 364;

        [Theory]
        [InlineData(31, 0)]
        [InlineData('!', 1)]
        [InlineData('A', 1)]
        [InlineData('a', 1)]
        [InlineData('Z', 1)]
        [InlineData('z', 1)]
        [InlineData('0', 1)]
        [InlineData('@', 1)]
        [InlineData('~', 1)]
        [InlineData(127, 0)]
        public void isuidc_Test(byte input, ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ISUIDC_ORDINAL, new List<ushort> {input});

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}

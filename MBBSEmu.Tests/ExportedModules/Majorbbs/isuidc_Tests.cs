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
        [InlineData('~', 1)]
        [InlineData(127, 0)]
        public void isuidc_Test(byte input, ushort expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In


            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ISUIDC_ORDINAL, new List<ushort> {input});

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}

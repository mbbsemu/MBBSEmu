using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int ISSUPC_ORDINAL = 362;

        [Theory]
        [InlineData(31, 0)]
        [InlineData('!', 0)]
        [InlineData('A', 1)]
        [InlineData('a', 1)]
        [InlineData('Z', 1)]
        [InlineData('z', 1)]
        [InlineData('0', 1)]
        [InlineData('@', 0)]
        [InlineData('~', 1)]
        [InlineData(127, 0)]
        public void issupc_Test(byte input, ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ISSUPC_ORDINAL, new List<ushort> { input });

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}

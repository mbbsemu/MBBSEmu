using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class l2as_Tests : ExportedModuleTestBase
    {
        private const int L2AS_ORDINAL = 377;

        [Theory]
        [InlineData(65535, "65535")]
        [InlineData(0, "0")]
        [InlineData(-50, "-50")]
        [InlineData(-2147418111, "-2147418111")]
        [InlineData(-31337, "-31337")]
        [InlineData(31337, "31337")]
        [InlineData(2147418111, "2147418111")]
        [InlineData(32767, "32767")]
        [InlineData(2147418112, "2147418112")]
        public void L2ASTest(long inputValue, string expectedValue)
        {
            //Reset State
            Reset();

            var inputHigh = (ushort) (inputValue >> 16);
            var inputLow = (ushort) inputValue;

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, L2AS_ORDINAL, new List<ushort> { inputLow, inputHigh });

            //Verify Results
            var resultString = mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true);
            Assert.Equal(expectedValue, Encoding.ASCII.GetString(resultString));
        }
    }
}

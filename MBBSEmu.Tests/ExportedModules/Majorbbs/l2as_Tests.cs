using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class l2as_Tests : ExportedModuleTestBase
    {
        private const int L2AS_ORDINAL = 377;

        [Theory]
        [InlineData(0xFFFF, 0x0, "65535")]
        [InlineData(0x0, 0x0, "0")]
        [InlineData(0x7A69, 0x0, "31337")]
        [InlineData(0xFFFF, 0x7FFE, "2147418111")]
        [InlineData(0x7FFF, 0x0, "32767")]
        [InlineData(0x0, 0x7FFF, "2147418112")]
        public void L2ASTest(ushort inputLow, ushort inputHigh, string expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, L2AS_ORDINAL, new List<ushort> { inputLow, inputHigh });

            //Verify Results
            var resultString = mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true);
            Assert.Equal(expectedValue, Encoding.ASCII.GetString(resultString));
        }
    }
}
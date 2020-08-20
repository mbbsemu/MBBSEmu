using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class ul2as_Tests : MajorbbsTestBase
    {
        private const int UL2AS_ORDINAL = 1040;

        [Theory]
        [InlineData(0xFFFF, 0xFFFF, "4294967295")]
        [InlineData(0x0, 0xFFFF, "4294901760")]
        [InlineData(0xFFFF, 0x0, "65535")]
        public void UL2ASTest(ushort inputLow, ushort inputHigh, string expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(UL2AS_ORDINAL, new List<ushort> { inputLow, inputHigh });

            //Verify Results
            var resultString = mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true);
            Assert.Equal(expectedValue, Encoding.ASCII.GetString(resultString));
        }
    }
}

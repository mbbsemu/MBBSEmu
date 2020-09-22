using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class ncdate_Tests : ExportedModuleTestBase
    {
        private const int NCDATE_ORDINAL = 428;

        [Theory]
        [InlineData(20785, "09/17/20")]
        [InlineData(5535, "12/31/90")]
        [InlineData(33, "01/01/80")]
        [InlineData(0, "00/00/80")]
        [InlineData(ushort.MaxValue, "15/31/7")]
        public void NCDATE_Test(ushort inputValue, string expectedString)
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCDATE_ORDINAL, new List<ushort> { inputValue });

            //Verify Results
            var actualString = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true));

            Assert.Equal(expectedString, actualString);

        }
    }
}

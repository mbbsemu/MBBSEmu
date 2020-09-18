using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class dcdate_Tests : ExportedModuleTestBase
    {
        private const int DCDATE_ORDINAL = 157;

        [Theory]
        [InlineData("09/17/20", 20785)]
        [InlineData("12/31/90", 5535)]
        [InlineData("1/1/80", 33)]
        public void DCDATE_Test(string inputString, ushort expectedValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DCDATE_ORDINAL, new List<IntPtr16> { string1Pointer });

            //Verify Results

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);

        }
    }
}

using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strlwr_Tests : ExportedModuleTestBase
    {
        private const int STRLWR_ORDINAL = 579;

        [Theory]
        [InlineData("TEST", "test")]
        [InlineData("TEST1", "test1")]
        [InlineData("TEST test TEST", "test test test")]
        [InlineData("TeSt", "test")]
        public void STRLWE_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRLWR_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(stringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(stringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}

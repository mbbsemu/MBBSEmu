using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strupr_Tests : ExportedModuleTestBase
    {
        private const int STRUPR_ORDINAL = 587;

        [Theory]
        [InlineData("TEST", "TEST")]
        [InlineData("TEST$*", "TEST$*")]
        [InlineData("TEST test TEST", "TEST TEST TEST")]
        [InlineData("TeSt3", "TEST3")]
        public void STRUPR_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRUPR_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(stringPointer, true)));
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(new IntPtr16(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX), true)));
        }
    }
}

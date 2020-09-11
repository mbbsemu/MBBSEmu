using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cncnum_Tests : ExportedModuleTestBase
    {
        private const int CNCNUM_ORDINAL = 127;

        [Theory]
        [InlineData("100\0", 0, "100", 4)]
        [InlineData("-100\0", 0, "-100", 5)]
        [InlineData("-100abc000\0", 0, "-100", 5)]
        [InlineData("1234567890123\0", 0, "123456789012", 13)] //test truncation
        [InlineData("abc123\0", 0, "", 0)]
        [InlineData("abc123\0", 3, "123", 7)]
        [InlineData("abc123\0", 2, "", 2)]
        [InlineData("abc 123\0", 3, "123", 8)]
        [InlineData("abc -123\0", 3, "-123", 9)]
        [InlineData("\0", 0, "", 0)]
        public void cncnum_Test(string inputString, ushort nxtcmdStartingOffset, string expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            var inputLength = (ushort)inputString.Length;

            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString));
            mbbsModule.Memory.SetWord("INPLEN", inputLength);

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCNUM_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuCore.Registers.DX, mbbsEmuCpuCore.Registers.AX, true)));
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

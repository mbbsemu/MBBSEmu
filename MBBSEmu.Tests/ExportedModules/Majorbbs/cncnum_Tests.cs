using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int CNCNUM_ORDINAL = 127;

        [Theory]
        [InlineData("100", 0, "100", 3)]
        [InlineData("-100", 0, "-100", 4)]
        [InlineData("-100abc000", 0, "-100", 4)]
        [InlineData("1234567890123", 0, "12345678901", 11)] //test truncation
        [InlineData("abc123", 0, "", 0)]
        [InlineData("abc123", 3, "123", 6)]
        [InlineData("abc123", 2, "", 2)]
        [InlineData("abc 123", 3, "123", 7)]
        [InlineData("abc -123", 3, "-123", 8)]
        [InlineData("", 0, "", 0)]
        public void cncnum_Test(string inputString, ushort nxtcmdStartingOffset, string expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            var inputLength = (ushort)inputString.Length;

            var input = mbbsModule.Memory.GetVariablePointer("INPUT");

            mbbsModule.Memory.SetArray(input, Encoding.ASCII.GetBytes(inputString));
            mbbsModule.Memory.SetByte(input + inputString.Length, 0);
            mbbsModule.Memory.SetWord("INPLEN", inputLength);

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            var expectedNxtCmd = currentNxtcmd + expectedNxtcmdOffset;
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCNUM_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.Equal(expectedResult, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuCore.Registers.DX, mbbsEmuCpuCore.Registers.AX, true)));
            Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

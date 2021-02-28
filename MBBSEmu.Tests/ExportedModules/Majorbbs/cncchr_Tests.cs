using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int CNCCHR_ORDINAL = 122;

        [Theory]
        [InlineData(" 123\0", 0, '1', 2)]
        [InlineData("123\0", 1, '2', 2)]
        [InlineData("1 2\0", 0, '1', 2)]
        [InlineData("2 1\0", 2, '1', 3)]
        [InlineData("123 456\0", 4, '4', 5)]
        [InlineData("123    456\0", 3, '4', 8)]
        [InlineData("123\0", 3, '\0', 3)]
        [InlineData("\0", 0, '\0', 0)]
        [InlineData("123\0", 4, '\0', 4)]
        public void cncchr_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            var inputLength = (ushort) inputString.Length;

            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString));
            mbbsModule.Memory.SetWord("INPLEN", inputLength);

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCCHR_ORDINAL, new List<FarPtr>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

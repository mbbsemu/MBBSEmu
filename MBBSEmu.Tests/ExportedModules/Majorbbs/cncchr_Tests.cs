using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cncchr_Tests : MajorbbsTestBase
    {
        private const int CNCCHR_ORDINAL = 122;

        [Theory]

        //Simple commands with nxtcmd == input
        [InlineData("123", 1, '2', 2)]
        [InlineData("1 2", 0, '1', 2)]
        [InlineData("2 1", 2, '1', 3)]
        [InlineData("123 456", 4, '4', 5)]
        [InlineData("\0", 0, '\0', 0)]
        public void cncchr_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            var inputLength = (ushort) inputString.Length;

            //parsin() does this check
            if (inputLength == 1 && inputString[0] == '\0')
                inputLength = 0;

            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString.Replace(' ', '\0')));
            mbbsModule.Memory.SetWord("INPLEN", );

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(CNCCHR_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

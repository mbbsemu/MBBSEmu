using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{

    public class cncuid_Tests : ExportedModuleTestBase
    {
        private const int CNCUID_ORDINAL = 129;

        [Theory]
        [InlineData("SYSOP\0", 0, "SYSOP", 5)]
        [InlineData("GUEST\0", 0, "GUEST", 5)]
        [InlineData("MULTIPLE WORDS\0", 0, "MULTIPLE WORDS", 14)]
        [InlineData("INVALID\0STRING", 0, "INVALID", 7)]
        [InlineData("INVALID\0STRING", 8, "STRING", 14)]
        [InlineData("INVALID\x01STRING", 0, "INVALID", 7)]
        public void cncuid_Test(string inputString, ushort nxtcmdStartingOffset, string expectedResult, ushort expectedNxtcmdOffset)
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCUID_ORDINAL, new List<FarPtr>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuCore.Registers.DX, mbbsEmuCpuCore.Registers.AX, true)));
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }

}
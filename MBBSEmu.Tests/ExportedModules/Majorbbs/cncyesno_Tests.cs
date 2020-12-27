using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cncyesno_Tests : ExportedModuleTestBase
    {

        private const int CNCYESNO_ORDINAL = 131;

        [Theory]

        //Simple commands with nxtcmd == input
        [InlineData("YES", 0, 'Y', 4)]
        [InlineData("Y", 0, 'Y', 2)]
        [InlineData("Y TEST", 0, 'Y', 2)]
        [InlineData("YES THIS IS A TEST", 0, 'Y', 4)]
        [InlineData("TEST YES", 0, 'T', 0)]
        [InlineData("TEST", 0, 'T', 0)]
        [InlineData("NO", 0, 'N', 3)]
        [InlineData("N", 0, 'N', 2)]
        [InlineData("N TEST", 0, 'N', 2)]
        [InlineData("NO THIS IS A TEST", 0, 'N', 3)]
        [InlineData("TEST NO", 0, 'T', 0)]

        //Incremented nxtcmd Tests
        [InlineData("TEST NO", 5, 'N', 3)]
        [InlineData("TEST YES", 5, 'Y', 4)]
        [InlineData("TEST TEST", 5, 'T', 5)]
        public void CNCYESNO_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString.Replace(' ', '\0')));
            mbbsModule.Memory.SetWord("INPLEN", (ushort)inputString.Length);

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCYESNO_ORDINAL, new List<FarPtr>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

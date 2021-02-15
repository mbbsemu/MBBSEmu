using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cncsig_Tests : ExportedModuleTestBase
    {
        private const int CNCSIG_ORDINAL = 128;

        [Theory]
        [InlineData("FIRST SECOND\0", 0, "/FIRST", 6, "SECOND")]
        [InlineData("FIRST SECOND\0", 6, "/SECOND", 13, "")]
        [InlineData("FIRST SECOND THIRD\0", 1, "/IRST", 6, "SECOND THIRD")]
        [InlineData("FIRST SECOND THIRD\0", 13, "/THIRD", 19, "")]
        [InlineData(" /MyUser\0", 0, "/MyUser", 9, "")]
        [InlineData("/MyUser\0", 0, "/MyUser", 8, "")]
        [InlineData("/MyUserNameIsTooLong\0", 0, "/MyUserNa", 9, "meIsTooLong")]
        [InlineData("123456789012345678901234567890\0", 0, "/12345678", 8, "9012345678901234567890")]
        [InlineData("\0", 0, "", 0, "")]
        [InlineData("OneSixSix                Hello              \0", 8, "/x", 25, "Hello              ")]
        public void cncsig_Test(string inputString, ushort nxtcmdStartingOffset, string expectedResult, ushort expectedNxtcmdOffset, string expectedNxtcmdRemaining)
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCSIG_ORDINAL, new List<FarPtr>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");

            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedNxtcmdRemaining, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuMemoryCore.GetPointer("NXTCMD"),true )));
            Assert.Equal(expectedResult, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuCore.Registers.DX, mbbsEmuCpuCore.Registers.AX, true)));
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

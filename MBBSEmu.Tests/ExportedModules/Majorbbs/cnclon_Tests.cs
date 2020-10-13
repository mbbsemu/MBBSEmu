using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cnclon_Tests : ExportedModuleTestBase
    {
        private const int CNCLON_ORDINAL = 126;
        private const int CNCINT_ORDINAL = 125;

        [Theory]
        [InlineData("100", 0, 100, 3)]
        [InlineData("-100", 0, -100, 4)]
        [InlineData("-100abc000", 0, -100, 4)]
        [InlineData("-89755abc000", 0, -89755, 6)]
        [InlineData("89755abc000", 0, 89755, 5)]
        [InlineData("1234567890123", 0, 0, 11)] //test truncation
        [InlineData("abc123", 0, 0, 0)]
        [InlineData("abc123", 3, 123, 6)]
        [InlineData("abc123", 2, 0, 2)]
        [InlineData("abc 123", 3, 123, 7)]
        [InlineData("abc -123", 3, -123, 8)]
        [InlineData("", 0, 0, 0)]
        public void cnclon_Test(string inputString, ushort nxtcmdStartingOffset, int expectedResult, ushort expectedNxtcmdOffset)
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCLON_ORDINAL, new List<IntPtr16>());

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.GetLong());
            Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }

        [Theory]
        [InlineData("100", 0, 100, 3)]
        [InlineData("-100", 0, -100, 4)]
        [InlineData("-100abc000", 0, -100, 4)]
        [InlineData("-89755abc000", 0, -24219, 6)]
        [InlineData("89755abc000", 0, 24219, 5)]
        [InlineData("1234567890123", 0, 0, 11)] //test truncation
        [InlineData("abc123", 0, 0, 0)]
        [InlineData("abc123", 3, 123, 6)]
        [InlineData("abc123", 2, 0, 2)]
        [InlineData("abc 123", 3, 123, 7)]
        [InlineData("abc -123", 3, -123, 8)]
        [InlineData("", 0, 0, 0)]
        public void cncint_Test(string inputString, ushort nxtcmdStartingOffset, short expectedResult, ushort expectedNxtcmdOffset)
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCINT_ORDINAL, new List<IntPtr16>());

            //Verify Results
            Assert.Equal(expectedResult, (short) mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}

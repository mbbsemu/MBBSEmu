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
        [InlineData("-100", 0, 0, 0)] //no sign: nothing is read
        [InlineData("-100abc000", 0, 0, 0)]
        [InlineData("-89755abc000", 0, 0, 0)]
        [InlineData("89755abc000", 0, 89755, 5)]
        [InlineData("1234567890123", 0, 1912276171, 13)] //every digit is read, the value wraps modulo 2^32
        [InlineData("abc123", 0, 0, 0)]
        [InlineData("abc123", 3, 123, 6)]
        [InlineData("abc123", 2, 0, 2)]
        [InlineData("abc 123", 3, 123, 7)]
        [InlineData("abc -123", 3, 0, 3)]
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCLON_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.GetLong());
            Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }

        [Theory]
        [InlineData("100", 0, 100, 3)]
        [InlineData("-100", 0, 0, 0)] //no sign: nothing is read
        [InlineData("-100abc000", 0, 0, 0)]
        [InlineData("-89755abc000", 0, 0, 0)]
        [InlineData("89755abc000", 0, 24219, 5)]
        [InlineData("1234567890123", 0, 1227, 13)] //low 16 bits of the wrapped 32-bit value
        [InlineData("abc123", 0, 0, 0)]
        [InlineData("abc123", 3, 123, 6)]
        [InlineData("abc123", 2, 0, 2)]
        [InlineData("abc 123", 3, 123, 7)]
        [InlineData("abc -123", 3, 0, 3)]
        [InlineData("", 0, 0, 0)]
        [InlineData(" ", 0, 0, 0)]
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNCINT_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.Equal(expectedResult, (short)mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }

        /// <summary>
        ///     Expected values are those of the original kernels' CNCLON and CNCINT, executed with NXTCMD at the start
        ///     of the same input (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical results). The inputs
        ///     start without white space, which this emulation skips before the number (the kernel leaves it to morcnc()).
        /// </summary>
        [Theory]
        [InlineData("+5", 0x00000000u, 0)]
        [InlineData("-", 0x00000000u, 0)]
        [InlineData("--5", 0x00000000u, 0)]
        [InlineData("-12345678901234", 0x00000000u, 0)]
        [InlineData("2147483647", 0x7FFFFFFFu, 10)]
        [InlineData("2147483648", 0x80000000u, 10)]
        [InlineData("4294967295", 0xFFFFFFFFu, 10)]
        [InlineData("4294967296", 0x00000000u, 10)]
        [InlineData("12345678901", 0xDFDC1C35u, 11)]
        [InlineData("123456789012", 0xBE991A14u, 12)]
        [InlineData("1234567890123", 0x71FB04CBu, 13)]
        [InlineData("99999", 0x0001869Fu, 5)]
        [InlineData("65535", 0x0000FFFFu, 5)]
        [InlineData("65536", 0x00010000u, 5)]
        [InlineData("32768", 0x00008000u, 5)]
        [InlineData("12 34", 0x0000000Cu, 2)]
        [InlineData("007", 0x00000007u, 3)]
        [InlineData("5x", 0x00000005u, 1)]
        public void cnclon_cncint_MatchKernel(string inputString, uint expectedLong, ushort expectedNxtcmdOffset)
        {
            foreach (var ordinal in new[] { CNCLON_ORDINAL, CNCINT_ORDINAL })
            {
                Reset();

                var input = mbbsModule.Memory.GetVariablePointer("INPUT");
                mbbsModule.Memory.SetArray(input, Encoding.ASCII.GetBytes(inputString));
                mbbsModule.Memory.SetByte(input + inputString.Length, 0);
                mbbsModule.Memory.SetWord("INPLEN", (ushort)inputString.Length);

                var startNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
                var expectedNxtCmd = startNxtcmd + expectedNxtcmdOffset;

                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal, new List<FarPtr>());

                Assert.Equal((ushort)(expectedLong & 0xFFFF), mbbsEmuCpuCore.Registers.AX);
                if (ordinal == CNCLON_ORDINAL)
                    Assert.Equal((ushort)(expectedLong >> 16), mbbsEmuCpuCore.Registers.DX);
                Assert.Equal(expectedNxtCmd, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
            }
        }
    }
}

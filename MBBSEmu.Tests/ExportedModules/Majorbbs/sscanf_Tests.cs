using System.Collections.Generic;
using System.Text;
using System;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sscanf_Tests : ExportedModuleTestBase
    {
        private const int SSCANF_ORDINAL = 562;

        [Theory]
        [InlineData("1203", 1203, 1)]
        [InlineData("+1203", 1203, 1)]
        [InlineData("-1203", -1203, 1)]
        [InlineData(" \t1203\t ", 1203, 1)]
        [InlineData(" \t+1203\t ", 1203, 1)]
        [InlineData(" \t-1203\t ", -1203, 1)]
        [InlineData("", 0, 0)]
        [InlineData("a1203", 0, 0)]
        public void sscanf_basicInteger_Test(string inputString, short expectedInteger, int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(" %d "));

            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, intPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }

        [Theory]
        [InlineData("%s %d %s", "test +45 another", "test", 45, "another", 3)]
        [InlineData("%s %d %s", "yohoho -1.2 another", "yohoho", -1, ".2", 3)]
        [InlineData("%s %d%s", "yohoha +3456another", "yohoha", 3456, "another", 3)]
        [InlineData("%s %d %s", "yohoho -1", "yohoho", -1, "", 2)]
        [InlineData("%s %d %s", "yohoho", "yohoho", 0, "", 1)]
        [InlineData("%s %d %s", "", "", 0, "", 0)]
        // double percent & ignored format
        [InlineData("%s %d %% ignored %s", "yohoha +3456 % ignored another", "yohoha", 3456, "another", 3)]
        // double percent & mismatching ignored format
        [InlineData("%s %d %% ignored %s", "yohoha +3456 % Ignored another", "yohoha", 3456, "", 2)]
        // unfinished percent at end of string
        [InlineData("%s %d%", "yohoha +3456another", "yohoha", 3456, "", 2)]
        public void sscanf_fancy_Test(
            string formatString,
            string inputString,
            string expectedFirstString,
            short expectedInteger,
            string expectedSecondString,
            int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(formatString));

            var firstStringPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var secondStringPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, firstStringPointer, intPointer, secondStringPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedFirstString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(firstStringPointer, true)));
            Assert.Equal(expectedSecondString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(secondStringPointer, true)));
            Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }

        [Fact]
        public void invalid_formatString_throws()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", 32);
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes("abc4567"));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("abc45%45q"));

            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test & Assert
            Assert.Throws<ArgumentException>(() =>
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, intPointer }));
        }
    }
}

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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, intPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }

        [Theory]
        [InlineData("120348343", 120348343, 1)]
        [InlineData("+120348343", 120348343, 1)]
        [InlineData("-120348343", -120348343, 1)]
        [InlineData(" \t120348343\t ", 120348343, 1)]
        [InlineData(" \t+120348343\t ", 120348343, 1)]
        [InlineData(" \t-120348343\t ", -120348343, 1)]
        [InlineData("", 0, 0)]
        [InlineData("a120348343", 0, 0)]
        public void sscanf_longInteger_Test(string inputString, int expectedInteger, int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("%ld"));

            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 4);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, intPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);

            int value = mbbsEmuMemoryCore.GetWord(intPointer) | (mbbsEmuMemoryCore.GetWord(intPointer + 2) << 16);
            Assert.Equal(expectedInteger, value);
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
        [InlineData("%4s%d%s","yoho-3456this is fun", "yoho", -3456, "this", 3)]
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
            mbbsEmuMemoryCore.FillArray(firstStringPointer, 129, (byte)'a');
            var secondStringPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            mbbsEmuMemoryCore.FillArray(secondStringPointer, 129, (byte)'a');
            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, firstStringPointer, intPointer, secondStringPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            if (expectedResult > 0)
                Assert.Equal(expectedFirstString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(firstStringPointer, true)));
            if (expectedResult > 1)
                Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
            if (expectedResult > 2)
                Assert.Equal(expectedSecondString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(secondStringPointer, true)));
        }

        [Theory]
        [InlineData("%2c/%2c/%4c", "12/31/1999", "12", "31", "1999", 3)]
        [InlineData("%2c%2c%4c", "12/31/1999", "12", "/3", "1/19", 3)]
        [InlineData("%2c%2c%4c", "12311999", "12", "31", "1999", 3)]
        [InlineData("%c%2c%4c", "2311999", "2", "31", "1999", 3)]
        public void sscanf_dateparse_character_Test(
            string formatString,
            string inputString,
            string expectedMonth,
            string expectedDay,
            string expectedYear,
            int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(formatString));

            var monthPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var dayPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var yearPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, monthPointer, dayPointer, yearPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedMonth, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(monthPointer, true)));
            Assert.Equal(expectedDay, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(dayPointer, true)));
            Assert.Equal(expectedYear, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(yearPointer, true)));
        }

        [Theory]
        [InlineData("%2c%2c%4c", "12/31/1999", "12aa", "/3aa", "1/19")]
        [InlineData("%c%1c%2c", "12311999", "1aaa", "2aaa", "31aa")]
        public void sscanf_character_doesntNullTerminate_Test(
            string formatString,
            string inputString,
            string expected1,
            string expected2,
            string expected3)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(formatString));

            // allocates 3 contiguous blocks of memory of size 5. Fills them each with the string "aaaa\0"
            var ptr1 = mbbsEmuMemoryCore.Malloc(5);
            mbbsEmuMemoryCore.FillArray(ptr1, 4, (byte) 'a');
            var ptr2 = mbbsEmuMemoryCore.Malloc(5);
            mbbsEmuMemoryCore.FillArray(ptr2, 4, (byte) 'a');
            var ptr3 = mbbsEmuMemoryCore.Malloc(5);
            mbbsEmuMemoryCore.FillArray(ptr3, 4, (byte) 'a');

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, ptr1, ptr2, ptr3 });

            //Verify Results
            Assert.Equal(3, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected1, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(ptr1, true)));
            Assert.Equal(expected2, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(ptr2, true)));
            Assert.Equal(expected3, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(ptr3, true)));
        }

        [Theory]
        [InlineData("%2d/%2d/%4d", "12/31/1999", 12, 31, 1999, 3)]
        [InlineData("%2d%2d%4d", "12/31/1999", 12, 0, 0, 1)]
        public void sscanf_dateparse_integer_Test(
            string formatString,
            string inputString,
            int expectedMonth,
            int expectedDay,
            int expectedYear,
            int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(formatString));

            var monthPointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);
            var dayPointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);
            var yearPointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, monthPointer, dayPointer, yearPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)expectedMonth, mbbsEmuMemoryCore.GetWord(monthPointer));
            Assert.Equal((ushort)expectedDay, mbbsEmuMemoryCore.GetWord(dayPointer));
            Assert.Equal((ushort)expectedYear, mbbsEmuMemoryCore.GetWord(yearPointer));
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
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, intPointer }));
        }
    }
}

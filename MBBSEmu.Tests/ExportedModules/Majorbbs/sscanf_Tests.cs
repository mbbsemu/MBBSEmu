using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Text;
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
        [InlineData("", 0, -1)] //no input: EOF
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
            Assert.Equal(expectedResult, (short)mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }

        [Theory]
        [InlineData("120348343", 120348343, 1)]
        [InlineData("+120348343", 120348343, 1)]
        [InlineData("-120348343", -120348343, 1)]
        [InlineData(" \t120348343\t ", 120348343, 1)]
        [InlineData(" \t+120348343\t ", 120348343, 1)]
        [InlineData(" \t-120348343\t ", -120348343, 1)]
        [InlineData("", 0, -1)] //no input: EOF
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
            Assert.Equal(expectedResult, (short)mbbsEmuCpuRegisters.AX);

            int value = mbbsEmuMemoryCore.GetWord(intPointer) | (mbbsEmuMemoryCore.GetWord(intPointer + 2) << 16);
            Assert.Equal(expectedInteger, value);
        }

        [Theory]
        [InlineData("24675841", 610752577, 1)]
        [InlineData("12345678", 305419896, 1)]
        [InlineData("1FFFF", 131071, 1)]
        public void sscanf_longInteger_hexConversion_Test(string inputString, int expectedInteger, int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("%lx"));

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
        [InlineData("%s %d %s", "", "", 0, "", -1)] //no input: EOF
        // double percent & ignored format
        [InlineData("%s %d %% ignored %s", "yohoha +3456 % ignored another", "yohoha", 3456, "another", 3)]
        // double percent & mismatching ignored format
        [InlineData("%s %d %% ignored %s", "yohoha +3456 % Ignored another", "yohoha", 3456, "", 2)]
        // unfinished percent at end of string
        [InlineData("%s %d%", "yohoha +3456another", "yohoha", 3456, "", 2)]
        [InlineData("%4s%d%s", "yoho-3456this is fun", "yoho", -3456, "this", 3)]
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
            Assert.Equal(expectedResult, (short)mbbsEmuCpuRegisters.AX);
            if (expectedResult > 0)
                Assert.Equal(expectedFirstString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(firstStringPointer, true)));
            if (expectedResult > 1)
                Assert.Equal((ushort)expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
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
            mbbsEmuMemoryCore.FillArray(ptr1, 4, (byte)'a');
            var ptr2 = mbbsEmuMemoryCore.Malloc(5);
            mbbsEmuMemoryCore.FillArray(ptr2, 4, (byte)'a');
            var ptr3 = mbbsEmuMemoryCore.Malloc(5);
            mbbsEmuMemoryCore.FillArray(ptr3, 4, (byte)'a');

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
        public void sscanf_PreservesNonAsciiBytesInStringConversion()
        {
            //Reset State
            Reset();

            // 0xAD is an invalid single-byte UTF-8 sequence and gets replaced with '?'
            // if decoded via Encoding.ASCII/UTF8 -- %s must copy it through untouched.
            var inputBytes = new byte[] { (byte)'a', 0xAD, (byte)'b', 0x0 };
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)inputBytes.Length);
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", inputBytes);

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 4);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("%s"));

            var resultPointer = mbbsEmuMemoryCore.AllocateVariable(null, 8);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, resultPointer });

            //Verify Results
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal(new byte[] { (byte)'a', 0xAD, (byte)'b' }, mbbsEmuMemoryCore.GetArray(resultPointer, 3).ToArray());
        }

        [Fact]
        public void sscanf_PreservesNonAsciiBytesInCharacterConversion()
        {
            //Reset State
            Reset();

            var inputBytes = new byte[] { 0xAD, 0x0 };
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)inputBytes.Length);
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", inputBytes);

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 4);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("%c"));

            var resultPointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<FarPtr> { stringPointer, formatPointer, resultPointer });

            //Verify Results
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0xAD, mbbsEmuMemoryCore.GetByte(resultPointer));
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

        /// <summary>
        ///     Expected values are those of the original kernels' SSCANF, executed on the same input and format with
        ///     three 16-byte outputs filled with 0xEE beforehand (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give
        ///     identical results): the return value, and every byte of each output afterwards.
        /// </summary>
        [Theory]
        [InlineData("123", "%d", 1, "7B00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-5", "%d", 1, "FBFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("+7", "%d", 1, "0700EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("70000", "%d", 1, "7011EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4000000000", "%d", 1, "0028EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("99999999999", "%d", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("123", "%ld", 1, "7B000000EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-5", "%ld", 1, "FBFFFFFFEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("2147483648", "%ld", 1, "00000080EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4000000000", "%ld", 1, "00286BEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("99999999999", "%ld", 1, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("65535", "%u", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-1", "%u", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("70000", "%u", 1, "7011EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4294967295", "%lu", 1, "FFFFFFFFEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-1", "%lu", 1, "FFFFFFFFEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ffff", "%x", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("FFFFFFFF", "%lx", 1, "FFFFFFFFEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x1A", "%x", 1, "1A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1A", "%x", 1, "1A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("777", "%o", 1, "FF01EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x1A", "%i", 1, "1A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("012", "%i", 1, "0A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%i", 1, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-0x10", "%i", 1, "F0FFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12345", "%3d", 1, "7B00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12345", "%3d%d", 2, "7B00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "2D00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("  42", "%d", 1, "2A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("\t\n42", "%d", 1, "2A00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", "%d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("   ", "%d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 2", "%d %d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1,2", "%d,%d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 ,2", "%d,%d", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12 x", "%d%c", 2, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "20EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12 x", "%d %c", 2, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("hello world", "%s", 1, "68656C6C6F00EEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("hello world", "%5s", 1, "68656C6C6F00EEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("hello", "%c", 1, "68EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData(" hello", "%c", 1, "20EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%2c", 1, "6162EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 2", "%*d %d", 1, "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("x5", "x%d", 1, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("y5", "x%d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("5", "%hd", 1, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1.5", "%d", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12abc", "%d%s", 2, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-", "%d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("- 5", "%d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("+", "%d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-99999999999", "%ld", 1, "00000080EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("99999999999", "%lu", 1, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-99999999999", "%lu", 1, "00000080EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("123456789ABC", "%lx", 1, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-4000000000", "%ld", 1, "00D89411EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4294967296", "%ld", 1, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("99999999999x", "%ld%c", 2, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "39EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("123456789012345x", "%ld%c", 2, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "32EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("70000", "%hd", 1, "7011EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-123", "%2d", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("+123", "%2d", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x1A", "%2i", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x1A", "%3i", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x", "%i", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0xg", "%x%c", 2, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "67EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("08", "%i%c", 2, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "38EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1A", "%d%c", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "41EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("777", "%lo", 1, "FF010000EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("7778", "%o%c", 2, "FF01EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "38EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc,def", "%[a-z]%c", 2, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "2CEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc,def", "%[^,],%s", 2, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "64656600EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("  abc", "%[a-z]", 0, "00EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%*[a-z]%n", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12345", "%3d%n", 1, "7B00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0300EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 ", "%d %d", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", " x", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData(" ", "%s", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", "%c", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1", "%*d%d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%*c%c", 1, "62EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("hello world", "%*s %s", 1, "776F726C6400EEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("5", "%5c", -1, "35EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%n", 0, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("x", "%d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("xyz", "x%n", 0, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%1d%1d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1-2", "%d%d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "FEFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-0", "%d", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x10", "%x", 1, "1000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-ff", "%x", 1, "01FFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("+0x10", "%x", 1, "1000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4294967295", "%d", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("65536", "%u", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-65536", "%d", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("4294967296", "%lu", 1, "FFFFFF7FEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("99999999999", "%x", 1, "FFFFEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%c%%", 1, "31EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1%2", "%d%%%d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 % 2", "%d %% %d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%F", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%Fd", 1, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%d%n", 1, "0C00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", "%n", 0, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 %2", "%d%%%d", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1% 2", "%d%% %d", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", "%[a-z]", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%[a-z]", 1, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-x", "%d%c", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("--5", "%d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x", "%x", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 5", "%d %5c", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "35EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1 5", "%d%5c", 1, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "2035EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%[]a]", 1, "6100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("]a", "%[]a]%c", 1, "5D6100EEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("a-z", "%[a-]%c", 2, "612D00EEEEEEEEEEEEEEEEEEEEEEEEEE", "7AEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%2[a-z]%c", 2, "616200EEEEEEEEEEEEEEEEEEEEEEEEEE", "63EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData(" 5", " %d", 1, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("5 ", "%d ", 1, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("5 6", "%d%d", 2, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0600EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("5\t6", "%d%d", 2, "0500EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0600EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc def", "%s%s", 2, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "64656600EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%1s%1s", 2, "6100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "6200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%ld%n", 1, "0C000000EEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("x12", "%*c%ld", 1, "0C000000EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-", "%x", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0X1f", "%X", 1, "1F00EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-0", "%u", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1e5", "%d%c", 2, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "65EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("  ", "%c", 1, "20EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData(" x", "%c%c", 2, "20EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%*d%n", 0, "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%s%n", 1, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "0300EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%c%n", 1, "61EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("ab", "%2c%n", 1, "6162EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "0200EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%[a-z]%n", 1, "61626300EEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12 ", "%*d %n", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("12", "%*d %n", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("1", "%*d", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("abc", "%*s", 0, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("", "%*d", -1, "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("x", "x%n", 0, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("x", "%*c%n", 0, "0100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x5", "%1i%c", 2, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("0x5", "%1x%c", 2, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-0x5", "%2x%c", 2, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("-0x5", "%3x", 1, "0000EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("\u00E9\u00E9a", "%[\u00E9]", 1, "E9E900EEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("a\u00E9b", "%[^\u00E9]%c", 2, "6100EEEEEEEEEEEEEEEEEEEEEEEEEEEE", "E9EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("\u00E9x", "\u00E9%c", 1, "78EEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        [InlineData("\u00E0\u00E9\u00F0z", "%[\u00E0-\u00F0]%c", 2, "E0E9F000EEEEEEEEEEEEEEEEEEEEEEEE", "7AEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE")]
        public void sscanf_MatchesKernel(string inputString, string formatString, int expectedResult, string expectedOutput1,
            string expectedOutput2, string expectedOutput3)
        {
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.Latin1.GetBytes(inputString + "\0"));
            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.Latin1.GetBytes(formatString + "\0"));

            var outputs = new List<FarPtr>();
            for (var i = 0; i < 3; i++)
            {
                var outputPointer = mbbsEmuMemoryCore.AllocateVariable($"OUTPUT{i}", 16);
                mbbsEmuMemoryCore.FillArray(outputPointer, 16, 0xEE);
                outputs.Add(outputPointer);
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL,
                new List<FarPtr> { stringPointer, formatPointer, outputs[0], outputs[1], outputs[2] });

            Assert.Equal(expectedResult, (short)mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedOutput1, Convert.ToHexString(mbbsEmuMemoryCore.GetArray(outputs[0], 16)));
            Assert.Equal(expectedOutput2, Convert.ToHexString(mbbsEmuMemoryCore.GetArray(outputs[1], 16)));
            Assert.Equal(expectedOutput3, Convert.ToHexString(mbbsEmuMemoryCore.GetArray(outputs[2], 16)));
        }
    }
}

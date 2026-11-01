using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class fscanf_Tests : FileTestBase, IDisposable
    {
        private const int FSCANF_ORDINAL = 232;

        [Fact]
        public void sscanf_reads_records()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n"
                + "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n"
                + "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n";
            //The leading space skips the line end left by the previous record: like the kernel's scanner, a literal
            //character in the format does not skip white space in the input.
            const string FORMAT = " { %d %d %d %s %s }";

            var filePath = CreateTextFile("file.txt", FILE_CONTENTS);

            Assert.Equal(FILE_CONTENTS.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", "r");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(FORMAT.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatPointer, Encoding.ASCII.GetBytes(FORMAT));

            var intPointer1 = mbbsEmuMemoryCore.AllocateVariable("FIRST", 2);
            var intPointer2 = mbbsEmuMemoryCore.AllocateVariable("SECOND", 2);
            var intPointer3 = mbbsEmuMemoryCore.AllocateVariable("THIRD", 2);
            var strPointer1 = mbbsEmuMemoryCore.AllocateVariable("FOURTH", 32);
            var strPointer2 = mbbsEmuMemoryCore.AllocateVariable("FIFTH", 32);

            //Execute Test
            int lines = 0;
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<FarPtr> {
                    filep, formatPointer, intPointer1, intPointer2, intPointer3, strPointer1, strPointer2
                });
            Assert.Equal(5, mbbsEmuCpuRegisters.AX);

            while (mbbsEmuCpuRegisters.AX == 5)
            {
                ++lines;

                Assert.Equal(5, mbbsEmuMemoryCore.GetWord(intPointer1));
                Assert.Equal(0xFFFE, mbbsEmuMemoryCore.GetWord(intPointer2));
                Assert.Equal(4, mbbsEmuMemoryCore.GetWord(intPointer3));
                Assert.Equal("testing", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(strPointer1, stripNull: true)));
                Assert.Equal("abc", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(strPointer2, stripNull: true)));

                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<FarPtr> {
                    filep, formatPointer, intPointer1, intPointer2, intPointer3, strPointer1, strPointer2
                });
            }

            Assert.Equal(0, fclose(filep));

            Assert.Equal(6, lines);
        }

        /// <summary>
        ///     The character fscanf() reads ahead and does not use stays in the file, as with the kernel's scanner, which
        ///     pushes it back. The return values of the successive calls and the next character in the file are those
        ///     of the original kernels' FSCANF on a FILE holding the same text (The Major BBS 6.x and Worldgroup
        ///     MAJORBBS.EXE give identical results).
        /// </summary>
        [Theory]
        [InlineData("12x#", "%d", "1", 'x')]
        [InlineData("12 x#", "%d", "1", ' ')]
        [InlineData("{ 5 }\r\n{ 6 }##", "{ %d }", "1,0", '\r')]
        [InlineData("{ 5 }\r\n{ 6 }##", " { %d }", "1,1", '#')]
        [InlineData("abc def ;", "%s", "1,1", ' ')]
        [InlineData("ab#", "%c", "1,1", '#')]
        [InlineData("x5#", "%d", "0", 'x')]
        [InlineData("  -x#", "%d", "0", 'x')]
        [InlineData("0xg#", "%x", "1", 'g')]
        [InlineData("12345#", "%3d", "1,1", '#')]
        [InlineData("1,2#", "%d,%d", "2", '#')]
        [InlineData("1 ,2#", "%d,%d", "1", ' ')]
        [InlineData("abc,def#", "%[a-z]", "1", ',')]
        [InlineData("12 34#", "%d%d", "2", '#')]
        [InlineData("5 #", "%d ", "1", '#')]
        [InlineData("ab#", "%2c", "1", '#')]
        [InlineData("x#", "x", "0", '#')]
        [InlineData("y#", "x", "0", 'y')]
        [InlineData("-5#", "%0d", "0", '-')]
        [InlineData("-5#", "%1d", "0", '-')]
        [InlineData("+#", "%1d", "0", '+')]
        [InlineData("0x5#", "%1x", "1", 'x')]
        [InlineData("ab#", "%0s", "1", 'a')]
        [InlineData("ab#", "%0[a-z]", "1", 'a')]
        [InlineData("ab#", "%0c", "1", 'a')]
        [InlineData("  ab#", "%0s", "1", 'a')]
        [InlineData("12#", "%0d", "0", '1')]
        [InlineData("b#", "%[a]", "0", 'b')]
        [InlineData("bc#", "%[a]%c", "1", 'c')]
        [InlineData("bc#", "%[a]%[a]", "0,0", 'b')]
        [InlineData("ab#", "%[b]%c", "1", 'b')]
        public void fscanf_LeavesUnusedCharacterInFile(string contents, string format, string expectedResults, char expectedNext)
        {
            Reset();

            //Binary mode, so fgetc() returns the next byte as it is (the kernel run read a raw buffer)
            CreateTextFile("file.txt", contents);
            var filep = fopen("file.txt", "rb");

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(format.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatPointer, Encoding.ASCII.GetBytes(format + "\0"));
            var outputs = new List<FarPtr> { filep, formatPointer };
            for (var i = 0; i < 3; i++)
                outputs.Add(mbbsEmuMemoryCore.AllocateVariable(null, 16));

            foreach (var expectedResult in expectedResults.Split(','))
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, outputs);
                Assert.Equal(int.Parse(expectedResult), (short)mbbsEmuCpuRegisters.AX);
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FGETC_ORDINAL, new List<FarPtr> { filep });
            Assert.Equal((ushort)expectedNext, mbbsEmuCpuRegisters.AX);

            Assert.Equal(0, fclose(filep));
        }

        /// <summary>
        ///     The kernel's scanner counts the characters it reads in 16 bits: %n stores the count and %ln stores it with
        ///     a zero high word, so after 65,536 characters the count starts again from 0. Measured on the original
        ///     kernels' SSCANF, which shares the scanner (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical
        ///     results): 40,000 spaces then "1x" store 40,001 (0x9C41) with a zero high word, and a count of 98,303
        ///     stores 32,767.
        /// </summary>
        [Theory]
        [InlineData(40000, 0x9C41)]
        [InlineData(65536, 1)]
        [InlineData(70000, 4465)]
        public void fscanf_CharacterCountIsSixteenBits(int spaces, int expectedCount)
        {
            Reset();

            CreateTextFile("file.txt", new string(' ', spaces) + "1x");
            var filep = fopen("file.txt", "rb");

            const string FORMAT = "%d%n%ln";
            var formatPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(FORMAT.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatPointer, Encoding.ASCII.GetBytes(FORMAT + "\0"));
            var valuePointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);
            var countPointer = mbbsEmuMemoryCore.AllocateVariable(null, 2);
            var longCountPointer = mbbsEmuMemoryCore.AllocateVariable(null, 4);
            mbbsEmuMemoryCore.SetArray(longCountPointer, new byte[] { 0xEE, 0xEE, 0xEE, 0xEE });

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL,
                new List<FarPtr> { filep, formatPointer, valuePointer, countPointer, longCountPointer });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal(1, mbbsEmuMemoryCore.GetWord(valuePointer));
            Assert.Equal(expectedCount, mbbsEmuMemoryCore.GetWord(countPointer));
            Assert.Equal((uint)expectedCount, mbbsEmuMemoryCore.GetDWord(longCountPointer));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fscanf_PreservesNonAsciiBytesInStringConversion()
        {
            //Reset State
            Reset();

            // 0xAD is an invalid single-byte UTF-8 sequence and gets replaced with '?'
            // if decoded via Encoding.ASCII/UTF8 -- %s must copy it through untouched.
            var fileBytes = new byte[] { (byte)'a', 0xAD, (byte)'b' };
            var filePath = Path.Join(mbbsModule.ModulePath, "nonascii.txt");
            File.WriteAllBytes(filePath, fileBytes);

            var filep = fopen("nonascii.txt", "r");
            Assert.NotEqual(0, filep.Segment);

            const string FORMAT = "%s";
            var formatPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(FORMAT.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatPointer, Encoding.ASCII.GetBytes(FORMAT));

            var resultPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 8);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<FarPtr> { filep, formatPointer, resultPointer });

            //Verify Results
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal(fileBytes, mbbsEmuMemoryCore.GetArray(resultPointer, (ushort)fileBytes.Length).ToArray());

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fscanf_invalid_filestream_Test()
        {
            //Reset State
            Reset();

            //Execute Test -- pass in invalid pointer
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<ushort> { 0, 0 });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}

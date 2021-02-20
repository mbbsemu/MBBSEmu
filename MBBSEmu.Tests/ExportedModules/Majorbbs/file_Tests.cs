using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class file_Tests : FileTestBase, IDisposable
    {
        private const string LOREM_IPSUM = "Lorem ipsum dolor sit amet, consectetur adipiscing elit,"
            + "sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Porttitor leo "
            + "a diam sollicitudin tempor id eu nisl. Nascetur ridiculus mus mauris vitae ultricies "
            + "leo. Dictum non consectetur a erat. Nunc vel risus commodo viverra maecenas accumsan "
            + "lacus vel facilisis. Ipsum nunc aliquet bibendum enim facilisis. Malesuada fames ac "
            + "turpis egestas maecenas pharetra. Nunc lobortis mattis aliquam faucibus purus. "
            + "Pretium vulputate sapien nec sagittis. Rutrum quisque non tellus orci. Lobortis "
            + "feugiat vivamus at augue eget arcu dictum.\r\n";

        private const int LOREM_IPSUM_LENGTH = 594;

        [Fact]
        public void length_matches_constant()
        {
            Assert.Equal(LOREM_IPSUM_LENGTH, LOREM_IPSUM.Length);
        }

        [Theory]
        [InlineData("r")]
        [InlineData("r+")]
        public void fopen_read_doesntExist(string mode)
        {
            //Reset State
            Reset();

            var filep = fopen("NOEXIST.TXT", mode);

            Assert.Equal(0, filep.Segment);
            Assert.Equal(0, filep.Offset);

            Assert.Equal(-1, fclose(filep));
        }

        [Theory]
        [InlineData("w")]
        [InlineData("w+")]
        [InlineData("a")]
        [InlineData("a+")]
        public void fopen_write_implicitCreate(string mode)
        {
            //Reset State
            Reset();

            var filep = fopen("NOWEXIST.TXT", mode);

            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.True(File.Exists(Path.Combine(mbbsModule.ModulePath, "NOWEXIST.TXT")));

            Assert.Equal(0, fclose(filep));
            Assert.Equal(-1, fclose(filep));
        }

        [Theory]
        [InlineData("r")]
        [InlineData("r+")]
        [InlineData("w")]
        [InlineData("w+")]
        public void fopen_read_and_close(string mode)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fclose(filep));
            Assert.Equal(-1, fclose(filep));
        }

        [Theory]
        [InlineData("r", 1)]
        [InlineData("r", 2)]
        [InlineData("r", 4)]
        [InlineData("r", 8)]
        [InlineData("r", 16)]
        [InlineData("r", 32)]
        [InlineData("r+", 7)]
        public void fopen_read_and_actually_read_and_close_bytes(string mode, ushort bufSize)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var memoryStream = new MemoryStream();
            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            while (memoryStream.Length < LOREM_IPSUM.Length)
            {
                var elementsRead = fread(dstBuf, 1, bufSize, filep);
                if (elementsRead == 0)
                    break;

                Assert.True(elementsRead <= bufSize);
                memoryStream.Write(mbbsEmuMemoryCore.GetArray(dstBuf, elementsRead));
            }

            Assert.Equal(LOREM_IPSUM, Encoding.ASCII.GetString(memoryStream.ToArray()));

            Assert.Equal(0, fclose(filep));
            Assert.Equal(-1, fclose(filep));
        }

        [Theory]
        [InlineData("r", 2, 1)]
        [InlineData("r", 2, 2)]
        [InlineData("r", 2, 4)]
        [InlineData("r", 3, 8)]
        [InlineData("r", 3, 16)]
        [InlineData("r", 5, 1)]
        [InlineData("r+", 7, 3)]
        public void fopen_read_and_actually_read_and_close_elements(string mode, ushort elementSize, ushort numElementsToReadPerCall)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);
            var totalToBeRead = LOREM_IPSUM.Length / elementSize * elementSize;

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var memoryStream = new MemoryStream();
            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            while (memoryStream.Length < totalToBeRead)
            {
                var elementsRead = fread(dstBuf, elementSize, numElementsToReadPerCall, filep);
                if (elementsRead == 0)
                    break;

                Assert.True(elementsRead <= numElementsToReadPerCall);
                memoryStream.Write(mbbsEmuMemoryCore.GetArray(dstBuf, (ushort)(elementSize * elementsRead)));
            }

            Assert.Equal(LOREM_IPSUM.Substring(0, totalToBeRead), Encoding.ASCII.GetString(memoryStream.ToArray()));

            Assert.Equal(0, fclose(filep));
            Assert.Equal(-1, fclose(filep));
        }

        [Theory]
        [InlineData("w")]
        [InlineData("w+")]
        public void fopen_write_truncates_preexisting_file(string mode)
        {
            //Reset State
            Reset();

            var contents = "This is really cool!\r\n";
            var filePath = CreateTextFile("file.txt", contents);

            Assert.Equal(contents.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fclose(filep));

            Assert.Equal(0, new FileInfo(filePath).Length);
        }

        [Theory]
        [InlineData("r", 8, "Lorem ip", LOREM_IPSUM_LENGTH)]
        [InlineData("r+", 8, "Lorem ip", LOREM_IPSUM_LENGTH)]
        [InlineData("w", 8, "", 0)]
        [InlineData("w+", 8, "", 0)]
        [InlineData("a", 8, "", LOREM_IPSUM_LENGTH)]
        [InlineData("a+", 8, "", LOREM_IPSUM_LENGTH)]
        public void fopen_initial_read(string mode, ushort bytes, string expectedString, ushort expectedFileLength)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            var bytesRead = fread(dstBuf, 1, bytes, filep);

            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(dstBuf, bytesRead)));

            Assert.Equal(0, fclose(filep));

            Assert.Equal(expectedFileLength, new FileInfo(filePath).Length);
        }

        [Theory]
        [InlineData("w", 1)]
        [InlineData("w", 2)]
        [InlineData("w", 6)]
        [InlineData("w", 13)]
        [InlineData("w+", 2)]
        [InlineData("a", 9)]
        [InlineData("a+", 100)]
        public void fopen_write_new_file(string mode, ushort elementSize)
        {
            //Reset State
            Reset();

            var filep = fopen("FILE.TXT", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var buffer = mbbsEmuMemoryCore.AllocateVariable(null, LOREM_IPSUM_LENGTH);
            mbbsEmuMemoryCore.SetArray(buffer, Encoding.ASCII.GetBytes(LOREM_IPSUM));

            var numElements = LOREM_IPSUM.Length / elementSize;
            Assert.Equal(numElements, fwrite(buffer, elementSize, (ushort) numElements , filep));

            Assert.Equal(0, fclose(filep));

            Assert.Equal(elementSize * numElements, new FileInfo(Path.Join(mbbsModule.ModulePath, "FILE.TXT")).Length);
        }

        [Fact]
        public void fopen_write_fprintf_new_file()
        {
            //Reset State
            Reset();

            var filep = fopen("FILE.TXT", "w");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var controlString = "%s";
            var numberOfCharacters = 100;

            Assert.Equal(numberOfCharacters, f_printf(filep, controlString, LOREM_IPSUM.Substring(0, numberOfCharacters)));

            Assert.Equal(0, fclose(filep));

            Assert.Equal(numberOfCharacters, new FileInfo(Path.Join(mbbsModule.ModulePath, "FILE.TXT")).Length);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("a+")]
        public void fopen_append_file(string mode)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", mode);
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var buffer = mbbsEmuMemoryCore.AllocateVariable(null, LOREM_IPSUM_LENGTH);
            mbbsEmuMemoryCore.SetArray(buffer, Encoding.ASCII.GetBytes("TEST"));

            Assert.Equal(4, fwrite(buffer, 1, 4, filep));

            Assert.Equal(0, fclose(filep));

            Assert.Equal(LOREM_IPSUM.Length + 4, new FileInfo(filePath).Length);

            Assert.Equal(LOREM_IPSUM + "TEST", ReadTextFile("file.txt"));
        }

        [Fact]
        public void fclose_invalidPointer()
        {
            Reset();

            Assert.Equal(-1, fclose(FarPtr.Empty));
        }

        [Theory]
        [InlineData('A', 5)]
        [InlineData('R', 10)]
        [InlineData('!', 20)]
        [InlineData('z', 25)]
        [InlineData('l', 55)]
        public void fputc_fgetc_fseek_origin0_file(byte inputChar, int fseekOffset)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "w");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, fseekOffset, 0));

            Assert.Equal(1, fputc(inputChar, filep));

            Assert.Equal(0,fseek(filep, fseekOffset, 0));

            Assert.Equal(inputChar, fgetc(filep));

            Assert.Equal(0, fclose(filep));
        }

        [Theory]
        [InlineData(4, 'm', 'n')]
        [InlineData(9, 'u', 'j')]
        [InlineData(14, 'l', 'k')]
        [InlineData(20, 't', 'r')]
        [InlineData(22, 'a', '.')]
        public void fgetc_ungetc_fseek_origin0_file(int fseekOffset, ushort getValue, ushort ungetValue)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "r");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, fseekOffset, 0));

            Assert.Equal((byte)getValue, fgetc(filep));

            Assert.Equal((byte)ungetValue, ungetc(ungetValue, filep));

            var curFileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(filep, FileStruct.Size));
            var curFileStream = majorbbs.FilePointerDictionary[curFileStruct.curp.Offset];
            Assert.Equal(ungetValue, curFileStream.ReadByte());

            Assert.Equal(0, fclose(filep));
        }

        [Theory]
        [InlineData("Mary Had a Little Lamb\0", 0)]
        [InlineData("---+++---111\0", 10)]
        [InlineData("((((jumbo)))))\0", 20)]
        [InlineData(" yeah \0", 25)]
        [InlineData("\0", 40)]
        [InlineData("A\0", 0)]
        public void fputs_fgets_fseek_origin0_file(string stringPut, int fseekOffset)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);
            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var stringPutPtr = mbbsEmuMemoryCore.AllocateVariable("STRING_PUT", (ushort)stringPut.Length);
            mbbsEmuMemoryCore.SetArray(stringPutPtr, Encoding.ASCII.GetBytes(stringPut));

            var stringGetPtr = mbbsEmuMemoryCore.AllocateVariable("STRING_GET", (ushort)stringPut.Length);

            var filep = fopen("FILE.TXT", "w");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, fseekOffset, 0));

            Assert.Equal(1, fputs(stringPutPtr, filep));

            Assert.Equal(0, fseek(filep, fseekOffset, 0));

            Assert.Equal(stringGetPtr, fgets(stringGetPtr, (ushort)stringPut.Length, filep));
            Assert.Equal(stringPut, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("STRING_GET")));

            Assert.Equal(0, fclose(filep));
        }

        [Theory]
        [InlineData(4, 'm', 1)]
        [InlineData(10, 'm', 1)]
        [InlineData(20, 't', 1)]
        [InlineData(25, 't', 1)]
        [InlineData(55, ',', 1)]
        [InlineData(-1, '\n', 2)]
        [InlineData(-2, '\n', 2)]
        [InlineData(-3, '.', 2)]
        [InlineData(-12, 'c', 2)]
        [InlineData(-22, 'u', 2)]
        [InlineData(-27, 't', 2)]
        [InlineData(-57, 'c', 2)]
        public void fgetc_fseek_origin1and2_file_ascii(int fseekOffset, byte expectedChar, ushort originNum)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "rt");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, 0, 0));

            Assert.Equal(0, fseek(filep, fseekOffset, originNum));

            Assert.Equal(expectedChar, fgetc(filep));

            Assert.Equal(0, fclose(filep));
        }

        [Theory]
        [InlineData(4, 'm', 1)]
        [InlineData(10, 'm', 1)]
        [InlineData(20, 't', 1)]
        [InlineData(25, 't', 1)]
        [InlineData(55, ',', 1)]
        [InlineData(-1, '\n', 2)]
        [InlineData(-2, '\r', 2)]
        [InlineData(-3, '.', 2)]
        [InlineData(-12, 'c', 2)]
        [InlineData(-22, 'u', 2)]
        [InlineData(-27, 't', 2)]
        [InlineData(-57, 'c', 2)]
        public void fgetc_fseek_origin1and2_file(int fseekOffset, byte expectedChar, ushort originNum)
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "rb");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, 0, 0));

            Assert.Equal(0, fseek(filep, fseekOffset, originNum));

            Assert.Equal(expectedChar, fgetc(filep));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fgetc_fseek_EOF_file()
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "r");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);
            
            Assert.Equal(0, fseek(filep, 0, 2));

            Assert.Equal(0xFFFF, fgetc(filep));
            
            var curFileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(filep, FileStruct.Size));
            var curFileStream = majorbbs.FilePointerDictionary[curFileStruct.curp.Offset];
            
            Assert.Equal(LOREM_IPSUM_LENGTH, curFileStream.Position);
            Assert.True(curFileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.EOF));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fputc_fseek_EOF_file()
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "a");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            Assert.Equal(0, fseek(filep, 0, 2));

            Assert.Equal(1, fputc(0, filep));

            var curFileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(filep, FileStruct.Size));
            var curFileStream = majorbbs.FilePointerDictionary[curFileStruct.curp.Offset];

            Assert.Equal(LOREM_IPSUM_LENGTH + 1, curFileStream.Position);
            Assert.True(curFileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.EOF));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fgets_fseek_EOF_file()
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "r");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var stringGetPtr = mbbsEmuMemoryCore.AllocateVariable("STRING_GET", (ushort)"TEST".Length);
            mbbsEmuMemoryCore.SetArray(stringGetPtr, Encoding.ASCII.GetBytes("TEST"));
            
            Assert.Equal(0, fseek(filep, 0, 2));

            Assert.Equal(new FarPtr(), fgets(stringGetPtr, 4, filep));

            var curFileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(filep, FileStruct.Size));
            var curFileStream = majorbbs.FilePointerDictionary[curFileStruct.curp.Offset];
            Assert.Equal(LOREM_IPSUM_LENGTH, curFileStream.Position );
            Assert.True(curFileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.EOF));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fputs_fseek_EOF_file()
        {
            //Reset State
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            Assert.Equal(LOREM_IPSUM.Length, new FileInfo(filePath).Length);

            var filep = fopen("FILE.TXT", "a");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var stringPutPtr = mbbsEmuMemoryCore.AllocateVariable("STRING_PUT", (ushort)"TEST".Length);
            mbbsEmuMemoryCore.SetArray(stringPutPtr, Encoding.ASCII.GetBytes("TEST"));

            Assert.Equal(0, fseek(filep, 0, 2));

            Assert.Equal(1, fputs(stringPutPtr, filep));

            var curFileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(filep, FileStruct.Size));
            var curFileStream = majorbbs.FilePointerDictionary[curFileStruct.curp.Offset];
            Assert.Equal(LOREM_IPSUM_LENGTH + 5, curFileStream.Position);
            Assert.True(curFileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.EOF));

            Assert.Equal(0, fclose(filep));
        }

        [Fact]
        public void fgetc_InvalidStream_Throw()
        {
            //Reset State
            Reset();
            
            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => fgetc(new FarPtr()));
        }

        [Fact]
        public void fputc_InvalidStream_Throw()
        {
            //Reset State
            Reset();

            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => fputc(0, new FarPtr()));
        }

        [Fact]
        public void fgets_InvalidStream_Throw()
        {
            //Reset State
            Reset();

            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => fgets(new FarPtr(), 0, new FarPtr()));
        }

        [Fact]
        public void fputs_InvalidStream_Throw()
        {
            //Reset State
            Reset();

            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => fputs(new FarPtr(), new FarPtr()));
        }

        [Fact]
        public void ungetc_InvalidStream_Throw()
        {
            //Reset State
            Reset();

            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => ungetc(0, new FarPtr()));
        }

        [Fact]
        public void fseek_InvalidStream_Throw()
        {
            //Reset State
            Reset();

            //Pass empty pointer
            Assert.Throws<FileNotFoundException>(() => fseek(new FarPtr(), 0, 0));
        }
    }
}

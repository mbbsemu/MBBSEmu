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
            + "feugiat vivamus at augue eget arcu dictum.";

        private const int LOREM_IPSUM_LENGTH = 592;

        [Fact]
        public void length_matches_constant()
        {
            Assert.Equal(LOREM_IPSUM_LENGTH, LOREM_IPSUM.Length);
        }

        // TODO(add fgetc/fputc fgets/fputs fseek tests)

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

            Assert.Equal(-1, fclose(IntPtr16.Empty));
        }
    }
}

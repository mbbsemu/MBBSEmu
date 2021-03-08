using FluentAssertions;
using MBBSEmu.HostProcess.Structs;
using System;
using System.IO;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class posix_file_Tests : FileTestBase, IDisposable
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
            LOREM_IPSUM_LENGTH.Should().Be(LOREM_IPSUM.Length);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_RDONLY)]
        [InlineData(EnumOpenFlags.O_RDRW)]
        [InlineData(EnumOpenFlags.O_WRONLY)]
        public void open_read_doesntExist(EnumOpenFlags mode)
        {
            Reset();

            var fd = open("NOEXIST.TXT", mode);

            fd.Should().Be(0xFFFF);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_CREAT | EnumOpenFlags.O_RDONLY)]
        [InlineData(EnumOpenFlags.O_CREAT | EnumOpenFlags.O_RDRW)]
        [InlineData(EnumOpenFlags.O_CREAT | EnumOpenFlags.O_WRONLY)]
        public void open_write_implicitCreate(EnumOpenFlags mode)
        {
            Reset();

            var fd = open("NOWEXIST.TXT", mode);

            fd.Should().NotBe(0xFFFF);

            File.Exists(Path.Combine(mbbsModule.ModulePath, "NOWEXIST.TXT")).Should().BeTrue();

            close(fd).Should().Be(0);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_RDONLY)]
        [InlineData(EnumOpenFlags.O_RDRW)]
        [InlineData(EnumOpenFlags.O_WRONLY)]
        public void open_read_and_close(EnumOpenFlags mode)
        {
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);

            var fd = open("file.txt", mode);
            fd.Should().NotBe(0xFFFF);

            close(fd).Should().Be(0);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_RDONLY, 1)]
        [InlineData(EnumOpenFlags.O_RDONLY, 2)]
        [InlineData(EnumOpenFlags.O_RDONLY, 4)]
        [InlineData(EnumOpenFlags.O_RDONLY, 8)]
        [InlineData(EnumOpenFlags.O_RDONLY, 16)]
        [InlineData(EnumOpenFlags.O_RDONLY, 32)]
        [InlineData(EnumOpenFlags.O_RDRW, 7)]
        public void open_read_and_actually_read_and_close_bytes(EnumOpenFlags mode, ushort bufSize)
        {
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);

            var fd = open("file.txt", mode);
            fd.Should().NotBe(0xFFFF);

            var memoryStream = new MemoryStream();
            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            while (memoryStream.Length < LOREM_IPSUM.Length)
            {
                var elementsRead = read(fd, dstBuf, bufSize);
                if (elementsRead == 0)
                    break;

                elementsRead.Should().BeLessOrEqualTo(bufSize);

                memoryStream.Write(mbbsEmuMemoryCore.GetArray(dstBuf, elementsRead));
            }

            Encoding.ASCII.GetString(memoryStream.ToArray()).Should().Be(LOREM_IPSUM);

            close(fd).Should().Be(0);
            close(fd).Should().Be(0xFFFF);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_WRONLY)]
        [InlineData(EnumOpenFlags.O_RDRW)]
        public void open_write_doesnt_truncate_preexisting_file(EnumOpenFlags mode)
        {
            Reset();

            var contents = "This is really cool!\r\n";
            var filePath = CreateTextFile("file.txt", contents);

            new FileInfo(filePath).Length.Should().Be(contents.Length);

            var fd = open("file.txt", mode);
            fd.Should().NotBe(0xFFFF);

            close(fd).Should().Be(0);

            new FileInfo(filePath).Length.Should().Be(contents.Length);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_TRUNC)]
        [InlineData(EnumOpenFlags.O_RDRW | EnumOpenFlags.O_TRUNC)]
        public void open_write_truncate_preexisting_file(EnumOpenFlags mode)
        {
            Reset();

            var contents = "This is really cool!\r\n";
            var filePath = CreateTextFile("file.txt", contents);

            new FileInfo(filePath).Length.Should().Be(contents.Length);

            var fd = open("file.txt", mode);
            fd.Should().NotBe(0xFFFF);

            close(fd).Should().Be(0);

            new FileInfo(filePath).Length.Should().Be(0);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_RDONLY, 8, "Lorem ip", LOREM_IPSUM_LENGTH)]
        [InlineData(EnumOpenFlags.O_RDRW, 8, "Lorem ip", LOREM_IPSUM_LENGTH)]
        [InlineData(EnumOpenFlags.O_WRONLY, 8, "", LOREM_IPSUM_LENGTH)]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_TRUNC, 8, "", 0)]
        public void open_initial_read(EnumOpenFlags mode, ushort bytes, string expectedString, ushort expectedFileLength)
        {
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);

            var fd = open("file.txt", mode);
            fd.Should().NotBe(0xFFFF);

            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            var bytesRead = read(fd, dstBuf, bytes);
            if ((short)bytesRead > 0)
                Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(dstBuf, bytesRead)).Should().Be(expectedString);

            close(fd).Should().Be(0);

            new FileInfo(filePath).Length.Should().Be(expectedFileLength);
        }

        [Theory]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_CREAT, 1)]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_CREAT, 2)]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_CREAT, 6)]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_CREAT, 13)]
        [InlineData(EnumOpenFlags.O_WRONLY | EnumOpenFlags.O_CREAT, LOREM_IPSUM_LENGTH)]
        [InlineData(EnumOpenFlags.O_RDRW | EnumOpenFlags.O_CREAT, 2)]
        public void open_write_new_file(EnumOpenFlags mode, ushort elementSize)
        {
            Reset();

            var fd = open("FILE.TXT", mode);
            fd.Should().NotBe(0xFFFF);

            var buffer = mbbsEmuMemoryCore.AllocateVariable(null, LOREM_IPSUM_LENGTH);
            mbbsEmuMemoryCore.SetArray(buffer, Encoding.ASCII.GetBytes(LOREM_IPSUM));

            var written = 0;
            while (written < LOREM_IPSUM.Length)
            {
                var bytesWritten = write(fd, buffer + written, (ushort)Math.Min(LOREM_IPSUM.Length - written, elementSize));
                bytesWritten.Should().BeLessThan(0xFFFF);
                written += bytesWritten;
            }

            close(fd).Should().Be(0);

            new FileInfo(Path.Join(mbbsModule.ModulePath, "FILE.TXT")).Length.Should().Be(LOREM_IPSUM.Length);
        }

        [Fact]
        public void read_writeonly_fails()
        {
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);

            var fd = open("file.txt", EnumOpenFlags.O_WRONLY);
            fd.Should().NotBe(0xFFFF);

            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            var bytesRead = read(fd, dstBuf, 4096);
            bytesRead.Should().Be(0xFFFF);
            mbbsEmuMemoryCore.GetByte(dstBuf).Should().Be(0);

            close(fd).Should().Be(0);
        }

        [Fact]
        public void write_readonly_fails()
        {
            Reset();

            var filePath = CreateTextFile("file.txt", LOREM_IPSUM);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);

            var fd = open("file.txt", EnumOpenFlags.O_RDONLY);
            fd.Should().NotBe(0xFFFF);

            var dstBuf = mbbsEmuMemoryCore.AllocateVariable(null, 4096);
            var bytesWritten = write(fd, dstBuf, 4096);
            bytesWritten.Should().Be(0xFFFF);
            mbbsEmuMemoryCore.GetByte(dstBuf).Should().Be(0);

            close(fd).Should().Be(0);

            new FileInfo(filePath).Length.Should().Be(LOREM_IPSUM.Length);
        }

        [Fact]
        public void close_invalidHandle()
        {
            Reset();

            close(6).Should().Be(0xFFFF);
        }

        [Fact]
        public void read_invalidHandle()
        {
            Reset();

            read(6, FarPtr.Empty, 1).Should().Be(0xFFFF);
        }

        [Fact]
        public void write_invalidHandle()
        {
            Reset();

            write(6, FarPtr.Empty, 1).Should().Be(0xFFFF);
        }

        [Fact]
        public void open_text_fails()
        {
            Reset();

            Assert.Throws<ArgumentException>(() => open("Dummy.txt", EnumOpenFlags.O_TEXT));
        }
    }
}

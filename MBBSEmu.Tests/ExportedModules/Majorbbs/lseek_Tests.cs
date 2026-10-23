using FluentAssertions;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class lseek_Tests : FileTestBase
    {
        private const int LSEEK_ORDINAL = 396;

        private const string FILE_CONTENTS = "0123456789ABCDEFGHIJ";

        private int lseek(ushort fd, int offset, ushort origin)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LSEEK_ORDINAL, new List<ushort>
            {
                fd,
                (ushort)(offset & 0xFFFF),
                (ushort)(offset >> 16),
                origin
            });
            return mbbsEmuCpuRegisters.GetLong();
        }

        [Theory]
        [InlineData(5, 0, 5, '5')]
        [InlineData(-3, 2, 17, 'H')]
        [InlineData(0, 2, 20, null)]
        public void lseek_from_origin_test(int offset, ushort origin, int expectedPosition, char? expectedByte)
        {
            //Reset State
            Reset();

            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            lseek(fileHandle, offset, origin).Should().Be(expectedPosition);

            var destPtr = mbbsEmuMemoryCore.Malloc(1);
            var bytesRead = read(fileHandle, destPtr, 1);
            if (expectedByte.HasValue)
            {
                bytesRead.Should().Be(1);
                ((char)mbbsEmuMemoryCore.GetByte(destPtr)).Should().Be(expectedByte.Value);
            }
            else
            {
                bytesRead.Should().Be(0);
            }

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void lseek_from_current_test()
        {
            //Reset State
            Reset();

            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            lseek(fileHandle, 10, 0).Should().Be(10);
            lseek(fileHandle, -4, 1).Should().Be(6);

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void lseek_before_start_of_file_test()
        {
            //Reset State
            Reset();

            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            lseek(fileHandle, -1, 0).Should().Be(-1);

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void unknownFileHandle_lseek_test()
        {
            //Reset State
            Reset();

            lseek(55, 0, 0).Should().Be(-1);
        }
    }
}

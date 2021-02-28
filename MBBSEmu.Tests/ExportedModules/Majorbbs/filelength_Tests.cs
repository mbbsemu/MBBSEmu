using FluentAssertions;
using MBBSEmu.HostProcess.Structs;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class filelength_Tests: FileTestBase
    {
        [Fact]
        public void filelength_test()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "Lorem ipsum dolor sit amet, consectetur adipiscing elit,"
                                         + "sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Porttitor leo "
                                         + "a diam sollicitudin tempor id eu nisl. Nascetur ridiculus mus mauris vitae ultricies "
                                         + "leo. Dictum non consectetur a erat. Nunc vel risus commodo viverra maecenas accumsan "
                                         + "lacus vel facilisis. Ipsum nunc aliquet bibendum enim facilisis. Malesuada fames ac "
                                         + "turpis egestas maecenas pharetra. Nunc lobortis mattis aliquam faucibus purus. "
                                         + "Pretium vulputate sapien nec sagittis. Rutrum quisque non tellus orci. Lobortis "
                                         + "feugiat vivamus at augue eget arcu dictum.";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            var fileLength = filelength(fileHandle);
            fileLength.Should().Be(FILE_CONTENTS.Length);

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void unknownFileHandle_filelength_test()
        {
            //Reset State
            Reset();

            var fileLength = filelength(55);
            fileLength.Should().Be(-1);
        }
    }
}

using FluentAssertions;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Structs;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class setmode_Tests : FileTestBase
    {
        const string FILE_CONTENTS = "Lorem ipsum dolor sit amet, consectetur adipiscing elit,"
                                     + "sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Porttitor leo "
                                     + "a diam sollicitudin tempor id eu nisl. Nascetur ridiculus mus mauris vitae ultricies "
                                     + "leo. Dictum non consectetur a erat. Nunc vel risus commodo viverra maecenas accumsan "
                                     + "lacus vel facilisis. Ipsum nunc aliquet bibendum enim facilisis. Malesuada fames ac "
                                     + "turpis egestas maecenas pharetra. Nunc lobortis mattis aliquam faucibus purus. "
                                     + "Pretium vulputate sapien nec sagittis. Rutrum quisque non tellus orci. Lobortis "
                                     + "feugiat vivamus at augue eget arcu dictum.";

        [Fact]
        public void setmode_binary_test()
        {
            //Reset State
            Reset();

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            var fileStreamPointer = majorbbs.FilePointerDictionary[fileHandle];
            var fileStructPointer = mbbsEmuMemoryCore.GetOrAllocateVariablePointer($"FILE_{fileStreamPointer.Name}-{majorbbs.FilePointerDictionary.Count}", FileStruct.Size);
            var fileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(fileStructPointer, FileStruct.Size));

            Assert.False(fileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.Binary));

            setmode(fileHandle, 0x8000);

            var fileStructChanged = new FileStruct(mbbsEmuMemoryCore.GetArray(fileStructPointer, FileStruct.Size));

            Assert.True(fileStructChanged.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.Binary));
            Assert.Equal(0x4000, mbbsEmuCpuRegisters.AX);

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void setmode_text_test()
        {
            //Reset State
            Reset();

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            var fileHandle = open("file.txt", EnumOpenFlags.O_RDONLY | EnumOpenFlags.O_BINARY);
            fileHandle.Should().NotBe(0xFFFF);

            var fileStreamPointer = majorbbs.FilePointerDictionary[fileHandle];
            var fileStructPointer = mbbsEmuMemoryCore.GetOrAllocateVariablePointer($"FILE_{fileStreamPointer.Name}-{majorbbs.FilePointerDictionary.Count}", FileStruct.Size);
            var fileStruct = new FileStruct(mbbsEmuMemoryCore.GetArray(fileStructPointer, FileStruct.Size));
            fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.Binary;

            Assert.True(fileStruct.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.Binary));

            setmode(fileHandle, 0x4000);

            var fileStructChanged = new FileStruct(mbbsEmuMemoryCore.GetArray(fileStructPointer, FileStruct.Size));

            Assert.False(fileStructChanged.flags.IsFlagSet((ushort)FileStruct.EnumFileFlags.Binary));
            Assert.Equal(0x8000, mbbsEmuCpuRegisters.AX);

            close(fileHandle);

            File.Delete(fileName);
        }

        [Fact]
        public void unknownFileHandle_setmode_test()
        {
            //Reset State
            Reset();

            setmode(55, 0x4000);
            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.AX);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class filelength_Tests: ExportedModuleTestBase
    {
        private const int FILELENGTH_ORDINAL = 211;
        private const int OPEN_ORDINAL = 451;
        private const int CLOSE_ORDINAL = 110;

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

            const string fileName = "file.txt";
            const string fileMode = "r";
            long expectedValue = FILE_CONTENTS.Length;
            long failedValue = -1;

            //Create Text File
            var filePath = Path.Join(mbbsModule.ModulePath, fileName);
            using var sw = File.Create(filePath);
            sw.Write(Encoding.ASCII.GetBytes(FILE_CONTENTS));
            sw.Close();
            
            //Set Argument Values to be Passed In
            var filenamePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(fileName.Length + 1));
            mbbsEmuMemoryCore.SetArray(filenamePointer, Encoding.ASCII.GetBytes(fileName));

            var modePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(fileMode.Length + 1));
            mbbsEmuMemoryCore.SetArray(modePointer, Encoding.ASCII.GetBytes(fileMode));
            
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OPEN_ORDINAL, new List<IntPtr16> { filenamePointer, modePointer });

            var fileHandle = mbbsEmuCpuRegisters.GetPointer();
            
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FILELENGTH_ORDINAL,new List<IntPtr16> { fileHandle });
            
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
            
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CLOSE_ORDINAL,new List<IntPtr16> { fileHandle });
            
            File.Delete(filePath);
            
            //Test -1 return, use deleted file handle which won't exist
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FILELENGTH_ORDINAL,new List<IntPtr16> { fileHandle });
            
            Assert.Equal((ushort)(failedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(failedValue >> 16), mbbsEmuCpuRegisters.DX);
            
        }
    }
}

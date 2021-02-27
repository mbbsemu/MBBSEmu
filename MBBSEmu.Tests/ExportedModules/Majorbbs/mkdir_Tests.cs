using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class mkdir_Tests : ExportedModuleTestBase, IDisposable
    {
        private const int MKDIR_ORDINAL = 413;

        public mkdir_Tests() : base(Path.Join(Path.GetTempPath(), "mkdir"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            Directory.Delete(mbbsModule.ModulePath, recursive: true);
        }

        [Fact]
        public void mkdir_subdir_Test()
        {
            Reset();

            //Pointer to sub directory
            var subdirPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            mbbsEmuMemoryCore.SetArray(subdirPointer, Encoding.ASCII.GetBytes("subdir"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MKDIR_ORDINAL, new List<FarPtr>() { subdirPointer });

            Assert.True(Directory.Exists(mbbsModule.ModulePath + "/subdir"));
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void mkdir_subdir_alreadyexists_Test()
        {
            Reset();

            //Create directory
            Directory.CreateDirectory(mbbsModule.ModulePath + "/subdir2");

            //Pointer to sub directory
            var subdirPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            mbbsEmuMemoryCore.SetArray(subdirPointer, Encoding.ASCII.GetBytes("subdir2"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MKDIR_ORDINAL, new List<FarPtr>() { subdirPointer });

            Assert.True(Directory.Exists(mbbsModule.ModulePath + "/subdir2"));
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}

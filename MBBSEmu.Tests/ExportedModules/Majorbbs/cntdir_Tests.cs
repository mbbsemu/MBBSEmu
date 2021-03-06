using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class cntdir_Tests : ExportedModuleTestBase, IDisposable
    {
        private const int CNTDIR_ORDINAL = 132;

        public cntdir_Tests() : base(Path.Join(Path.GetTempPath(), "cntdir"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            Directory.Delete(mbbsModule.ModulePath, recursive: true);
        }

        [Fact]
        public void cntdir_0_Test()
        {
            Reset();

            // create no files, so no match
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNTDIR_ORDINAL, new List<FarPtr> { new() });

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("NUMFILS"));
            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("NUMBYTS"));
        }

        [Fact]
        public void cntdir_1_file_Test()
        {
            Reset();

            // create 1 empty file
            CreateFile("test.dat");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNTDIR_ORDINAL, new List<FarPtr>() { new() });

            Assert.Equal(1, mbbsEmuMemoryCore.GetWord("NUMFILS"));
            Assert.Equal(9, mbbsEmuMemoryCore.GetWord("NUMBYTS"));
        }

        [Fact]
        public void cntdir_3_file_Test()
        {
            Reset();

            // create 1 empty file
            CreateFile("test.dat");
            CreateFile("test1.dat");
            CreateFile("test2.dat");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNTDIR_ORDINAL, new List<FarPtr>() { new() });

            Assert.Equal(3, mbbsEmuMemoryCore.GetWord("NUMFILS"));
            Assert.Equal(27, mbbsEmuMemoryCore.GetWord("NUMBYTS"));
        }

        [Fact]
        public void cntdir_5_file_subdir_Test()
        {
            Reset();

            // create 1 empty file
            CreateFile("subdir/test.dat");
            CreateFile("subdir/test1.dat");
            CreateFile("subdir/test2.dat");
            CreateFile("subdir/xxx.dat");
            CreateFile("subdir/hello.dat");

            //Pointer to sub directory
            var subdirPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            mbbsEmuMemoryCore.SetArray(subdirPointer, Encoding.ASCII.GetBytes("subdir/*"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CNTDIR_ORDINAL, new List<FarPtr>() { subdirPointer });

            Assert.Equal(5, mbbsEmuMemoryCore.GetWord("NUMFILS"));
            Assert.Equal(45, mbbsEmuMemoryCore.GetWord("NUMBYTS"));
        }

        private void CreateFile(string file)
        {
            // replace slashes with the system slash
            file = file.Replace('/', Path.DirectorySeparatorChar);

            var path = Path.Join(mbbsModule.ModulePath, file);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var writer = new StreamWriter(path);
            writer.Write("Testing\r\n");
        }
    }
}

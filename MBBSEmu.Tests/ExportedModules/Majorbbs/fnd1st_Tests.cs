using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class fnd1st_Tests : ExportedModuleTestBase, IDisposable
    {
      private const int FND1ST_ORDINAL = 694;
      private const int FNDNXT_ORDINAL = 695;

      private const long FIVE_SECOND_TICKS = 5 * 1_000 * 10_000;

      private readonly long ticksNow = DateTime.Now.Ticks;

        public fnd1st_Tests() : base(Path.Join(Path.GetTempPath(), "fnd1st"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            Directory.Delete(mbbsModule.ModulePath, recursive: true);
        }

        [Fact]
        public void ModulePath_Test()
        {
            CreateFile("dog.txt");
            CreateFile("man.txt");
            CreateFile("areallylongfilename.txt");
            CreateFile("not_a_text_file.dat");

            _EnumerateTextFiles_Test("*.txt");
        }

        [Fact]
        public void SubDir_Test()
        {
            CreateFile("subdir/dog.txt");
            CreateFile("subdir/man.txt");
            CreateFile("subdir/areallylongfilename.txt");
            CreateFile("subdir/not_a_text_file.dat");

            _EnumerateTextFiles_Test("SUBDIR/*.TXT");
        }

        [Fact]
        public void NoMatch_Test()
        {
            Reset();

            // create no files, so no match

            var fndblkPointer = mbbsEmuMemoryCore.AllocateVariable("FNDBLK", FndblkStruct.StructSize);
            var searchPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            mbbsEmuMemoryCore.SetArray(searchPointer, Encoding.ASCII.GetBytes("anyfile.*"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FND1ST_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment, searchPointer.Offset, searchPointer.Segment, 0 });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void ExactMatch_Test()
        {
            Reset();

            CreateFile("test.dat");

            var fndblkPointer = mbbsEmuMemoryCore.AllocateVariable("FNDBLK", FndblkStruct.StructSize);
            var searchPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            mbbsEmuMemoryCore.SetArray(searchPointer, Encoding.ASCII.GetBytes("test.dat"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FND1ST_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment, searchPointer.Offset, searchPointer.Segment, 0 });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal("test.dat", GetFileFromFndblk(fndblkPointer));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FNDNXT_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment });
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        private void _EnumerateTextFiles_Test(string search)
        {
            Reset();

            var fndblkPointer = mbbsEmuMemoryCore.AllocateVariable("FNDBLK", FndblkStruct.StructSize);
            var searchPointer = mbbsEmuMemoryCore.AllocateVariable("STR", (ushort)(search.Length + 1));

            mbbsEmuMemoryCore.SetArray(searchPointer, Encoding.ASCII.GetBytes(search));

            var files = new List<string>();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FND1ST_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment, searchPointer.Offset, searchPointer.Segment, 0 });
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            files.Add(GetFileFromFndblk(fndblkPointer));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FNDNXT_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment });
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            files.Add(GetFileFromFndblk(fndblkPointer));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FNDNXT_ORDINAL, new List<ushort>() { fndblkPointer.Offset, fndblkPointer.Segment });
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            files.Sort();
            Assert.Equal(new List<string>() { "dog.txt", "man.txt" }, files);
        }

        private string GetFileFromFndblk(IntPtr16 fndblkPointer)
        {
            var fbs = new FndblkStruct(mbbsEmuMemoryCore.GetArray(fndblkPointer, FndblkStruct.StructSize));
            Assert.Equal(9, fbs.Size);
            Assert.Equal(0, (byte) fbs.Attributes & (byte) ~FndblkStruct.AttributeFlags.Archive);
            Assert.True(Math.Abs(ticksNow - fbs.DateTime.Ticks) <= FIVE_SECOND_TICKS);

            return fbs.Name;
        }

        private void CreateFile(string file)
        {
            // replace slashes with the system slash
            file = file.Replace('/', Path.DirectorySeparatorChar);

            var path = Path.Join(mbbsModule.ModulePath, file);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (StreamWriter writer = new System.IO.StreamWriter(path))
            {
                writer.Write("Testing\r\n");
            }
        }
    }
}

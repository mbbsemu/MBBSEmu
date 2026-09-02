using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class fnd1st_Tests : ExportedModuleTestBase, IDisposable
    {
        private const int FND1ST_ORDINAL = 694;
        private const int FNDNXT_ORDINAL = 695;

        //DOS directory entries store the time to a two-second boundary, so a
        //decoded timestamp can legitimately sit either side of the real one
        private static readonly TimeSpan DOS_TIMESTAMP_GRANULARITY = TimeSpan.FromSeconds(2);

        //Timestamp each file as it is written, so the assertions compare what fnd1st
        //decoded against the file itself rather than against the clock
        private readonly Dictionary<string, DateTime> _createdFiles = new();

        public fnd1st_Tests() : base(Path.Join(Path.GetTempPath(), $"fnd1st{Guid.NewGuid():N}"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public override void Dispose()
        {
            base.Dispose();

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

        private string GetFileFromFndblk(FarPtr fndblkPointer)
        {
            var fbs = new FndblkStruct(mbbsEmuMemoryCore.GetArray(fndblkPointer, FndblkStruct.StructSize));
            Assert.Equal(9, fbs.Size);
            Assert.Equal(0, (byte)fbs.Attributes & (byte)~FndblkStruct.AttributeFlags.Archive);
            Assert.True(_createdFiles.TryGetValue(fbs.Name, out var writtenAt),
                $"fnd1st returned \"{fbs.Name}\", which this test never created");
            Assert.True((fbs.DateTime - writtenAt).Duration() <= DOS_TIMESTAMP_GRANULARITY,
                $"fnd1st reported {fbs.DateTime:O} for \"{fbs.Name}\", which was written at {writtenAt:O}");

            return fbs.Name;
        }

        [Fact]
        public void find1st_dirnotfound_Test()
        {
            //Reset State
            Reset();

            var fndblkPointer = mbbsEmuMemoryCore.AllocateVariable("FNDBLK", FndblkStruct.StructSize);
            var searchPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 64);

            //Pass in directory that does not exist
            mbbsEmuMemoryCore.SetArray(searchPointer, Encoding.ASCII.GetBytes("hello/test.dat"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FND1ST_ORDINAL, new List<ushort> { fndblkPointer.Offset, fndblkPointer.Segment, searchPointer.Offset, searchPointer.Segment, 0 });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void findnxt_invalid_guid_Test()
        {
            //Reset State
            Reset();

            //Execute Test -- pass in invalid pointer
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FNDNXT_ORDINAL, new List<ushort> { 0, 0 });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
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

            _createdFiles[Path.GetFileName(path)] = File.GetLastWriteTime(path);
        }
    }
}

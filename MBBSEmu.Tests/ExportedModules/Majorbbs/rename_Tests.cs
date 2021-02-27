using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rename_Tests : ExportedModuleTestBase, IDisposable
    {
        private const int RENAME_ORDINAL = 496;

        public rename_Tests() : base(Path.Join(Path.GetTempPath(), "rename"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            Directory.Delete(mbbsModule.ModulePath, recursive: true);
        }

        [Theory]
        [InlineData("oldname.dat", "newname.dat", 0, false)]
        [InlineData("o.txt", "longname.dat", 0, false)]
        [InlineData("a", "b", 0, false)]
        [InlineData("oldname.dat", "newname.dat", 65535, true)]
        [InlineData("", "newname.dat", 65535, true)]
        [InlineData("oldfile.txt", "", 65535, true)]
        public void rename_file_Test(string oldFileName, string newFileName, ushort axValue, bool shouldThrowException)
        {
            Reset();

            if(!shouldThrowException)
                CreateFile(oldFileName);
            
            //Pointer to old and new filenames
            var fromNamePointer = mbbsEmuMemoryCore.AllocateVariable("FROM_NAME", (ushort) oldFileName.Length);
            mbbsEmuMemoryCore.SetArray(fromNamePointer, Encoding.ASCII.GetBytes(oldFileName));

            var toNamePointer = mbbsEmuMemoryCore.AllocateVariable("TO_NAME", (ushort) newFileName.Length);
            mbbsEmuMemoryCore.SetArray(toNamePointer, Encoding.ASCII.GetBytes(newFileName));

            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RENAME_ORDINAL, new List<FarPtr> {fromNamePointer, toNamePointer});
            }
            catch (Exception)
            {
                Assert.True(shouldThrowException);
                Assert.Equal(axValue, mbbsEmuCpuRegisters.AX);
            }

            Assert.NotEqual(shouldThrowException, File.Exists(mbbsModule.ModulePath + newFileName));
            Assert.Equal(axValue, mbbsEmuCpuRegisters.AX);
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

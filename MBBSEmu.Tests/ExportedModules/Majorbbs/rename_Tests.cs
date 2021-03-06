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
        [InlineData("oldname.dat", "NEWNAME.DAT", 0)]
        [InlineData("o.txt", "LONGNAM.DAT", 0)]
        [InlineData("a", "B", 0)]
        [InlineData("oldname.dat", "NEWNAME.DAT", 65535)]
        [InlineData("", "NEWNAME.DAT", 65535)]
        [InlineData("oldfile.txt", "", 65535)]
        public void rename_file_Test(string oldFileName, string newFileName, ushort axValue)
        {
            Reset();

            if(axValue == 0)
                CreateFile(oldFileName);
            
            //Pointer to old and new filenames
            var fromNamePointer = mbbsEmuMemoryCore.AllocateVariable("FROM_NAME", (ushort) (oldFileName.Length + 1));
            mbbsEmuMemoryCore.SetArray(fromNamePointer, Encoding.ASCII.GetBytes(oldFileName));

            var toNamePointer = mbbsEmuMemoryCore.AllocateVariable("TO_NAME", (ushort) (newFileName.Length + 1));
            mbbsEmuMemoryCore.SetArray(toNamePointer, Encoding.ASCII.GetBytes(newFileName));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RENAME_ORDINAL, new List<FarPtr> {fromNamePointer, toNamePointer});
            
            Assert.Equal(axValue, mbbsEmuCpuRegisters.AX);    
            Assert.Equal(axValue == 0, File.Exists(Path.Combine(mbbsModule.ModulePath, newFileName)));
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

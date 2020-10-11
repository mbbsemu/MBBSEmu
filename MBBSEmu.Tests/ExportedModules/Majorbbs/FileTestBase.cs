using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs {
    public class FileTestBase : ExportedModuleTestBase, IDisposable
    {
        protected const int FOPEN_ORDINAL = 225;
        protected const int FREAD_ORDINAL = 229;
        protected const int FWRITE_ORDINAL = 312;
        protected const int FPRINTF_ORDINAL = 226;
        protected const int FCLOSE_ORDINAL = 205;
        private static readonly Random RANDOM = new Random(Guid.NewGuid().GetHashCode());

        protected FileTestBase() : base(Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            majorbbs.Dispose();

            Directory.Delete(mbbsModule.ModulePath,  recursive: true);
        }

        protected IntPtr16 fopen(string filename, string mode) {
            //Set Argument Values to be Passed In
            var filenamePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(filename.Length + 1));
            mbbsEmuMemoryCore.SetArray(filenamePointer, Encoding.ASCII.GetBytes(filename));

            var modePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(mode.Length + 1));
            mbbsEmuMemoryCore.SetArray(modePointer, Encoding.ASCII.GetBytes(mode));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FOPEN_ORDINAL, new List<IntPtr16> { filenamePointer, modePointer });

            return mbbsEmuCpuRegisters.GetPointer();
        }

        protected short fclose(IntPtr16 filep)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FCLOSE_ORDINAL, new List<IntPtr16> { filep });

            return (short) mbbsEmuCpuRegisters.AX;
        }

        protected ushort fread(IntPtr16 destPtr, ushort size, ushort count, IntPtr16 filep)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FREAD_ORDINAL, new List<ushort>
            {
                destPtr.Offset,
                destPtr.Segment,
                size,
                count,
                filep.Offset,
                filep.Segment,
            });

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort fwrite(IntPtr16 destPtr, ushort size, ushort count, IntPtr16 filep)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FWRITE_ORDINAL, new List<ushort>
            {
                destPtr.Offset,
                destPtr.Segment,
                size,
                count,
                filep.Offset,
                filep.Segment,
            });

            return mbbsEmuCpuRegisters.AX;
        }

        protected string CreateTextFile(string filename, string contents)
        {
            var filePath = Path.Join(mbbsModule.ModulePath, filename);

            using FileStream sw = File.Create(filePath);
            sw.Write(Encoding.ASCII.GetBytes(contents));

            return filePath;
        }

        protected string ReadTextFile(string filename)
        {
            var filePath = Path.Join(mbbsModule.ModulePath, filename);

            using FileStream sw = File.Open(filePath, FileMode.Open);
            var data = new byte[32*1024];
            var read = sw.Read(data);

            return Encoding.ASCII.GetString(data, 0, read);
        }
    }
}

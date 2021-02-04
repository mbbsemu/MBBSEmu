using MBBSEmu.HostProcess.Structs;
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

        protected const int OPEN_ORDINAL = 451;
        protected const int READ_ORDINAL = 866;
        protected const int WRITE_ORDINAL = 867;
        protected const int CLOSE_ORDINAL = 110;

        protected FileTestBase() : base(Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
        }

        public void Dispose()
        {
            majorbbs.Dispose();

            Directory.Delete(mbbsModule.ModulePath,  recursive: true);
        }

        protected FarPtr fopen(string filename, string mode) {
            //Set Argument Values to be Passed In
            var filenamePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(filename.Length + 1));
            mbbsEmuMemoryCore.SetArray(filenamePointer, Encoding.ASCII.GetBytes(filename));

            var modePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(mode.Length + 1));
            mbbsEmuMemoryCore.SetArray(modePointer, Encoding.ASCII.GetBytes(mode));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FOPEN_ORDINAL, new List<FarPtr> { filenamePointer, modePointer });

            return mbbsEmuCpuRegisters.GetPointer();
        }

        protected short fclose(FarPtr filep)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FCLOSE_ORDINAL, new List<FarPtr> { filep });

            return (short) mbbsEmuCpuRegisters.AX;
        }

        protected ushort fread(FarPtr destPtr, ushort size, ushort count, FarPtr filep)
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

        protected ushort fwrite(FarPtr srcPtr, ushort size, ushort count, FarPtr filep)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FWRITE_ORDINAL, new List<ushort>
            {
                srcPtr.Offset,
                srcPtr.Segment,
                size,
                count,
                filep.Offset,
                filep.Segment,
            });

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort f_printf(FarPtr filep, string formatString, params object[] values)
        {
            var fprintfParameters = new List<ushort> {filep.Offset, filep.Segment};

            //Add Formatted String
            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(formatString));
            fprintfParameters.Add(inputStingParameterPointer.Offset);
            fprintfParameters.Add(inputStingParameterPointer.Segment);

            //Add Parameters
            var parameterList = GenerateParameters(values);
            fprintfParameters.AddRange(parameterList);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FPRINTF_ORDINAL, fprintfParameters);

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort open(string filename, EnumOpenFlags mode) {
            var filenamePointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)(filename.Length + 1));
            mbbsEmuMemoryCore.SetArray(filenamePointer, Encoding.ASCII.GetBytes(filename));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OPEN_ORDINAL, new List<ushort>
            {
                filenamePointer.Offset,
                filenamePointer.Segment,
                (ushort)mode
            });

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort close(ushort fd)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CLOSE_ORDINAL, new List<ushort> { fd });

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort read(ushort fd, FarPtr destPtr, ushort count)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, READ_ORDINAL, new List<ushort>
            {
                fd,
                destPtr.Offset,
                destPtr.Segment,
                count
            });

            return mbbsEmuCpuRegisters.AX;
        }

        protected ushort write(ushort fd, FarPtr srcPtr, ushort count)
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, WRITE_ORDINAL, new List<ushort>
            {
                fd,
                srcPtr.Offset,
                srcPtr.Segment,
                count,
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

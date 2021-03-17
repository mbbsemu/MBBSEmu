using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
using NLog;
using System;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Doscalls : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFB;

        public const ushort DosSegmentBase = 0x200;
        public ushort DosSegmentOffset = 0;

        public new void Dispose()
        {
            base.Dispose();
        }

        internal Doscalls(IClock clock, ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService) : base(
            clock, logger, configuration, fileUtility, globalCache, module, channelDictionary, textVariableService)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool onlyProperties = false)
        {
            switch (ordinal)
            {
                case 89:
                    return dossetvec;
            }

            if (onlyProperties)
            {
                var methodPointer = new FarPtr(Segment, ordinal);
#if DEBUG
                //_logger.Debug($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.Data;
            }

            switch (ordinal)
            {
                case 34:
                    DosAllocSeg();
                    break;
                case 44:
                    DosLoadModule();
                    break;
                case 45:
                    DosGetProcAddr();
                    break;
                case 47:
                    DosGetModHandle();
                    break;
                case 48:
                    DosGetModName();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in DOSCALLS: {ordinal}");
            }

            return null;
        }

        public void SetRegisters(CpuRegisters registers)
        {
            Registers = registers;
        }

        public void SetState(ushort channelNumber)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     This call allocates a segment of memory to a requesting process.
        /// </summary>
        public void DosAllocSeg()
        {
            var size = GetParameter(0);
            var selectorPointer = GetParameterPointer(1);
            var flags = GetParameter(3);

            var allocatedSegment = (ushort) (DosSegmentBase + DosSegmentOffset);

            Module.ProtectedMemory.AddSegment(allocatedSegment);
            Module.Memory.SetWord(selectorPointer, allocatedSegment);
            DosSegmentOffset++;
            Registers.AX = 0;

            RealignStack(8);
        }

        private ReadOnlySpan<byte> dossetvec => new byte[] {0x0, 0x0, 0x0, 0x0};

        /// <summary>
        ///     DosLoadModule tries to load a dynamic link module.
        ///     If the module is an OS/2 dynamic link module then the module is loaded and a handle to the module is returned.
        /// </summary>
        public void DosLoadModule()
        {
            _logger.Warn($"Loading DLL's dynamically is currently not supported");
            Registers.AX = 2; //ERROR_FILE_NOT_FOUND

            RealignStack(14);
        }

        /// <summary>
        ///     This call returns a handle to a previously loaded dynamic link module.
        ///
        ///     Signature: DosGetModHandle (ModuleName, ModuleHandle)
        /// </summary>
        public void DosGetModHandle()
        {
            _logger.Warn($"({Module.ModuleIdentifier}) Getting External Modules is currently not supported");
            Registers.AX = 0;
        }

        /// <summary>
        ///     This call returns the fully qualified drive, path, file name, and extension associated with a referenced module handle.
        ///
        ///     Signature: DosGetModName (ModuleHandle, BufferLength, Buffer)
        /// </summary>
        public void DosGetModName()
        {
            var bufferPointer = GetParameterPointer(0);
            var bufferLength = GetParameter(2);
            var moduleHandle = GetParameter(3);

            //I've only seen this in TW2002, and it passes in Code Segment
            if (Module.ProtectedMemory.HasSegment(moduleHandle))
            {
                var moduleFileName = Module.MainModuleDll.File.FileName + '\0';
                Module.Memory.SetArray(bufferPointer, Encoding.ASCII.GetBytes(moduleFileName));
                Registers.AX = 0;
            }
            else
            {
                _logger.Warn("");
                Registers.AX = 6;
            }

            RealignStack(8);

        }

        /// <summary>
        ///     This call returns a far address to a desired procedure within a dynamic link module.
        ///
        ///     Signature: USHORT  rc = DosGetProcAddr(ModuleHandle, ProcName, ProcAddress);
        /// </summary>
        public void DosGetProcAddr()
        {
            _logger.Warn($"({Module.ModuleIdentifier}) Getting External Procedures is currently not supported");
            Registers.AX = 6;
            RealignStack(10);
        }
    }
}

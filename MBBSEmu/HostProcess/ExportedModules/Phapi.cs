using System;
using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using NLog;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Phapi : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFD;

        internal Phapi(ILogger logger, IConfiguration configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {

            if (offsetsOnly)
            {
                var methodPointer = new IntPtr16(0xFFFC, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.ToSpan();
            }

            switch (ordinal)
            {
                case 16:
                    DosCreatedAlias();
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in PHAPI: {ordinal}");
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
        ///     Create a data selector corresponding to an executable (code) selector.
        ///
        ///     Because we don't do any relocation, just pass back the segment identifier
        /// </summary>
        private void DosCreatedAlias()
        {
            var destinationPointer = GetParameterPointer(0);
            var segmentIdentifier = GetParameter(2);

            Module.Memory.SetWord(destinationPointer, segmentIdentifier);

            RealignStack(6);

            Registers.AX = 0;
        }

    }
}

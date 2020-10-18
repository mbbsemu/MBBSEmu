using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using NLog;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Galmsg : ExportedModuleBase, IExportedModule
    {
        internal Galmsg(ILogger logger, IConfiguration configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
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
                return methodPointer.Data;
            }

            switch (ordinal)
            {
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in GALMSG: {ordinal}");
            }

        }

        public void SetState(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

        public void SetRegisters(CpuRegisters registers)
        {
            throw new NotImplementedException();
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }
    }
}

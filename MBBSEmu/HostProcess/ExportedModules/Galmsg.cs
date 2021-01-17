using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Galmsg : ExportedModuleBase, IExportedModule
    {

        public const ushort Segment = 0xFFFA;

        public new void Dispose()
        {
            base.Dispose();
        }

        internal Galmsg(IClock clock, ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            clock, logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            if (offsetsOnly)
            {
                var methodPointer = new FarPtr(0xFFFC, ordinal);
#if DEBUG
                //_logger.Debug($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
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
            return;
        }

        public void SetRegisters(CpuRegisters registers)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            return;
        }
    }
}

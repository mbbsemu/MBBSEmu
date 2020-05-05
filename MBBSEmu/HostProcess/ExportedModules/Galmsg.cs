using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Galmsg : ExportedModuleBase, IExportedModule
    {
        internal Galmsg(MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(module, channelDictionary)
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
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in GALMSG: {ordinal}");
            }

            return null;
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

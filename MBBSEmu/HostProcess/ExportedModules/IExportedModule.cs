using MBBSEmu.CPU;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public interface IExportedModule
    {
        ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false);
        void SetState(CpuRegisters registers, ushort channelNumber);
        void UpdateSession(ushort channelNumber);
    }
}
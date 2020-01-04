using System;
using MBBSEmu.CPU;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public interface IExportedModule
    {
        ReadOnlySpan<byte> Invoke(ushort ordinal);
        void SetState(CpuRegisters registers, ushort channelNumber);
    }
}

using MBBSEmu.CPU;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public interface IExportedModule : IDisposable
    {
        ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false);
        void SetState(ushort channelNumber);
        void SetRegisters(CpuRegisters registers);
        void UpdateSession(ushort channelNumber);
    }
}
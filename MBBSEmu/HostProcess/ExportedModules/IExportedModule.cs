using MBBSEmu.CPU;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public interface IExportedModule
    {
        ushort Invoke(ushort ordinal);
        void SetState(CpuRegisters registers, ushort channelNumber);
    }
}

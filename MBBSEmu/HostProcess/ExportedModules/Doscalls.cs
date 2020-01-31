using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Doscalls : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFB;

        internal Doscalls(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool onlyProperties = false)
        {
            switch (ordinal)
            {
                case 89:
                    return dossetvec;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Ordinal: {ordinal}");
            }
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

        private ReadOnlySpan<byte> dossetvec => new byte[] {0x0, 0x0, 0x0, 0x0};
    }
}

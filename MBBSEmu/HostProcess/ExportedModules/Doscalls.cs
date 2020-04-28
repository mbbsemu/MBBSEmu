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

        public const ushort DosSegmentBase = 0x200;
        public ushort DosSegmentOffset = 0;

        internal Doscalls(MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(module, channelDictionary)
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
                var methodPointer = new IntPtr16(Segment, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.ToSpan();
            }

            switch (ordinal)
            {
                case 34:
                    DosAllocSeg();
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

        public void DosAllocSeg()
        {
            var size = GetParameter(0);
            var selectorPointer = GetParameterPointer(1);
            var flags = GetParameter(3);

            Module.Memory.AddSegment((ushort) (DosSegmentBase + DosSegmentOffset));

            var segPointer = new IntPtr16(0x300,0x0);

            DosSegmentOffset++;

            Registers.AX = 0;
        }

        private ReadOnlySpan<byte> dossetvec => new byte[] {0x0, 0x0, 0x0, 0x0};
    }
}

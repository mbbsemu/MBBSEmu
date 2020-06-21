using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Phapi : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFD;

        internal Phapi(MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(module, channelDictionary)
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
        ///     TODO: Need to research what this does
        /// </summary>
        private void DosCreatedAlias()
        {
            //Set stack back to entry state, minus parameters
            var previousBP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 1));
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 3));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 5));

            Registers.SP += 12;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousBP);
            Registers.SP -= 2;
            Registers.BP = Registers.SP;

            Registers.AX = 1;
        }

    }
}

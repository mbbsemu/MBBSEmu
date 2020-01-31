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

        internal Phapi(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            throw new NotImplementedException();
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

    }
}

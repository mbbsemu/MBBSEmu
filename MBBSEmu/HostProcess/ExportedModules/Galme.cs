using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Galme : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFC;

        internal Galme(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
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
                case 123:
                    simpsnd();
                    break;
                case 248:
                    txtlen();
                    break;
            }

            return null;
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Simple Send Message Method
        ///
        ///     Signature: int simpsnd(struct message *msg, char *text, char *filatt)
        /// </summary>
        private void simpsnd()
        {
            var messageStructPointer = GetParameterPointer(0);
            var textPointer = GetParameterPointer(2);
            var fileAttachmentPointer = GetParameterPointer(4);

#if DEBUG
            _logger.Warn("Ignoring SIMPSND for now, messaging not enabled in MBBSEmu");
#endif

            Registers.AX = 1;
        }

        /// <summary>
        ///     Returns the maximum message length supported by this board
        ///
        ///     Signature: unsigned txtlen(void)
        /// </summary>
        private void txtlen()
        {
            Registers.AX = ushort.MaxValue;
        }
    }
}

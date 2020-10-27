using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
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

        internal Galme(ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
            var txtlenPointer = Module.Memory.AllocateVariable("TXTLEN", 0x2);
            Module.Memory.SetWord(txtlenPointer, 0x400);

        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            switch (ordinal)
            {
                case 248:
                    return _txtlen;
            }

            if (offsetsOnly)
            {
                var methodPointer = new IntPtr16(0xFFFC, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.Data;
            }

            switch (ordinal)
            {
                case 30:
                    oldsend();
                    break;
                case 123:
                    simpsnd();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in GALME: {ordinal}");
            }

            return null;
        }

        public void SetRegisters(CpuRegisters registers)
        {
            Registers = registers;
        }

        public void SetState(ushort channelNumber)
        {
            ChannelNumber = channelNumber;
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

        private void oldsend()
        {
#if DEBUG
            _logger.Warn("Ignoring OLDSEND for now, messaging not enabled in MBBSEmu");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Returns the maximum message length supported by this board
        ///
        ///     Signature: unsigned txtlen(void)
        /// </summary>
        private ReadOnlySpan<byte> _txtlen => Module.Memory.GetVariablePointer("TXTLEN").Data;
    }
}

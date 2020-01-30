using System;
using MBBSEmu.DependencyInjection;
using NLog;

namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single Relocation Record
    /// </summary>
    public class RelocationRecord
    {
        protected static readonly ILogger _logger;

        static RelocationRecord()
        {
            _logger = ServiceResolver.GetService<ILogger>();
        }

        public byte[] Data;
        public byte SourceType => Data[0];
        public EnumRecordsFlag Flag => (EnumRecordsFlag) Data[1];
        public ushort Offset => BitConverter.ToUInt16(Data, 2);

        public Tuple<EnumRecordsFlag, ushort, ushort, ushort> TargetTypeValueTuple
        {
            get
            {
                switch (Flag)
                {
                    case EnumRecordsFlag.InternalRefAdditive:
                    case EnumRecordsFlag.InternalRef:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.InternalRef, Data[4],
                            Data[5], BitConverter.ToUInt16(Data, 6));

                    case EnumRecordsFlag.ImportOrdinalAdditive:
                    case EnumRecordsFlag.ImportOrdinal:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.ImportOrdinal,
                            BitConverter.ToUInt16(Data, 4), BitConverter.ToUInt16(Data, 6), 0);

                    case EnumRecordsFlag.ImportNameAdditive:
                    case EnumRecordsFlag.ImportName:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.ImportName,
                            BitConverter.ToUInt16(Data, 4), BitConverter.ToUInt16(Data, 6), 0);
                    case EnumRecordsFlag.OSFIXUP:
                    case EnumRecordsFlag.OSFIXUPAdditive:
                        //_logger.Warn($"Ignoring OSFIXUP Flag");
                        return null;
                    default:
                        _logger.Warn($"Unknown Relocation Flag Value: {Flag} ({Convert.ToString((byte)Flag, 2)})");
                        return null;
                }
            }
        }
    }
}
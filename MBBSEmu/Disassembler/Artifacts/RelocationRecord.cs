using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single Relocation Record
    /// </summary>
    public class RelocationRecord
    {
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
                    case EnumRecordsFlag.INTERNALREF | EnumRecordsFlag.ADDITIVE:
                    case EnumRecordsFlag.INTERNALREF:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.INTERNALREF, Data[4],
                            Data[5], BitConverter.ToUInt16(Data, 6));

                    case EnumRecordsFlag.IMPORTORDINAL | EnumRecordsFlag.ADDITIVE:
                    case EnumRecordsFlag.IMPORTORDINAL:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.IMPORTORDINAL,
                            BitConverter.ToUInt16(Data, 4), BitConverter.ToUInt16(Data, 6), 0);

                    case EnumRecordsFlag.IMPORTNAME | EnumRecordsFlag.ADDITIVE:
                    case EnumRecordsFlag.IMPORTNAME:
                        return new Tuple<EnumRecordsFlag, ushort, ushort, ushort>(EnumRecordsFlag.IMPORTNAME,
                            BitConverter.ToUInt16(Data, 4), BitConverter.ToUInt16(Data, 6), 0);
                    default:
                        break;
                }

                return null;
            }
        }
    }
}
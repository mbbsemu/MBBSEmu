using MBBSEmu.Btrieve.Enums;
using System;

namespace MBBSEmu.Btrieve
{
    public class BtrieveKeyDefinition
    {
        public byte[] Data;
        public ushort TotalRecords => BitConverter.ToUInt16(Data, 0x6);
        public ushort Position => (ushort) (Offset + 1);
        public ushort Length => BitConverter.ToUInt16(Data, 0x16);
        public ushort RecordLength => (ushort) (Length + 8);
        public ushort Offset => BitConverter.ToUInt16(Data, 0x14);
        public EnumKeyDataType DataType => (EnumKeyDataType) Data[0x1C];
        public EnumKeyAttributeMask Attributes => (EnumKeyAttributeMask) Data[0x8];
        public ushort Number { get; set; }
        public bool Segment => Data[0x2] == 0;
        public ushort SegmentOf { get; set; }
    }
}

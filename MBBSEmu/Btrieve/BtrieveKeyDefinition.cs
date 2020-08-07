using MBBSEmu.Btrieve.Enums;
using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Key Definition with a Btrieve File
    /// </summary>
    public class BtrieveKeyDefinition
    {
        /// <summary>
        ///     Raw Data of the Key Definition
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        ///     Total Records that use/implement this key
        /// </summary>
        public ushort TotalRecords => BitConverter.ToUInt16(Data, 0x6);
        
        /// <summary>
        ///     Absolute Position in the file of this Key Definition
        /// </summary>
        public ushort Position => (ushort) (Offset + 1);

        /// <summary>
        ///     Data Length of the defined Key
        /// </summary>
        public ushort Length => BitConverter.ToUInt16(Data, 0x16);

        /// <summary>
        ///     Offset of the defined Key within the Btrieve Records
        /// </summary>
        public ushort Offset => BitConverter.ToUInt16(Data, 0x14);

        /// <summary>
        ///     Data Type of the defined Key
        /// </summary>
        public EnumKeyDataType DataType => (EnumKeyDataType) Data[0x1C];

        /// <summary>
        ///     Attributes of the defined Key
        /// </summary>
        public EnumKeyAttributeMask Attributes => (EnumKeyAttributeMask) Data[0x8];

        /// <summary>
        ///     Number (ordinal) of the defined Key
        /// </summary>
        public ushort Number { get; set; }

        /// <summary>
        ///     Defines if the Key is a Segment Key
        /// </summary>
        public bool Segment => Data[0x2] == 0;

        /// <summary>
        ///     If the defined Key is a Segment Key, which Key Number (ordinal) is it a Segment of
        /// </summary>
        public ushort SegmentOf { get; set; }
    }
}

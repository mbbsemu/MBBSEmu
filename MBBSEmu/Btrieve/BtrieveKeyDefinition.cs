using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Key Definition with a Btrieve File
    /// </summary>
    public class BtrieveKeyDefinition
    {
        public ushort Number { get; set; }
        public ushort Length { get; set; }
        public ushort Offset { get; set; }
        public EnumKeyDataType DataType { get; set; }
        public EnumKeyAttributeMask Attributes {get; set; }
        /// <summary>
        ///     Defines if the Key is a Segment Key
        /// </summary>
        public bool Segment { get; set; }

        /// <summary>
        ///     If the defined Key is a Segment Key, which Key Number (ordinal) is it a Segment of
        /// </summary>
        public ushort SegmentOf { get; set; }

        public int SegmentIndex { get; set; }

        public ushort Position => (ushort) (Offset + 1);

        public bool IsUnique { get => (Attributes & EnumKeyAttributeMask.Duplicates) == 0; }

        public bool AllowDuplicates { get => !IsUnique; }

        public bool IsNumeric
        {
            get
            {
                switch (DataType)
                {
                    case EnumKeyDataType.Integer:
                    case EnumKeyDataType.AutoInc:
                    case EnumKeyDataType.Unsigned:
                    case EnumKeyDataType.UnsignedBinary:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}

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

        public byte NullValue { get; set; }

        public ushort Position => (ushort) (Offset + 1);

        public bool IsModifiable { get => Attributes.HasFlag(EnumKeyAttributeMask.Modifiable); }

        public bool IsUnique { get => !AllowDuplicates; }

        public bool AllowDuplicates {
            get
            {
                return Attributes.HasFlag(EnumKeyAttributeMask.Duplicates)
                    || Attributes.HasFlag(EnumKeyAttributeMask.RepeatingDuplicatesKey);
            }
        }

        public bool IsNullable { get =>
            Attributes.HasFlag(EnumKeyAttributeMask.NullAllSegments)
                || Attributes.HasFlag(EnumKeyAttributeMask.NullAnySegment)
                //|| IsString; // string is implicitly nullable
                || Attributes.HasFlag(EnumKeyAttributeMask.OldStyleBinary);
        }

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

        public bool IsString
        {
            get
            {
                switch (DataType)
                {
                    case EnumKeyDataType.String:
                    case EnumKeyDataType.Lstring:
                    case EnumKeyDataType.Zstring:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}

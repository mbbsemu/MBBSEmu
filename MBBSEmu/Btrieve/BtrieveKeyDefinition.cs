using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Key Definition with a Btrieve File. Keys are made up of one or more
    ///     BtrieveKeyDefinitions.
    /// </summary>
    public class BtrieveKeyDefinition
    {
        /// <summary>
        ///     The number of the key, starting from 0. Databases have at least one key.
        /// </summary>
        /// <value></value>
        public ushort Number { get; set; }
        /// <summary>
        ///     The length of the key in bytes.
        /// </summary>
        public ushort Length { get; set; }
        /// <summary>
        ///     The offset within the record where the key bytes are contained.
        /// </summary>
        public ushort Offset { get; set; }
        /// <summary>
        ///     The data type of the key.
        /// </summary>
        public EnumKeyDataType DataType { get; set; }
        /// <summary>
        ///     Key attribute flags.
        /// </summary>
        public EnumKeyAttributeMask Attributes { get; set; }

        /// <summary>
        ///     Defines if the Key is a Segment Key. The PrimarySegment of a composite key has this
        ///     value set.
        /// </summary>
        public bool Segment { get; set; }

        /// <summary>
        ///     If the defined Key is a Segment Key, which Key Number (ordinal) it is a Segment of.
        /// </summary>
        public ushort SegmentOf { get; set; }

        /// <summary>
        ///     Sequential index of this key in a composite key.
        /// </summary>
        public int SegmentIndex { get; set; }

        /// <summary>
        ///     The null value associated with this key. Only valid if nullable.
        /// </summary>
        public byte NullValue { get; set; }

        /// <summary>
        ///     The offset of the key as a position, which starts at 1 in Btrieve. This value is
        ///     simply Offset + 1.
        /// </summary>
        public ushort Position => (ushort)(Offset + 1);

        /// <summary>
        ///     Whether this key uses the ACS table.
        /// </summary>
        public bool RequiresACS => Attributes.HasFlag(EnumKeyAttributeMask.NumberedACS);

        /// <summary>
        ///     The ACS table used by this key.
        /// </summary>
        public byte[] ACS { get; set; }

        /// <summary>
        ///     Whether the key inside the record can be modified after being inserted.
        /// </summary>
        public bool IsModifiable => Attributes.HasFlag(EnumKeyAttributeMask.Modifiable);

        /// <summary>
        ///     Whether the key is unique, i.e. does not allow duplicates.
        /// </summary>
        public bool IsUnique => !AllowDuplicates;

        /// <summary>
        ///     Whether the key allows duplicates.
        /// </summary>
        public bool AllowDuplicates =>
            Attributes.HasFlag(EnumKeyAttributeMask.Duplicates)
            || Attributes.HasFlag(EnumKeyAttributeMask.RepeatingDuplicatesKey);

        /// <summary>
        ///     Whether the key allows null values.
        /// </summary>
        public bool IsNullable =>
            Attributes.HasFlag(EnumKeyAttributeMask.NullAllSegments)
            || Attributes.HasFlag(EnumKeyAttributeMask.NullAnySegment)
            || IsString; // string is implicitly nullable

        /// <summary>
        ///     Whether the key is a string type.
        /// </summary>
        public bool IsString
        {
            get
            {
                switch (DataType)
                {
                    case EnumKeyDataType.String:
                    case EnumKeyDataType.Lstring:
                    case EnumKeyDataType.Zstring:
                    case EnumKeyDataType.OldAscii:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}

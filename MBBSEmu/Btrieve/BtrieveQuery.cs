using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Btrieve Query that is executed against a given Btrieve File
    /// </summary>
    public class BtrieveQuery
    {
        /// <summary>
        ///     Key Value to be queried on
        /// </summary>
        public byte[] Key { get; set; }

        /// <summary>
        ///     Data Type of the given Key
        /// </summary>
        public EnumKeyDataType KeyDataType { get; set; }

        /// <summary>
        ///     Length of the given Key
        /// </summary>
        public ushort KeyLength { get; set; }

        /// <summary>
        ///     Offset within the record of the given Key
        /// </summary>
        public ushort KeyOffset { get; set; }
    }
}
using System;

namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Btrieve Key Attribute BitMask used on Key Definitions
    /// </summary>
    [Flags]
    public enum EnumKeyAttributeMask : ushort
    {
        Duplicates = 1,
        Modifiable = 1 << 1,
        OldStyleBinary = 1 << 2,
        NullAllSegments = 1 << 3,
        SegmentedKey = 1 << 4,
        NumberedACS = 1 << 5,
        DescendingKeySegment = 1 << 6,
        RepeatingDuplicatesKey = 1 << 7,
        UseExtendedDataType = 1 << 8,
        NullAnySegment = 1 << 9,
    }
}

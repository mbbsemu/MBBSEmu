using System;

namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Btrieve Key Attribute BitMask
    /// </summary>
    [Flags]
    public enum EnumKeyAttributeMask : byte
    {
        Duplicates = 1,
        Modifiable = 1 << 1,
        OldStyleBinary = 1 << 2,
        Null0 = 1 << 3,
        SegmentedKey = 1 << 4,
        NumberedACS = 1 << 5,
        DescendingKeySegment = 1 << 6,
        SupplementalKey = 1 << 7
    }
}

namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Data Types Defined for Btrieve Keys
    /// </summary>
    public enum EnumKeyDataType : byte
    {
        String = 0,
        Integer = 1,
        Float = 2,
        Date = 3,
        Time = 4,
        Decimal = 5,
        Money = 6,
        Logical = 7,
        Numeric = 8,
        Bfloat = 9,
        Lstring = 0xA,
        Zstring = 0xB,
        Unsigned = 0xD,
        AutoInc = 0xF
    }
}

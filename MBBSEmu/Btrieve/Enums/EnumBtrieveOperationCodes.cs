namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Btrieve Operation Codes that are passed into Btrieve
    /// </summary>
    public enum EnumBtrieveOperationCodes : ushort
    {
        //Utility
        Open = 0x0,
        Close = 0x1,

        //Get Operations
        GetEqual = 0x5,
        GetNext = 0x6,
        GetPrevious = 0x7,
        GetGreater = 0x8,
        GetGreaterOrEqual = 0x9,
        GetLess = 0xA,
        GetLessOrEqual = 0xB,
        GetFirst = 0xC,
        GetLast = 0xD,

        //Information Operations
        Stat = 0xF,
        SetOwner = 0x1D,

        //Step Operations
        StepFirst = 0x21,
        StepLast = 0x22,
        StepNext = 0x18,
        StepNextExtended = 0x26,
        StepPrevious = 0x23,
        StepPreviousExtended = 0x27,

        //Get Key Operations
        GetKeyEqual = 0x37,
        GetKeyNext = 0x38,
        GetKeyGreater = 0x3A,
        GetKeyGreaterOrEqual = 0x3B,
        GetKeyLess = 0x3C,
        GetKeyLessOrEqual = 0x3D,
        GetKeyFirst = 0x3E,
        GetKeyLast = 0x3F,

        None = ushort.MaxValue
    }
}

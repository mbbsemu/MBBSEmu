namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Btrieve Operation Codes that are passed into Btrieve
    /// </summary>
    public enum EnumBtrieveOperationCodes : ushort
    {
        //Get Operations
        GetEqual = 0x5,
        GetNext = 0x6,
        GetPrevious = 0x7,
        GetGreater = 0x8,
        GetGreaterThanOrEqual = 0x9,
        GetLessThan = 0xA,
        GetLessThanOrEqual = 0xB,
        GetFirst = 0xC,
        GetLast = 0xD,

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
        GetKeyFirst = 0x3E,
        GetKeyLast = 0x3F,

        None = ushort.MaxValue
    }
}

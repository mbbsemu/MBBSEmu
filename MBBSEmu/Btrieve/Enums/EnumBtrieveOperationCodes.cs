namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Btrieve Operation Codes that are passed into Btrieve
    /// </summary>
    public enum EnumBtrieveOperationCodes : ushort
    {
        StepFirst = 0x21,
        StepLast = 0x22,
        StepNext = 0x18,
        StepNextExtended = 0x26,
        StepPrevious = 0x23,
        StepPreviousExtended = 0x27
    }
}

namespace MBBSEmu.Host
{
    /// <summary>
    ///     When Passing Pointers Back/Forth Between the Module and the Host,
    ///     we use these segment identifiers to identify which area of "memory"
    ///     the specified offset lives in on the host process
    /// </summary>
    public enum EnumHostSegments 
    {
        MemoryPointer = 0xFFFF,
        FilePointer = 0xFFFE,
        BtrPointer = 0xFFFD,
        MsgPointer = 0xFFFC,
        UserPointer = 0xFFFB,
        ChannelArrayPointer = 0xFFFA 
    }
}

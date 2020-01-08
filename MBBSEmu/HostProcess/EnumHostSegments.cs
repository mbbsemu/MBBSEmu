namespace MBBSEmu.HostProcess
{
    /// <summary>
    ///     When Passing Pointers Back/Forth Between the Module and the Host,
    ///     we use these segment identifiers to identify which area of "memory"
    ///     the specified offset lives in on the host process
    /// </summary>
    public enum EnumHostSegments : ushort
    {

        /// <summary>
        ///     Base Segment for Host Memory
        ///     Valid Range: 0x200 -> 0x2FF
        /// </summary>
        HostMemorySegmentBase = 0x200,

    }
}

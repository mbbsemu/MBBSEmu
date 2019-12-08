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
        ///     Segment used by ALLOCZ() and other methods to allocate blocks of memory
        ///     for later use
        /// </summary>
        HostMemorySegment = 0xFFFF,

        /// <summary>
        ///     Denotes Segment for holding Msg File Pointers
        ///
        ///     This is used to identify if a 16:16 pointer is a valid Msg pointer or not
        /// </summary>
        Msg = 0xFFFE,

        /// <summary>
        ///     Segment that holds the user struct
        /// </summary>
        User = 0xFFFD,

        /// <summary>
        ///     Segment that holds the channel array
        /// </summary>
        ChannelArray = 0xFFFC,

        /// <summary>
        ///     Denotes Segment for holding Btrieve File Pointers
        ///
        ///     This is used to verify a 16:16 pointer is a valid Btrieve pointer or not
        /// </summary>
        BtrieveFile = 0xFFFB,

        /// <summary>
        ///     Segment that holds the registration number for this instance of MajorBBS  
        /// </summary>
        Bturno = 0xEFFF
    }
}

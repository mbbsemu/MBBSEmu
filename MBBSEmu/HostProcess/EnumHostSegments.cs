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
        ///     Segment for int usernum
        /// </summary>
        UserNum = 0xFFFA,

        /// <summary>
        ///     Segment for int status (raw status from btusts)
        /// </summary>
        Status = 0xFFF9,

        /// <summary>
        ///     UsrAcc Struct for Retrieved User Information
        /// </summary>
        UsrAcc = 0xFFF8,

        /// <summary>
        ///     Output Buffer Segment
        /// </summary>
        Prfbuf = 0xFFF7,

        /// <summary>
        ///     Number of Channels Defined
        /// </summary>
        Nterms = 0xFFF5,

        /// <summary>
        ///     Pointer for the Current User Struct in the User Array
        ///
        ///     Example: var user = User[UserPtr];
        /// </summary>
        UserPtr = 0xFFF4,

        /// <summary>
        ///     Segment that contains the full array of User structs for each channel
        /// </summary>
        User = 0xFFF3,

        /// <summary>
        ///     Segment that holds the registration number for this instance of MajorBBS  
        /// </summary>
        Bturno = 0xEFFF,

        /// <summary>
        ///     Volatile Data Segment
        ///     Valid Range is 0xA01 - 0xAFF
        /// </summary>
        VolatileDataSegment = 0x0A00,

        /// <summary>
        ///     Stack Segment
        /// </summary>
        StackSegment = 0x0
    }
}

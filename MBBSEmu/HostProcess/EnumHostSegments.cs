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
        ///     Variables (non-pointer, such as bturno[], margs[], etc.) that are hosted in the Host Process
        ///     require their own segment for access. We use this base as our starting point for up to 255
        ///     host variables.
        ///     Valid Range: 0x500->0x5FF
        /// </summary>
        VariableSegmentBase = 0x500,

        /// <summary>
        ///     Segments which hold the 4 byte pointers to variables within MAJORBBS.H
        ///     Valid Range: 0x400 -> 0x440
        /// </summary>
        VariablePointerSegmentBase = 0x400,

        /// <summary>
        ///     Base Segment for Volatile Data
        ///     Valid Range: 0xA01 -> 0xAFF
        /// </summary>
        VolatileDataSegmentBase = 0x0300,
        
        /// <summary>
        ///     Base Segment for Host Memory
        ///     Valid Range: 0x200 -> 0x2FF
        /// </summary>
        HostMemorySegmentBase = 0x200,

        /// <summary>
        ///     Base Segment for File Stream Pointers
        ///     Valid Range: 0x100 -> 0x1FF
        /// </summary>
        FilePointerSegmentBase = 0x0100,

        /// <summary>
        ///     Stack Segment
        /// </summary>
        StackSegment = 0x0
    }
}

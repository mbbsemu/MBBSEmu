namespace MBBSEmu.Btrieve.Enums
{
    /// <summary>
    ///     Specifies whether the operation code requires a key value.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class RequiresKey : System.Attribute {}

    /// <summary>
    ///     Specifies whether the operation code operates on a previous query.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class UsesPreviousQuery : System.Attribute {}

    /// <summary>
    ///     Specifies whether the operation code results in data being acquired.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class AcquiresData : System.Attribute {}

    /// <summary>
    ///     Specifies whether the operation code results in key data being queried.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class QueryOnly : System.Attribute {}

    /// <summary>
    ///     Btrieve Operation Codes that are passed into Btrieve
    /// </summary>
    public enum EnumBtrieveOperationCodes : ushort
    {
        // Utility
        Open = 0x0,
        Close = 0x1,
        Insert = 0x2,
        Update = 0x3,
        Delete = 0x4,

        // Acquire Operations
        [AcquiresData]
        [RequiresKey]
        AcquireEqual = 0x5,

        [AcquiresData]
        [UsesPreviousQuery]
        AcquireNext = 0x6,

        [AcquiresData]
        [UsesPreviousQuery]
        AcquirePrevious = 0x7,

        [AcquiresData]
        [RequiresKey]
        AcquireGreater = 0x8,

        [AcquiresData]
        [RequiresKey]
        AcquireGreaterOrEqual = 0x9,

        [AcquiresData]
        [RequiresKey]
        AcquireLess = 0xA,

        [AcquiresData]
        [RequiresKey]
        AcquireLessOrEqual = 0xB,

        [AcquiresData]
        AcquireFirst = 0xC,

        [AcquiresData]
        AcquireLast = 0xD,

        Create = 0xE,
        // Information Operations
        Stat = 0xF,
        Extend = 0x10,
        GetPosition = 0x16,
        GetDirectChunkOrRecord = 0x17,
        SetOwner = 0x1D,

        // Step Operations, operates on physical offset not keys
        [AcquiresData]
        StepFirst = 0x21,

        [AcquiresData]
        StepLast = 0x22,

        [AcquiresData]
        [UsesPreviousQuery]
        StepNext = 0x18,

        [AcquiresData]
        [UsesPreviousQuery]
        StepNextExtended = 0x26,

        [AcquiresData]
        [UsesPreviousQuery]
        StepPrevious = 0x23,

        [AcquiresData]
        [UsesPreviousQuery]
        StepPreviousExtended = 0x27,

        // Query Operations
        [QueryOnly]
        [RequiresKey]
        QueryEqual = 0x37,

        [QueryOnly]
        [UsesPreviousQuery]
        QueryNext = 0x38,

        [QueryOnly]
        [UsesPreviousQuery]
        QueryPrevious = 0x39,

        [QueryOnly]
        [RequiresKey]
        QueryGreater = 0x3A,

        [QueryOnly]
        [RequiresKey]
        QueryGreaterOrEqual = 0x3B,

        [QueryOnly]
        [RequiresKey]
        QueryLess = 0x3C,

        [QueryOnly]
        [RequiresKey]
        QueryLessOrEqual = 0x3D,

        [QueryOnly]
        QueryFirst = 0x3E,

        [QueryOnly]
        QueryLast = 0x3F,

        None = ushort.MaxValue
    }

    /// <summary>
    /// Btrieve error codes
    /// </summary>
    public enum BtrieveError : ushort
    {
        Success = 0,
        InvalidOperation = 1,
        IOError = 2,
        FileNotOpen = 3,
        KeyValueNotFound = 4,
        DuplicateKeyValue = 5,
        InvalidKeyNumber = 6,
        DifferentKeyNumber = 7,
        InvalidPositioning = 8,
        EOF = 9,
        NonModifiableKeyValue = 10,
        InvalidFileName = 11,
        FileNotFound = 12,
        ExtendedFileError = 13,
        PreImageOpenError = 14,
        PreImageIOError = 15,
        ExpansionError = 16,
        CloseError = 17,
        DiskFull = 18,
        UnrecoverableError = 19,
        RecordManagerInactive = 20,
        KeyBufferTooShort = 21,
        DataBufferLengthOverrun = 22,
        PositionBlockLength = 23,
        PageSizeError = 24,
        CreateIOError = 25,
        InvalidNumberOfKeys = 26,
        InvalidKeyPosition = 27,
        BadRecordLength = 28,
        BadKeyLength = 29,
        NotBtrieveFile = 30,
        TransactionIsActive = 37,
        /* Btrieve version 5.x returns this status code
if you attempt to perform a Step, Update, or Delete operation on a
key-only file or a Get operation on a data only file */
        OperationNotAllowed = 41,
        AccessDenied = 46,
        InvalidInterface = 53,
    }

    public static class Extensions
    {
        public static bool RequiresKey(this EnumBtrieveOperationCodes code)
        {
            var memberInstance = code.GetType().GetMember(code.ToString());
            if (memberInstance.Length <= 0) return false;

            return System.Attribute.GetCustomAttribute(memberInstance[0], typeof(RequiresKey)) != null;
        }

        public static bool UsesPreviousQuery(this EnumBtrieveOperationCodes code)
        {
            var memberInstance = code.GetType().GetMember(code.ToString());
            if (memberInstance.Length <= 0) return false;

            return System.Attribute.GetCustomAttribute(memberInstance[0], typeof(UsesPreviousQuery)) != null;
        }

        public static bool AcquiresData(this EnumBtrieveOperationCodes code)
        {
            var memberInstance = code.GetType().GetMember(code.ToString());
            if (memberInstance.Length <= 0) return false;

            return System.Attribute.GetCustomAttribute(memberInstance[0], typeof(AcquiresData)) != null;
        }
    }
}

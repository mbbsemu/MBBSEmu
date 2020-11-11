namespace MBBSEmu.Btrieve.Enums
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class UsesPreviousQuery : System.Attribute {}

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class AcquiresData : System.Attribute {}

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

        // Acquire Operations
        [AcquiresData]
        AcquireEqual = 0x5,
        [AcquiresData]
        [UsesPreviousQuery]
        AcquireNext = 0x6,
        [AcquiresData]
        [UsesPreviousQuery]
        AcquirePrevious = 0x7,
        [AcquiresData]
        AcquireGreater = 0x8,
        [AcquiresData]
        AcquireGreaterOrEqual = 0x9,
        [AcquiresData]
        AcquireLess = 0xA,
        [AcquiresData]
        AcquireLessOrEqual = 0xB,
        [AcquiresData]
        AcquireFirst = 0xC,
        [AcquiresData]
        AcquireLast = 0xD,

        // Information Operations
        Stat = 0xF,
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
        QueryEqual = 0x37,
        [QueryOnly]
        [UsesPreviousQuery]
        QueryNext = 0x38,
        [QueryOnly]
        [UsesPreviousQuery]
        QueryPrevious = 0x39,
        [QueryOnly]
        QueryGreater = 0x3A,
        [QueryOnly]
        QueryGreaterOrEqual = 0x3B,
        [QueryOnly]
        QueryLess = 0x3C,
        [QueryOnly]
        QueryLessOrEqual = 0x3D,
        [QueryOnly]
        QueryFirst = 0x3E,
        [QueryOnly]
        QueryLast = 0x3F,

        None = ushort.MaxValue
    }

    public static class Extensions
    {
        public static bool UsesPreviousQuery(this EnumBtrieveOperationCodes code)
        {
            var memberInstance = code.GetType().GetMember(code.ToString());
            if (memberInstance == null || memberInstance.Length <= 0) return false;

            return System.Attribute.GetCustomAttribute(memberInstance[0], typeof(UsesPreviousQuery)) != null;
        }

        public static bool AcquiresData(this EnumBtrieveOperationCodes code)
        {
            var memberInstance = code.GetType().GetMember(code.ToString());
            if (memberInstance == null || memberInstance.Length <= 0) return false;

            return System.Attribute.GetCustomAttribute(memberInstance[0], typeof(AcquiresData)) != null;
        }
    }
}

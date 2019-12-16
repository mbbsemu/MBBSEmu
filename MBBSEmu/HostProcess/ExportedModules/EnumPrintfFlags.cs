using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Flags specified for printf
    ///
    ///     More Info: http://www.cplusplus.com/reference/cstdio/printf/
    /// </summary>
    [Flags]
    public enum EnumPrintfFlags
    {
        None = 0,
        LeftJustify = 1,
        Signed = 1 << 1,
        Space = 1 << 2,
        DecimalOrHex = 1 << 3,
        LeftPadZero = 1 << 4
    }
}

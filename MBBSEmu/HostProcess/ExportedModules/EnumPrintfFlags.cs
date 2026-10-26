using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Flags specified for printf
    ///
    ///     See PrintfFormatter for the Borland C++ semantics of each flag
    /// </summary>
    [Flags]
    public enum EnumPrintfFlags
    {
        None = 0,
        LeftJustify = 1,
        Signed = 1 << 1,
        Space = 1 << 2,
        AlternateForm = 1 << 3,
        LeftPadZero = 1 << 4
    }
}

using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     File Access Flags passed into C++ FOPEN method
    ///
    ///     Reference: http://www.cplusplus.com/reference/cstdio/fopen/
    /// </summary>
    [Flags]
    public enum EnumFileAccessFlags
    {
        Read = 1,
        Write = 1 << 1,
        Append = 1 << 2,
        Update = 1 << 3,
        Binary = 1 << 4,
        Text = 1 << 5
    }
}

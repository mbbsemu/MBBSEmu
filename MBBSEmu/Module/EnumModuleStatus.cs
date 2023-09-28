using System;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Enumerator used to specify the status of a Module
    /// </summary>
    [Flags]
    public enum EnumModuleStatus
    {
        /// <summary>
        ///     Module is Enabled and running
        /// </summary>
        Enabled,

        /// <summary>
        ///    Module is Disabled and not running
        /// </summary>
        Disabled,

        /// <summary>
        ///     Module has crashed
        /// </summary>
        Crashed
    }
}

using System;

namespace MBBSEmu.HostProcess.Attributes
{
    /// <summary>
    ///     Attribute used to denote an Exported Module
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    class ExportedModuleAttribute : Attribute
    {
        /// <summary>
        ///     Module Name as it would appear in the compiled Imported Name Table
        /// </summary>
        public string Name;
    }
}

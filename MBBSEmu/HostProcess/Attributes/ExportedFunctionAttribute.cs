using System;

namespace MBBSEmu.HostProcess.Attributes
{
    /// <summary>
    ///     Attribute used to decorate an Exported Function within an Exported Module
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ExportedFunctionAttribute : Attribute
    {
        /// <summary>
        ///     Ordinal as it would appear in the Module's .H file
        /// </summary>
        public ushort Ordinal;

        /// <summary>
        ///     Name of the Exported Function
        /// </summary>
        public string Name;
    }
}

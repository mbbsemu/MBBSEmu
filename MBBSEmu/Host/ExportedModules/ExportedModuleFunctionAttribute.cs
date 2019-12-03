using System;

namespace MBBSEmu.Host.ExportedModules
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ExportedModuleAttribute : Attribute
    {
        public int Ordinal;
        public string Name;
        public EnumExportedModuleType ExportedModuleType;
    }
}

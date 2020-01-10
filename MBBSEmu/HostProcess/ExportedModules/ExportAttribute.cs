using System;

namespace MBBSEmu.HostProcess.ExportedModules
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ExportAttribute : Attribute
    {
        public string Name { get; set; }
        public int Ordinal { get; set; }

        public ExportAttribute(string name, int ordinal)
        {
            Name = name;
            Ordinal = ordinal;
        }
    }
}

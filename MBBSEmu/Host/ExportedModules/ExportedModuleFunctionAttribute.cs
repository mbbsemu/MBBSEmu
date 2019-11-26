using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Host
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ExportedModuleFunctionAttribute : Attribute
    {
        public int Ordinal;
        public string Name;
    }
}

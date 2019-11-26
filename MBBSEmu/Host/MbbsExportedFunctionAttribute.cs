using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Host
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class MbbsExportedFunctionAttribute : Attribute
    {
        public int Ordinal;
        public string Name;
    }
}

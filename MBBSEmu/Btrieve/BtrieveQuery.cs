using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.Btrieve
{
    public class BtrieveQuery
    {
        public byte[] Key { get; set; }

        public EnumKeyDataType KeyDataType { get; set; }

        public ushort KeyLength { get; set; }

        public ushort KeyOffset { get; set; }
    }
}
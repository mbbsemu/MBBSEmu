using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Btrieve
{
    public class BtrieveKey
    {
        public BtrieveKeyDefinition Definition { get; set; }
        public List<BtrieveKeyRecord> Keys { get; set; }

        public BtrieveKey()
        {
            Keys = new List<BtrieveKeyRecord>();
        }
    }
}

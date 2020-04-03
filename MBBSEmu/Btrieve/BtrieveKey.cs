using System.Collections.Generic;

namespace MBBSEmu.Btrieve
{
    public class BtrieveKey
    {
        public List<BtrieveKeyDefinition> Segments { get; set; }

        public BtrieveKey()
        {
            Segments = new List<BtrieveKeyDefinition>();
        }

        public BtrieveKey(BtrieveKeyDefinition keyDefinition)
        {
            Segments = new List<BtrieveKeyDefinition>();
            Segments.Add(keyDefinition);
        }
    }
}

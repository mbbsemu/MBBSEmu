using System.Collections.Generic;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a defined Btrieve Key entity
    ///
    ///     Btrieve Keys can contain N segments. By default Keys have one segment
    /// </summary>
    public class BtrieveKey
    {
        public List<BtrieveKeyDefinition> Segments { get; set; }

        public BtrieveKey()
        {
            Segments = new List<BtrieveKeyDefinition>();
        }

        public BtrieveKey(BtrieveKeyDefinition keyDefinition)
        {
            Segments = new List<BtrieveKeyDefinition> {keyDefinition};
        }
    }
}

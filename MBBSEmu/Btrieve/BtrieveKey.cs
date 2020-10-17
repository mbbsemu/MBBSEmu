using System.Collections.Generic;
using System.Linq;

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

        public BtrieveKeyDefinition PrimarySegment
        {
            get => Segments[0];
        }

        public bool IsComposite
        {
            get => Segments.Count > 1;
        }

        public int Length
        {
            get => Segments.Sum(segment => segment.Length);
        }

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

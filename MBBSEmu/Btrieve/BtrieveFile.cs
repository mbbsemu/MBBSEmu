using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFile
    {
        /// <summary>
        ///     Filename of Btrieve File
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        ///     Total Number of Records in Btrieve File
        /// </summary>
        public ushort RecordCount { get; set; }

        /// <summary>
        ///     Maximum Length of Records in Btrieve File
        ///     Used with Btrieve Files which implement Variable Length Records
        /// </summary>
        public ushort MaxRecordLength { get; set; }

        /// <summary>
        ///     Record Length of Records in Btrieve File
        /// </summary>
        public ushort RecordLength { get; set; }

        /// <summary>
        ///     Length of Pages within the Btrieve File
        /// </summary>
        public ushort PageLength { get; set; }

        /// <summary>
        ///     Number of Pages within the Btrieve File
        /// </summary>
        public ushort PageCount { get; set; }

        /// <summary>
        ///     Number of Keys Defined in the Btrieve File
        /// </summary>
        public ushort KeyCount { get; set; }

        /// <summary>
        ///     Raw contents of Btrieve File
        /// </summary>
        [JsonIgnore]
        public byte[] Data { get; set; }

        /// <summary>
        ///     Btrieve Records
        /// </summary>
        public List<BtrieveRecord> Records { get; set; }

        /// <summary>
        ///     Btrieve Keys
        /// </summary>
        public Dictionary<ushort, BtrieveKey> Keys { get; set; }

        public BtrieveFile()
        {
            Records = new List<BtrieveRecord>();
            Keys = new Dictionary<ushort, BtrieveKey>();
        }
    }
}

using System.Data.SQLite;
using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Btrieve Query that is executed against a given Btrieve File
    /// </summary>
    public class BtrieveQuery : IDisposable
    {
        /// <summary>
        ///     Key Value to be queried on
        /// </summary>
        public byte[] Key { get; set; }

        /// <summary>
        ///     Key Definition
        /// </summary>
        public BtrieveKeyDefinition KeyDefinition { get; set; }

        public uint Position { get; set; }

        public SQLiteDataReader Reader { get; set; }

        public BtrieveQuery()
        {
            Position = 0;
        }

        public void Dispose()
        {
            Reader?.Dispose();
        }
    }
}

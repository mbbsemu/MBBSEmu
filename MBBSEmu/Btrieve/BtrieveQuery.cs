using Microsoft.Data.Sqlite;
using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Btrieve Query that is executed against a given Btrieve File. Remember to
    ///     dispose of this object when it is no longer needed.
    /// </summary>
    public class BtrieveQuery : IDisposable
    {
        /// <summary>
        ///     Wrapper class that contains both a SqliteDataReader and its associated SqliteCommand
        ///     so that you can close both via Dispose.
        /// </summary>
        public class SqliteReader : IDisposable
        {
            public SqliteCommand Command { get; set; }
            public SqliteDataReader DataReader { get; set; }

            public bool Read() => DataReader.Read();

            public void Dispose()
            {
                DataReader?.Dispose();
                Command?.Dispose();
            }
        }

        public enum CursorDirection {
            Forward,
            Reverse
        }

        public CursorDirection Direction { get; set; }

        /// <summary>
        ///     Initial Key Value to be queried on
        /// </summary>
        public byte[] KeyData { get; set; }

        /// <summary>
        ///     Last Key Value retrieved during GetNext/GetPrevious cursor movement,
        ///     as a Sqlite object.
        /// </summary>
        public object LastKey { get; set; }

        /// <summary>
        ///     Key Definition
        /// </summary>
        public BtrieveKey Key { get; set; }

        /// <summary>
        ///     Current position of the query. Changes as GetNext/GetPrevious is called
        /// </summary>
        /// <value></value>
        public uint Position { get; set; }

        public SqliteReader Reader { get; set; }

        public BtrieveQuery()
        {
            Position = 0;
            Direction = CursorDirection.Forward;
        }

        public void Dispose()
        {
            Reader?.Dispose();
            Reader = null;
        }
    }
}

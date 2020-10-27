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

        /// <summary>
        ///     Delegate to invoke to fetch the next data reader. Return null to stop the GetNext()
        ///     flow. You can chain endlessly by keeping ContinuationReader set, but if you want
        ///     the enumeration to end, set thisQuery.ContinuationReader back to null in the
        ///     delegate.
        /// </summary>
        public delegate SqliteReader GetContinuationReader(BtrieveQuery thisQuery);

        /// <summary>
        ///     Key Value to be queried on
        /// </summary>
        public byte[] KeyData { get; set; }

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

        /// <summary>
        ///     A delegate to invoke after which the last record has been read. Allows the
        ///     continuation of a query -> getNext() flow by creating a new SQLiteDataReader that
        ///     returns more records.
        /// </summary>
        public GetContinuationReader ContinuationReader { get; set; }

        public BtrieveQuery()
        {
            Position = 0;
        }

        public void Dispose()
        {
            Reader?.Dispose();
            Reader = null;
        }
    }
}

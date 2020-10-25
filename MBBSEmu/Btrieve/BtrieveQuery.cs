﻿using System.Data.SQLite;
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
        ///     Delegate to invoke to fetch the next data reader. Return null to stop the GetNext()
        ///     flow. You can chain endlessly by keeping ContinuationReader set, but if you want
        ///     the enumeration to end, set thisQuery.ContinuationReader back to null in the
        ///     delegate.
        /// </summary>
        public delegate SQLiteDataReader GetContinuationReader(BtrieveQuery thisQuery);

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

        public SQLiteDataReader Reader { get; set; }
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
            Reader?.Close();
            Reader?.Dispose();
        }
    }
}

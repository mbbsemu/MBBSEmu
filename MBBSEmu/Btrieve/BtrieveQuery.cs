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
                // Purposefully not Disposing of Command because it's likely cached by BtrieveFileProcessor, so let that class
                // handle the cleanup
            }
        }

        public enum CursorDirection
        {
            Seek,
            Forward,
            Reverse
        }

        /// <summary>
        ///     The BtrieveFileProcessor associated with this query
        /// </summary>
        public BtrieveFileProcessor Processor { get; init; }

        /// <summary>
        ///     The direction this cursor is currently moving along.
        /// </summary>
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

        private SqliteReader _reader;
        public SqliteReader Reader
        {
            get => _reader;
            set // overloaded so that we can call Dispose() on Reader when changed/nulled
            {
                if (_reader != value)
                {
                    _reader?.Dispose();
                    _reader = value;
                }
            }
        }

        public BtrieveQuery(BtrieveFileProcessor processor)
        {
            Position = 0;
            Direction = CursorDirection.Forward;
            Processor = processor;
        }

        public void Dispose()
        {
            Reader = null;
        }

        /// <summary>
        ///     Moves along the cursor until we hit position
        /// </summary>
        private void SeekTo(uint position)
        {
            while (Reader.Read())
            {
                var cursorPosition = (uint)Reader.DataReader.GetInt32(0);
                if (cursorPosition == position)
                    return;
            }

            // at end, nothing left
            Reader = null;
        }

        private void ChangeDirection(CursorDirection newDirection)
        {
            if (LastKey == null) // no successful prior query, so abort
                return;

            var sql = $"SELECT id, {Key.SqliteKeyName}, data FROM data_t WHERE {Key.SqliteKeyName} ";
            switch (newDirection)
            {
                case CursorDirection.Forward:
                    sql += $">= @value ORDER BY {Key.SqliteKeyName} ASC";
                    break;
                case CursorDirection.Reverse:
                    sql += $"<= @value ORDER BY {Key.SqliteKeyName} DESC";
                    break;
                default:
                    throw new ArgumentException($"Bad direction: {newDirection}");
            }

            var command = Processor.GetSqliteCommand(sql);
            command.Parameters.AddWithValue("@value", LastKey);

            Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            Direction = newDirection;
            // due to duplicate keys, we need to seek past the current position since we might serve
            // data already served.
            //
            // For example, if you have 4 identical keys with id 1,2,3,4 and are currently at id 2
            // and seek previous expecting id 1, sqlite might return a cursor counting from 4,3,2,1
            // and the cursor would point to 4, returning the wrong result. This next call skips
            // 4,3,2 until the cursor is at the proper point.
            SeekTo(Position);
        }

        /// <summary>
        ///     Updates Position based on the value of current Sqlite cursor.
        ///
        ///     <para/>If the query has ended, it invokes query.ContinuationReader to get the next
        ///     Sqlite cursor and continues from there.
        /// </summary>
        /// <param name="query">Current query</param>
        /// <returns>The found record</returns>
        public BtrieveRecord Next(CursorDirection cursorDirection)
        {
            if (Direction != cursorDirection)
            {
                Reader = null;
                ChangeDirection(cursorDirection);
            }

            // out of records?
            if (Reader == null || !Reader.Read())
            {
                Reader = null;
                return null;
            }

            Position = (uint)Reader.DataReader.GetInt32(0);
            LastKey = Reader.DataReader.GetValue(1);

            using var stream = Reader.DataReader.GetStream(2);
            var data = BtrieveUtil.ReadEntireStream(stream);

            return new BtrieveRecord(Position, data);
        }
    }
}

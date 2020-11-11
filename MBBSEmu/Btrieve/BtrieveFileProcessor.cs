using MBBSEmu.Btrieve.Enums;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     The BtrieveFileProcessor class is used to abstract the loading, parsing, and querying of
    ///     legacy Btrieve Files.
    ///
    ///     Legacy Btrieve files (.DAT) are converted on load to MBBSEmu format files (.DB), which
    ///     are Sqlite representations of the underlying Btrieve Data. This means the legacy .DAT
    ///     files are only used once on initial load and are not modified. All Inserts & Updates
    ///     happen within the new .DB Sqlite files.
    ///
    ///     These .DB files can be inspected and modified/edited cleanly once MBBSEmu has exited.
    ///     Attempting to modify the files during runtime is unsupported and will likely cause
    ///     concurrent access exceptions to fire in MBBSEmu.
    /// </summary>
    public class BtrieveFileProcessor : IDisposable
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly IFileUtility _fileFinder;

        /// <summary>
        ///     Current Position (offset) of the current BtrieveRecord in the Btrieve File.
        /// </summary>
        public uint Position { get; set; }

        /// <summary>
        ///     The Previous Query that was executed.
        /// </summary>
        public BtrieveQuery PreviousQuery { get; set; }

        /// <summary>
        ///     The length in bytes of each record.
        ///     <para/>Currently only fixed length record databases are supported.
        /// </summary>
        public int RecordLength { get; set; }

        /// <summary>
        ///     Whether the database contains variable length records.
        ///     <para/>If true, the RecordLength field is the fixed record length portion. Total
        ///     record size is RecordLength + some variable length
        /// </summary>
        public bool VariableLengthRecords { get; set; }

        /// <summary>
        ///     The active connection to the Sqlite database.
        /// </summary>
        private SqliteConnection _connection;

        /// <summary>
        ///     An offset -> BtrieveRecord cache used to speed up record access by reducing Sqlite
        ///     lookups.
        /// </summary>
        private readonly Dictionary<uint, BtrieveRecord> _cache = new Dictionary<uint, BtrieveRecord>();

        /// <summary>
        ///     The list of keys in this database, keyed by key number.
        /// </summary>
        public Dictionary<ushort, BtrieveKey> Keys { get; set; }

        /// <summary>
        ///     The list of autoincremented keys in this database, keyed by key number.
        /// </summary>
        private Dictionary<ushort, BtrieveKey> AutoincrementedKeys { get; set; }

        /// <summary>
        ///     The length of each page in the original Btrieve database.
        ///     <para/>Not actually used beyond initial database conversion.
        /// </summary>
        public int PageLength { get; set; }

        /// <summary>
        ///     The full path of the loaded database.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        ///     Closes all long lived resources, such as the Sqlite connection.
        /// </summary>
        public void Dispose()
        {
            _logger.Info($"Closing sqlite DB {FullPath}");

            PreviousQuery?.Dispose();

            _connection.Close();
            _connection.Dispose();
            _connection = null;

            _cache.Clear();
        }

        public BtrieveFileProcessor()
        {
            Keys = new Dictionary<ushort, BtrieveKey>();
            AutoincrementedKeys = new Dictionary<ushort, BtrieveKey>();
        }

        /// <summary>
        ///     Constructor to load the specified Btrieve File at the given Path
        /// </summary>
        /// <param name="fileUtility"></param>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        public BtrieveFileProcessor(IFileUtility fileUtility, string path, string fileName)
        {
            _fileFinder = fileUtility;

            Keys = new Dictionary<ushort, BtrieveKey>();
            AutoincrementedKeys = new Dictionary<ushort, BtrieveKey>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            var loadedFileName = _fileFinder.FindFile(path, fileName);

            // hack for MUTANTS which tries to load a DATT file
            if (Path.GetExtension(loadedFileName).ToUpper() == ".DATT")
                loadedFileName = Path.ChangeExtension(loadedFileName, ".DAT");

            // If a .DB version exists, load it over the .DAT file
            var dbFileName = loadedFileName.ToUpper().Replace(".DAT", ".DB");
            var fullPath = Path.Combine(path, dbFileName);

            if (File.Exists(fullPath))
            {
                LoadSqlite(fullPath);
            }
            else
            {
                var btrieveFile = new BtrieveFile();
                btrieveFile.LoadFile(_logger, path, loadedFileName);
                CreateSqliteDB(fullPath, btrieveFile);
            }

            //Set Position to First Record
            StepFirst();
        }

        /// <summary>
        ///     Loads an MBBSEmu representation of a Btrieve File from the specified Sqlite DB file.
        /// </summary>
        private void LoadSqlite(string fullPath)
        {
            _logger.Info($"Opening sqlite DB {fullPath}");

            FullPath = fullPath;

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullPath,
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            LoadSqliteMetadata();
        }

        /// <summary>
        ///     Loads metadata from the loaded Sqlite table, such as RecordLength and all the key metadata.
        /// </summary>
        private void LoadSqliteMetadata()
        {
            using (var cmd = new SqliteCommand("SELECT record_length, page_length, variable_length_records FROM metadata_t;", _connection))
            {
                using var reader = cmd.ExecuteReader();
                try
                {
                    if (!reader.Read())
                        throw new ArgumentException($"Can't read metadata_t from {FullPath}");

                    RecordLength = reader.GetInt32(0);
                    PageLength = reader.GetInt32(1);
                    VariableLengthRecords = reader.GetBoolean(2);
                }
                finally
                {
                    reader.Close();
                }
            }

            using (var cmd =
                new SqliteCommand(
                    "SELECT number, segment, attributes, data_type, offset, length FROM keys_t ORDER BY number, segment;",
                    _connection))
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var number = reader.GetInt32(0);
                    var btrieveKeyDefinition = new BtrieveKeyDefinition()
                    {
                        Number = (ushort)number,
                        Segment = reader.GetInt32(1) != 0,
                        SegmentOf = reader.GetInt32(1) != 0 ? (ushort)number : (ushort)0,
                        Attributes = (EnumKeyAttributeMask)reader.GetInt32(2),
                        DataType = (EnumKeyDataType)reader.GetInt32(3),
                        Offset = (ushort)reader.GetInt32(4),
                        Length = (ushort)reader.GetInt32(5),
                    };

                    if (!Keys.TryGetValue(btrieveKeyDefinition.Number, out var btrieveKey))
                    {
                        btrieveKey = new BtrieveKey();
                        Keys[btrieveKeyDefinition.Number] = btrieveKey;
                    }

                    var index = btrieveKey.Segments.Count;
                    btrieveKeyDefinition.SegmentIndex = index;
                    btrieveKey.Segments.Add(btrieveKeyDefinition);

                    if (btrieveKeyDefinition.DataType == EnumKeyDataType.AutoInc)
                        AutoincrementedKeys[btrieveKeyDefinition.Number] = btrieveKey;
                }
                reader.Close();
            }
        }

        /// <summary>
        ///     Returns the total number of records contained in the database.
        /// </summary>
        public int GetRecordCount()
        {
            using var stmt = new SqliteCommand("SELECT COUNT(*) FROM data_t;", _connection);
            return (int)(long)stmt.ExecuteScalar();
        }

        /// <summary>
        ///     Sets Position to the offset of the first Record in the loaded Btrieve File.
        /// </summary>
        public bool StepFirst()
        {
            // TODO consider grabbing data at the same time and prepopulating the cache
            using var cmd = new SqliteCommand("SELECT id FROM data_t ORDER BY id LIMIT 1", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint)reader.GetInt32(0) : 0;
            reader.Close();
            return Position > 0;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File.
        /// </summary>
        public bool StepNext()
        {
            // TODO consider grabbing data at the same time and prepopulating the cache
            using var cmd = new SqliteCommand($"SELECT id FROM data_t WHERE id > {Position} ORDER BY id LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                    return false;

                Position = (uint)reader.GetInt32(0);
                return true;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Sets Position to the offset of the previous logical record in the loaded Btrieve File.
        /// </summary>
        public bool StepPrevious()
        {
            using var cmd = new SqliteCommand($"SELECT id FROM data_t WHERE id < {Position} ORDER BY id DESC LIMIT 1;",
                _connection);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                    return false;

                Position = (uint)reader.GetInt32(0);
                return true;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Sets Position to the offset of the last Record in the loaded Btrieve File.
        /// </summary>
        public bool StepLast()
        {
            // TODO consider grabbing data at the same time and prepopulating the cache
            using var cmd = new SqliteCommand("SELECT id FROM data_t ORDER BY id DESC LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint)reader.GetInt32(0) : 0;
            reader.Close();
            return Position > 0;
        }

        /// <summary>
        ///     Returns the Record at the current Position
        /// </summary>
        /// <returns></returns>
        public byte[] GetRecord() => GetRecord(Position)?.Data;

        /// <summary>
        ///     Returns the Record at the specified Offset, while also updating Position to match.
        /// </summary>
        public BtrieveRecord GetRecord(uint offset)
        {
            Position = offset;

            if (_cache.TryGetValue(offset, out var record))
                return record;

            using var cmd = new SqliteCommand($"SELECT data FROM data_t WHERE id = {offset}", _connection);
            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            try
            {
                if (!reader.Read())
                    return null;

                using var stream = reader.GetStream(0);
                var data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                record = new BtrieveRecord(offset, data);
                _cache[offset] = record;
                return record;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Updates the Record at the current Position.
        /// </summary>
        public bool Update(byte[] recordData) => Update(Position, recordData);

        /// <summary>
        ///     Updates the Record at the specified Offset.
        /// </summary>
        public bool Update(uint offset, byte[] recordData)
        {
            if (VariableLengthRecords && recordData.Length != RecordLength)
                _logger.Warn($"Updating variable length record of {recordData.Length} bytes into {FullPath}");

            if (!VariableLengthRecords && recordData.Length != RecordLength)
            {
                _logger.Warn(
                    $"Btrieve Record Size Mismatch. Expected Length {RecordLength}, Actual Length {recordData.Length}");
                recordData = ForceSize(recordData, RecordLength);
            }

            using var transaction = _connection.BeginTransaction();
            using var updateCmd = new SqliteCommand()
            {
                Connection = _connection,
                Transaction = transaction
            };

            if (!InsertAutoincrementValues(transaction, recordData))
            {
                transaction.Rollback();
                return false;
            }

            var sb = new StringBuilder("UPDATE data_t SET data=@data, ");
            sb.Append(
                string.Join(", ", Keys.Values.Select(key => $"{key.SqliteKeyName}=@{key.SqliteKeyName}").ToList()));
            sb.Append(" WHERE id=@id;");
            updateCmd.CommandText = sb.ToString();

            updateCmd.Parameters.AddWithValue("@id", offset);
            updateCmd.Parameters.AddWithValue("@data", recordData);
            foreach (var key in Keys.Values)
                updateCmd.Parameters.AddWithValue($"@{key.SqliteKeyName}",
                    key.ExtractKeyInRecordToSqliteObject(recordData));

            int queryResult;
            try
            {
                queryResult = updateCmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                _logger.Warn(ex, $"Failed to update record because {ex.Message}");
                transaction.Rollback();
                return false;
            }

            transaction.Commit();

            if (queryResult == 0)
                return false;

            _cache[offset] = new BtrieveRecord(offset, recordData);
            return true;
        }

        /// <summary>
        ///     Copies b into an array of size size, growing or shrinking as necessary.
        /// </summary>
        private static byte[] ForceSize(byte[] b, int size)
        {
            var ret = new byte[size];
            Array.Copy(b, 0, ret, 0, Math.Min(b.Length, size));
            return ret;
        }

        /// <summary>
        ///     Searches for any zero-filled autoincremented memory in record and figures out
        ///     the autoincremented value to use, inserting it back into record.
        /// </summary>
        private bool InsertAutoincrementValues(SqliteTransaction transaction, byte[] record)
        {
            var zeroedKeys = AutoincrementedKeys.Values
                .Where(key => key.KeyInRecordIsAllZero(record))
                .Select(key => $"(MAX({key.SqliteKeyName}) + 1)")
                .ToList();

            if (zeroedKeys.Count == 0)
                return true;

            var sb = new StringBuilder("SELECT ");
            sb.Append(string.Join(", ", zeroedKeys));
            sb.Append(" FROM data_t;");

            using var cmd = new SqliteCommand(sb.ToString(), _connection, transaction);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                {
                    _logger.Error("Unable to query for MAX autoincremented values, unable to update");
                    return false;
                }

                var i = 0;
                foreach (var segment in AutoincrementedKeys.Values.SelectMany(x => x.Segments))
                {
                    var b = segment.Length switch
                    {
                        2 => BitConverter.GetBytes(reader.GetInt16(i++)),
                        4 => BitConverter.GetBytes(reader.GetInt32(i++)),
                        8 => BitConverter.GetBytes(reader.GetInt64(i++)),
                        _ => throw new ArgumentException($"Key integer length not supported {segment.Length}"),
                    };
                    Array.Copy(b, 0, record, segment.Offset, segment.Length);
                }
                return true;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Inserts a new Btrieve Record.
        /// </summary>
        /// <return>Position of the newly inserted item, or 0 on failure</return>
        public uint Insert(byte[] record)
        {
            if (VariableLengthRecords && record.Length != RecordLength)
                _logger.Warn($"Inserting variable length record of {record.Length} bytes into {FullPath}");

            if (!VariableLengthRecords && record.Length != RecordLength)
            {
                _logger.Warn(
                    $"Btrieve Record Size Mismatch TRUNCATING. Expected Length {RecordLength}, Actual Length {record.Length}");
                record = ForceSize(record, RecordLength);
            }

            using var transaction = _connection.BeginTransaction();
            using var insertCmd = new SqliteCommand() { Connection = _connection };
            insertCmd.Transaction = transaction;

            if (!InsertAutoincrementValues(transaction, record))
            {
                transaction.Rollback();
                return 0;
            }

            var sb = new StringBuilder("INSERT INTO data_t(data, ");
            sb.Append(string.Join(", ", Keys.Values.Select(key => key.SqliteKeyName).ToList()));
            sb.Append(") VALUES(@data, ");
            sb.Append(string.Join(", ", Keys.Values.Select(key => $"@{key.SqliteKeyName}").ToList()));
            sb.Append(");");
            insertCmd.CommandText = sb.ToString();

            insertCmd.Parameters.AddWithValue("@data", record);
            foreach (var key in Keys.Values)
            {
                insertCmd.Parameters.AddWithValue($"@{key.SqliteKeyName}",
                    key.ExtractKeyInRecordToSqliteObject(record));
            }

            int queryResult;
            try
            {
                queryResult = insertCmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                _logger.Warn(ex, $"{FullPath}: Failed to insert record because {ex.Message}");
                transaction.Rollback();
                return 0;
            }

            var lastInsertRowId = Convert.ToUInt32(new SqliteCommand("SELECT last_insert_rowid()", _connection, transaction).ExecuteScalar());

            transaction.Commit();

            if (queryResult == 0)
                return 0;

            _cache[lastInsertRowId] = new BtrieveRecord(lastInsertRowId, record);
            return lastInsertRowId;
        }

        /// <summary>
        ///     Deletes the Btrieve Record at the Current Position within the File.
        /// </summary>
        public bool Delete()
        {
            _cache.Remove(Position);

            using var cmd = new SqliteCommand($"DELETE FROM data_t WHERE id={Position};", _connection);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        ///     Deletes all records within the current Btrieve File.
        /// </summary>
        public bool DeleteAll()
        {
            _cache.Clear();

            Position = 0;

            using var cmd = new SqliteCommand($"DELETE FROM data_t;", _connection);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        ///     Performs a Key Based Query on the loaded Btrieve File
        /// </summary>
        /// <param name="keyNumber">Which key to query against</param>
        /// <param name="key">The key data to query against</param>
        /// <param name="btrieveOperationCode">Which query to perform</param>
        /// <param name="newQuery">true to start a new query, false to continue a prior one</param>
        public bool PerformOperation(int keyNumber, ReadOnlySpan<byte> key, EnumBtrieveOperationCodes btrieveOperationCode)
        {
            switch (btrieveOperationCode)
            {
                case EnumBtrieveOperationCodes.StepFirst:
                    return StepFirst();
                case EnumBtrieveOperationCodes.StepLast:
                    return StepLast();
                case EnumBtrieveOperationCodes.StepNext:
                case EnumBtrieveOperationCodes.StepNextExtended:
                    return StepNext();
                case EnumBtrieveOperationCodes.StepPrevious:
                case EnumBtrieveOperationCodes.StepPreviousExtended:
                    return StepPrevious();
            }

            var newQuery = !btrieveOperationCode.UsesPreviousQuery();
            BtrieveQuery currentQuery;

            if (newQuery || PreviousQuery == null)
            {
                currentQuery = new BtrieveQuery
                {
                    Key = Keys[(ushort)keyNumber],
                    KeyData = key == null ? null : new byte[key.Length],
                };

                /*
                 * TODO -- It appears MajorBBS/WG don't respect the Btrieve length for the key, as it's just part of a struct.
                 * There are modules that define in their btrieve file a STRING key of length 1, but pass in a char*
                 * So for the time being, we just make the key length we're looking for whatever was passed in.
                 */
                if (key != null)
                {
                    Array.Copy(key.ToArray(), 0, currentQuery.KeyData, 0, key.Length);
                }

                // Wipe any previous query we have and store this new query
                PreviousQuery?.Dispose();
                PreviousQuery = currentQuery;
            }
            else if (PreviousQuery == null)
            {
                return false;
            }
            else
            {
                currentQuery = PreviousQuery;
            }

            return btrieveOperationCode switch
            {
                EnumBtrieveOperationCodes.AcquireEqual => GetByKeyEqual(currentQuery),
                EnumBtrieveOperationCodes.QueryEqual => GetByKeyEqual(currentQuery),

                EnumBtrieveOperationCodes.AcquireFirst => GetByKeyFirst(currentQuery),
                EnumBtrieveOperationCodes.QueryFirst => GetByKeyFirst(currentQuery),

                EnumBtrieveOperationCodes.AcquireLast => GetByKeyLast(currentQuery),
                EnumBtrieveOperationCodes.QueryLast => GetByKeyLast(currentQuery),

                EnumBtrieveOperationCodes.AcquireGreater => GetByKeyGreater(currentQuery, ">"),
                EnumBtrieveOperationCodes.QueryGreater => GetByKeyGreater(currentQuery, ">"),
                EnumBtrieveOperationCodes.AcquireGreaterOrEqual => GetByKeyGreater(currentQuery, ">="),
                EnumBtrieveOperationCodes.QueryGreaterOrEqual => GetByKeyGreater(currentQuery, ">="),

                EnumBtrieveOperationCodes.AcquireLess => GetByKeyLess(currentQuery, "<"),
                EnumBtrieveOperationCodes.QueryLess => GetByKeyLess(currentQuery, "<"),
                EnumBtrieveOperationCodes.AcquireLessOrEqual => GetByKeyLess(currentQuery, "<="),
                EnumBtrieveOperationCodes.QueryLessOrEqual => GetByKeyLess(currentQuery, "<="),

                EnumBtrieveOperationCodes.QueryNext => GetByKeyNext(currentQuery),
                EnumBtrieveOperationCodes.QueryPrevious => GetByKeyPrevious(currentQuery),

                _ => throw new Exception($"Unsupported Operation Code: {btrieveOperationCode}")
            };
        }

        /// <summary>
        ///     Returns the Record at the specified position.
        /// </summary>
        public ReadOnlySpan<byte> GetRecordByOffset(uint absolutePosition)
        {
            Position = absolutePosition;
            return GetRecord(absolutePosition).ToSpan();
        }

        /// <summary>
        ///     Returns the defined Data Length of the specified Key
        /// </summary>
        public ushort GetKeyLength(ushort keyNumber) => (ushort)Keys[keyNumber].Length;

        /// <summary>
        ///     Updates Position to the next logical position based on the sorted key query, always
        ///     ascending in sort order.
        /// </summary>
        private bool GetByKeyNext(BtrieveQuery query) => NextReader(query, BtrieveQuery.CursorDirection.Forward);

        /// <summary>
        ///     Updates Position to the prior logical position based on the sorted key query, always
        ///     ascending in sort order.
        /// </summary>
        private bool GetByKeyPrevious(BtrieveQuery query) => NextReader(query, BtrieveQuery.CursorDirection.Reverse);

        /// <summary>
        ///     Calls NextReader with an always-true query matcher.
        /// </summary>
        private bool NextReader(BtrieveQuery query, BtrieveQuery.CursorDirection cursorDirection) =>
            NextReader(query, (query, record) => true, cursorDirection);

        /// <summary>
        ///     Updates Position based on the value of current Sqlite cursor.
        ///
        ///     <para/>If the query has ended, it invokes query.ContinuationReader to get the next
        ///     Sqlite cursor and continues from there.
        /// </summary>
        /// <param name="query">Current query</param>
        /// <param name="matcher">Delegate function for verifying results. If this matcher returns
        ///     false, the query is aborted and returns no more results.</param>
        /// <param name="cursorDirection">Which direction to move along the query results</param>
        /// <returns>true if the Sqlite cursor returned a valid item</returns>
        private bool NextReader(BtrieveQuery query, BtrieveQuery.QueryMatcher matcher, BtrieveQuery.CursorDirection cursorDirection)
        {
            var (success, record) = query.Next(matcher, cursorDirection);

            if (success)
                Position = query.Position;

            if (record != null)
                _cache[query.Position] = record;

            return success;
        }

        /// <summary>
        ///     Returns true if the retrievedRecord has equal keyData for the specified key.
        /// </summary>
        private static bool RecordMatchesKey(BtrieveRecord retrievedRecord, BtrieveKey key, byte[] keyData)
        {
            var keyA = key.ExtractKeyDataFromRecord(retrievedRecord.Data);
            var keyB = keyData;

            switch (key.PrimarySegment.DataType)
            {
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                case EnumKeyDataType.OldAscii:
                    return string.Equals(BtrieveKey.ExtractNullTerminatedString(keyA),
                        BtrieveKey.ExtractNullTerminatedString(keyB));
                default:
                    return keyA.SequenceEqual(keyB);
            }
        }

        /// <summary>
        ///     Gets the first equal record for the given key in query.
        /// </summary>
        private bool GetByKeyEqual(BtrieveQuery query)
        {
            BtrieveQuery.QueryMatcher initialMatcher = (query, record) => RecordMatchesKey(record, query.Key, query.KeyData);

            var sqliteObject = query.Key.KeyDataToSqliteObject(query.KeyData);
            var command = new SqliteCommand() { Connection = _connection };
            if (sqliteObject == null)
            {
                command.CommandText = $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} IS NULL";
            }
            else
            {
                command.CommandText = $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} >= @value ORDER BY {query.Key.SqliteKeyName} ASC";
                command.Parameters.AddWithValue("@value", query.Key.KeyDataToSqliteObject(query.KeyData));
            }

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            return NextReader(query, initialMatcher, BtrieveQuery.CursorDirection.Forward);
        }

        /// <summary>
        ///     Gets the first record greater or greater-than-equal-to than the given key in query.
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="oprator">Which operator to use, valid values are ">" and ">="</param>
        private bool GetByKeyGreater(BtrieveQuery query, string oprator)
        {
            var command = new SqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} {oprator} @value ORDER BY {query.Key.SqliteKeyName} ASC",
                _connection);
            command.Parameters.AddWithValue("@value", query.Key.KeyDataToSqliteObject(query.KeyData));

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            return NextReader(query, BtrieveQuery.CursorDirection.Forward);
        }

        /// <summary>
        ///     Gets the first record less or less-than-equal-to than the given key in query.
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="oprator">Which operator to use, valid values are "<" and "<="</param>
        private bool GetByKeyLess(BtrieveQuery query, string oprator)
        {
            // this query finds the first item less than
            var command = new SqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} {oprator} @value ORDER BY {query.Key.SqliteKeyName} DESC",
                _connection);
            command.Parameters.AddWithValue("@value", query.Key.KeyDataToSqliteObject(query.KeyData));

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command,
            };
            query.Direction = BtrieveQuery.CursorDirection.Reverse;
            return NextReader(query, BtrieveQuery.CursorDirection.Reverse);
        }

        /// <summary>
        ///     Gets the first record sorted by the given key in query.
        /// </summary>
        private bool GetByKeyFirst(BtrieveQuery query)
        {
            var command = new SqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t ORDER BY {query.Key.SqliteKeyName} ASC", _connection);

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            return NextReader(query, BtrieveQuery.CursorDirection.Forward);
        }

        /// <summary>
        ///     Gets the last record sorted by the given key in query.
        /// </summary>
        private bool GetByKeyLast(BtrieveQuery query)
        {
            var command = new SqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t ORDER BY {query.Key.SqliteKeyName} DESC",
                _connection);

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            query.Direction = BtrieveQuery.CursorDirection.Reverse;
            return NextReader(query, BtrieveQuery.CursorDirection.Reverse);
        }

        /// <summary>
        ///     Creates the Sqlite data_t table.
        /// </summary>
        private void CreateSqliteDataTable(SqliteConnection connection, BtrieveFile btrieveFile)
        {
            var sb = new StringBuilder("CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL");
            foreach (var key in btrieveFile.Keys.Values)
            {
                sb.Append($", {key.SqliteKeyName} {key.SqliteColumnType()}");
            }

            sb.Append(");");

            using var cmd = new SqliteCommand(sb.ToString(), connection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Creates the Sqlite data_t indices.
        /// </summary>
        private void CreateSqliteDataIndices(SqliteConnection connection, BtrieveFile btrieveFile)
        {
            foreach (var key in btrieveFile.Keys.Values)
            {
                var possiblyUnique = key.IsUnique ? "UNIQUE" : "";
                using var command = new SqliteCommand(
                    $"CREATE {possiblyUnique} INDEX {key.SqliteKeyName}_index on data_t({key.SqliteKeyName})",
                    _connection);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        ///     Fills in the Sqlite data_t table with all the data from btrieveFile.
        /// </summary>
        private void PopulateSqliteDataTable(SqliteConnection connection, BtrieveFile btrieveFile)
        {
            using var transaction = connection.BeginTransaction();
            foreach (var record in btrieveFile.Records.OrderBy(x => x.Offset))
            {
                using var insertCmd = new SqliteCommand()
                {
                    Connection = _connection,
                    Transaction = transaction
                };

                var sb = new StringBuilder("INSERT INTO data_t(data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => key.SqliteKeyName).ToList()));
                sb.Append(") VALUES(@data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => $"@{key.SqliteKeyName}").ToList()));
                sb.Append(");");

                insertCmd.CommandText = sb.ToString();
                insertCmd.Parameters.AddWithValue("@data", record.Data);
                foreach (var key in btrieveFile.Keys.Values)
                    insertCmd.Parameters.AddWithValue($"@{key.SqliteKeyName}",
                        key.ExtractKeyInRecordToSqliteObject(record.Data));

                try
                {
                    insertCmd.ExecuteNonQuery();
                }
                catch (SqliteException ex)
                {
                    _logger.Error(ex, $"Error importing btrieve data {ex.Message}");
                }
            }

            transaction.Commit();
        }

        /// <summary>
        ///     Creates the Sqlite metadata_t table.
        /// </summary>
        private void CreateSqliteMetadataTable(SqliteConnection connection, BtrieveFile btrieveFile)
        {
            const string statement =
                "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL)";

            using var cmd = new SqliteCommand(statement, connection);
            cmd.ExecuteNonQuery();

            using var insertCmd = new SqliteCommand() { Connection = connection };
            cmd.CommandText =
                "INSERT INTO metadata_t(record_length, physical_record_length, page_length, variable_length_records) VALUES(@record_length, @physical_record_length, @page_length, @variable_length_records)";
            cmd.Parameters.AddWithValue("@record_length", btrieveFile.RecordLength);
            cmd.Parameters.AddWithValue("@physical_record_length", btrieveFile.PhysicalRecordLength);
            cmd.Parameters.AddWithValue("@page_length", btrieveFile.PageLength);
            cmd.Parameters.AddWithValue("@variable_length_records", btrieveFile.VariableLengthRecords ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Creates the Sqlite keys_t table.
        /// </summary>
        private void CreateSqliteKeysTable(SqliteConnection connection, BtrieveFile btrieveFile)
        {
            const string statement =
                "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE(number, segment))";

            using var cmd = new SqliteCommand(statement, connection);
            cmd.ExecuteNonQuery();

            using var insertCmd = new SqliteCommand() { Connection = connection };
            cmd.CommandText =
                "INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(@number, @segment, @attributes, @data_type, @offset, @length, @null_value)";

            foreach (var keyDefinition in btrieveFile.Keys.SelectMany(key => key.Value.Segments))
            {
                // only grab the first
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@number", keyDefinition.Number);
                cmd.Parameters.AddWithValue("@segment", keyDefinition.SegmentIndex);
                cmd.Parameters.AddWithValue("@attributes", keyDefinition.Attributes);
                cmd.Parameters.AddWithValue("@data_type", keyDefinition.DataType);
                cmd.Parameters.AddWithValue("@offset", keyDefinition.Offset);
                cmd.Parameters.AddWithValue("@length", keyDefinition.Length);
                cmd.Parameters.AddWithValue("@null_value", keyDefinition.NullValue);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        ///     Creates the Sqlite database from btrieveFile.
        /// </summary>
        public void CreateSqliteDB(string fullpath, BtrieveFile btrieveFile)
        {
            _logger.Info($"Creating sqlite db {fullpath}");

            FullPath = fullpath;

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullpath,
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            RecordLength = btrieveFile.RecordLength;
            PageLength = btrieveFile.PageLength;
            Keys = btrieveFile.Keys;
            foreach (var key in btrieveFile.Keys.Where(key =>
                key.Value.PrimarySegment.DataType == EnumKeyDataType.AutoInc))
            {
                AutoincrementedKeys.Add(key.Value.PrimarySegment.Number, key.Value);
            }

            CreateSqliteMetadataTable(_connection, btrieveFile);
            CreateSqliteKeysTable(_connection, btrieveFile);
            CreateSqliteDataTable(_connection, btrieveFile);
            CreateSqliteDataIndices(_connection, btrieveFile);
            PopulateSqliteDataTable(_connection, btrieveFile);
        }
    }
}

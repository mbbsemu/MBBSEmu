using MBBSEmu.Btrieve.Enums;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Util;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
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
        public const int SQLITE_CONSTRAINT = 19;
        public const int SQLITE_CONSTRAINT_UNIQUE = 2067;
        public const int SQLITE_CONSTRAINT_TRIGGER = 1811;

        /// <summary>
        ///     The current version of the database schema, stored inside metadata_t.version
        /// </summary>
        public const int CURRENT_VERSION = 2;

        const int ACS_LENGTH = 256;

        protected static readonly IMessageLogger _logger = new LogFactory().GetLogger<MessageLogger>();

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

        public byte[] ACS { get; set; }

        /// <summary>
        ///     The active connection to the Sqlite database.
        /// </summary>
        public SqliteConnection Connection;

        public bool BtrieveDriverMode { get; set; }

        /// <summary>
        ///     An offset -> BtrieveRecord cache used to speed up record access by reducing Sqlite
        ///     lookups.
        /// </summary>
        private readonly IDictionary<uint, BtrieveRecord> _cache;

        private readonly ConcurrentDictionary<string, SqliteCommand> _sqlCommands = new();
        private Dictionary<ushort, BtrieveKey> _keys;

        /// <summary>
        ///     The list of keys in this database, keyed by key number.
        /// </summary>
        public Dictionary<ushort, BtrieveKey> Keys
        {
            get => _keys;
            set
            {
                _keys = value;
                _autoincrementedKeys = _keys.Values
                    .Where(key => key.PrimarySegment.DataType == EnumKeyDataType.AutoInc)
                    .ToList();
            }
        }

        /// <summary>
        ///     The list of autoincremented keys in this database.
        ///
        ///     <para/>Created when Keys is set as a property
        /// </summary>
        private List<BtrieveKey> _autoincrementedKeys;

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

            foreach (var cmd in _sqlCommands.Values)
            {
                cmd.Dispose();
            }
            _sqlCommands.Clear();

            Connection.Close();
            Connection.Dispose();
            Connection = null;

            _cache.Clear();
        }

        public BtrieveFileProcessor()
        {
            _cache = new LRUCache<uint, BtrieveRecord>(0);
        }

        /// <summary>
        ///     Constructor to load the specified Btrieve File at the given Path
        /// </summary>
        /// <param name="fileUtility"></param>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        public BtrieveFileProcessor(IFileUtility fileUtility, string path, string fileName, int cacheSize)
        {
            _fileFinder = fileUtility;
            _cache = new LRUCache<uint, BtrieveRecord>(cacheSize);

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            var loadedFileName = _fileFinder.FindFile(path, fileName);

            // If a .DB version exists, load it over the .DAT file
            var fullPathDAT = Path.Combine(path, loadedFileName);
            var fullPathDB = Path.Combine(path, loadedFileName.ToUpper().Replace(".DAT", ".DB"));

            FileInfo fileInfoDAT = new FileInfo(fullPathDAT);
            FileInfo fileInfoDB = new FileInfo(fullPathDB);

            // if both DAT/DB exist, check if the DAT has an a newer time, if so
            // we want to reconvert it by deleting the DB
            if (fileInfoDAT.Exists && fileInfoDB.Exists && fileInfoDAT.LastWriteTime > fileInfoDB.LastWriteTime)
            {
                _logger.Warn($"{fullPathDAT} is newer than {fullPathDB}, reconverting the DAT -> DB");
                File.Delete(fullPathDB);
                fileInfoDB = new FileInfo(fullPathDB);
            }

            if (fileInfoDB.Exists)
            {
                LoadSqlite(fullPathDB);
            }
            else
            {
                var btrieveFile = new BtrieveFile();
                btrieveFile.LoadFile(_logger, path, loadedFileName);
                CreateSqliteDB(fullPathDB, btrieveFile);
            }

            //Set Position to First Record
            StepFirst();
        }

        /// <summary>
        ///     Loads an MBBSEmu representation of a Btrieve File from the specified Sqlite DB file.
        /// </summary>
        private void LoadSqlite(string fullPath)
        {
            _logger.Debug($"Opening sqlite DB {fullPath}");

            FullPath = fullPath;

            Connection = new SqliteConnection(GetDefaultConnectionStringBuilder(fullPath).ToString());
            Connection.Open();

            LoadSqliteMetadata();
            Keys = LoadSqliteKeys();
        }

        /// <summary>
        ///     Loads metadata from the loaded Sqlite database into memory
        /// </summary>
        private void LoadSqliteMetadata()
        {
            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand("SELECT record_length, page_length, variable_length_records, version, acs FROM metadata_t", Connection);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                    throw new ArgumentException($"Can't read metadata_t from {FullPath}");

                RecordLength = reader.GetInt32(0);
                PageLength = reader.GetInt32(1);
                VariableLengthRecords = reader.GetBoolean(2);

                if (!reader.IsDBNull(4))
                {
                    using var acsStream = reader.GetStream(4);
                    if (acsStream.Length != ACS_LENGTH)
                        throw new ArgumentException($"The ACS length is not 256 in the database. This is corrupt. {FullPath}");

                    ACS = BtrieveUtil.ReadEntireStream(acsStream);
                }

                var version = reader.GetInt32(3);

                reader.Close();

                if (version != CURRENT_VERSION)
                {
                    UpgradeDatabaseFromVersion(version);
                }
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Upgrades a database to the current version. Updating metadata_t.version should be done within a transaction.
        /// </summary>
        /// <param name="oldVersion"></param>
        private void UpgradeDatabaseFromVersion(int oldVersion)
        {
            switch (oldVersion)
            {
                case 1:
                    UpgradeDatabaseFromVersion1To2();
                    break;
                default:
                    throw new ArgumentException("Invalid old version");
            }

            LoadSqliteMetadata();
        }

        /// <summary>
        ///     Upgrades the sqlite DB from version 1 to 2.
        ///
        ///     <para/>2 adds the non_modifiable triggers
        /// </summary>
        private void UpgradeDatabaseFromVersion1To2()
        {
            var transaction = Connection.BeginTransaction();
            // creates the missing triggers
            var keys = LoadSqliteKeys(transaction);
            CreateSqliteTriggers(transaction, keys.Values);

            // and bump the version
            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand("UPDATE metadata_t SET version = 2", Connection, transaction);
            cmd.ExecuteNonQuery();

            try
            {
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
            }
        }

        /// <summary>
        ///     Loads all the key data from keys_t into memory
        /// </summary>
        private Dictionary<ushort, BtrieveKey> LoadSqliteKeys(SqliteTransaction transaction = null)
        {
            var keys = new Dictionary<ushort, BtrieveKey>();

            var cmd =
                GetSqliteCommand(
                    "SELECT number, segment, attributes, data_type, offset, length FROM keys_t ORDER BY number, segment",
                    transaction);
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

                if (btrieveKeyDefinition.RequiresACS)
                {
                    if (ACS == null)
                        throw new ArgumentException($"Key {btrieveKeyDefinition.Number} requires ACS, but none was read. This database is likely corrupt: {FullPath}");

                    btrieveKeyDefinition.ACS = ACS;
                }

                if (!keys.TryGetValue(btrieveKeyDefinition.Number, out var btrieveKey))
                {
                    btrieveKey = new BtrieveKey();
                    keys[btrieveKeyDefinition.Number] = btrieveKey;
                }

                var index = btrieveKey.Segments.Count;
                btrieveKeyDefinition.SegmentIndex = index;
                btrieveKey.Segments.Add(btrieveKeyDefinition);
            }
            reader.Close();

            return keys;
        }

        /// <summary>
        ///     Returns the total number of records contained in the database.
        /// </summary>
        public int GetRecordCount()
        {
            var cmd = GetSqliteCommand("SELECT COUNT(*) FROM data_t");
            return (int)(long)cmd.ExecuteScalar();
        }

        /// <summary>
        ///     Sets Position to the offset of the first Record in the loaded Btrieve File.
        /// </summary>
        private bool StepFirst()
        {
            var cmd = GetSqliteCommand("SELECT id, data FROM data_t ORDER BY id LIMIT 1");
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            Position = (uint)reader.GetInt32(0);
            GetAndCacheBtrieveRecord(Position, reader, ordinal: 1);
            reader.Close();
            return true;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File.
        /// </summary>
        private bool StepNext()
        {
            var cmd = GetSqliteCommand("SELECT id, data FROM data_t WHERE id > @position ORDER BY id LIMIT 1");
            cmd.Parameters.AddWithValue("@position", Position);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                    return false;

                Position = (uint)reader.GetInt32(0);
                GetAndCacheBtrieveRecord(Position, reader, ordinal: 1);
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
        private bool StepPrevious()
        {
            var cmd = GetSqliteCommand("SELECT id, data FROM data_t WHERE id < @position ORDER BY id DESC LIMIT 1");
            cmd.Parameters.AddWithValue("@position", Position);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                    return false;

                Position = (uint)reader.GetInt32(0);
                GetAndCacheBtrieveRecord(Position, reader, 1);
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
        private bool StepLast()
        {
            var cmd = GetSqliteCommand("SELECT id, data FROM data_t ORDER BY id DESC LIMIT 1");
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            Position = (uint)reader.GetInt32(0);
            GetAndCacheBtrieveRecord(Position, reader, 1);
            reader.Close();
            return Position > 0;
        }

        /// <summary>
        ///     Returns the Record at the current Position
        /// </summary>
        /// <returns></returns>
        public byte[] GetRecord() => GetRecord(Position)?.Data;

        private BtrieveRecord GetAndCacheBtrieveRecord(uint id, SqliteDataReader reader, int ordinal)
        {
            using var stream = reader.GetStream(ordinal);
            var data = BtrieveUtil.ReadEntireStream(stream);

            var record = new BtrieveRecord(id, data);
            _cache[id] = record;
            return record;
        }

        /// <summary>
        ///     Returns the Record at the specified physical offset, while also updating Position to match.
        /// </summary>
        public BtrieveRecord GetRecord(uint offset)
        {
            Position = offset;

            if (_cache.TryGetValue(offset, out var record))
                return record;

            var cmd = GetSqliteCommand("SELECT data FROM data_t WHERE id = @offset");
            cmd.Parameters.AddWithValue("@offset", offset);
            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            try
            {
                if (!reader.Read())
                    return null;

                return GetAndCacheBtrieveRecord(offset, reader, 0);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        ///     Returns the Record at the specified physical offset, updating Position and setting logical currency.
        /// </summary>
        /// <param name="offset">physical offset of the record</param>
        /// <param name="keyNumber">key number to set logical currency on</param>
        /// <returns></returns>
        public BtrieveRecord GetRecord(uint offset, int keyNumber)
        {
            var ret = GetRecord(offset);
            if (keyNumber >= 0)
            {
                var key = Keys[(ushort)keyNumber];
                var keyData = new byte[key.Length];
                key.ExtractKeyDataFromRecord(ret.ToSpan()).CopyTo(keyData.AsSpan());

                PreviousQuery?.Dispose();
                PreviousQuery = new BtrieveQuery(this)
                {
                    Direction = BtrieveQuery.CursorDirection.Seek,
                    Position = offset,
                    Key = key,
                    KeyData = keyData,
                    LastKey = key.ExtractKeyInRecordToSqliteObject(ret.ToSpan()),
                };
            }
            return ret;
        }

        /// <summary>
        ///     Updates the Record at the current Position.
        /// </summary>
        public BtrieveError Update(byte[] recordData) => Update(Position, recordData);

        /// <summary>
        ///     Updates the Record at the specified Offset.
        /// </summary>
        public BtrieveError Update(uint offset, byte[] recordData)
        {
            if (VariableLengthRecords && recordData.Length != RecordLength)
                _logger.Debug($"Updating variable length record of {recordData.Length} bytes into {FullPath}");

            if (!VariableLengthRecords && recordData.Length != RecordLength)
            {
                _logger.Warn(
                    $"Btrieve Record Size Mismatch. Expected Length {RecordLength}, Actual Length {recordData.Length}");
                recordData = ForceSize(recordData, RecordLength);
            }

            using var transaction = Connection.BeginTransaction();

            if (!InsertAutoincrementValues(transaction, recordData))
            {
                transaction.Rollback();
                return BtrieveError.DuplicateKeyValue;
            }

            string updateSql;
            if (Keys.Count > 0)
            {
                var sb = new StringBuilder("UPDATE data_t SET data=@data, ");
                sb.Append(
                    string.Join(", ", Keys.Values.Select(key => $"{key.SqliteKeyName}=@{key.SqliteKeyName}").ToList()));
                sb.Append(" WHERE id=@id;");
                updateSql = sb.ToString();
            }
            else
            {
                updateSql = "UPDATE data_t SET data=@data WHERE id=@id";
            }

            var updateCmd = GetSqliteCommand(updateSql, transaction);
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
                _logger.Warn(ex, $"Failed to update record because {ex.SqliteErrorCode} {ex.SqliteExtendedErrorCode} {ex.Message}");
                transaction.Rollback();

                // emulating strange MBBS behavior here. If an update fails on a constraint check,
                // it returns 0. If a key is modified, it catastro's
                if (ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_UNIQUE)
                    return BtrieveError.DuplicateKeyValue;

                if (BtrieveDriverMode && ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_TRIGGER)
                    return BtrieveError.NonModifiableKeyValue;

                throw;
            }

            try
            {
                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Failed to commit during update record because {ex.Message}");
                transaction.Rollback();
                queryResult = 0;
            }

            if (queryResult == 0)
                return BtrieveError.InvalidKeyNumber;

            _cache[offset] = new BtrieveRecord(offset, recordData);
            return BtrieveError.Success;
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
            var zeroedKeys = _autoincrementedKeys
                .Where(key => key.KeyInRecordIsAllZero(record))
                .Select(key => $"(MAX({key.SqliteKeyName}) + 1)")
                .ToList();

            if (zeroedKeys.Count == 0)
                return true;

            var sb = new StringBuilder("SELECT ");
            sb.Append(string.Join(", ", zeroedKeys));
            sb.Append(" FROM data_t;");

            var cmd = GetSqliteCommand(sb.ToString(), transaction);
            using var reader = cmd.ExecuteReader();
            try
            {
                if (!reader.Read())
                {
                    _logger.Error("Unable to query for MAX autoincremented values, unable to update");
                    return false;
                }

                var i = 0;
                foreach (var segment in _autoincrementedKeys.SelectMany(x => x.Segments))
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
        public uint Insert(byte[] record, LogLevel logLevel)
        {
            if (VariableLengthRecords && record.Length != RecordLength)
                _logger.Debug($"Inserting variable length record of {record.Length} bytes into {FullPath}");

            if (!VariableLengthRecords && record.Length != RecordLength)
            {
                _logger.Warn(
                    $"Btrieve Record Size Mismatch TRUNCATING. Expected Length {RecordLength}, Actual Length {record.Length}");
                record = ForceSize(record, RecordLength);
            }

            using var transaction = Connection.BeginTransaction();

            if (!InsertAutoincrementValues(transaction, record))
            {
                transaction.Rollback();
                return 0;
            }

            string insertSql;
            if (Keys.Count > 0)
            {
                var sb = new StringBuilder("INSERT INTO data_t(data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => key.SqliteKeyName).ToList()));
                sb.Append(") VALUES(@data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => $"@{key.SqliteKeyName}").ToList()));
                sb.Append(");");
                insertSql = sb.ToString();
            }
            else
            {
                insertSql = "INSERT INTO data_t(data) VALUES (@data)";
            }

            var insertCmd = GetSqliteCommand(insertSql, transaction);
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
                _logger.Log(logLevel, $"{FullPath}: Failed to insert record because {ex.Message}", ex);
                transaction.Rollback();
                return 0;
            }

            var lastInsertRowId = Convert.ToUInt32(GetSqliteCommand("SELECT last_insert_rowid()", transaction).ExecuteScalar());

            try
            {
                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.Log(logLevel, $"Failed to commit during insert: {ex.Message}");
                transaction.Rollback();
                queryResult = 0;
            }

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

            var cmd = GetSqliteCommand("DELETE FROM data_t WHERE id=@position");
            cmd.Parameters.AddWithValue("@position", Position);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        ///     Deletes all records within the current Btrieve File.
        /// </summary>
        public bool DeleteAll()
        {
            _cache.Clear();

            Position = 0;

            return GetSqliteCommand("DELETE FROM data_t").ExecuteNonQuery() > 0;
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
                case EnumBtrieveOperationCodes.Delete:
                    return Delete();
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
                currentQuery = new BtrieveQuery(this)
                {
                    Key = Keys[(ushort)keyNumber],
                    KeyData = key == null ? null : new byte[key.Length],
                };

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

                EnumBtrieveOperationCodes.AcquireNext => GetByKeyNext(currentQuery),
                EnumBtrieveOperationCodes.QueryNext => GetByKeyNext(currentQuery),

                EnumBtrieveOperationCodes.AcquirePrevious => GetByKeyPrevious(currentQuery),
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
        ///     Updates Position based on the value of current Sqlite cursor.
        ///
        ///     <para/>If the query has ended, it invokes query.ContinuationReader to get the next
        ///     Sqlite cursor and continues from there.
        /// </summary>
        /// <param name="query">Current query</param>
        /// <param name="cursorDirection">Which direction to move along the query results</param>
        /// <returns>true if the Sqlite cursor returned a valid item</returns>
        private bool NextReader(BtrieveQuery query, BtrieveQuery.CursorDirection cursorDirection)
        {
            var record = query.Next(cursorDirection);

            if (record == null)
                return false;

            Position = query.Position;
            _cache[query.Position] = record;
            return true;
        }

        /// <summary>
        ///     Gets the first equal record for the given key in query.
        /// </summary>
        private bool GetByKeyEqual(BtrieveQuery query)
        {
            var sqliteObject = query.Key.KeyDataToSqliteObject(query.KeyData);
            SqliteCommand command;
            if (sqliteObject == null)
            {
                command = GetSqliteCommand($"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} IS NULL");
            }
            else
            {
                command = GetSqliteCommand($"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} = @value ORDER BY {query.Key.SqliteKeyName} ASC");
                command.Parameters.AddWithValue("@value", query.Key.KeyDataToSqliteObject(query.KeyData));
            }

            query.Reader = new BtrieveQuery.SqliteReader()
            {
                DataReader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo),
                Command = command
            };
            query.Direction = BtrieveQuery.CursorDirection.Seek;
            return NextReader(query, BtrieveQuery.CursorDirection.Seek);
        }

        /// <summary>
        ///     Gets the first record greater or greater-than-equal-to than the given key in query.
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="oprator">Which operator to use, valid values are ">" and ">="</param>
        private bool GetByKeyGreater(BtrieveQuery query, string oprator)
        {
            var command = GetSqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} {oprator} @value ORDER BY {query.Key.SqliteKeyName} ASC");
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
            var command = GetSqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t WHERE {query.Key.SqliteKeyName} {oprator} @value ORDER BY {query.Key.SqliteKeyName} DESC");
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
            var command = GetSqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t ORDER BY {query.Key.SqliteKeyName} ASC");

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
            var command = GetSqliteCommand(
                $"SELECT id, {query.Key.SqliteKeyName}, data FROM data_t ORDER BY {query.Key.SqliteKeyName} DESC");

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
        private void CreateSqliteDataTable(BtrieveFile btrieveFile)
        {
            var sb = new StringBuilder("CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL");
            foreach (var key in btrieveFile.Keys.Values)
            {
                sb.Append($", {key.SqliteKeyName} {key.SqliteColumnType()}");
            }

            sb.Append(");");

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand(sb.ToString(), Connection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Creates the Sqlite data_t indices.
        /// </summary>
        private void CreateSqliteDataIndices(BtrieveFile btrieveFile)
        {
            foreach (var key in btrieveFile.Keys.Values)
            {
                var possiblyUnique = key.IsUnique ? "UNIQUE" : "";
                // not using GetSqliteCommand since this is used once and caching it provides no benefit
                using var cmd = new SqliteCommand(
                    $"CREATE {possiblyUnique} INDEX {key.SqliteKeyName}_index on data_t({key.SqliteKeyName})", Connection);
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateSqliteTriggers(ICollection<BtrieveKey> keys)
            => CreateSqliteTriggers(transaction: null, keys);

        private void CreateSqliteTriggers(SqliteTransaction transaction, ICollection<BtrieveKey> keys)
        {
            var nonModifiableKeys = keys.Where(key => !key.IsModifiable).ToList();
            if (nonModifiableKeys.Count == 0)
                return;

            StringBuilder builder = new StringBuilder("CREATE TRIGGER non_modifiable "
                + "BEFORE UPDATE ON data_t "
                + "BEGIN "
                + "SELECT "
                + "CASE ");

            foreach (var key in nonModifiableKeys)
            {
                builder.Append($"WHEN NEW.{key.SqliteKeyName} != OLD.{key.SqliteKeyName} THEN " +
                    $"RAISE (ABORT,'You modified a non-modifiable {key.SqliteKeyName}!') ");
            }

            builder.Append("END; END;");

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand(builder.ToString(), Connection, transaction);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Fills in the Sqlite data_t table with all the data from btrieveFile.
        /// </summary>
        private void PopulateSqliteDataTable(BtrieveFile btrieveFile)
        {
            using var transaction = Connection.BeginTransaction();

            string insertSql;
            if (Keys.Count > 0)
            {
                var sb = new StringBuilder("INSERT INTO data_t(data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => key.SqliteKeyName).ToList()));
                sb.Append(") VALUES(@data, ");
                sb.Append(string.Join(", ", Keys.Values.Select(key => $"@{key.SqliteKeyName}").ToList()));
                sb.Append(");");
                insertSql = sb.ToString();
            }
            else
            {
                insertSql = "INSERT INTO data_t(data) VALUES (@data)";
            }

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var insertCmd = new SqliteCommand(insertSql, Connection, transaction);
            insertCmd.Prepare();

            foreach (var record in btrieveFile.Records.OrderBy(x => x.Offset))
            {
                insertCmd.Parameters.Clear();
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

            try
            {
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
            }
        }

        private static object SqliteNullable(object o)
            => o == null ? DBNull.Value : o;

        /// <summary>
        ///     Creates the Sqlite metadata_t table.
        /// </summary>
        private void CreateSqliteMetadataTable(BtrieveFile btrieveFile)
        {
            const string createTableStatement =
                "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL, acs_name STRING, acs BLOB)";

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var createTableCommand = new SqliteCommand(createTableStatement, Connection);
            createTableCommand.ExecuteNonQuery();

            const string insertIntoTableStatement =
                "INSERT INTO metadata_t(record_length, physical_record_length, page_length, variable_length_records, version, acs_name, acs) VALUES(@record_length, @physical_record_length, @page_length, @variable_length_records, @version, @acs_name, @acs)";
            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand(insertIntoTableStatement, Connection);

            cmd.Parameters.AddWithValue("@record_length", btrieveFile.RecordLength);
            cmd.Parameters.AddWithValue("@physical_record_length", btrieveFile.PhysicalRecordLength);
            cmd.Parameters.AddWithValue("@page_length", btrieveFile.PageLength);
            cmd.Parameters.AddWithValue("@variable_length_records", btrieveFile.VariableLengthRecords ? 1 : 0);
            cmd.Parameters.AddWithValue("@version", CURRENT_VERSION);
            cmd.Parameters.AddWithValue("@acs_name", SqliteNullable(btrieveFile.ACSName));
            cmd.Parameters.AddWithValue("@acs", SqliteNullable(btrieveFile.ACS));
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Creates the Sqlite keys_t table.
        /// </summary>
        private void CreateSqliteKeysTable(BtrieveFile btrieveFile)
        {
            const string createTableStatement =
                "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE(number, segment))";

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var createTableCommand = new SqliteCommand(createTableStatement, Connection);
            createTableCommand.ExecuteNonQuery();

            const string insertIntoTableStatement =
                "INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(@number, @segment, @attributes, @data_type, @offset, @length, @null_value)";

            // not using GetSqliteCommand since this is used once and caching it provides no benefit
            using var cmd = new SqliteCommand(insertIntoTableStatement, Connection);

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

            CreateSqliteDBWithConnectionString(GetDefaultConnectionStringBuilder(fullpath), btrieveFile);
        }

        public void CreateSqliteDBWithConnectionString(SqliteConnectionStringBuilder connectionString, BtrieveFile btrieveFile) =>
            CreateSqliteDBWithConnectionString(connectionString.ToString(), btrieveFile);

        private void CreateSqliteDBWithConnectionString(string connectionString, BtrieveFile btrieveFile)
        {
            Connection = new SqliteConnection(connectionString);
            Connection.Open();

            RecordLength = btrieveFile.RecordLength;
            PageLength = btrieveFile.PageLength;
            VariableLengthRecords = btrieveFile.VariableLengthRecords;
            Keys = btrieveFile.Keys;

            CreateSqliteMetadataTable(btrieveFile);
            CreateSqliteKeysTable(btrieveFile);
            CreateSqliteDataTable(btrieveFile);
            CreateSqliteDataIndices(btrieveFile);
            CreateSqliteTriggers(Keys.Values);
            PopulateSqliteDataTable(btrieveFile);
        }

        public SqliteCommand GetSqliteCommand(string sql, SqliteTransaction transaction = null)
        {
            var cmd = _sqlCommands.GetOrAdd(sql, sql => {
                var cmd = new SqliteCommand(sql, Connection);
                cmd.Prepare();
                return cmd;
            });
            cmd.Transaction = transaction;
            cmd.Parameters.Clear();
            return cmd;
        }

        public static SqliteConnectionStringBuilder GetDefaultConnectionStringBuilder(string filepath) => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = filepath,
                Pooling = false,
            };
    }
}

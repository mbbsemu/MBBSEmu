using MBBSEmu.Btrieve.Enums;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     The BtrieveFileProcessor class is used to handle Loading, Parsing, and Querying legacy Btrieve Files
    ///
    ///     Legacy Btrieve files (.DAT) are converted on load to MBBSEmu format files (.EMU), which are JSON representations
    ///     of the underlying Btrieve Data. This means the legacy .DAT files are only used once on initial load and are not
    ///     modified. All Inserts & Updates happen within the new .EMU JSON files.
    /// </summary>
    public class BtrieveFileProcessor : IDisposable
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly IFileUtility _fileFinder;

        /// <summary>
        ///     Current Position (offset) of the Processor in the Btrieve File
        /// </summary>
        public uint Position { get; set; }

        /// <summary>
        ///     The Previous Query that was executed
        /// </summary>
        private BtrieveQuery PreviousQuery { get; set; }

        public int RecordLength { get; set; }

        private SQLiteConnection _connection;
        private readonly Dictionary<uint, BtrieveRecord> _cache = new Dictionary<uint, BtrieveRecord>();
        public Dictionary<ushort, BtrieveKey> Keys { get; set; }
        private Dictionary<ushort, BtrieveKeyDefinition> AutoincrementedKeys { get; set; }

        public int PageLength { get; set; }

        public string FullPath { get; set; }

        public void Dispose()
        {
            _connection.Close();
            _cache.Clear();
        }

        /// <summary>
        ///     Constructor to load the specified Btrieve File at the given Path
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="path"></param>
        public BtrieveFileProcessor(IFileUtility fileUtility, string path, string fileName)
        {
            _fileFinder = fileUtility;

            Keys = new Dictionary<ushort, BtrieveKey>();
            AutoincrementedKeys = new Dictionary<ushort, BtrieveKeyDefinition>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            var loadedFileName = _fileFinder.FindFile(path, fileName);

            // hack for MUTANTS which tries to load a DATT file
            if (Path.GetExtension(loadedFileName).ToUpper() == ".DATT")
                loadedFileName = Path.ChangeExtension(loadedFileName, ".DAT");

            //If a .EMU version exists, load it over the .DAT file
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
        ///     Loads an MBBSEmu representation of a Btrieve File
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        private void LoadSqlite(string fullPath)
        {
            _logger.Info($"Opening sqlite DB {fullPath}");

            FullPath = fullPath;

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullPath,
            }.ToString();

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            LoadSqliteMetadata();
        }

        private void LoadSqliteMetadata()
        {
            using (var cmd = new SQLiteCommand("SELECT record_length, page_length FROM metadata_t;", _connection))
            {
                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    throw new ArgumentException($"Can't read metadata_t from {_connection.VfsName}");

                RecordLength = reader.GetInt32(0);
                PageLength = reader.GetInt32(1);
            }

            using (var cmd = new SQLiteCommand("SELECT number, segment, attributes, data_type, offset, length FROM keys_t ORDER BY number, segment;", _connection))
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var number = reader.GetInt32(0);
                    var btrieveKeyDefinition = new BtrieveKeyDefinition() {
                        Number = (ushort) number,
                        Segment = reader.GetInt32(1) != 0,
                        SegmentOf = reader.GetInt32(1) != 0 ? (ushort) number : (ushort) 0,
                        Attributes = (EnumKeyAttributeMask) reader.GetInt32(2),
                        DataType = (EnumKeyDataType) reader.GetInt32(3),
                        Offset = (ushort) reader.GetInt32(4),
                        Length = (ushort) reader.GetInt32(5),
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
                        AutoincrementedKeys[btrieveKeyDefinition.Number] = btrieveKeyDefinition;
                }
            }
        }

        public int GetRecordCount()
        {
            using var stmt = new SQLiteCommand("SELECT COUNT(*) FROM data_t;", _connection);
            return (int) (long) stmt.ExecuteScalar();
        }

        /// <summary>
        ///     Sets Position to the offset of the first Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public bool StepFirst()
        {
            // TODO consider grabbing data at the same and prepopulating the cache
            using var cmd = new SQLiteCommand("SELECT id FROM data_t LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint) reader.GetInt32(0) : 0;
            return Position > 0;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public bool StepNext()
        {
            // TODO consider grabbing data at the same and prepopulating the cache
            using var cmd = new SQLiteCommand($"SELECT id FROM data_t WHERE id > {Position} LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            Position = (uint) reader.GetInt32(0);
            return true;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public bool StepPrevious()
        {
            using var cmd = new SQLiteCommand($"SELECT id FROM data_t WHERE id < {Position} ORDER BY id DESC LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            Position = (uint) reader.GetInt32(0);
            return true;
        }

        /// <summary>
        ///     Sets Position to the offset of the last Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public bool StepLast()
        {
            // TODO consider grabbing data at the same and prepopulating the cache
            using var cmd = new SQLiteCommand("SELECT id FROM data_t ORDER BY id DESC LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint) reader.GetInt32(0) : 0;
            return Position > 0;
        }

        /// <summary>
        ///     Returns the Record at the current Position
        /// </summary>
        /// <returns></returns>
        public byte[] GetRecord() => GetRecord(Position)?.Data;

        /// <summary>
        ///     Returns the Record at the specified Offset
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public BtrieveRecord GetRecord(uint offset)
        {
            if (_cache.TryGetValue(offset, out var record))
                return record;

            using var cmd = new SQLiteCommand($"SELECT data FROM data_t WHERE id={offset}", _connection);
            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo);

            if (!reader.Read())
                return null;

            var data = new byte[RecordLength];
            reader.GetBlob(0, readOnly:true).Read(data, data.Length, 0);

            record = new BtrieveRecord(offset, data);
            _cache[offset] = record;
            return record;
        }

        /// <summary>
        ///     Updates the Record at the current Position
        /// </summary>
        /// <param name="recordData"></param>
        public bool Update(byte[] recordData) => Update(Position, recordData);

        /// <summary>
        ///     Updates the Record at the specified Offset
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="recordData"></param>
        public bool Update(uint offset, byte[] recordData)
        {
            if (recordData.Length != RecordLength)
            {
                _logger.Warn($"Btrieve Record Size Mismatch. Expected Length {RecordLength}, Actual Length {recordData.Length}");
                recordData = ForceSize(recordData, RecordLength);
            }

            using var transaction = _connection.BeginTransaction();
            using var updateCmd = new SQLiteCommand(_connection);
            updateCmd.Transaction = transaction;

            InsertAutoincrementValues(transaction, recordData);

            StringBuilder sb = new StringBuilder("UPDATE data_t SET data=@data, ");
            sb.Append(string.Join(", ", Keys.SelectMany(key => key.Value.Segments).Select(segment => $"{segment.SqliteKeyName}=@{segment.SqliteKeyName}").ToList()));
            sb.Append(" WHERE id=@id;");
            updateCmd.CommandText = sb.ToString();

            updateCmd.Parameters.AddWithValue("@id", offset);
            updateCmd.Parameters.AddWithValue("@data", recordData);
            foreach(var key in Keys)
            {
                foreach(var segment in key.Value.Segments)
                {
                    var keyData = recordData.AsSpan().Slice(segment.Offset, segment.Length);
                    updateCmd.Parameters.AddWithValue($"@{segment.SqliteKeyName}", SqliteType(segment, keyData.ToArray()));
                }
            }

            int queryResult;
            try
            {
                queryResult = updateCmd.ExecuteNonQuery();
            }
            catch (SQLiteException ex)
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

        /// <summary>Copies b into an array of size size, growing or shrinking</summary>
        private static byte[] ForceSize(byte[] b, int size)
        {
            var ret = new byte[size];
            Array.Copy(b, 0, ret, 0, Math.Min(b.Length, size));
            return ret;
        }

        private bool HasZeroKeyValue(byte[] recordData, BtrieveKeyDefinition key)
        {
            var convertible = (IConvertible) SqliteType(key, recordData.AsSpan().Slice(key.Offset, key.Length).ToArray());
            return convertible.ToInt64(null) == 0;
        }

        /// <summary>
        ///     Searches for any zero-filled autoincremented memory in recordData and figures out
        ///     the value to use, inserting it back into recordData
        /// </summary>
        private void InsertAutoincrementValues(SQLiteTransaction transaction, byte[] recordData)
        {
            var zeroedKeys = AutoincrementedKeys
                .Where(key => HasZeroKeyValue(recordData, key.Value))
                .Select(key => $"(MAX({key.Value.SqliteKeyName}) + 1)")
                .ToList();

            if (zeroedKeys.Count == 0)
                return;

            StringBuilder sb = new StringBuilder("SELECT ");
            sb.Append(string.Join(", ", zeroedKeys));
            sb.Append(" FROM data_t;");

            using var cmd = new SQLiteCommand(sb.ToString(), _connection, transaction);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                _logger.Error("Unable to query for MAX autoincremented values, unable to update");
                return;
            }

            var i = 0;
            foreach (var key in AutoincrementedKeys)
            {
                byte[] b;
                switch (key.Value.Length) {
                    case 2:
                        b = BitConverter.GetBytes(reader.GetInt16(i++));
                        break;
                    case 4:
                        b = BitConverter.GetBytes(reader.GetInt32(i++));
                        break;
                    case 8:
                        b = BitConverter.GetBytes(reader.GetInt64(i++));
                        break;
                    default:
                        throw new ArgumentException($"Key integer length not supported {key.Value.Length}");
                }

                Array.Copy(b, 0, recordData, key.Value.Offset, key.Value.Length);
            }
        }

        /// <summary>
        ///     Inserts a new Btrieve Record at the End of the currently loaded Btrieve File
        /// </summary>
        /// <param name="recordData"></param>
        /// <return>Position of the newly inserted item, or 0 on failure</return>
        public uint Insert(byte[] recordData)
        {
            if (recordData.Length != RecordLength)
            {
                _logger.Warn($"Btrieve Record Size Mismatch. Expected Length {RecordLength}, Actual Length {recordData.Length}");
                recordData = ForceSize(recordData, RecordLength);
            }

            using var transaction = _connection.BeginTransaction();
            using var insertCmd = new SQLiteCommand(_connection);
            insertCmd.Transaction = transaction;

            InsertAutoincrementValues(transaction, recordData);

            StringBuilder sb = new StringBuilder("INSERT INTO data_t(data, ");
            sb.Append(string.Join(", ", Keys.SelectMany(key => key.Value.Segments).Select(segment => segment.SqliteKeyName).ToList()));
            sb.Append(") VALUES(@data, ");
            sb.Append(string.Join(", ", Keys.SelectMany(key => key.Value.Segments).Select(segment => $"@{segment.SqliteKeyName}").ToList()));
            sb.Append(");");
            insertCmd.CommandText = sb.ToString();

            insertCmd.Parameters.AddWithValue("@data", recordData);
            foreach(var key in Keys)
            {
                foreach(var segment in key.Value.Segments)
                {
                    var keyData = recordData.AsSpan().Slice(segment.Offset, segment.Length);
                    insertCmd.Parameters.AddWithValue($"@{segment.SqliteKeyName}", SqliteType(segment, keyData.ToArray()));
                }
            }

            int queryResult;
            try
            {
                queryResult = insertCmd.ExecuteNonQuery();
            }
            catch (SQLiteException ex)
            {
                _logger.Warn(ex, $"{FullPath}: Failed to insert record because {ex.Message}");
                transaction.Rollback();
                return 0;
            }

            transaction.Commit();

            if (queryResult == 0)
                return 0;

            _cache[(uint) _connection.LastInsertRowId] = new BtrieveRecord((uint) _connection.LastInsertRowId, recordData);
            return (uint) _connection.LastInsertRowId;
        }

        /// <summary>
        ///     Deletes the Btrieve Record at the Current Position within the File
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            _cache.Remove(Position);

            using var cmd = new SQLiteCommand($"DELETE FROM data_t WHERE id={Position};", _connection);
            return cmd.ExecuteNonQuery() > 0 ? true : false;
        }

        /// <summary>
        ///     Deletes all records within the current Btrieve File
        /// </summary>
        public bool DeleteAll()
        {
            _cache.Clear();

            Position = 0;

            using var cmd = new SQLiteCommand($"DELETE FROM data_t;", _connection);
            return cmd.ExecuteNonQuery() > 0 ? true : false;
        }

        /// <summary>
        ///     Performs a Step based Seek on the loaded Btrieve File
        /// </summary>
        /// <param name="operationCode"></param>
        /// <returns></returns>
        public bool Seek(EnumBtrieveOperationCodes operationCode)
        {
            return operationCode switch
            {
                EnumBtrieveOperationCodes.GetFirst => StepFirst(),
                EnumBtrieveOperationCodes.GetNext => StepNext(),
                EnumBtrieveOperationCodes.GetPrevious => StepPrevious(),
                _ => throw new Exception($"Unsupported Btrieve Operation: {operationCode}")
            };
        }

        /// <summary>
        ///     Performs a Key Based Query on the loaded Btrieve File
        /// </summary>
        /// <param name="keyNumber"></param>
        /// <param name="key"></param>
        /// <param name="btrieveOperationCode"></param>
        /// <param name="newQuery"></param>
        /// <returns></returns>
        public bool SeekByKey(ushort keyNumber, ReadOnlySpan<byte> key, EnumBtrieveOperationCodes btrieveOperationCode, bool newQuery = true)
        {
            BtrieveQuery currentQuery;

            if (newQuery)
            {
                currentQuery = new BtrieveQuery
                {
                    Key = Keys[keyNumber],
                    KeyData = key == null ? null : new byte[key.Length],
                };

                /*
                 * Occasionally, zstring keys are marked with a length of 1, where as the length is actually
                 * longer in the defined struct. Because of this, if the key passed in is longer than the definition,
                 * we increase the size of the defined key in the query.
                 */
                /*if (key != null && key.Length != currentQuery.KeyDefinition.Length)
                {
                    _logger.Warn($"Adjusting Query Key Size, Data Size {key.Length} differs from Defined Key Size {currentQuery.KeyDefinition.Length}");
                    currentQuery.KeyLength = (ushort)key.Length;
                }*/

                /*
                 * TODO -- It appears MajorBBS/WG don't respect the Btrieve length for the key, as it's just part of a struct.
                 * There are modules that define in their btrieve file a STRING key of length 1, but pass in a char*
                 * So for the time being, we just make the key length we're looking for whatever was passed in.
                 */
                if (key != null)
                {
                    Array.Copy(key.ToArray(), 0, currentQuery.KeyData, 0, key.Length);
                }

                //Update Previous for the next run
                PreviousQuery?.Dispose();
                PreviousQuery = currentQuery;
            }
            else
            {
                currentQuery = PreviousQuery;
            }

            return btrieveOperationCode switch
            {
                EnumBtrieveOperationCodes.GetEqual => GetByKeyEqual(currentQuery),
                EnumBtrieveOperationCodes.GetKeyEqual => GetByKeyEqual(currentQuery),

                EnumBtrieveOperationCodes.GetFirst => GetByKeyFirst(currentQuery),
                EnumBtrieveOperationCodes.GetKeyFirst => GetByKeyFirst(currentQuery),

                EnumBtrieveOperationCodes.GetLast => GetByKeyLast(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLast => GetByKeyLast(currentQuery),

                EnumBtrieveOperationCodes.GetNext => GetByKeyNext(currentQuery),
                EnumBtrieveOperationCodes.GetKeyNext => GetByKeyNext(currentQuery),

                EnumBtrieveOperationCodes.GetKeyGreater => GetByKeyGreater(currentQuery, ">"),
                EnumBtrieveOperationCodes.GetGreater => GetByKeyGreater(currentQuery, ">"),
                EnumBtrieveOperationCodes.GetGreaterOrEqual => GetByKeyGreater(currentQuery, ">="),

                EnumBtrieveOperationCodes.GetLess => GetByKeyLess(currentQuery, "<"),
                EnumBtrieveOperationCodes.GetKeyLess => GetByKeyLess(currentQuery, "<"),
                EnumBtrieveOperationCodes.GetLessOrEqual => GetByKeyLess(currentQuery, "<="),
                // TODO verify, is GetKeyLessOrEqual?

                _ => throw new Exception($"Unsupported Operation Code: {btrieveOperationCode}")
            };
        }

        /// <summary>
        ///     Returns the Record at the specified position
        /// </summary>
        /// <param name="absolutePosition"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> GetRecordByOffset(uint absolutePosition)
        {
            Position = absolutePosition;
            return GetRecord(absolutePosition).ToSpan();
        }

        /// <summary>
        ///     Returns the defined Data Length of the specified Key
        /// </summary>
        /// <param name="keyNumber"></param>
        /// <returns></returns>
        public ushort GetKeyLength(ushort keyNumber) => (ushort)Keys[keyNumber].Length;

        /// <summary>
        ///     Returns the defined Key Type of the specified Key
        /// </summary>
        /// <param name="keyNumber"></param>
        /// <returns></returns>
        public EnumKeyDataType GetKeyType(ushort keyNumber) => Keys[keyNumber].PrimarySegment.DataType;

        /// <summary>
        ///     Retrieves the First Record (lowest sort order by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyFirst(BtrieveQuery query)
        {
            if (query.Key.IsComposite)
                throw new ArgumentException("Composite query NYI");

            using var command = new SQLiteCommand(
                $"SELECT id, data FROM data_t ORDER BY {query.Key.PrimarySegment.SqliteKeyName} ASC", _connection);

            query.Reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            return NextReader(query);
        }

        /// <summary>
        ///     GetNext for Non-Numeric Key Types
        ///
        ///     Search for the next logical record with a matching Key that comes after the current Position
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyNext(BtrieveQuery query)
        {
            return NextReader(query);
        }

        private bool NextReader(BtrieveQuery query)
        {
            if (query.Reader == null)
                return false;

            if (!query.Reader.Read())
            {
                query.Reader.Dispose();
                query.Reader = null;
                return false;
            }

            var data = new byte[RecordLength];
            query.Reader.GetBlob(1, readOnly:true).Read(data, data.Length, 0);

            query.Position = Position = (uint) query.Reader.GetInt32(0);
            _cache[query.Position] = new BtrieveRecord(query.Position, data);
            return true;
        }

        /// <summary>
        ///     Gets the First Logical Record for the given key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyEqual(BtrieveQuery query)
        {
            using var command = new SQLiteCommand(_connection);

            if (query.Key.IsComposite)
            {
                var sb = new StringBuilder();
                sb.Append("SELECT id, data FROM data_t WHERE ");
                sb.Append(string.Join(" AND ", query.Key.Segments.Select(segment => $"{segment.SqliteKeyName}=@{segment.SqliteKeyName}").ToList()));
                sb.Append(";");

                _logger.Error($"{FullPath} {sb.ToString()}");

                command.CommandText = sb.ToString();
                foreach (var segment in query.Key.Segments)
                    command.Parameters.AddWithValue($"@{segment.SqliteKeyName}", SqliteType(query.Key.PrimarySegment, query.KeyData));
            }
            else
            {
                command.CommandText = $"SELECT id, data FROM data_t WHERE {query.Key.PrimarySegment.SqliteKeyName}=@value";
                command.Parameters.AddWithValue("@value", SqliteType(query.Key.PrimarySegment, query.KeyData));
            }

            query.Reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            return NextReader(query);
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyGreater(BtrieveQuery query, string oprator)
        {
            //if (query.Key.IsComposite)
                //throw new ArgumentException("Composite query NYI");

            using var command = new SQLiteCommand(
                $"SELECT id, data FROM data_t WHERE {query.Key.PrimarySegment.SqliteKeyName} {oprator} @value ORDER BY {query.Key.PrimarySegment.SqliteKeyName} ASC", _connection);
            command.Parameters.AddWithValue("@value", SqliteType(query.Key.PrimarySegment, query.KeyData));

            query.Reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            return NextReader(query);
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyLess(BtrieveQuery query, string oprator)
        {
            //if (query.Key.IsComposite)
                //throw new ArgumentException("Composite query NYI");

            using var command = new SQLiteCommand(
                $"SELECT id, data FROM data_t WHERE {query.Key.PrimarySegment.SqliteKeyName} {oprator} @value ORDER BY {query.Key.PrimarySegment.SqliteKeyName} DESC", _connection);
            command.Parameters.AddWithValue("@value", SqliteType(query.Key.PrimarySegment, query.KeyData));

            query.Reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            return NextReader(query);
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Greater Than or Equal To the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterOrEqual(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Less Than or Equal To the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessOrEqual(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Retrieves the Last Record, highest, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool GetByKeyLast(BtrieveQuery query)
        {
            if (query.Key.IsComposite)
                throw new ArgumentException("Composite query NYI");

            using var command = new SQLiteCommand(
                $"SELECT id, data FROM data_t ORDER BY {query.Key.PrimarySegment.SqliteKeyName} DESC", _connection);

            query.Reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
            return NextReader(query);
        }

        /// <summary>
        ///     Writes a similar file as BUTIL -RECOVER to verify records loaded from recovery process match records loaded by MBBSEmu
        /// </summary>
        /// <param name="file"></param>
        /// <param name="records"></param>
        public static void WriteBtrieveRecoveryFile(string file, IEnumerable<BtrieveRecord> records)
        {
            using var outputFile = File.Open(file, FileMode.Create);

            foreach (var r in records)
            {
                outputFile.Write(Encoding.ASCII.GetBytes($"{r.Data.Length},"));
                outputFile.Write(r.Data);
                outputFile.Write(new byte[] { 0xD, 0xA });
            }
            outputFile.WriteByte(0x1A);
            outputFile.Close();
        }

        private static object SqliteType(BtrieveKeyDefinition key, ReadOnlySpan<byte> data)
        {
            /*if (key.Length != data.Length)
                _logger.Error("Inserting key value with mismatched length");*/

            switch (key.DataType)
            {
                case EnumKeyDataType.Unsigned:
                case EnumKeyDataType.UnsignedBinary:
                    switch (key.Length)
                    {
                        case 2:
                            return BitConverter.ToUInt16(data);
                        case 4:
                            return BitConverter.ToUInt32(data);
                        case 8:
                            return BitConverter.ToUInt64(data);
                        default:
                            throw new ArgumentException($"Bad unsigned integer key length {key.Length}");
                    }
                case EnumKeyDataType.AutoInc:
                case EnumKeyDataType.Integer:
                    switch (key.Length)
                    {
                        case 2:
                            return BitConverter.ToInt16(data);
                        case 4:
                            return BitConverter.ToInt32(data);
                        case 8:
                            return BitConverter.ToInt64(data);
                        default:
                            throw new ArgumentException($"Bad integer key length {key.Length}");
                    }
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                    // very important to trim trailing nulls/etc
                    return ToCleanString(data);
                default:
                    return data.ToArray();
            }
        }

        public static string ToCleanString(ReadOnlySpan<byte> b)
        {
            int strlen = b.IndexOf((byte) 0);
            if (strlen < 0)
                strlen = b.Length;

            return Encoding.ASCII.GetString(b.Slice(0, strlen));
        }

        private static string SqliteType(BtrieveKey key, BtrieveKeyDefinition keyDefinition)
        {
            String type;
            switch (keyDefinition.DataType)
            {
                case EnumKeyDataType.AutoInc:
                    return "INTEGER NOT NULL UNIQUE";
                case EnumKeyDataType.Integer:
                case EnumKeyDataType.Unsigned:
                case EnumKeyDataType.UnsignedBinary:
                    type = "INTEGER NOT NULL";
                    break;
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                    type = "TEXT NOT NULL";
                    break;
                default:
                    _logger.Warn($"Defaulting to BLOB type for {keyDefinition.DataType}");
                    type = "BLOB NOT NULL";
                    break;
            }

            if (!key.IsComposite && keyDefinition.IsUnique)
            {
                type += " UNIQUE";
            }

            return type;
        }

        private void CreateSqliteDataTable(SQLiteConnection connection, BtrieveFile btrieveFile)
        {
            StringBuilder sb = new StringBuilder("CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL");
            foreach(var key in btrieveFile.Keys)
            {
                foreach (var segment in key.Value.Segments)
                {
                    sb.Append($", {segment.SqliteKeyName} {SqliteType(key.Value, segment)}");
                }
            }
            // check for uniqueness constraints on composite keys
            foreach(var key in btrieveFile.Keys)
            {
                if (key.Value.IsComposite && key.Value.IsUnique)
                {
                    sb.Append(", UNIQUE(");
                    sb.Append(string.Join(", ", key.Value.Segments.Select(segment => segment.SqliteKeyName).ToList()));
                    sb.Append(")");
                }
            }
            sb.Append(");");

            using var cmd = new SQLiteCommand(sb.ToString(), connection);
            cmd.ExecuteNonQuery();
        }

        private void PopulateSqliteDataTable(SQLiteConnection connection, BtrieveFile btrieveFile)
        {
            using var transaction = connection.BeginTransaction();
            foreach (var record in btrieveFile.Records.OrderBy(x => x.Offset))
            {
                using var insertCmd = new SQLiteCommand(connection);

                var sb = new StringBuilder("INSERT INTO data_t(data, ");
                sb.Append(string.Join(", ", Keys.SelectMany(key => key.Value.Segments).Select(segment => segment.SqliteKeyName).ToList()));
                sb.Append(") VALUES(@data, ");
                sb.Append(string.Join(", ", Keys.SelectMany(key => key.Value.Segments).Select(segment => $"@{segment.SqliteKeyName}").ToList()));
                sb.Append(");");

                insertCmd.CommandText = sb.ToString();
                insertCmd.Parameters.AddWithValue("@data", record.Data);
                foreach(var key in btrieveFile.Keys)
                {
                    foreach (var segment in key.Value.Segments)
                    {
                        var keyData = record.Data.AsSpan().Slice(segment.Offset, segment.Length);
                        insertCmd.Parameters.AddWithValue($"@{segment.SqliteKeyName}", SqliteType(segment, keyData.ToArray()));
                    }
                }

                try
                {
                    insertCmd.ExecuteNonQuery();
                }
                catch (SQLiteException ex)
                {
                    _logger.Error(ex, $"Error importing btrieve data {ex.Message}");
                }
            }
            transaction.Commit();
        }

        private void CreateSqliteMetadataTable(SQLiteConnection connection, BtrieveFile btrieveFile)
        {
            string statement = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL)";

            using var cmd = new SQLiteCommand(statement, connection);
            cmd.ExecuteNonQuery();

            using var insertCmd = new SQLiteCommand(connection);
            cmd.CommandText = "INSERT INTO metadata_t(record_length, physical_record_length, page_length) VALUES(@record_length, @physical_record_length, @page_length)";
            cmd.Parameters.AddWithValue("@record_length", btrieveFile.RecordLength);
            cmd.Parameters.AddWithValue("@physical_record_length", btrieveFile.PhysicalRecordLength);
            cmd.Parameters.AddWithValue("@page_length", btrieveFile.PageLength);
            cmd.ExecuteNonQuery();
        }

        private void CreateSqliteKeysTable(SQLiteConnection connection, BtrieveFile btrieveFile)
        {
            string statement = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, UNIQUE(number, segment))";

            using var cmd = new SQLiteCommand(statement, connection);
            cmd.ExecuteNonQuery();

            using var insertCmd = new SQLiteCommand(connection);
            cmd.CommandText = "INSERT INTO keys_t(number, segment, attributes, data_type, offset, length) VALUES(@number, @segment, @attributes, @data_type, @offset, @length)";

            foreach (var key in btrieveFile.Keys)
            {
                foreach (var keyDefinition in key.Value.Segments)
                {
                    // only grab the first
                    cmd.Reset();
                    cmd.Parameters.AddWithValue("@number", keyDefinition.Number);
                    cmd.Parameters.AddWithValue("@segment", keyDefinition.SegmentIndex);
                    cmd.Parameters.AddWithValue("@attributes", keyDefinition.Attributes);
                    cmd.Parameters.AddWithValue("@data_type", keyDefinition.DataType);
                    cmd.Parameters.AddWithValue("@offset", keyDefinition.Offset);
                    cmd.Parameters.AddWithValue("@length", keyDefinition.Length);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void CreateSqliteDB(string fullpath, BtrieveFile btrieveFile)
        {
            _logger.Warn($"Creating sqlite db {fullpath}");

            FullPath = fullpath;

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullpath,
            }.ToString();

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            RecordLength = btrieveFile.RecordLength;
            PageLength = btrieveFile.PageLength;
            Keys = btrieveFile.Keys;
            foreach (var key in btrieveFile.Keys)
            {
                if (key.Value.PrimarySegment.DataType == EnumKeyDataType.AutoInc)
                    AutoincrementedKeys.Add(key.Value.PrimarySegment.Number, key.Value.PrimarySegment);
            }

            CreateSqliteMetadataTable(_connection, btrieveFile);
            CreateSqliteKeysTable(_connection, btrieveFile);
            CreateSqliteDataTable(_connection, btrieveFile);
            PopulateSqliteDataTable(_connection, btrieveFile);
        }
    }
}

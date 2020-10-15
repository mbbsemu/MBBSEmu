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
        public Dictionary<int, BtrieveKeyDefinition> Keys { get; set; }

        public int PageLength { get; set; }

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
        public BtrieveFileProcessor(IFileUtility fileUtility, string fileName, string path)
        {
            _fileFinder = fileUtility;

            Keys = new Dictionary<int, BtrieveKeyDefinition>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            var loadedFileName = _fileFinder.FindFile(path, fileName);

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

            using (var cmd = new SQLiteCommand("SELECT id, attributes, data_type, offset, length FROM keys_t;", _connection))
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var number = reader.GetInt32(0);
                    Keys[number] = new BtrieveKeyDefinition() {
                        Number = (ushort) number,
                        Attributes = (EnumKeyAttributeMask) reader.GetInt32(1),
                        DataType = (EnumKeyDataType) reader.GetInt32(2),
                        Offset = (ushort) reader.GetInt32(3),
                        Length = (ushort) reader.GetInt32(4),
                        Segment = false,
                    };
                }
            }
        }

        public int GetRecordCount()
        {
            using var stmt = new SQLiteCommand("SELECT COUNT(*) FROM data_t;", _connection);
            return (int) stmt.ExecuteScalar();
        }

        /// <summary>
        ///     Sets Position to the offset of the first Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepFirst()
        {
            using var cmd = new SQLiteCommand("SELECT id FROM data_t LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint) reader.GetInt32(0) : 0;
            return Position > 0 ? (ushort) 1 : (ushort) 0;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepNext()
        {
            using var cmd = new SQLiteCommand($"SELECT id FROM data_t WHERE id > {Position} LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return 0;

            Position = (uint) reader.GetInt32(0);
            return 1;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepPrevious()
        {
            using var cmd = new SQLiteCommand($"SELECT id FROM data_t WHERE id < {Position} LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return 0;

            Position = (uint) reader.GetInt32(0);
            return 1;
        }

        /// <summary>
        ///     Sets Position to the offset of the last Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepLast()
        {
            using var cmd = new SQLiteCommand("SELECT id FROM data_t ORDER BY DESC LIMIT 1;", _connection);
            using var reader = cmd.ExecuteReader();

            Position = reader.Read() ? (uint) reader.GetInt32(0) : 0;
            return Position > 0 ? (ushort) 1 : (ushort) 0;
        }

        /// <summary>
        ///     Returns the Record at the current Position
        /// </summary>
        /// <returns></returns>
        public byte[] GetRecord() => GetRecord(Position).Data;

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

            _logger.Error($"Looking up offset {offset} length {RecordLength}");

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
        public void Update(byte[] recordData) => Update(Position, recordData);

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
                recordData = forceSize(recordData, RecordLength);
            }

            using var insertCmd = new SQLiteCommand(_connection);

            StringBuilder sb = new StringBuilder("UPDATE data_t SET data=@data");
            foreach(var key in Keys)
            {
                sb.Append($", key{key.Value.Number}=@key{key.Value.Number}");
            }
            sb.Append(" WHERE id=@id;");
            insertCmd.CommandText = sb.ToString();

            //var data = record.Data;
            insertCmd.Parameters.AddWithValue("@data", recordData);
            foreach(var key in Keys)
            {
                var keyData = recordData.AsSpan().Slice(key.Value.Offset, key.Value.Length);
                insertCmd.Parameters.AddWithValue($"key{key.Value.Number}", SqliteType(key.Value, keyData.ToArray()));
            }

            if (insertCmd.ExecuteNonQuery() == 0)
                return false;

            _cache[offset] = new BtrieveRecord(offset, recordData);
            return true;
        }

        private static byte[] forceSize(byte[] b, int size)
        {
            var ret = new byte[size];
            for (int i = 0; i < Math.Min(b.Length, size); ++i)
                ret[i] = b[i];

            return ret;
        }

        /// <summary>
        ///     Inserts a new Btrieve Record at the End of the currently loaded Btrieve File
        /// </summary>
        /// <param name="recordData"></param>
        public bool Insert(byte[] recordData)
        {
            if (recordData.Length != RecordLength)
            {
                _logger.Warn($"Btrieve Record Size Mismatch. Expected Length {RecordLength}, Actual Length {recordData.Length}");
                recordData = forceSize(recordData, RecordLength);
            }

            using var insertCmd = new SQLiteCommand(_connection);

            StringBuilder sb = new StringBuilder("INSERT INTO data_t(data");
            foreach(var key in Keys)
            {
                sb.Append($", key{key.Value.Number}");
            }
            sb.Append(") VALUES(@data");
            foreach(var key in Keys)
            {
                sb.Append($", @key{key.Value.Number}");
            }
            sb.Append(");");
            insertCmd.CommandText = sb.ToString();

            //var data = record.Data;
            insertCmd.Parameters.AddWithValue("@data", recordData);
            foreach(var key in Keys)
            {
                var keyData = recordData.AsSpan().Slice(key.Value.Offset, key.Value.Length);
                insertCmd.Parameters.AddWithValue($"key{key.Value.Number}", SqliteType(key.Value, keyData.ToArray()));
            }

            if (insertCmd.ExecuteNonQuery() == 0)
                return false;

            _cache[(uint) _connection.LastInsertRowId] = new BtrieveRecord((uint) _connection.LastInsertRowId, recordData);
            return true;
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

            using var cmd = new SQLiteCommand($"DELETE FROM data_t;", _connection);
            return cmd.ExecuteNonQuery() > 0 ? true : false;
        }

        /// <summary>
        ///     Performs a Step based Seek on the loaded Btrieve File
        /// </summary>
        /// <param name="operationCode"></param>
        /// <returns></returns>
        public ushort Seek(EnumBtrieveOperationCodes operationCode)
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
        public ushort SeekByKey(ushort keyNumber, ReadOnlySpan<byte> key, EnumBtrieveOperationCodes btrieveOperationCode, bool newQuery = true)
        {
            BtrieveQuery currentQuery;

            if (newQuery)
            {
                currentQuery = new BtrieveQuery
                {
                    KeyOffset = Keys[keyNumber].Offset,
                    KeyDataType = Keys[keyNumber].DataType,
                    Key = key == null ? null : new byte[key.Length],
                    KeyLength = GetKeyLength(keyNumber)
                };

                /*
                 * Occasionally, zstring keys are marked with a length of 1, where as the length is actually
                 * longer in the defined struct. Because of this, if the key passed in is longer than the definition,
                 * we increase the size of the defined key in the query.
                 */
                if (key != null && key.Length != currentQuery.KeyLength)
                {
                    _logger.Warn($"Adjusting Query Key Size, Data Size {key.Length} differs from Defined Key Size {currentQuery.KeyLength}");
                    currentQuery.KeyLength = (ushort)key.Length;
                }

                /*
                 * TODO -- It appears MajorBBS/WG don't respect the Btrieve length for the key, as it's just part of a struct.
                 * There are modules that define in their btrieve file a STRING key of length 1, but pass in a char*
                 * So for the time being, we just make the key length we're looking for whatever was passed in.
                 */
                if (key != null)
                {
                    Array.Copy(key.ToArray(), 0, currentQuery.Key, 0, key.Length);
                }

                //Update Previous for the next run
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

                EnumBtrieveOperationCodes.GetKeyFirst when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyFirstAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyFirst when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyFirstAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyFirst => GetByKeyFirstNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetFirst when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyFirstAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetFirst when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyFirstAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetFirst => GetByKeyFirstNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetKeyNext when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyNextAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyNext when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyNextAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyNext => GetByKeyNextNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetKeyGreater when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyGreater when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyGreater => GetByKeyGreaterNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetGreater when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetGreater when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetGreater => GetByKeyGreaterNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetGreaterOrEqual => GetByKeyGreaterOrEqualNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetLessOrEqual => GetByKeyLessOrEqualNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetLess when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLessAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetLess when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyLessAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetLess => GetByKeyLessNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetKeyLess when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLessAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLess when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyLessAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLess => GetByKeyLessNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetLast when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLastAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetLast when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyLastAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetLast => GetByKeyLastNumeric(currentQuery),

                EnumBtrieveOperationCodes.GetKeyLast when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLastAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLast when currentQuery.KeyDataType == EnumKeyDataType.String => GetByKeyLastAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLast => GetByKeyLastNumeric(currentQuery),

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
        public EnumKeyDataType GetKeyType(ushort keyNumber) => Keys[keyNumber].DataType;

        /// <summary>
        ///     Retrieves the First Record, numerically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyFirstAlphabetical(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Retrieves the First Record, numerically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        private ushort GetByKeyFirstNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     GetNext for Non-Numeric Key Types
        ///
        ///     Search for the next logical record with a matching Key that comes after the current Position
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyNextAlphabetical(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     GetNext for Numeric key types
        ///
        ///     Key Value needs to be incremented, then the specific key needs to be found
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyNextNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Gets the First Logical Record for the given key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyEqual(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterAlphabetical(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Greater Than or Equal To the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterOrEqualNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Less Than or Equal To the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessOrEqualNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Greater Than the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessAlphabetical(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Less Than the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessNumeric(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Retrieves the Last Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLastAlphabetical(BtrieveQuery query)
        {
            return 0;
        }

        /// <summary>
        ///     Search for the Last logical record after the current position with the specified Key value
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLastNumeric(BtrieveQuery query)
        {
            return 0;
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

        private static object SqliteType(BtrieveKeyDefinition key, byte[] data)
        {
            if (key.Length != data.Length)
                _logger.Error("Inserting key value with mismatched length");

            switch (key.DataType)
            {
                case EnumKeyDataType.Integer:
                case EnumKeyDataType.Unsigned:
                    switch (key.Length)
                    {
                        case 1:
                            return data[0];
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
                    return Encoding.ASCII.GetString(data);
                default:
                    return data;
            }
        }

        private static string SqliteType(EnumKeyDataType dataType)
        {
            switch (dataType)
            {
                case EnumKeyDataType.Integer:
                case EnumKeyDataType.Unsigned:
                    return "INTEGER";
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                    return "TEXT";
                default:
                    return "BLOB";
            }
        }

        private void CreateSqliteDataTable(SQLiteConnection connection, BtrieveFile btrieveFile)
        {
            StringBuilder sb = new StringBuilder("CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL");
            foreach(var key in btrieveFile.Keys)
            {
                var segment = key.Value.Segments[0];
                sb.Append($", key{segment.Number} {SqliteType(segment.DataType)} NOT NULL");
            }
            sb.Append(");");

            using var cmd = new SQLiteCommand(sb.ToString(), connection);
            cmd.ExecuteNonQuery();

            using var transaction = connection.BeginTransaction();
            foreach (var record in btrieveFile.Records.OrderBy(x => x.Offset))
            {
                using var insertCmd = new SQLiteCommand(connection);

                sb = new StringBuilder("INSERT INTO data_t(data");
                foreach(var key in btrieveFile.Keys)
                {
                    var segment = key.Value.Segments[0];
                    sb.Append($", key{segment.Number}");
                }
                sb.Append(") VALUES(@data");
                foreach(var key in btrieveFile.Keys)
                {
                    var segment = key.Value.Segments[0];
                    sb.Append($", @key{segment.Number}");
                }
                sb.Append(");");
                insertCmd.CommandText = sb.ToString();

                insertCmd.Parameters.AddWithValue("@data", record.Data);
                foreach(var key in btrieveFile.Keys)
                {
                    var segment = key.Value.Segments[0];
                    var keyData = record.Data.AsSpan().Slice(segment.Offset, segment.Length);
                    insertCmd.Parameters.AddWithValue($"key{segment.Number}", SqliteType(segment, keyData.ToArray()));
                }
                insertCmd.ExecuteNonQuery();
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
            string statement = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL)";

            using var cmd = new SQLiteCommand(statement, connection);
            cmd.ExecuteNonQuery();

            using var insertCmd = new SQLiteCommand(connection);
            cmd.CommandText = "INSERT INTO keys_t(id, attributes, data_type, offset, length) VALUES(@id, @attributes, @data_type, @offset, @length)";

            foreach (var key in btrieveFile.Keys)
            {
                // only grab the first
                var segment = key.Value.Segments[0];
                {
                    cmd.Reset();
                    cmd.Parameters.AddWithValue("@id", segment.Number);
                    cmd.Parameters.AddWithValue("@attributes", segment.Attributes);
                    cmd.Parameters.AddWithValue("@data_type", segment.DataType);
                    cmd.Parameters.AddWithValue("@offset", segment.Offset);
                    cmd.Parameters.AddWithValue("@length", segment.Length);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void CreateSqliteDB(string fullpath, BtrieveFile btrieveFile)
        {
            _logger.Warn($"Creating sqlite db {fullpath}");

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullpath,
            }.ToString();

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            RecordLength = btrieveFile.RecordLength;
            PageLength = btrieveFile.PageLength;
            foreach (var key in btrieveFile.Keys)
                Keys.Add(key.Value.Segments[0].Number, key.Value.Segments[0]);

            CreateSqliteMetadataTable(_connection, btrieveFile);
            CreateSqliteKeysTable(_connection, btrieveFile);
            CreateSqliteDataTable(_connection, btrieveFile);
        }
    }
}

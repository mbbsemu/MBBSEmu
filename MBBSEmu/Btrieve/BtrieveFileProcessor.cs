using MBBSEmu.Btrieve.Enums;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFileProcessor
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly IFileUtility _fileFinder;

        /// <summary>
        ///     File Name of the Btrieve File currently loaded into the Processor
        /// </summary>
        public string LoadedFileName { get; set; }

        /// <summary>
        ///     File Path of the Btrieve File currently loaded into the Processor
        /// </summary>
        public string LoadedFilePath { get; set; }

        /// <summary>
        ///     Btrieve File Loaded into the Processor
        /// </summary>
        public BtrieveFile LoadedFile { get; set; }

        /// <summary>
        ///     Current Position (offset) of the Processor in the Btrieve File
        /// </summary>
        public uint Position { get; set; }

        /// <summary>
        ///     The Previous Query that was executed
        /// </summary>
        private BtrieveQuery PreviousQuery { get; set; }

        /// <summary>
        ///     Constructor to load the specified Btrieve File at the given Path
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="path"></param>
        public BtrieveFileProcessor(string fileName, string path)
        {
            _fileFinder = DependencyInjection.ServiceResolver.GetService<IFileUtility>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            LoadedFilePath = path;
            LoadedFileName = _fileFinder.FindFile(path, fileName);

            //If a .EMU version exists, load it over the .DAT file
            var jsonFileName = LoadedFileName.ToUpper().Replace(".DAT", ".EMU");
            if (File.Exists($"{LoadedFilePath}{jsonFileName}"))
            {
                LoadedFileName = jsonFileName;
                LoadJson(LoadedFilePath, LoadedFileName);
            }
            else
            {
                LoadBtrieve(LoadedFilePath, LoadedFileName);
                SaveJson();
            }

            //Ensure loaded records (regardless of source) are in order by their offset within the Btrieve file
            LoadedFile.Records = LoadedFile.Records.OrderBy(x => x.Offset).ToList();

            //Set Position to First Record
            Position = LoadedFile.Records.OrderBy(x => x.Offset).FirstOrDefault()?.Offset ?? 0;
        }

        /// <summary>
        ///     Loads a Btrieve .DAT File
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        private void LoadBtrieve(string path, string fileName)
        {
            //Sanity Check if we're missing .DAT files and there are available .VIR files that can be used
            var virginFileName = fileName.Replace(".DAT", ".VIR");
            if (!File.Exists($"{path}{fileName}") && File.Exists($"{path}{virginFileName}"))
            {
                File.Copy($"{path}{virginFileName}", $"{path}{fileName}");
                _logger.Warn($"Created {fileName} by copying {virginFileName} for first use");
            }

            //If we're missing a DAT file, just bail. Because we don't know the file definition, we can't just create a "blank" one.
            if (!File.Exists($"{path}{fileName}"))
            {
                _logger.Error($"Unable to locate existing btrieve file {fileName}");
                throw new FileNotFoundException($"Unable to locate existing btrieve file {fileName}");
            }

            LoadedFile = new BtrieveFile(File.ReadAllBytes($"{path}{fileName}")) { FileName = LoadedFileName };
#if DEBUG
            _logger.Info($"Opened {fileName} and read {LoadedFile.Data.Length} bytes");
#endif
            //Only Parse Keys if they are defined
            if (LoadedFile.KeyCount > 0)
                LoadBtrieveKeyDefinitions();

            //Only load records if there are any present
            if (LoadedFile.RecordCount > 0)
                LoadBtrieveRecords();
        }

        /// <summary>
        ///     Loads an MBBSEmu representation of a Btrieve File
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        private void LoadJson(string path, string fileName)
        {
            LoadedFile = JsonConvert.DeserializeObject<BtrieveFile>(File.ReadAllText($"{path}{fileName}"));
        }

        private void SaveJson()
        {
            var jsonFileName = LoadedFileName.ToUpper().Replace(".DAT", ".EMU");
            File.WriteAllText($"{LoadedFilePath}{jsonFileName}",
                JsonConvert.SerializeObject(LoadedFile, Formatting.Indented));
        }

        /// <summary>
        ///     Loads Btrieve Key Definitions from the Btrieve DAT File Header
        /// </summary>
        private void LoadBtrieveKeyDefinitions()
        {
            ushort keyDefinitionBase = 0x110;
            const ushort keyDefinitionLength = 0x1E;
            ReadOnlySpan<byte> btrieveFileContentSpan = LoadedFile.Data;

            ushort currentKeyNumber = 0;
            ushort previousKeyNumber = 0;
            while (currentKeyNumber < LoadedFile.KeyCount)
            {
                var keyDefinition = new BtrieveKeyDefinition { Data = btrieveFileContentSpan.Slice(keyDefinitionBase, keyDefinitionLength).ToArray() };

                if (keyDefinition.Segment)
                {
                    keyDefinition.SegmentOf = previousKeyNumber;
                    keyDefinition.Number = previousKeyNumber;
                }
                else
                {
                    keyDefinition.Number = currentKeyNumber;
                    currentKeyNumber++;
                }

#if DEBUG
                _logger.Info("----------------");
                _logger.Info("Loaded Key Definition:");
                _logger.Info("----------------");
                _logger.Info($"Number: {keyDefinition.Number}");
                _logger.Info($"Total Records: {keyDefinition.TotalRecords}");
                _logger.Info($"Data Type: {keyDefinition.DataType}");
                _logger.Info($"Attributes: {keyDefinition.Attributes}");
                _logger.Info($"Position: {keyDefinition.Position}");
                _logger.Info($"Length: {keyDefinition.Length}");
                _logger.Info("----------------");
#endif
                if (!LoadedFile.Keys.TryGetValue(keyDefinition.Number, out var key))
                {
                    key = new BtrieveKey(keyDefinition);
                    LoadedFile.Keys.Add(keyDefinition.Number, key);
                }
                else
                {
                    key.Segments.Add(keyDefinition);
                }


                keyDefinitionBase += keyDefinitionLength;
            }
        }

        /// <summary>
        ///     Loads Btrieve Records from Data Pages
        /// </summary>
        private void LoadBtrieveRecords()
        {
            var recordsLoaded = 0;
            //Starting at 1, since the first page is the header
            for (var i = 1; i <= LoadedFile.PageCount; i++)
            {
                var pageOffset = (LoadedFile.PageLength * i);
                var recordsInPage = (LoadedFile.PageLength / LoadedFile.RecordLength);

                //Key Page
                if (BitConverter.ToUInt32(LoadedFile.Data, pageOffset + 0x8) == uint.MaxValue)
                    continue;

                //Key Constraint Page
                if (LoadedFile.Data[pageOffset + 0x6] == 0xAC)
                    continue;

                //Page data starts 6 bytes in
                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == LoadedFile.RecordCount)
                        break;

                    //TODO -- Need to figure out the source of this padding and if it's related to the key definition
                    if (BitConverter.ToUInt64(LoadedFile.Data, pageOffset + (LoadedFile.RecordLength * j)) ==
                        ulong.MaxValue)
                    {
                        _logger.Warn("Found Record Padding (8 bytes), adjusting Btrieve Values");
                        LoadedFile.RecordPadding = 8;
                        LoadedFile.RecordLength += 8;
                        recordsInPage = (LoadedFile.PageLength / LoadedFile.RecordLength);
                    }

                    var recordArray = new byte[LoadedFile.RecordLength];
                    Array.Copy(LoadedFile.Data, pageOffset + (LoadedFile.RecordLength * j), recordArray, 0, LoadedFile.RecordLength);

                    //End of Page 0xFFFFFFFF
                    if (BitConverter.ToUInt32(recordArray, 0) == uint.MaxValue)
                        continue;

                    LoadedFile.Records.Add(new BtrieveRecord((uint)(pageOffset + (LoadedFile.RecordLength * j)), recordArray));
                    recordsLoaded++;
                }
            }
#if DEBUG
            _logger.Info($"Loaded {recordsLoaded} records. Resetting cursor to 0");
#endif
        }

        /// <summary>
        ///     Sets Position to the offset of the first Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepFirst()
        {
            Position = LoadedFile.Records.OrderBy(x => x.Offset).FirstOrDefault()?.Offset ?? 0;
            return 0;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepNext()
        {
            var nextRecord = LoadedFile.Records.OrderBy(x => x.Offset).FirstOrDefault(x => x.Offset > Position);

            if (nextRecord == null)
                return 0;

            Position = nextRecord.Offset;
            return 1;
        }

        /// <summary>
        ///     Sets Position to the offset of the next logical record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepPrevious()
        {
            var previousRecord = LoadedFile.Records.Where(x => x.Offset < Position).OrderByDescending(x => x.Offset)
                .FirstOrDefault();

            if (previousRecord == null)
                return 0;

            Position = previousRecord.Offset;
            return 1;
        }

        /// <summary>
        ///     Sets Position to the offset of the last Record in the loaded Btrieve File
        /// </summary>
        /// <returns></returns>
        public ushort StepLast()
        {
            var lastRecord = LoadedFile.Records.OrderByDescending(x => x.Offset).FirstOrDefault();

            if (lastRecord == null)
                return 0;

            Position = lastRecord.Offset;
            return 1;
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
            var result = LoadedFile.Records.FirstOrDefault(x => x.Offset == offset);

            if (result == null)
                _logger.Error($"No Record found at offset {offset}");

            return result;
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
        public void Update(uint offset, byte[] recordData)
        {
            if (recordData.Length != LoadedFile.RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {LoadedFile.RecordLength}, Actual Length {recordData.Length}");

            //Find the Record to Update
            for (var i = 0; i < LoadedFile.Records.Count; i++)
            {
                if (LoadedFile.Records[i].Offset != offset) continue;

                //Update It
                LoadedFile.Records[i].Data = recordData;
                break;
            }

            //Save the Change
            SaveJson();
        }

        /// <summary>
        ///     Inserts a new Btrieve Record at the End of the currently loaded Btrieve File
        /// </summary>
        /// <param name="recordData"></param>
        public void Insert(byte[] recordData)
        {
            if (recordData.Length != LoadedFile.RecordLength)
                _logger.Warn($"Btrieve Record Size Mismatch. Expected Length {LoadedFile.RecordLength}, Actual Length {recordData.Length} ({LoadedFile})");

            //Make it +1 of the last record loaded, or make it 1 if it's the first
            var newRecordOffset = LoadedFile.Records.OrderByDescending(x => x.Offset).FirstOrDefault()?.Offset + 1 ?? 1;

            LoadedFile.Records.Add(new BtrieveRecord(newRecordOffset, recordData));

#if DEBUG
            _logger.Info($"Inserted Record into {LoadedFile.FileName} (Offset: {newRecordOffset})");
#endif
            SaveJson();
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
                    KeyOffset = LoadedFile.Keys[keyNumber].Segments[0].Offset,
                    KeyDataType = LoadedFile.Keys[keyNumber].Segments[0].DataType,
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
                    currentQuery.KeyLength = (ushort) key.Length;
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
                EnumBtrieveOperationCodes.GetFirst => GetByKeyFirstNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetKeyFirst => GetByKeyFirstNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetKeyNext when currentQuery.KeyDataType == EnumKeyDataType.AutoInc => GetByKeyNextAutoInc(currentQuery),
                EnumBtrieveOperationCodes.GetKeyNext => GetByKeyNext(currentQuery),
                EnumBtrieveOperationCodes.GetKeyGreater when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetGreater when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyGreaterAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetGreater => GetByKeyGreaterNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetGreaterOrEqual => GetByKeyGreaterOrEqualNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetLess => GetByKeyLessNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLess when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLessAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetKeyLess => GetByKeyLessNumeric(currentQuery),
                EnumBtrieveOperationCodes.GetLast when currentQuery.KeyDataType == EnumKeyDataType.Zstring => GetByKeyLastAlphabetical(currentQuery),
                EnumBtrieveOperationCodes.GetLast => GetByKeyLastNumeric(currentQuery),
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
            foreach (var record in LoadedFile.Records)
            {
                if (record.Offset == absolutePosition)
                    return record.Data;
            }

            throw new Exception($"No Btrieve Record located at Offset {absolutePosition}");
        }

        /// <summary>
        ///     Returns the defined Data Length of the specified Key
        /// </summary>
        /// <param name="keyNumber"></param>
        /// <returns></returns>
        public ushort GetKeyLength(ushort keyNumber) => (ushort)LoadedFile.Keys[keyNumber].Segments.Sum(x => x.Length);

        /// <summary>
        ///     Returns the defined Key Type of the specified Key
        /// </summary>
        /// <param name="keyNumber"></param>
        /// <returns></returns>
        public EnumKeyDataType GetKeyType(ushort keyNumber) => LoadedFile.Keys[keyNumber].Segments[0].DataType;

        /// <summary>
        ///     Retrieves the First Record, numerically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyFirstAlphabetical(BtrieveQuery query)
        {
            //Since we're doing FIRST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.OrderBy(x => x.Offset);
            var lowestRecordOffset = uint.MaxValue;
            var lowestRecordKeyValue = "ZZZZZZZZZZZZZZZZZ"; //hacky way of ensuring the first record we find is before this alphabetically :P

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKeyValue = Encoding.ASCII.GetString(r.ToSpan().Slice(query.KeyOffset, query.KeyLength)).TrimEnd('\0');

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (string.Compare(recordKeyValue, lowestRecordKeyValue) < 0)
                {
                    lowestRecordOffset = r.Offset;
                    lowestRecordKeyValue = recordKeyValue;
                }
            }

            //No first??? Throw 0
            if (lowestRecordOffset == uint.MaxValue)
            {
                _logger.Warn($"Unable to locate First record by key {LoadedFileName}:{query.KeyOffset}:{query.KeyLength}");
                return 0;
            }
#if DEBUG
            _logger.Info($"Offset set to {lowestRecordOffset}");
#endif
            Position = (uint)lowestRecordOffset;
            return 1;
        }

        /// <summary>
        ///     Retrieves the First Record, numerically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        private ushort GetByKeyFirstNumeric(BtrieveQuery query)
        {
            //Since we're doing FIRST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.OrderBy(x => x.Offset);
            var lowestRecordOffset = uint.MaxValue;
            var lowestRecordKeyValue = uint.MaxValue;

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);
                uint recordKeyValue = 0;

                switch (query.KeyLength)
                {
                    case 0: //null -- so just treat it as 16-bit
                        recordKeyValue = 0;
                        break;
                    case 2:
                        recordKeyValue = BitConverter.ToUInt16(recordKey);
                        break;
                    case 4:
                        recordKeyValue = BitConverter.ToUInt32(recordKey);
                        break;
                }

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (recordKeyValue < lowestRecordKeyValue)
                {
                    lowestRecordOffset = r.Offset;
                    lowestRecordKeyValue = recordKeyValue;
                }
            }

            //No first??? Throw 0
            if (lowestRecordOffset == uint.MaxValue)
            {
                _logger.Warn($"Unable to locate First record by key {LoadedFileName}:{query.KeyOffset}:{query.KeyLength}");
                return 0;
            }
#if DEBUG
            _logger.Info($"Offset set to {lowestRecordOffset}");
#endif
            Position = lowestRecordOffset;
            return 1;
        }

        /// <summary>
        ///     GetNext for Non-AutoInc Key Types
        ///
        ///     Search for the next logical record with a matching Key that comes after the current Position
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyNext(BtrieveQuery query)
        {
            //Searching All Records AFTER current for the given key
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);

            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);

                if (recordKey.SequenceEqual(query.Key))
                {
                    Position = (uint)r.Offset;
                    return 1;
                }
            }

            //Unable to find record with matching key value
            return 0;
        }

        /// <summary>
        ///     GetNext for AutoInc key types
        ///
        ///     Key Value needs to be incremented, then the specific key needs to be found
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyNextAutoInc(BtrieveQuery query)
        {
            //Set Query Value to Current Value
            query.Key = (new ReadOnlySpan<byte>(GetRecord()).Slice(query.KeyOffset, query.KeyLength)).ToArray();

            //Increment the Value & Save It
            switch (query.KeyLength)
            {
                case 2:
                    query.Key = BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(query.Key) + 1));
                    break;
                case 4:
                    query.Key = BitConverter.GetBytes(BitConverter.ToUInt32(query.Key) + 1);
                    break;
                default:
                    throw new Exception($"Unsupported Key Length: {query.KeyLength}");
            }

            return GetByKeyEqual(query);
        }

        /// <summary>
        ///     Gets the First Logical Record for the given key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyEqual(BtrieveQuery query)
        {
            //Searching All Records for the Given Key
            var recordsToSearch = LoadedFile.Records.OrderBy(x => x.Offset);

            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);

                if (recordKey.SequenceEqual(query.Key))
                {
                    Position = r.Offset;
                    return 1;
                }
            }

            //Unable to find record with matching key value
            return 0;
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterAlphabetical(BtrieveQuery query)
        {
            //Since we're doing FIRST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);
            var currentRecordKey = Encoding.ASCII.GetString(query.Key).TrimEnd('\0');

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKeyValue = Encoding.ASCII.GetString(r.ToSpan().Slice(query.KeyOffset, query.KeyLength)).TrimEnd('\0');

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (string.Compare(recordKeyValue, currentRecordKey) > 0)
                {
                    Position = r.Offset;
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Greater Than the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterOrEqualNumeric(BtrieveQuery query)
        {
            //Only searching Records AFTER the current one in logical order
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);
            uint queryKeyValue = 0;

            //Get the Query Key Value
            switch (query.KeyLength)
            {
                case 0: //null -- so just treat it as 16-bit
                    queryKeyValue = 0;
                    break;
                case 2:
                    queryKeyValue = BitConverter.ToUInt16(query.Key);
                    break;
                case 4:
                    queryKeyValue = BitConverter.ToUInt32(query.Key);
                    break;
            }

            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);
                uint recordKeyValue = 0;

                switch (query.KeyLength)
                {
                    case 0: //null -- so just treat it as 16-bit
                        recordKeyValue = 0;
                        break;
                    case 2:
                        recordKeyValue = BitConverter.ToUInt16(recordKey);
                        break;
                    case 4:
                        recordKeyValue = BitConverter.ToUInt32(recordKey);
                        break;
                }

                if (recordKeyValue >= queryKeyValue)
                {
                    Position = r.Offset;
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Greater Than the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyGreaterNumeric(BtrieveQuery query)
        {
            //Only searching Records AFTER the current one in logical order
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);
            uint queryKeyValue = 0;

            //Get the Query Key Value
            switch (query.KeyLength)
            {
                case 0: //null -- so just treat it as 16-bit
                    queryKeyValue = 0;
                    break;
                case 2:
                    queryKeyValue = BitConverter.ToUInt16(query.Key);
                    break;
                case 4:
                    queryKeyValue = BitConverter.ToUInt32(query.Key);
                    break;
            }

            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);
                uint recordKeyValue = 0;

                switch (query.KeyLength)
                {
                    case 0: //null -- so just treat it as 16-bit
                        recordKeyValue = 0;
                        break;
                    case 2:
                        recordKeyValue = BitConverter.ToUInt16(recordKey);
                        break;
                    case 4:
                        recordKeyValue = BitConverter.ToUInt32(recordKey);
                        break;
                }

                if (recordKeyValue > queryKeyValue)
                {
                    Position = r.Offset;
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Retrieves the Next Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessAlphabetical(BtrieveQuery query)
        {
            //Since we're doing FIRST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);
            var currentRecordKey = Encoding.ASCII.GetString(query.Key).TrimEnd('\0');

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKeyValue = Encoding.ASCII.GetString(r.ToSpan().Slice(query.KeyOffset, query.KeyLength)).TrimEnd('\0');

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (string.Compare(recordKeyValue, currentRecordKey) < 0)
                {
                    Position = r.Offset;
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Search for the next logical record after the current position with a Key value that is Less Than or Equal To the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLessNumeric(BtrieveQuery query)
        {
            //Only searching Records AFTER the current one in logical order
            var recordsToSearch = LoadedFile.Records.Where(x => x.Offset > Position).OrderBy(x => x.Offset);
            uint queryKeyValue = 0;

            //Get the Query Key Value
            switch (query.KeyLength)
            {
                case 0: //null -- so just treat it as 16-bit
                    queryKeyValue = 0;
                    break;
                case 2:
                    queryKeyValue = BitConverter.ToUInt16(query.Key);
                    break;
                case 4:
                    queryKeyValue = BitConverter.ToUInt32(query.Key);
                    break;
            }

            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);
                uint recordKeyValue = 0;

                switch (query.KeyLength)
                {
                    case 0: //null -- so just treat it as 16-bit
                        recordKeyValue = 0;
                        break;
                    case 2:
                        recordKeyValue = BitConverter.ToUInt16(recordKey);
                        break;
                    case 4:
                        recordKeyValue = BitConverter.ToUInt32(recordKey);
                        break;
                }

                if (recordKeyValue < queryKeyValue)
                {
                    Position = (uint)r.Offset;
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        ///     Retrieves the Last Record, Alphabetically, by the specified key
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLastAlphabetical(BtrieveQuery query)
        {
            //Since we're doing FIRST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.OrderBy(x => x.Offset);
            var lowestRecordOffset = uint.MaxValue;
            var lowestRecordKeyValue = "\0"; //hacky way of ensuring the first record we find is before this alphabetically :P

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKeyValue = Encoding.ASCII.GetString(r.ToSpan().Slice(query.KeyOffset, query.KeyLength)).TrimEnd('\0');

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (string.Compare(recordKeyValue, lowestRecordKeyValue) > 0)
                {
                    lowestRecordOffset = r.Offset;
                    lowestRecordKeyValue = recordKeyValue;
                }
            }

            //No first??? Throw 0
            if (lowestRecordOffset == uint.MaxValue)
            {
                _logger.Warn($"Unable to locate Last record by key {LoadedFileName}:{query.KeyOffset}:{query.KeyLength}");
                return 0;
            }
#if DEBUG
            _logger.Info($"Offset set to {lowestRecordOffset}");
#endif
            Position = (uint)lowestRecordOffset;
            return 1;
        }

        /// <summary>
        ///     Search for the Last logical record after the current position with the specified Key value
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private ushort GetByKeyLastNumeric(BtrieveQuery query)
        {
            //Since we're doing LAST, we want to search all available records
            var recordsToSearch = LoadedFile.Records.OrderBy(x => x.Offset);
            var highestRecordOffset = uint.MaxValue;
            var highestRecordKeyValue = -1;

            //Loop through each record
            foreach (var r in recordsToSearch)
            {
                var recordKey = r.ToSpan().Slice(query.KeyOffset, query.KeyLength);
                uint recordKeyValue = 0;

                switch (query.KeyLength)
                {
                    case 0: //null -- so just treat it as 16-bit
                        recordKeyValue = 0;
                        break;
                    case 2:
                        recordKeyValue = BitConverter.ToUInt16(recordKey);
                        break;
                    case 4:
                        recordKeyValue = BitConverter.ToUInt32(recordKey);
                        break;
                }

                //Compare the value of the key, we're looking for the lowest
                //If it's lower than the lowest we've already compared, save the offset
                if (recordKeyValue > highestRecordKeyValue)
                {
                    highestRecordOffset = r.Offset;
                    highestRecordKeyValue = (int)recordKeyValue;
                }
            }

            //No first??? Throw 0
            if (highestRecordOffset == uint.MaxValue)
                return 0;
#if DEBUG
            _logger.Info($"Offset set to {highestRecordOffset}");
#endif
            Position = (uint)highestRecordOffset;
            return 1;
        }

    }
}

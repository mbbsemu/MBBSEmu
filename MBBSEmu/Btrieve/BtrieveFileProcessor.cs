using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using MBBSEmu.IO;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFileProcessor
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private BtrieveFile _btrieveFile;

        private readonly IConfiguration _configuration;
        private readonly IFileUtility _fileFinder;

        public ushort RecordLength => _btrieveFile.RecordLength;
        public string FileName => _btrieveFile.FileName;

        private string _path { get; set; }
        private string _fileName { get; set; }

        public ushort CurrentRecordNumber { get; set; }


        public uint AbsolutePosition { get; set; }

        private BtrieveQuery CurrentQuery { get; set; }

        public BtrieveFileProcessor(string fileName, string path, ushort maxRecordLength)
        {

            _configuration = DependencyInjection.ServiceResolver.GetService<IConfiguration>();
            _fileFinder = DependencyInjection.ServiceResolver.GetService<IFileUtility>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            _path = path;

            _fileName = _fileFinder.FindFile(path, fileName);

            //If a .EMU version exists, load it over the .DAT file
            var jsonFileName = fileName.ToUpper().Replace(".DAT", ".EMU");
            if (File.Exists($"{path}{jsonFileName}"))
            {
                _fileName = jsonFileName;
                LoadJson(path, jsonFileName);
            }
            else
            {
                _fileName = fileName;
                _btrieveFile = new BtrieveFile() { FileName = fileName };
                LoadBtrieve(path, fileName);
                SaveJson();
            }
        }

        /// <summary>
        ///     Loads a Btrieve .DAT File
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        private void LoadBtrieve(string path, string fileName)
        {

            var virginFileName = fileName.Replace(".DAT", ".VIR");
            if (!File.Exists($"{path}{fileName}") && File.Exists($"{path}{virginFileName}"))
            {
                File.Copy($"{path}{virginFileName}", $"{path}{fileName}");
                _logger.Warn($"Created {fileName} by copying {virginFileName} for first use");
            }

            //MajorBBS/WG will create a blank btrieve file if attempting to open one that doesn't exist
            if (!File.Exists($"{path}{fileName}"))
            {
                _logger.Warn($"Unable to locate existing btrieve file {fileName}, simulating creation of a new one");

                _btrieveFile = new BtrieveFile {RecordCount = 0};
                return;
            }

            _btrieveFile.Data = File.ReadAllBytes($"{path}{fileName}");
#if DEBUG
            _logger.Info($"Opened {fileName} and read {_btrieveFile.Data.Length} bytes");
            _logger.Info("Parsing Header...");
#endif
            LoadBtrieveHeader();

            //Only Parse Keys if they are defined
            if (_btrieveFile.KeyCount > 0)
                LoadBtrieveKeyDefinitions();

            //Only load records if there are any present
            if (_btrieveFile.RecordCount > 0)
                LoadBtrieveRecords();
        }

        /// <summary>
        ///     Loads an MBBSEmu representation of a Btrieve File
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        private void LoadJson(string path, string fileName)
        {
            _btrieveFile = JsonConvert.DeserializeObject<BtrieveFile>(File.ReadAllText($"{path}{fileName}"));
        }

        private void SaveJson()
        {
            var jsonFileName = _fileName.ToUpper().Replace(".DAT", ".EMU");
            File.WriteAllText($"{_path}{jsonFileName}",
                JsonConvert.SerializeObject(_btrieveFile, Formatting.Indented));
        }

        /// <summary>
        ///     Loads Btrieve File Properties from the File Header
        /// </summary>
        private void LoadBtrieveHeader()
        {

            _btrieveFile.RecordLength = BitConverter.ToUInt16(_btrieveFile.Data, 0x16);
            _btrieveFile.RecordCount = BitConverter.ToUInt16(_btrieveFile.Data, 0x1C);
            _btrieveFile.PageLength = BitConverter.ToUInt16(_btrieveFile.Data, 0x08);
            _btrieveFile.PageCount = (ushort)((_btrieveFile.Data.Length / _btrieveFile.PageLength) - 1); //-1 to not count the header
            _btrieveFile.KeyCount = BitConverter.ToUInt16(_btrieveFile.Data, 0x14);

#if DEBUG
            _logger.Info($"Page Size: {_btrieveFile.PageLength}");
            _logger.Info($"Page Count: {_btrieveFile.PageCount}");
            _logger.Info($"Record Length: {_btrieveFile.RecordLength}");
            _logger.Info($"Record Count: {_btrieveFile.RecordCount}");
            _logger.Info($"Key Count: {_btrieveFile.KeyCount}");
#endif 
        }

        /// <summary>
        ///     Loads Btrieve Key Definitions from the File Header
        /// </summary>
        private void LoadBtrieveKeyDefinitions()
        {
            ushort keyDefinitionBase = 0x110;
            const ushort keyDefinitionLength = 0x1E;
            ReadOnlySpan<byte> btrieveFileContentSpan = _btrieveFile.Data;

            ushort currentKeyNumber = 0;
            ushort previousKeyNumber = 0;
            while (currentKeyNumber < _btrieveFile.KeyCount)
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
                if (!_btrieveFile.Keys.TryGetValue(keyDefinition.Number, out var key))
                {
                    key = new BtrieveKey(keyDefinition);
                    _btrieveFile.Keys.Add(keyDefinition.Number, key);
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
            for (var i = 1; i <= _btrieveFile.PageCount; i++)
            {
                var pageOffset = (_btrieveFile.PageLength * i);
                var recordsInPage = (_btrieveFile.PageLength / _btrieveFile.RecordLength);

                //Key Page
                if (BitConverter.ToUInt32(_btrieveFile.Data, pageOffset + 0x8) == uint.MaxValue)
                    continue;

                //Key Constraint Page
                if (_btrieveFile.Data[pageOffset + 0x6] == 0xAC)
                    continue;


                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == _btrieveFile.RecordCount)
                        break;

                    var recordArray = new byte[_btrieveFile.RecordLength];
                    Array.Copy(_btrieveFile.Data, pageOffset + (_btrieveFile.RecordLength * j), recordArray, 0, _btrieveFile.RecordLength);

                    //End of Page 0xFFFFFFFF
                    if (BitConverter.ToUInt32(recordArray, 0) == uint.MaxValue)
                        continue;

                    _btrieveFile.Records.Add(new BtrieveRecord(pageOffset + (_btrieveFile.RecordLength * j), recordArray));
                    recordsLoaded++;
                }
            }
#if DEBUG
            _logger.Info($"Loaded {recordsLoaded} records. Resetting cursor to 0");
#endif
            CurrentRecordNumber = 0;
        }

        public ushort StepFirst()
        {
            CurrentRecordNumber = 0;

            return (ushort)(_btrieveFile.RecordCount == 0 ? 9 : 1);
        }

        public ushort StepNext()
        {
            if (CurrentRecordNumber + 1 >= _btrieveFile.Records.Count)
                return 0;

            CurrentRecordNumber++;

            return 1;
        }

        public ushort StepPrevious()
        {
            if (CurrentRecordNumber == 0) return 0;

            CurrentRecordNumber--;
            return 1;
        }

        public byte[] GetRecord() => GetRecord(CurrentRecordNumber);

        public byte[] GetRecord(ushort recordNumber) => _btrieveFile.Records[recordNumber].Data;


        public void Update(byte[] recordData) => Update(CurrentRecordNumber, recordData);

        public void Update(ushort recordNumber, byte[] recordData)
        {
            if (recordData.Length != _btrieveFile.RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {_btrieveFile.RecordLength}, Actual Length {recordData.Length}");

            _btrieveFile.Records[recordNumber].Data = recordData;

            if (_btrieveFile.RecordCount == 0)
                _btrieveFile.RecordCount++;

            SaveJson();
        }

        public void Insert(byte[] recordData, bool isVariableLength = false) => Insert(CurrentRecordNumber, recordData, isVariableLength);

        public void Insert(ushort recordNumber, byte[] recordData, bool isVariableLength = false)
        {
            if (!isVariableLength && recordData.Length != _btrieveFile.RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {_btrieveFile.RecordLength}, Actual Length {recordData.Length}");

            //Make it +1 of the last record loaded, or make it 1 if it's the first
            var newRecordOffset = _btrieveFile.Records.OrderByDescending(x => x.Offset).FirstOrDefault()?.Offset + 1 ?? 1;

            _btrieveFile.Records.Insert(recordNumber, new BtrieveRecord(newRecordOffset, recordData));

#if DEBUG
            _logger.Info($"Inserted Record into {_btrieveFile.FileName} (Offset: {newRecordOffset})");
#endif
            SaveJson();
        }

        public ushort Seek(EnumBtrieveOperationCodes operationCode)
        { 
            switch (operationCode)
            {
                case EnumBtrieveOperationCodes.GetFirst:
                    return StepFirst();
                case EnumBtrieveOperationCodes.GetNext:
                    return StepNext();
                case EnumBtrieveOperationCodes.GetPrevious:
                    return StepPrevious();
                default:
                    throw new Exception($"Unsupported Btrieve Operation: {operationCode}");
                    
            }
        }

        /// <summary>
        ///     Determines if the given key is present in the key collection
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ushort SeekByKey(ushort keyNumber, ReadOnlySpan<byte> key, EnumBtrieveOperationCodes operationCode = EnumBtrieveOperationCodes.None, bool newQuery = true)
        {
            if (newQuery)
            {
                CurrentQuery = new BtrieveQuery
                {
                    KeyOffset = _btrieveFile.Keys[keyNumber].Segments[0].Offset,
                    KeyDataType = _btrieveFile.Keys[keyNumber].Segments[0].DataType,
                    Key = new byte[key.Length]
                };

                //Get Key Information By Number

                /*
                 * TODO -- It appears MajorBBS/WG don't respect the Btrieve length for the key, as it's just part of a struct.
                 * There are modules that define in their btrieve file a STRING key of length 1, but pass in a char*
                 * So for the time being, we just make the key length we're looking for whatever was passed in.
                 */
                Array.Copy(key.ToArray(), 0, CurrentQuery.Key, 0, key.Length);

                AbsolutePosition = 0;
            }

            foreach (var r in _btrieveFile.Records.Where(x=> x.Offset > AbsolutePosition))
            {
                ReadOnlySpan<byte> recordKey;
                if (CurrentQuery.KeyDataType == EnumKeyDataType.Integer)
                {
                    recordKey = r.ToSpan().Slice(CurrentQuery.KeyOffset, key.Length);
                }
                else
                {
                    recordKey = r.ToSpan().Slice(CurrentQuery.KeyOffset, key.Length);
                }

                switch (operationCode)
                {
                    case EnumBtrieveOperationCodes.None:
                        {
                            if (recordKey.SequenceEqual(CurrentQuery.Key))
                            {
                                AbsolutePosition = (uint)r.Offset;
                                UpdateRecordNumberByAbsolutePosition(AbsolutePosition);
                                return 1;
                            }
#if DEBUG
                            else
                            {
                                if(CurrentQuery.KeyDataType == EnumKeyDataType.Integer)
                                    _logger.Info($"{BitConverter.ToString(recordKey.ToArray())} != {BitConverter.ToString(CurrentQuery.Key)} (Record: {r.Offset:X4}, Key: {r.Offset + CurrentQuery.KeyOffset:X4})");
                            }
#endif

                            break;
                        }
                    case EnumBtrieveOperationCodes.GetLessThan when CurrentQuery.KeyDataType == EnumKeyDataType.UnsignedBinary:
                        {
                           ushort.TryParse(Encoding.ASCII.GetString(CurrentQuery.Key), out var searchValue);
                            var keyValue = BitConverter.ToUInt16(recordKey);
                            if (keyValue < searchValue)
                            {
                                AbsolutePosition = (uint)r.Offset;
                                UpdateRecordNumberByAbsolutePosition(AbsolutePosition);
                                return 1;
                            }

                            break;
                        }
                }
            }
            return 0;
        }

        public ReadOnlySpan<byte> GetRecordByAbsolutePosition(uint absolutePosition)
        {
            foreach (var record in _btrieveFile.Records)
            {
                if (record.Offset == absolutePosition)
                    return record.Data;
            }

            return null;
        }

        private void UpdateRecordNumberByAbsolutePosition(uint absolutePosition)
        {
            CurrentRecordNumber = 0;
            foreach (var record in _btrieveFile.Records)
            {
                if (record.Offset == absolutePosition)
                    return;

                CurrentRecordNumber++;
            }
        }

        public ushort GetKeyLength(ushort keyNumber) => (ushort)_btrieveFile.Keys[keyNumber].Segments.Sum(x => x.Length);
    }
}

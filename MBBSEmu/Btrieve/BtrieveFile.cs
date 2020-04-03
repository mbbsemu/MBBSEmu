using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public string FileName;
        public ushort RecordCount;
        public ushort MaxRecordLength;
        public ushort RecordLength;
        public ushort CurrentRecordNumber;
        public ushort PageLength;
        public ushort PageCount;
        public ushort KeyCount;
        public uint AbsolutePosition;

        private readonly byte[] _btrieveFileContent;
        public readonly List<BtrieveRecord> Records;
        public readonly Dictionary<ushort, BtrieveKey> Keys;

        private ushort _queryKeyOffset;
        private int _queryKeyLength;
        private EnumKeyDataType _queryKeyType;
        private byte[] _queryKey;

        public BtrieveFile(string fileName, string path, ushort maxRecordLength)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            FileName = fileName;
            Records = new List<BtrieveRecord>();
            Keys = new Dictionary<ushort, BtrieveKey>();

            //Strip any absolute pathing
            if (FileName.ToUpper().StartsWith(@"\BBSV6") || FileName.ToUpper().StartsWith(@"\WGSERV"))
            {
                var relativePathStart = FileName.IndexOf('\\', FileName.IndexOf('\\') + 1);
                FileName = FileName.Substring(relativePathStart + 1);
            }

            //MajorBBS/WG will create a blank btrieve file if attempting to open one that doesn't exist
            if (!File.Exists($"{path}{FileName}"))
            {
                _logger.Warn($"Unable to locate existing btrieve file {FileName}, simulating creation of a new one");

                MaxRecordLength = maxRecordLength;
                RecordCount = 0;
                return;
            }

            MaxRecordLength = maxRecordLength;

            _btrieveFileContent = File.ReadAllBytes($"{path}{FileName}");
#if DEBUG
            _logger.Info($"Opened {FileName} and read {_btrieveFileContent.Length} bytes");
            _logger.Info("Parsing Header...");
#endif
            ParseHeader();

            //Only Parse Keys if they are defined
            if (KeyCount > 0)
                ParseKeyDefinitions();

            if (RecordCount > 0)
            {
                LoadRecords();
            }
            else
            {
#if DEBUG
                _logger.Info($"No records to load");
#endif
            }
        }

        private void ParseHeader()
        {

            RecordLength = BitConverter.ToUInt16(_btrieveFileContent, 0x16);
            RecordCount = BitConverter.ToUInt16(_btrieveFileContent, 0x1C);
            PageLength = BitConverter.ToUInt16(_btrieveFileContent, 0x08);
            PageCount = (ushort)((_btrieveFileContent.Length / PageLength) - 1); //-1 to not count the header
            KeyCount = BitConverter.ToUInt16(_btrieveFileContent, 0x14);

            //TODO: Support this eventually
            if (KeyCount > 1)
                _logger.Warn("MBBSEmu currently only supports 1 Btrieve Key (Key 0) -- all other keys will be ignored");
#if DEBUG
            _logger.Info($"Max Record Length: {MaxRecordLength}");
            _logger.Info($"Page Size: {PageLength}");
            _logger.Info($"Page Count: {PageCount}");
            _logger.Info($"Record Length: {RecordLength}");
            _logger.Info($"Record Count: {RecordCount}");
            _logger.Info($"Key Count: {KeyCount}");
            _logger.Info("Loading Records...");
#endif 
        }

        private void ParseKeyDefinitions()
        {
            var keyDefinitionBase = 0x110;
            var keyDefinitionLength = 0x1E;
            ReadOnlySpan<byte> btrieveFileContentSpan = _btrieveFileContent;

            ushort currentKeyNumber = 0;
            ushort previousKeyNumber = 0;
            while (currentKeyNumber < KeyCount)
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
                if (!Keys.TryGetValue(keyDefinition.Number, out var key))
                {
                    key = new BtrieveKey(keyDefinition);
                    Keys.Add(keyDefinition.Number, key);
                }
                else
                {
                    key.Segments.Add(keyDefinition);
                }


                keyDefinitionBase += keyDefinitionLength;
            }
        }

        private void LoadRecords()
        {
            var recordsLoaded = 0;
            //Starting at 1, since the first page is the header
            for (var i = 1; i <= PageCount; i++)
            {
                var pageOffset = (PageLength * i);
                var recordsInPage = (PageLength / RecordLength);

                if (BitConverter.ToUInt32(_btrieveFileContent, pageOffset + 0x8) == uint.MaxValue)
                    continue;


                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == RecordCount)
                        break;

                    var recordArray = new byte[RecordLength];
                    Array.Copy(_btrieveFileContent, pageOffset + (RecordLength * j), recordArray, 0, RecordLength);

                    //End of Page 0xFFFFFFFF
                    if (BitConverter.ToUInt32(recordArray, 0) == uint.MaxValue)
                        continue;

                    Records.Add(new BtrieveRecord(pageOffset + (RecordLength * j), recordArray));
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

            return (ushort)(RecordCount == 0 ? 9 : 1);
        }

        public ushort StepNext()
        {
            if (CurrentRecordNumber + 1 >= Records.Count)
                return 0;

            CurrentRecordNumber++;

            return 1;
        }

        public byte[] GetRecord() => GetRecord(CurrentRecordNumber);

        public byte[] GetRecord(ushort recordNumber) => Records[recordNumber].Data;


        public void Update(byte[] recordData) => Update(CurrentRecordNumber, recordData);

        public void Update(ushort recordNumber, byte[] recordData)
        {
            if (recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            Records[recordNumber].Data = recordData;

            if (RecordCount == 0)
                RecordCount++;
        }

        public void Insert(byte[] recordData, bool isVariableLength = false) => Insert(CurrentRecordNumber, recordData, isVariableLength);

        public void Insert(ushort recordNumber, byte[] recordData, bool isVariableLength = false)
        {
            if (!isVariableLength && recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            Records.Insert(recordNumber, new BtrieveRecord(0, recordData));
        }

        /// <summary>
        ///     Determines if the given key is present in the key collection
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ushort HasKey(ushort keyNumber, ReadOnlySpan<byte> key, EnumBtrieveOperationCodes operationCode = EnumBtrieveOperationCodes.None, bool newQuery = true)
        {
            if (newQuery)
            {
                _queryKeyOffset = Keys[keyNumber].Segments[0].Offset;
                _queryKeyLength = Keys[keyNumber].Segments.Sum(x => x.Length);
                _queryKeyType = Keys[keyNumber].Segments[0].DataType;

                //Get Key Information By Number
                _queryKey = new byte[_queryKeyLength];
                Array.Copy(key.ToArray(), 0, _queryKey, 0, key.Length);

                AbsolutePosition = 0;
            }

            foreach (var r in Records.Where(x=> x.Offset > AbsolutePosition))
            {
                var recordKey = r.ToSpan().Slice(_queryKeyOffset, _queryKeyLength);

                switch (operationCode)
                {
                    case EnumBtrieveOperationCodes.None:
                        {
                            if (recordKey.SequenceEqual(_queryKey))
                            {
                                AbsolutePosition = (uint)r.Offset;
                                UpdateRecordNumberByAbsolutePosition(AbsolutePosition);
                                return 1;
                            }

                            break;
                        }
                    case EnumBtrieveOperationCodes.GetLessThan when _queryKeyType == EnumKeyDataType.UnsignedBinary:
                        {
                           ushort.TryParse(Encoding.ASCII.GetString(_queryKey), out var searchValue);
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
            foreach (var record in Records)
            {
                if (record.Offset == absolutePosition)
                    return record.Data;
            }

            return null;
        }

        private void UpdateRecordNumberByAbsolutePosition(uint absolutePosition)
        {
            CurrentRecordNumber = 0;
            foreach (var record in Records)
            {
                if (record.Offset == absolutePosition)
                    return;

                CurrentRecordNumber++;
            }
        }
    }
}

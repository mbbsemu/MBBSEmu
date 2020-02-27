using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;

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
            if(KeyCount > 0)
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
            PageCount = (ushort) ((_btrieveFileContent.Length / PageLength) - 1); //-1 to not count the header
            KeyCount = BitConverter.ToUInt16(_btrieveFileContent, 0x14);

            //TODO: Support this eventually
            if(KeyCount > 1)
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
            while(currentKeyNumber < KeyCount)
            {
                var keyDefinition = new BtrieveKeyDefinition { Data = btrieveFileContentSpan.Slice(keyDefinitionBase, keyDefinitionLength).ToArray()};

                //TODO: Support this eventually
                if (keyDefinition.Segment)
                {
                    _logger.Warn("MBBSEmu currently does not support Key Segments, additional segments for this key will be ignored");
                    continue;
                }

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
                var newKey = new BtrieveKey() {Definition = keyDefinition};
                Keys.Add(keyDefinition.Number, newKey);

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

                //Key Page
                //Key Pages have 0xFFFFFFFF at 0x8
                if (BitConverter.ToUInt32(_btrieveFileContent, pageOffset + 8) == uint.MaxValue)
                {
                    //TODO: This will need to change as additional key support added
                    ushort currentKey = 0;

                    pageOffset += 0xC;
                    for (var j = 0; j < Keys[currentKey].Definition.TotalRecords; j++)
                    {
                        var keyRecordLength = Keys[currentKey].Definition.RecordLength;
                        var keyRecord = new byte[keyRecordLength];
                        
                        Array.Copy(_btrieveFileContent, pageOffset + (keyRecordLength * j), keyRecord, 0, keyRecordLength);

                        //Keys Start with 0xFFFFFFFF
                        if (BitConverter.ToUInt32(keyRecord, 0) != uint.MaxValue)
                            break;

                        Keys[currentKey].Keys.Add(new BtrieveKeyRecord(keyRecord, 0, Keys[currentKey].Definition.Length));
                    }
                    continue;
                }

                //Data Page
                if (_btrieveFileContent[pageOffset + 5] == 0x80)
                {
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

                    continue;
                }

                _logger.Warn($"Unknown Btrieve Page Identifier: {_btrieveFileContent[pageOffset + 5]:X2} at {pageOffset + 5:X4}");
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
        public ushort HasKey(ushort keyNumber, ReadOnlySpan<byte> key)
        {
            
            foreach (var k in Keys[keyNumber].Keys)
            {
                var result = true;
                for (var i = 0; i < key.Length; i++)
                {
                    if (k.Key[i] == key[i]) continue;

                    result = false;
                    break;
                }

                if (result)
                {
                    AbsolutePosition = k.Offset;
                    return 1;
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
    }
}

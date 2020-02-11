using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public string FileName;
        public ushort RecordCount;
        public ushort MaxRecordLength;
        public ushort RecordLength;
        public int RecordBaseOffset;
        public ushort CurrentRecordNumber;
        public ushort PageLength;
        public ushort PageCount;
        public ushort KeyLength;
        public ushort KeyCount;

        public int CurrentRecordOffset => (CurrentRecordNumber * RecordLength) + RecordBaseOffset;

        private readonly byte[] _btrieveFileContent;
        private readonly List<BtrieveRecord> _btrieveRecords;
        private readonly List<BtrieveKey> _btrieveKeys;

        public BtrieveFile(string fileName, string path, ushort maxRecordLength)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            FileName = fileName;
            _btrieveRecords = new List<BtrieveRecord>();
            _btrieveKeys = new List<BtrieveKey>();

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
            _logger.Info($"Opened {fileName} and read {_btrieveFileContent.Length} bytes");
            _logger.Info("Parsing Header...");
#endif
            ParseHeader();

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
            PageCount = BitConverter.ToUInt16(_btrieveFileContent, 0x20);
            KeyLength = BitConverter.ToUInt16(_btrieveFileContent, 0x11C);
            KeyCount = BitConverter.ToUInt16(_btrieveFileContent, 0x116);
#if DEBUG
            _logger.Info($"Max Record Length: {MaxRecordLength}");
            _logger.Info($"Page Size: {PageLength}");
            _logger.Info($"Page Count: {PageCount}");
            _logger.Info($"Record Length: {RecordLength}");
            _logger.Info($"Record Count: {RecordCount}");
            _logger.Info($"Key Length: {KeyLength}");
            _logger.Info($"Key Count: {KeyCount}");
            _logger.Info("Loading Records...");
#endif 
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
                if (_btrieveFileContent[pageOffset + 5] == 0x00)
                {
                    pageOffset += 8;

                    //Start of Keys 0xFFFFFF
                    if (BitConverter.ToUInt32(_btrieveFileContent, pageOffset) != uint.MaxValue)
                        continue;

                    pageOffset += 4;
                    for (int j = 0; j < KeyCount; j++)
                    {

                        var keyRecord = new byte[KeyLength];
                        Array.Copy(_btrieveFileContent, pageOffset + (KeyLength * j), keyRecord, 0, KeyLength);

                        //Keys Start with 0xFFFFFFFF
                        if (BitConverter.ToUInt32(keyRecord, 0) != uint.MaxValue)
                            break;

                        _btrieveKeys.Add(new BtrieveKey(keyRecord, 0));
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

                        _btrieveRecords.Add(new BtrieveRecord(pageOffset + (RecordLength * j), recordArray));
                        recordsLoaded++;
                    }
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
            if (CurrentRecordNumber + 1 >= _btrieveRecords.Count)
                return 0;

            CurrentRecordNumber++;

            return 1;
        }

        public byte[] GetRecord() => GetRecord(CurrentRecordNumber);

        public byte[] GetRecord(ushort recordNumber) => _btrieveRecords[recordNumber].Data;


        public void Update(byte[] recordData) => Update(CurrentRecordNumber, recordData);

        public void Update(ushort recordNumber, byte[] recordData)
        {
            if (recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            _btrieveRecords[recordNumber].Data = recordData;

            if (RecordCount == 0)
                RecordCount++;
        }

        public void Insert(byte[] recordData, bool isVariableLength = false) => Insert(CurrentRecordNumber, recordData, isVariableLength);

        public void Insert(ushort recordNumber, byte[] recordData, bool isVariableLength = false)
        {
            if (!isVariableLength && recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            _btrieveRecords.Insert(recordNumber, new BtrieveRecord(0, recordData));
        }

        /// <summary>
        ///     Searches records for a record using the key
        /// </summary>
        /// <param name="key"></param>
        public bool GetRecordByKey(ReadOnlySpan<byte> key)
        {
            var recordFound = false;
            for (ushort i = 0; i < _btrieveRecords.Count; i++)
            {
                var currentRecord = _btrieveRecords[i].Data;
                var isMatch = true;
                for (var j = 0; j < key.Length; j++)
                {
                    if (currentRecord[j] != key[j])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (!isMatch)
                    continue;

                CurrentRecordNumber = i;
                recordFound = true;
                break;
            }

            return recordFound;
        }
    }
}

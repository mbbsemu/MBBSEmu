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
        public int RecordBaseOffset;
        public ushort CurrentRecordNumber;
        public ushort PageSize;
        public ushort PageCount;

        public int CurrentRecordOffset => (CurrentRecordNumber * RecordLength) + RecordBaseOffset;

        private readonly byte[] _btrieveFileContent;
        private readonly List<byte[]> _btrieveRecords;

        public BtrieveFile(string fileName, string path, ushort maxRecordLength)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            FileName = fileName;

            //MajorBBS/WG will create a blank btrieve file if attempting to open one that doesn't exist
            if (!File.Exists($"{path}{FileName}"))
            {
                _logger.Warn($"Unable to locate existing btrieve file {FileName}, simulating creation of a new one");
                _btrieveRecords = new List<byte[]>();
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

            _btrieveRecords = new List<byte[]>(RecordCount);

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
            PageSize = BitConverter.ToUInt16(_btrieveFileContent, 0x08);
            PageCount = BitConverter.ToUInt16(_btrieveFileContent, 0x20);
#if DEBUG
            _logger.Info($"Max Record Length: {MaxRecordLength}");
            _logger.Info($"Page Size: {PageSize}");
            _logger.Info($"Page Count: {PageCount}");
            _logger.Info($"Record Length: {RecordLength}");
            _logger.Info($"Record Count: {RecordCount}");
            _logger.Info("Loading Records...");
#endif 
        }


        private void LoadRecords()
        {
            var recordsLoaded = 0;
            //Starting at 1, since the first page is the header
            for (var i = 1; i <= PageCount; i++)
            {
                var pageOffset = (PageSize * i + 6);
                var recordsInPage = (PageSize / RecordLength);

                //Not a data page?
                if (_btrieveFileContent[pageOffset - 1] != 0x80)
                    continue;

                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == RecordCount)
                        break;

                    var recordArray = new byte[RecordLength];
                    Array.Copy(_btrieveFileContent, pageOffset + (RecordLength * j), recordArray, 0, RecordLength);
                    _btrieveRecords.Add(recordArray);
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
            return (ushort) (RecordCount == 0 ? 0 : 1);
        }

        public ushort StepNext()
        {
            if (CurrentRecordNumber + 1 >= _btrieveRecords.Count)
                return 0;

            CurrentRecordNumber++;

#if DEBUG
            _logger.Info($"New Record Offset: 0x{CurrentRecordOffset:X4}");
#endif
            return 1;
        }

        public byte[] GetRecord() => GetRecord(CurrentRecordNumber);

        public byte[] GetRecord(ushort recordNumber) => _btrieveRecords[recordNumber];


        public void Update(byte[] recordData) => Update(CurrentRecordNumber, recordData);

        public void Update(ushort recordNumber, byte[] recordData)
        {
            if(recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            _btrieveRecords[recordNumber] = recordData;

            if (RecordCount == 0)
                RecordCount++;
        }

        public void Insert(byte[] recordData, bool isVariableLength = false) => Insert(CurrentRecordNumber, recordData, isVariableLength);

        public void Insert(ushort recordNumber, byte[] recordData, bool isVariableLength = false)
        {
            if (!isVariableLength && recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            _btrieveRecords.Insert(recordNumber, recordData);
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
                var currentRecord = _btrieveRecords[i];
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

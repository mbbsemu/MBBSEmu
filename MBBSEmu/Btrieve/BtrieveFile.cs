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
        public ushort RecordLength;

        public ushort CurrentRecordNumber;
        public int CurrentRecordOffset => (CurrentRecordNumber * RecordLength) + 0x206;

        private byte[] _btrieveFileContent;
        private readonly List<byte[]> _btrieveRecords;

        public BtrieveFile(string fileName, string path)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            FileName = fileName;

            _btrieveFileContent = File.ReadAllBytes($"{path}{FileName}");
#if DEBUG
            _logger.Info($"Opened {fileName} and read {_btrieveFileContent.Length} bytes");
            _logger.Info("Parsing Header...");
#endif
            RecordLength = BitConverter.ToUInt16(_btrieveFileContent, 0x16);
            RecordCount = BitConverter.ToUInt16(_btrieveFileContent, 0x1C);
#if DEBUG
            _logger.Info($"Record Length: {RecordLength}");
            _logger.Info($"Record Count: {RecordCount}");
            _logger.Info("Loading Records...");
#endif 
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

        private void LoadRecords()
        {
            for (ushort i = 0; i < RecordCount; i++)
            {
                CurrentRecordNumber = i;
                var recordArray = new byte[RecordLength];
                Array.Copy(_btrieveFileContent, CurrentRecordOffset, recordArray, 0, RecordLength);
                _btrieveRecords.Add(recordArray);
            }

#if DEBUG
            _logger.Info($"Loaded {CurrentRecordNumber} records. Resetting cursor to 0");
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
            CurrentRecordNumber++;

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
        }

        public void Insert(byte[] recordData) => Insert(CurrentRecordNumber, recordData);

        public void Insert(ushort recordNumber, byte[] recordData)
        {
            if (recordData.Length != RecordLength)
                throw new Exception($"Invalid Btrieve Record. Expected Length {RecordLength}, Actual Length {recordData.Length}");

            _btrieveRecords.Insert(recordNumber, recordData);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using MBBSEmu.Logging;
using NLog;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public string FileName;
        public int RecordCount;
        public int RecordLength;

        public ushort CurrentRecordNumber;
        public int CurrentRecordOffset => (CurrentRecordNumber * RecordLength) + 0x206;

        private byte[] _btrieveFileContent;
        private readonly Dictionary<int, byte[]> _btrieveRecords;

        public BtrieveFile(string fileName, string path)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(@"\"))
                path += @"\";

            FileName = fileName;

            _btrieveFileContent = File.ReadAllBytes($"{path}{FileName}");
            _logger.Info($"Opened {fileName} and read {_btrieveFileContent.Length} bytes");

            _logger.Info("Parsing Header...");
            RecordLength = BitConverter.ToUInt16(_btrieveFileContent, 0x16);
            _logger.Info($"Record Length: {RecordLength}");
            RecordCount = BitConverter.ToUInt16(_btrieveFileContent, 0x1C);
            _logger.Info($"Record Count: {RecordCount}");

            _btrieveRecords = new Dictionary<int, byte[]>(RecordCount);
            _logger.Info("Loading Records...");
            if (RecordCount > 0)
            {
                LoadRecords();
            }
            else
            {
                _logger.Info($"No records to load");
            }

        }

        private void LoadRecords()
        {
            for (ushort i = 0; i < RecordCount; i++)
            {
                CurrentRecordNumber = i;
                var recordArray = new byte[RecordLength];
                Array.Copy(_btrieveFileContent, CurrentRecordOffset, recordArray, 0, RecordLength);
                _btrieveRecords[i] = recordArray;
            }

            _logger.Info($"Loaded {CurrentRecordNumber} records. Resetting cursor to 0");
            CurrentRecordNumber = 0;
        }

        public ushort StepFirst()
        {
            CurrentRecordNumber = 0;
            return (ushort) (RecordCount == 0 ? 0 : 1);
        }

        public byte[] GetRecord() => GetRecord(CurrentRecordNumber);

        public byte[] GetRecord(ushort recordNumber) => _btrieveRecords[recordNumber];


    }
}

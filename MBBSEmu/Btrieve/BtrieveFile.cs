using System;
using System.Collections.Generic;
using System.IO;
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

        public int CurrentRecordNumber;
        public int CurrentRecordOffset => (CurrentRecordNumber * RecordLength) + 0x206;

        private byte[] _btrieveFileContent;
        private Dictionary<int, byte[]> _btrieveRecords;

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

            _logger.Info("Loading Records (if any)...");
            _btrieveRecords = new Dictionary<int, byte[]>(RecordCount);
        }

        private void LoadRecords()
        {
            for (var i = 0; i < RecordCount; i++)
            {
                

            }
        }
    }
}

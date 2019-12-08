using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MBBSEmu.Module
{
    public class McvFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        public readonly string FileName;
        public readonly byte[] FileContent;
        public readonly Dictionary<int, string> Messages;

        public McvFile(string fileName, string path = "")
        {
            Messages = new Dictionary<int, string>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(@"\"))
                path += @"\";
            FileName = fileName;

#if DEBUG
            _logger.Info($"Loading MCV File: {fileName}");
#endif
            FileContent = File.ReadAllBytes($"{path}{fileName}");

#if DEBUG
            _logger.Info($"Parsing MCV File: {fileName}");
#endif
            Parse();
        }

        private void Parse()
        {
            Span<byte> fileSpan = FileContent;
            
            //MSGUTL.H -- MCV File Info is always the last 16 bytes of a .MCV file
            var mcvInfo = fileSpan.Slice(fileSpan.Length - 16);
            var languagesOffset = BitConverter.ToInt32(mcvInfo.Slice(0, 4));
            var messageLengthsOffsets = BitConverter.ToInt32(mcvInfo.Slice(4, 4));
            var messageLocationsOffsets = BitConverter.ToInt32(mcvInfo.Slice(8, 4));
            var numberOfLanguages = BitConverter.ToInt16(mcvInfo.Slice(12, 2));
            var numberOfMessages = BitConverter.ToInt16(mcvInfo.Slice(14, 2));

            if(numberOfLanguages > 2)
                throw new Exception("MbbsEmu does not support modules that implement more than 2 languages");

            for (var i = 0; i < numberOfMessages; i++)
            {
                var offsetModifier = i * 4;
                var messageLength = BitConverter.ToInt32(fileSpan.Slice(messageLengthsOffsets + offsetModifier, 4));

                //Usually means there's a second language (RIP more often than not)
                //We won't support RIP, so we ignore the second value and only grab the 1st
                if (messageLength > ushort.MaxValue)
                {
                    messageLength = BitConverter.ToInt16(fileSpan.Slice(messageLengthsOffsets + offsetModifier, 2));
                }

                var messageLocationOffset = BitConverter.ToInt32(fileSpan.Slice(messageLocationsOffsets + offsetModifier, 4));

                var message = Encoding.ASCII.GetString(fileSpan.Slice(messageLocationOffset, messageLength));

                Messages.Add(i, message);
            }

#if DEBUG
            _logger.Info($"Successfully parsed and loaded {numberOfMessages} messages from {FileName}");
#endif
        }

        public bool GetBool(int ordinal) => Messages[ordinal].ToUpper().EndsWith("YES\0");

        public short GetNumeric(int ordinal)
        {
            var splitString = Messages[ordinal].Split(':');
            return short.Parse(splitString[1]);
        }

        public int GetLong(int ordinal)
        {
            var splitString = Messages[ordinal].Split(':');
            return int.Parse(splitString[1]);
        }

        public string GetAscii(int ordinal) => Messages[ordinal];

        public string GetString(int ordinal) => Messages[ordinal];
    }
}

using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MBBSEmu.Extensions;
namespace MBBSEmu.Module
{
    public class McvFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        public readonly string FileName;
        public readonly byte[] FileContent;
        public readonly Dictionary<int, byte[]> Messages;

        public McvFile(string fileName, string path = "")
        {
            Messages = new Dictionary<int, byte[]>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;
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

                var message = fileSpan.Slice(messageLocationOffset, messageLength);

                Messages.Add(i, message.ToArray());
            }

#if DEBUG
            _logger.Info($"Successfully parsed and loaded {numberOfMessages} messages from {FileName}");
#endif
        }

        /// <summary>
        ///     Gets the YES/NO value and returns it as a BOOL
        ///
        ///     Values are always "(space)YES" or "(space)NO", so we check that first non-space character
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public bool GetBool(int ordinal) => GetMessageValue(ordinal)[1] == 'Y';

        public short GetNumeric(int ordinal)
        {
            return short.Parse(GetMessageValue(ordinal).ToCharSpan());
        }

        public int GetLong(int ordinal)
        {
            return int.Parse(GetMessageValue(ordinal).ToCharSpan());
        }

        public ReadOnlySpan<byte> GetString(int ordinal)
        {
            if (!Messages.TryGetValue(ordinal, out var result))
                result = new byte[] {0x0};

            return result;
        }


        /// <summary>
        ///     This is faster than doing a .Split
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        private ReadOnlySpan<byte> GetMessageValue(int ordinal)
        {
            Span<byte> message = Messages[ordinal];
            for (var i = 0; i < message.Length; i++)
            {
                if (message[i] == 0x3A)
                    return message.Slice(i + 1);
            }
            throw new Exception($"Unable to find Message Value for Ordinal {ordinal}");
        }
    }
}

using MBBSEmu.Extensions;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Class for handling MCV file Parsing and loading of values
    /// </summary>
    public class McvFile
    {
        private static readonly byte[] NEW_LINE = {(byte)'\r', (byte)'\n'};

        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public readonly string FileName;
        public readonly byte[] FileContent;
        public readonly Dictionary<int, byte[]> Messages;
        private bool _dynamicLength;

        public McvFile(IFileUtility fileUtility, string fileName, string path = "")
        {
            Messages = new Dictionary<int, byte[]>();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            FileName = fileUtility.FindFile(path, fileName);

#if DEBUG
            _logger.Info($"Loading MCV File: {FileName}");
#endif
            FileContent = File.ReadAllBytes(Path.Combine(path, FileName));

#if DEBUG
            _logger.Info($"Parsing MCV File: {FileName}");
#endif
            Parse();
        }

        /// <summary>
        ///     Constructor Used for Testing, where the keys are manually passed in
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="messages"></param>
        public McvFile(string fileName, Dictionary<int, byte[]> messages)
        {
            FileName = fileName;
            Messages = messages;
        }

        /// <summary>
        ///     Parses the specified MCV File
        /// </summary>
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

            if (languagesOffset == 0)
                _dynamicLength = true;

            if(numberOfLanguages > 2)
                throw new Exception("MbbsEmu does not support modules that implement more than 2 languages");

            var messageLength = 0;
            for (var i = 0; i < numberOfMessages; i++)
            {
                var offsetModifier = i * 4;

                //Get Message Location
                var messageLocationOffset = BitConverter.ToInt32(fileSpan.Slice(messageLocationsOffsets + offsetModifier, 4));

                //Get Message Length
                if (!_dynamicLength)
                {
                    messageLength = BitConverter.ToInt32(fileSpan.Slice(messageLengthsOffsets + offsetModifier, 4));

                    //Usually means there's a second language (RIP more often than not)
                    //We won't support RIP, so we ignore the second value and only grab the 1st
                    if (messageLength > ushort.MaxValue)
                    {
                        messageLength = BitConverter.ToInt16(fileSpan.Slice(messageLengthsOffsets + offsetModifier, 2));
                    }
                }
                else
                {
                    //If no lengths are specified, we treat every entry as a null terminated cstring.
                    //Find the next null
                    for (var j = 0; j < ushort.MaxValue; j++)
                    {
                        if (fileSpan[messageLocationOffset + j] != 0x0) continue;

                        messageLength = j;
                        break;
                    }
                }
                var message = fileSpan.Slice(messageLocationOffset, messageLength);

                using var parsedMessage = new MemoryStream(message.Length * 2);
                foreach (var b in message)
                {
                    //Make sure any New Lines have Carriage Returns, and vice versa
                    if (b == 0xD || b == 0xA)
                        parsedMessage.Write(NEW_LINE);
                    else
                        parsedMessage.WriteByte(b);
                }

                Messages.Add(i, parsedMessage.ToArray());
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

        /// <summary>
        ///     Gets the numeric value of the specified ordinal and returns it as a word
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public short GetNumeric(int ordinal)
        {
            return short.Parse(GetNumericMessageValue(ordinal).ToCharSpan());
        }

        /// <summary>
        ///     Gets the numeric value of the specified ordinal and returns it as a double word
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public int GetLong(int ordinal)
        {
            return int.Parse(GetNumericMessageValue(ordinal).ToCharSpan());
        }

        public ushort GetHex(int ordinal)
        {
            var hexValue = Encoding.ASCII.GetString(GetMessageValue(ordinal));

            return ushort.Parse(hexValue, NumberStyles.HexNumber);
        }

        /// <summary>
        ///     Gets the string value of the specified ordinal
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> GetString(int ordinal)
        {
            if (!Messages.TryGetValue(ordinal, out var result))
                throw new ArgumentOutOfRangeException($"Message Number {ordinal} out of range in {FileName}");

            //Empty String
            if (result.Length == 0)
                return new byte[] { 0x0 };

            //If it's already null terminated, return it
            if (result[^1] == 0x0)
                return result;

            var resultArray = new byte[result.Length + 1];
            Array.Copy(result, 0, resultArray, 0, result.Length);

            return resultArray;
        }

        /// <summary>
        ///     This is faster than doing a .Split
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        private ReadOnlySpan<byte> GetMessageValue(int ordinal)
        {
            ReadOnlySpan<byte> message = Messages[ordinal];

            for (var i = 1; i <= message.Length; i++)
            {
                if (message[^i] == ':' || message[^i] == ' ')
                    return message.Slice(message.Length - i, i);
            }

            //If no terminating characters were found, treat it as if it were a cstring and find the first null
            for (var i = 0; i < message.Length; i++)
            {
                if (message[i] == 0x0)
                    return message.Slice(0, i + 1);
            }

            return message;
        }

        /// <summary>
        ///     Handles logic of parsing out numeric values from the specified MCV field
        ///
        ///     Numeric fields retrieved by numopt(), lngopt(), etc. ignore any leading ascii text prior to
        ///     the number.
        ///
        ///     This method splits the message and assumes the last value in the field is the number
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        private ReadOnlySpan<byte> GetNumericMessageValue(int ordinal)
        {
            var message = Encoding.ASCII.GetString(Messages[ordinal]).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if(message.Length == 0)
                throw new Exception($"Unable to locate value in field {FileName}:{ordinal}");

            //Return the last word which will contain the number
            //This specific issue was found in Infinity Complex
            return Encoding.ASCII.GetBytes(message[^1]);
        }
    }
}

using MBBSEmu.IO;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Parses a given Worldgroup/MajorBBS MSG file and outputs a compiled MCV file
    ///
    ///     Only one language is supported, so anything other than English/ANSI will be
    ///     ignored.
    /// </summary>
    public class MsgFile
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        private readonly string _modulePath;
        private readonly string _moduleName;
        private readonly IFileUtility _fileUtility;

        public readonly string FileName;
        public readonly string FileNameAtRuntime;

        public readonly Dictionary<string, byte[]> MsgValues;

        public MsgFile(IFileUtility fileUtility, string modulePath, string msgName)
        {
            MsgValues = new Dictionary<string, byte[]>();
            _modulePath = modulePath;
            _moduleName = msgName;
            _fileUtility = fileUtility;

            FileName = $"{msgName.ToUpper()}.MSG";
            FileNameAtRuntime = $"{msgName.ToUpper()}.MCV";

            _logger.Debug($"({_moduleName}) Compiling MCV from {FileName}");
            BuildMCV();
        }

        private enum MsgParseState {
            NEWLINE,
            IDENTIFIER,
            SPACE,
            BRACKET,
            JUNK
        };

        private static bool IsIdentifier(char c) =>
            char.IsDigit(c) || (c >= 'A' && c <= 'Z');

        private static bool IsAlnum(char c) =>
            char.IsLetterOrDigit(c);

        private static MemoryStream FixLineEndings(MemoryStream input)
        {

            var rawMessage = input.ToArray();
            input.SetLength(0);

            for (var i = 0; i < rawMessage.Length; ++i)
            {
                var c = rawMessage[i];
                if (c == '\n')
                {
                    var next = i < (rawMessage.Length - 1) ? (char)rawMessage[i + 1] : (char)0;
                    if (!IsAlnum(next) && next != '%' && next != '(' && next != '"')
                    {
                        input.WriteByte((byte)'\r');
                        continue;
                    }
                }
                input.WriteByte(c);
            }

            return input;
        }

        /// <summary>
        ///     Takes the Specified MSG File and compiles a MCV file to be used at runtime
        /// </summary>
        private void BuildMCV()
        {
            var path = _fileUtility.FindFile(_modulePath, Path.ChangeExtension(_moduleName, ".MSG"));
            var fileToRead = File.ReadAllBytes(Path.Combine(_modulePath, path));
            var state = MsgParseState.NEWLINE;
            var identifier = new StringBuilder();
            using var msgValue = new MemoryStream();
            var language = Encoding.ASCII.GetBytes("English/ANSI\0");
            var messages = new List<byte[]>();
            var last = (char)0;

            foreach (var b in fileToRead)
            {
                var c = (char)b;
                switch (state)
                {
                    case MsgParseState.NEWLINE when IsIdentifier(c):
                        state = MsgParseState.IDENTIFIER;
                        identifier.Clear();
                        identifier.Append(c);
                        break;
                    case MsgParseState.NEWLINE when c != '\n':
                        state = MsgParseState.JUNK;
                        break;
                    case MsgParseState.IDENTIFIER when IsIdentifier(c):
                        identifier.Append(c);
                        break;
                    case MsgParseState.IDENTIFIER when char.IsWhiteSpace(c):
                        state = MsgParseState.SPACE;
                        break;
                    case MsgParseState.IDENTIFIER when c == '{':
                        state = MsgParseState.BRACKET;
                        msgValue.SetLength(0);
                        break;
                    case MsgParseState.SPACE when c == '{':
                        state = MsgParseState.BRACKET;
                        msgValue.SetLength(0);
                        break;
                    case MsgParseState.SPACE when !char.IsWhiteSpace(c):
                        state = MsgParseState.JUNK;
                        break;
                    case MsgParseState.BRACKET when c == '~' && last == '~':
                        // double tilde, only output one tilde, so skip second output
                        break;
                    case MsgParseState.BRACKET when c == '}' && last == '~':
                        // escaped ~}, change '~' we've already collected to '}'
                        EscapeBracket(msgValue);
                        break;
                    case MsgParseState.BRACKET when c == '}':
                        var value = FixLineEndings(msgValue);
                        value.WriteByte(0); //Null Terminate

                        if (identifier.ToString().Equals("LANGUAGE"))
                            language = value.ToArray();
                        else
                            messages.Add(value.ToArray());

                        state = MsgParseState.JUNK;
                        msgValue.SetLength(0);
                        break;
                    case MsgParseState.BRACKET when c != '\r':
                        msgValue.WriteByte(b);
                        break;
                    case MsgParseState.JUNK when c == '\n':
                        state = MsgParseState.NEWLINE;
                        break;
                }
                last = c;
            }

            WriteMCV(language, messages);
        }

        private void EscapeBracket(MemoryStream input)
        {
            var arrayToParse = input.ToArray();
            input.SetLength(0);
            input.Write(arrayToParse[..^2]);
            input.WriteByte((byte)'}');
        }

        private void WriteUInt32(Stream stream, int value) => stream.Write(BitConverter.GetBytes(value));
        private void WriteUInt16(Stream stream, short value) => stream.Write(BitConverter.GetBytes(value));

        private void WriteMCV(byte[] language, IList<byte[]> messages, bool writeStringLengthTable = true)
        {
            if (language.Length == 0)
                throw new ArgumentException("Empty language, bad MSG parse");

            /*
            * 16-byte final header is
            *
            * uint32_t ;   // ptr to languages
            * uint32_t ;   // ptr to length array, always last
            * uint32_t ;   // ptr to location list
            * uint16_t ;   // count of languages
            * uint16_t ;   // count of messages
            */
            using var writer = File.Open(Path.Combine(_modulePath, FileNameAtRuntime), FileMode.Create);
            var offsets = new int[messages.Count];

            // write out the languages (just messages[0])
            writer.Write(language);

            var offset = language.Length;

            // write out each string with a null-terminator
            var i = 0;
            foreach (var message in messages)
            {
                offsets[i++] = offset;

                writer.Write(message);
                offset += message.Length;
            }

            var numberOfLanguages = 1;
            var stringLengthArrayPointer = 0;

            // write out the offset table first
            var stringLocationsPointer = offset;
            foreach (var msgOffset in offsets)
                WriteUInt32(writer, msgOffset);

            offset += 4 * offsets.Length;

            // write out the file lengths if requested
            if (writeStringLengthTable)
            {
                stringLengthArrayPointer = offset;

                foreach (var message in messages)
                    WriteUInt32(writer, message.Length + 1);

                // don't need to update offset since it's no longer referenced past this point
            }
            else
            {
                // ptr to location list
                WriteUInt32(writer, stringLocationsPointer);
            }

            // language pointer, always 0
            WriteUInt32(writer, 0);
            // length array
            WriteUInt32(writer, stringLengthArrayPointer);
            // ptr to location list
            WriteUInt32(writer, stringLocationsPointer);
            // count of languages
            WriteUInt16(writer, (short)numberOfLanguages);
            // count of messages
            WriteUInt16(writer, (short)messages.Count);
        }
    }
}

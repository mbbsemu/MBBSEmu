using MBBSEmu.IO;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
            Char.IsDigit(c) || (c >= 'A' && c <= 'Z');

        private static bool IsAlnum(char c) =>
            Char.IsLetterOrDigit(c);

        private static string FixLineEndings(string rawMessage) {
            var ret = new StringBuilder(rawMessage.Length + 16);

            for (var i = 0; i < rawMessage.Length; ++i)
            {
                var c = rawMessage[i];
                if (c == '\n')
                {
                    char next = i < (rawMessage.Length - 1) ? rawMessage[i + 1] : (char)0;
                    if (!IsAlnum(next) && next != '%' && next != '(' && next != '"')
                    {
                        ret.Append('\r');
                        continue;
                    }
                }
                ret.Append(c);
            }
            return ret.ToString();
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
            var str = new StringBuilder();
            var language = "";
            var messages = new List<string>();

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
                    case MsgParseState.IDENTIFIER when Char.IsWhiteSpace(c):
                        state = MsgParseState.SPACE;
                        break;
                    case MsgParseState.SPACE when c == '{':
                        state = MsgParseState.BRACKET;
                        str.Clear();
                        break;
                    case MsgParseState.SPACE when !Char.IsWhiteSpace(c):
                        state = MsgParseState.JUNK;
                        break;
                    case MsgParseState.BRACKET when c == '}':
                        var value = FixLineEndings(str.ToString());

                        if (identifier.ToString().Equals("LANGUAGE"))
                            language = value;
                        else
                            messages.Add(value);

                        state = MsgParseState.JUNK;
                        str.Clear();
                        break;
                    case MsgParseState.BRACKET when c != '\r':
                        str.Append(c);
                        break;
                    case MsgParseState.JUNK when c == '\n':
                        state = MsgParseState.NEWLINE;
                        break;
                }
            }

            WriteMCV(language, messages);
        }

        private byte[] GetStringBytes(string str) => Encoding.ASCII.GetBytes(str + "\0");

        private void WriteUInt32(Stream stream, int value) => stream.Write(BitConverter.GetBytes(value));
        private void WriteUInt16(Stream stream, short value) => stream.Write(BitConverter.GetBytes(value));

        private void WriteMCV(string language, List<string> messages, bool writeStringLengthTable = true)
        {
            /*
            * 16-byte final header is
            *
            * uint32_t ;   // ptr to languages
            * uint32_t ;   // ptr to length array, always last
            * uint32_t ;   // ptr to location list
            * uint16_t ;   // count of languages
            * uint16_t ;   // count of messages
            */
            using (var writer = File.Open(Path.Combine(_modulePath, FileNameAtRuntime), FileMode.Create))
            {
                var offset = 0;
                var offsets = new int[messages.Count];

                // write out the languages (just messages[0])
                writer.Write(GetStringBytes(language));

                offset = language.Length + 1;

                // write out each string with a null-terminator
                var i = 0;
                foreach (var message in messages)
                {
                    offsets[i++] = offset;

                    writer.Write(GetStringBytes(message));
                    offset += message.Length + 1;
                }

                var numberOfLanguages = 1;
                var numberOfMessages = messages.Count;
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

                    foreach (string message in messages)
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
}

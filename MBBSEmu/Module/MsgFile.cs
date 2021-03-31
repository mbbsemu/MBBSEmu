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

        /// <summary>
        ///     Takes the Specified MSG File and compiles a MCV file to be used at runtime
        /// </summary>
        private void BuildMCV()
        {
            using var msOutput = new MemoryStream();
            using var msMessages = new MemoryStream();
            using var msMessageLengths = new MemoryStream();
            using var msMessageOffsets = new MemoryStream();
            using var msCurrentValue = new MemoryStream();

            var messageCount = 0;

            var path = _fileUtility.FindFile(_modulePath, Path.ChangeExtension(_moduleName, ".MSG"));
            var fileToRead = File.ReadAllBytes(path);

            var variableName = string.Empty;
            var bInVariable = false;
            var bIgnoreNext = false;
            var checkForLanguage = false;

            msMessages.Write(Encoding.ASCII.GetBytes("English/ANSI\0"));
            for (var i = 0; i < fileToRead.Length; i++)
            {
                //','
                if (checkForLanguage && fileToRead[i] == ',')
                {
                    bIgnoreNext = true;
                    continue;
                }

                //We're no longer looking for a comma after the previous value
                checkForLanguage = false;

                //{
                if (fileToRead[i] == '{' && !bIgnoreNext && !bInVariable)
                {
                    bInVariable = true;

                    var variableStart = i;
                    i--;

                    while (i > 0 && fileToRead[i] <= ' ')
                        i--;

                    var variableNameEnd = i;

                    //Work Backwards to get Variable Name
                    while (i > 0 && fileToRead[i] >= ' ')
                        i--;
                    var variableNameStart = i;

                    var variableNameLength = (variableNameEnd +1) - variableNameStart;
                    variableName = Encoding.ASCII.GetString(fileToRead, i, variableNameLength).Trim();
                    i = variableStart;
                    continue;
                }

                //New Line Characters get stripped
                if (fileToRead[i] == 0xA)
                    continue;

                //Check for escaped end curly bracket
                if (fileToRead[i] == '~' && fileToRead[i + 1] == '}')
                {
                    i++;
                    msCurrentValue.WriteByte((byte)'}');
                    continue;
                }

                //}
                if (fileToRead[i] == '}')
                {
                    //Check to see if the next character is a comma, denoting another language option
                    checkForLanguage = true;

                    //This is set if we're ignoring a value, usually a language
                    if (bIgnoreNext)
                    {
                        bIgnoreNext = false;
                        continue;
                    }

                    if (!bInVariable)
                        continue;

                    bInVariable = false;

                    //Always null terminate
                    msCurrentValue.WriteByte(0x0);

                    //Language is written first, and not written to the offsets/lengths array
                    //So -- if we parsed it first, write it. If it wasn't in the MSG file, write it
                    //out to the MCV. We always put language first.
                    if (Encoding.ASCII.GetString(msCurrentValue.ToArray()) == "English/ANSI\0" || Encoding.ASCII.GetString(msMessages.ToArray()) == "English/RIP\0")
                    {
                        msMessages.Write(Encoding.ASCII.GetBytes("English/ANSI\0"));
                        msCurrentValue.SetLength(0);
                        continue;
                    }

                    //Write Result to respective output streams
                    msMessageOffsets.Write(BitConverter.GetBytes((int)msMessages.Position));
                    msMessages.Write(msCurrentValue.ToArray());
                    msMessageLengths.Write(BitConverter.GetBytes((int)msCurrentValue.Length));

                    //If it's a duplicate variable name, append with _X where X is the number of duplicate names so far
                    if (MsgValues.ContainsKey(variableName))
                        variableName += $"_{MsgValues.Count(x=> x.Key.StartsWith(variableName))}";

                    //Write to Variable Dictionary
                    MsgValues.Add(variableName, msCurrentValue.ToArray());

                    messageCount++;
                    msCurrentValue.SetLength(0);
                    continue;
                }

                //Otherwise, we're still parsing a value. Write it to the output buffer and move on to the next byte
                if (bInVariable)
                    msCurrentValue.WriteByte(fileToRead[i]);
            }

            //Build Final MCV File
            msOutput.Write(msMessages.ToArray());
            msOutput.Write(msMessageLengths.ToArray());
            msOutput.Write(msMessageOffsets.ToArray());

            //Final 16 Bytes of the MCV file contain the file information for parsing
            msOutput.Write(BitConverter.GetBytes((int)0));
            msOutput.Write(BitConverter.GetBytes((int)msMessages.Length));
            msOutput.Write(BitConverter.GetBytes((int)msMessages.Length + (int)msMessageLengths.Length));
            msOutput.Write(BitConverter.GetBytes((short)1));
            msOutput.Write(BitConverter.GetBytes((short)messageCount));

            //Write it to the disk
            File.WriteAllBytes(Path.Combine(_modulePath, FileNameAtRuntime), msOutput.ToArray());

            _logger.Debug($"({_moduleName}) Compiled {FileNameAtRuntime} ({MsgValues.Count} values, {msOutput.Length} bytes)");
        }
    }
}

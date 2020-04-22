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

        public readonly string FileName;
        public readonly string FileNameAtRuntime;

        public readonly Dictionary<string, byte[]> MsgValues;

        public MsgFile(string modulePath, string msgName)
        {
            MsgValues = new Dictionary<string, byte[]>();
            _modulePath = modulePath;
            _moduleName = msgName;

            FileName = $"{msgName.ToUpper()}.MSG";
            FileNameAtRuntime = $"{msgName.ToUpper()}.MCV";

            _logger.Info($"Compiling MCV from {FileName}");
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

            using var fileToRead = File.OpenRead($"{_modulePath}{_moduleName}.MSG");
            var ringBuffer = new byte[1]; 
            Span<byte> buffer = ringBuffer;
            var variableName = string.Empty;
            var bInVariable = false;
            var bIgnoreNext = false;
            var checkForLanguage = false;

            msMessages.Write(Encoding.ASCII.GetBytes("English/ANSI\0"));
            for (var i = 0; i < fileToRead.Length; i++)
            {
                fileToRead.Read(buffer);

                //','
                if (checkForLanguage && buffer[0] == 0x2C)
                {
                    bIgnoreNext = true;
                    continue;
                }

                //We're no longer looking for a comma after the previous value
                checkForLanguage = false;

                //{
                if (buffer[0] == 0x7B && !bIgnoreNext && !bInVariable)
                {   
                    bInVariable = true;

                    var currentPosition = fileToRead.Position;
                    fileToRead.Position--;
                    //Work Backwards to get Variable Name
                    while (buffer[0] >= 32 && fileToRead.Position > 0)
                    {
                        fileToRead.Read(buffer);
                        fileToRead.Position -= 2;
                    }

                    var variableNameLength = (currentPosition -1) - fileToRead.Position;
                    var variableNameBytes = new byte[variableNameLength];
                    fileToRead.Read(variableNameBytes, 0, variableNameBytes.Length);
                    fileToRead.Position = currentPosition;
                    variableName = Encoding.ASCII.GetString(variableNameBytes).Trim();

                    continue;
                }

                //New Line Characters get stripped
                if (buffer[0] == 0xA)
                    continue;

                //}
                if (buffer[0] == 0x7D)
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

                    //Write to Variable Dictionary
                    MsgValues.Add(variableName, msCurrentValue.ToArray());

                    messageCount++;
                    msCurrentValue.SetLength(0);
                    continue;
                }

                //Otherwise, we're still parsing a value. Write it to the output buffer and move on to the next byte
                if(bInVariable)
                    msCurrentValue.Write(buffer);
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
            File.WriteAllBytes($"{_modulePath}{FileNameAtRuntime}", msOutput.ToArray());

            _logger.Info($"Compiled {FileNameAtRuntime} ({MsgValues.Count} values, {msOutput.Length} bytes)");
        }
    }
}

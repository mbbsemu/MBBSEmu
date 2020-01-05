using MBBSEmu.Logging;
using NLog;
using System;
using System.IO;
using System.Text;
using MBBSEmu.Extensions;
using NLog.LayoutRenderers;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Parses the Module MSG file and builds an output MCV
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

        public MsgFile(string modulePath, string moduleName)
        {
            _modulePath = modulePath;
            _moduleName = moduleName;

            FileName = $"{moduleName.ToUpper()}.MSG";
            FileNameAtRuntime = $"{moduleName.ToUpper()}.MCV";

            _logger.Info($"Compiling MCV from {moduleName.ToUpper()}.MSG");
            BuildMCV();
        }

        /// <summary>
        ///     Takes the Specified Msg File and compiles a MCV file to be used at runtime
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

            var bInVariable = false;
            var bIgnoreNext = false;
            var checkForLanguage = false;
            for (var i = 0; i < fileToRead.Length; i++)
            {
                Span<byte> buffer = ringBuffer;
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
                if (buffer[0] == 0x7B && !bIgnoreNext)
                {
                    bInVariable = true;
                    continue;
                }

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

                    if (bInVariable)
                        bInVariable = false;

                    //Always null terminate
                    msCurrentValue.WriteByte(0x0);

                    //Language is written first, and not written to the offsets/lengths array
                    //So -- if we parsed it first, write it. If it wasn't in the MSG file, write it
                    //out to the MCV. We always put language first.
                    if (msMessages.Length == 0 || Encoding.ASCII.GetString(msCurrentValue.ToArray()) == "English/ANSI\0")
                    {
                        msMessages.Write(Encoding.ASCII.GetBytes("English/ANSI\0"));
                        msCurrentValue.SetLength(0);
                        continue;
                    }

                    //Skip RIP Definition
                    if (Encoding.ASCII.GetString(msMessages.ToArray()) == "English/RIP\0")
                    {
                        msCurrentValue.SetLength(0);
                        continue;
                    }

                    msMessageOffsets.Write(BitConverter.GetBytes((int)msMessages.Position));
                    msMessages.Write(msCurrentValue.ToArray());
                    msMessageLengths.Write(BitConverter.GetBytes((int)msCurrentValue.Length));
                    messageCount++;
                    msCurrentValue.SetLength(0);
                    continue;
                }

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

            _logger.Info($"Compiled {FileNameAtRuntime} ({msOutput.Length} bytes)");
        }
    }
}

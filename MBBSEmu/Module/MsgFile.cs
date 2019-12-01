using System;
using MBBSEmu.Logging;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        public readonly List<MsgRecord> MsgRecords;


        public readonly string FileName;
        public readonly string FileNameAtRuntime;

        public MsgFile(string modulePath, string moduleName)
        {
            _modulePath = modulePath;
            _moduleName = moduleName;

            FileName = $"{moduleName.ToUpper()}.MSG";
            FileNameAtRuntime = $"{moduleName.ToUpper()}.MCV";

            _logger.Info($"Building MCV from {moduleName.ToUpper()}.MSG");
            BuildMCV();
            _logger.Info($"Built {moduleName.ToUpper()}.MCV!");

            
        }

        private void BuildMCV()
        {

            var msOutput = new MemoryStream();
            var sbCurrentValue = new StringBuilder();
            var bMsgRecordMultiline = false;
            var messageCount = 0;
            var msMessages = new MemoryStream();
            var msMessageLengths = new MemoryStream();
            var msMessageOffsets = new MemoryStream();
            foreach (var s in File.ReadAllLines($"{_modulePath}{FileName}"))
            {
                if (string.IsNullOrEmpty(s) || (!s.Contains('{') && !bMsgRecordMultiline))
                    continue;

                //One Line Record
                if (s.Contains('}') && !bMsgRecordMultiline)
                {
                    var startIndex = s.IndexOf('{') + 1;
                    var endIndex = s.IndexOf('}');
                    var length = endIndex - startIndex;
                    sbCurrentValue.Append(s.Substring(startIndex, length));
                }

                //First line of a new multi-line record
                if (!s.Contains('}') && !bMsgRecordMultiline)
                {
                    bMsgRecordMultiline = true;
                    sbCurrentValue.Append(s.Substring(s.IndexOf('{') + 1));
                    continue;
                }

                //Middle of a Multi-Line Record
                if (bMsgRecordMultiline && !s.Contains('}'))
                {
                    sbCurrentValue.Append(s);
                    continue;
                }

                //End of a Multi-Line Record
                if (bMsgRecordMultiline && s.Contains('}'))
                {
                    sbCurrentValue.Append(s.Substring(0, s.IndexOf('}')));
                    bMsgRecordMultiline = false;
                }

                //Add Null Character than append it to the output values
                sbCurrentValue.Append("\0");

                //Language doesn't technically count as a messages entry
                if (s.StartsWith("LANGUAGE"))
                {
                    msMessages.Write(Encoding.ASCII.GetBytes(sbCurrentValue.ToString()));
                    sbCurrentValue.Clear();
                    continue;
                }

                messageCount++;
                msMessageOffsets.Write(BitConverter.GetBytes((int)msMessages.Position));
                msMessages.Write(Encoding.ASCII.GetBytes(sbCurrentValue.ToString()));
                msMessageLengths.Write(BitConverter.GetBytes(sbCurrentValue.Length));
                sbCurrentValue.Clear();
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
        }
    }
}

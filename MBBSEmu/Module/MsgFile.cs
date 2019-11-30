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
            var _outputValues = new List<string>();

            var sbCurrentValue = new StringBuilder();
            var bMsgRecordMultiline = false;
            foreach (var s in File.ReadAllLines($"{_modulePath}{FileName}"))
            {
                if (string.IsNullOrEmpty(s) || (!s.Contains('{') && !bMsgRecordMultiline))
                    continue;

                //One line record
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
                _outputValues.Add(sbCurrentValue.ToString());
                sbCurrentValue.Clear();
            }

            //Build Final Statistics
            var languageOffset = 0;
            var lengthArrayOffset = 0;
            var lengthArray = new List<int>();
            var locationArrayOffset = 0;
            var locationArray = new List<int>();
            var languages = 1;
            var messageCount = _outputValues.Count - 1; //Subtract 1 for language
            var currentOffset = 13;
            foreach (var msg in _outputValues)
            {
                if (msg == "English/ANSI\0")
                    continue;
                locationArray.Add(currentOffset);
                lengthArray.Add(msg.Length);
            }
        }
    }
}

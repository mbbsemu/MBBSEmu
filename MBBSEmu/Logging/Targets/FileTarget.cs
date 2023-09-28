using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MBBSEmu.Logging.Targets
{
    public class FileTarget : IDisposable
    {
        private List<FileStream> _logFiles;

        public FileTarget(string logFile)
        {
            _logFiles = new List<FileStream>
            {
                new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
            };
        }

        public FileTarget() { }

        public void Dispose()
        {
            foreach (var logFile in _logFiles)
                logFile.Dispose();
        }

        public void Write(string message, LogLevel logLevel = LogLevel.Information, string logFile = "")
        {
            //Set the target log file
            FileStream targetLogFile;
            if (string.IsNullOrEmpty(logFile))
            {
                targetLogFile = _logFiles.FirstOrDefault();

                if (targetLogFile == null)
                    throw new Exception("No log file specified");
            }
            else
            {
                targetLogFile = _logFiles.FirstOrDefault(x => x.Name == logFile);

                //If we have the file already open, continue
                if (targetLogFile != null) return;

                targetLogFile = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _logFiles.Add(targetLogFile);
            }

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] {message}{Environment.NewLine}";

            var logMessageBytes = Encoding.ASCII.GetBytes(logMessage);
            targetLogFile.Write(logMessageBytes, 0, logMessageBytes.Length);
        }

    }
}

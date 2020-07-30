using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MBBSEmu.Module
{
    public class MdfFile
    {
        private readonly string _mdfFile;
        public string ModuleName { get; set; }
        public string Developer { get; set; }
        public List<string> DLLFiles { get; set; }
        public List<string> MSGFiles { get; set; }
        public MdfFile(string mdfFile)
        {
            MSGFiles = new List<string>();
            _mdfFile = mdfFile;
            Parse();
        }

        private void Parse()
        {
            foreach (var line in File.ReadAllLines(_mdfFile))
            {
                //Only lines that contain : are actual values
                if (!line.Contains(':'))
                    continue;

                var keyValuePair = line.Split(':');
                switch (keyValuePair[0].ToUpper())
                {
                    case "MODULE NAME":
                        ModuleName = keyValuePair[1].Trim();
                        break;
                    case "DEVELOPER":
                        Developer = keyValuePair[1];
                        break;
                    case "DLLS":
                        DLLFiles = keyValuePair[1].Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS" when keyValuePair[1].Trim().Contains(' '):
                        MSGFiles = keyValuePair[1].Trim().Split(' ').Where(x=> !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS" when keyValuePair[1].Trim().Contains(','):
                        MSGFiles = keyValuePair[1].Trim().Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS":
                        MSGFiles.Add(keyValuePair[1].Trim());
                        break;
                }
            }
        }
    }
}

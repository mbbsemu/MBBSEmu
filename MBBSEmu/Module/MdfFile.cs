using System;
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
                switch (keyValuePair[0])
                {
                    case "Module Name":
                        ModuleName = keyValuePair[1].Trim();
                        break;
                    case "Developer":
                        Developer = keyValuePair[1];
                        break;
                    case "DLLs":
                        DLLFiles = keyValuePair[1].Split(',').ToList();
                        break;
                    case "MSGs":
                        MSGFiles = keyValuePair[1].Split(',').Where(x=> !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                }
            }
        }
    }
}

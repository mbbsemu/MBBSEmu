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

        private MdfFile()
        {
            ModuleName = "test";
            Developer = "test";
            DLLFiles = new List<string>();
            MSGFiles = new List<string>();
        }

        public static MdfFile createForTest()
        {
            return new MdfFile();
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
                    case "MSGS":
                        MSGFiles = keyValuePair[1].Split(' ').Where(x=> !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                }
            }
        }
    }
}

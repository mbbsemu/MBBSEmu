using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Defines a Modules given MDF File
    /// </summary>
    public class MdfFile
    {
        /// <summary>
        ///     MDF File Name
        /// </summary>
        private readonly string _mdfFile;

        /// <summary>
        ///     Module Name defined in the MDF File
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        ///     Developer Name defined in the MDF File
        /// </summary>
        public string Developer { get; set; }

        /// <summary>
        ///     Module DLL files defined in the MDF File
        /// </summary>
        public List<string> DLLFiles { get; set; }

        /// <summary>
        ///     Module MSG files defined in the MDF File
        /// </summary>
        public List<string> MSGFiles { get; set; }

        /// <summary>
        ///     List of DLL files Required by this Module to be loaded
        /// </summary>
        public List<string> Requires { get; set; }

        public List<string> Cleanup { get; set; }
        public List<string> BBSUp { get; set; }

        public MdfFile(string mdfFile)
        {
            MSGFiles = new List<string>();
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
            List<string> cleanup = new();
            List<string> bbsup = new();

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
                    case "REQUIRES":
                        Requires = keyValuePair[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                        break;
                    case "CLEANUP":
                        var value = keyValuePair[1].Trim();
                        if (value.Length > 0)
                            cleanup.Add(value);
                        break;
                    case "BBS UP":
                        value = keyValuePair[1].Trim();
                        if (value.Length > 0)
                            bbsup.Add(value);
                        break;
                    case "DLLS":
                        DLLFiles = keyValuePair[1].Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS" when keyValuePair[1].Trim().Contains(' '): //MSG files separated by a space
                        MSGFiles = keyValuePair[1].Trim().Split(' ').Where(x=> !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS" when keyValuePair[1].Trim().Contains(','): //MSG files separated by a comma
                        MSGFiles = keyValuePair[1].Trim().Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        break;
                    case "MSGS" when !string.IsNullOrWhiteSpace(keyValuePair[1]): //only one MSG file defined
                        MSGFiles.Add(keyValuePair[1].Trim());
                        break;
                }
            }

            Cleanup = cleanup;
            BBSUp = bbsup;
        }
    }
}

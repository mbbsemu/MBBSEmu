using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace MBBSEmu.Btrieve
{
    public class BtrieveFile
    {
        public string FileName;
        public int RecordCount;
        public int RecordSize;


        public BtrieveFile(string fileName, string path)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(@"\"))
                path += @"\";

            FileName = fileName;
        }

        private void ReadHeader()
        {

        }
    }
}

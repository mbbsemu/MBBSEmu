using System;
using System.IO;

namespace MBBSEmu.Btrieve
{
    public class BtrieveUtil
    {
        public static byte[] ReadEntireStream(Stream s)
        {
            var totalRead = 0;
            var buffer = new byte[s.Length];
            while (totalRead != s.Length)
            {
                var numRead = s.Read(buffer, totalRead, (int)s.Length - totalRead);
                // shouldn't happen, but guard against the infinite loop anyhow
                if (numRead == 0)
                {
                    throw new ArgumentException($"Failed to read entire blob stream of length {s.Length}");
                }
                totalRead += numRead;
            }
            return buffer;
        }

    }
}

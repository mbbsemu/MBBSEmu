using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    public class SysvblStruct
    {

        public byte[] Data;

        public const ushort Size = 1300;

        public SysvblStruct()
        {
            Data = new byte[Size];
        }

        public SysvblStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }
    }
}

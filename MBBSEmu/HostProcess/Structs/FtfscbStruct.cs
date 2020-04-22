using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    public class FtfscbStruct
    {
        public byte[] fname
        {
            get
            {
                ReadOnlySpan<byte> dataSpan = Data;
                return dataSpan.Slice(0, 13).ToArray();
            }
            set => Array.Copy(value, 0, Data, 0, 13);
        }

        public byte[] Data;

        public const ushort Size = 77;

        public FtfscbStruct()
        {
            Data = new byte[Size];
        }

        public FtfscbStruct(byte[] data)
        {
            Data = data;
        }

        public FtfscbStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }
    }
}

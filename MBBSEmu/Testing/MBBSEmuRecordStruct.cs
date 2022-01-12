using System.Text;
using System;

namespace MBBSEmu.Testing {
    public class MBBSEmuRecordStruct
    {
        public const int RECORD_LENGTH = 74;

        public byte[] Data { get; }

        // offset 2, length 32
        public string Key0
        {
            get => Encoding.ASCII.GetString(Data.AsSpan().Slice(2, 32)).TrimEnd((char)0);
            set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 2, value.Length);
        }

        // offset 34, length 4
        public int Key1
        {
            get => BitConverter.ToInt32(Data, 34);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 34, 4);
        }

        // offset 38, length 32
        public string Key2
        {
            get => Encoding.ASCII.GetString(Data.AsSpan().Slice(38, 32)).TrimEnd((char)0);
            set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 38, value.Length);
        }

        // offset 70, length 4
        public int Key3
        {
            get => BitConverter.ToInt32(Data, 70);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 70, 4);
        }

        public MBBSEmuRecordStruct() : this(new byte[RECORD_LENGTH]) { }

        public MBBSEmuRecordStruct(byte[] data)
        {
            if (data.Length != RECORD_LENGTH)
            {
                throw new NotSupportedException($"Record length must be {RECORD_LENGTH}");
            }

            Data = data;
        }
    };
}
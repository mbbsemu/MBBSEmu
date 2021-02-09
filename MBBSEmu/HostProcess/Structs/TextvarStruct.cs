using System;
using System.Text;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    public class TextvarStruct
    {
        public string name { get; set; }

        public FarPtr varrou { get; set; }

        private byte[] _data;

        public byte[] Data
        {
            get
            {
                Array.Clear(_data, 0, Size);
                Array.Copy(varrou.Data, 0, _data, 16, FarPtr.Size);

                if (name.Length > 16)
                    name = name.Substring(0, 16);

                Array.Copy(Encoding.ASCII.GetBytes(name), 0, _data, 0, name.Length);

                return _data;
            }
            set
            {
                _data = value;
                name = Encoding.ASCII.GetString(_data, 0, 16).Trim('\0');
                varrou = new FarPtr(value, 16);
            }
        }

        public const ushort Size = 20;

        public TextvarStruct()
        {
            _data = new byte[Size];
        }

        public TextvarStruct(string name, FarPtr varrou) : this()
        {
            this.name = name;
            this.varrou = varrou;
        }

        public TextvarStruct(ReadOnlySpan<byte> data) : this()
        {
            Data = data.ToArray();
        }
    }
}

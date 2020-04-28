using System;
using System.Collections.Generic;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     system-variable btrieve record layout (MAJORBBS.H)
    /// </summary>
    public class SysvblStruct
    {
        /// <summary>
        ///     4-character dummy key of "key"
        /// </summary>
        public byte[] key
        {
            get
            {
                Span<byte> dataSpan = Data;
                return dataSpan.Slice(0, 4).ToArray();
            }
            set => Array.Copy(value, 0, Data, 0, 4);
        }

        /// <summary>
        ///     display options by position number
        /// </summary>
        public byte[] dspopt
        {
            get
            {
                Span<byte> dataSpan = Data;
                return dataSpan.Slice(4, 6).ToArray();
            }
            set => Array.Copy(value, 4, Data, 0, 6);
        }

        public int[] calls
        {
            get
            {
                var result = new List<int>();
                for (var i = 0; i < 8; i++)
                {
                    result.Add(BitConverter.ToInt32(Data, 10 + (i * 4)));
                }

                return result.ToArray();
            }
            set
            {
                
                for (var i = 0; i < 8; i++)
                {
                    Array.Copy(BitConverter.GetBytes(value[i]), 0, Data, 10 + (i * 4), sizeof(int));
                }
            }
        }

        public byte[] lonmsg
        {
            get
            {
                Span<byte> dataSpan = Data;
                return dataSpan.Slice(42, 400).ToArray();
            }
            set => Array.Copy(value, 0, Data, 42, 400);
        }

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

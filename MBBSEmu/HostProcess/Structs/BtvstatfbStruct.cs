using System;
using System.Collections.Generic;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Struct for the buffer response from Btrieve STAT command
    /// </summary>
    public class BtvstatfbStruct
    {
        /// <summary>
        ///     Maximum number of Key Segments within a Btrieve File
        /// </summary>
        private const ushort MAXSEG = 24;

        public BtvfilespecStruct fs
        {
            get => new BtvfilespecStruct(new ReadOnlySpan<byte>(Data).Slice(0, BtvfilespecStruct.Size));
            set => Array.Copy(value.Data, 0, Data, 0, BtvfilespecStruct.Size);
        }

        public BtvkeyspecStruct[] keyspec
        {
            get
            {
                var output = new List<BtvkeyspecStruct>();
                for (var i = 0; i <  MAXSEG; i++)
                {
                    output.Add(new BtvkeyspecStruct(new ReadOnlySpan<byte>(Data).Slice(BtvfilespecStruct.Size + (i * BtvkeyspecStruct.Size), BtvkeyspecStruct.Size)));
                }

                return output.ToArray();
            }

            set
            {
                for (var i = 0; i < value.Length; i++)
                {
                    Array.Copy(value[i].Data, 0, Data, BtvfilespecStruct.Size + (i * BtvkeyspecStruct.Size), BtvkeyspecStruct.Size);
                }
            }
        }

        public byte[] altcol
        {
            get => new ReadOnlySpan<byte>(Data).Slice(BtvfilespecStruct.Size + (BtvkeyspecStruct.Size * MAXSEG), 256).ToArray();
            set => Array.Copy(altcol, 0, Data, BtvfilespecStruct.Size + (BtvkeyspecStruct.Size * MAXSEG), value.Length);
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = BtvfilespecStruct.Size + (BtvkeyspecStruct.Size * MAXSEG) + 265;

        public BtvstatfbStruct()
        {
            
        }

        public BtvstatfbStruct(byte[] data)
        {
            Data = data;
        }

        public BtvstatfbStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }
    }
}

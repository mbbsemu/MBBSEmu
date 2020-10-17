using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{

    /// <summary>
    ///     Representation of the BTVFILE struct
    ///
    ///     More Info: BTVSTF.H
    /// </summary>
    public class BtvFileStruct
    {

        /// <summary>
        ///     Position Block
        /// </summary>
        public int[] posblk
        {
            get
            {
                var result = new int[32];
                for (var i = 0; i < 32; i++)
                {
                    result[i] = BitConverter.ToInt32(Data, (i * 4));
                }

                return result;
            }

            set
            {
                for (var i = 0; i < 32; i++)
                {
                    Array.Copy(BitConverter.GetBytes(value[i]), 0, Data, (i * 4), 4);
                }

            }
        }

        /// <summary>
        ///     file name
        /// </summary>
        public IntPtr16 filenam
        {
            get
            {
                ReadOnlySpan<byte> btvFileStructSpan = Data;
                return new IntPtr16(btvFileStructSpan.Slice(128, 4));
            }
            set => Array.Copy(value.Data, 0, Data, 128, 4);
        }

        /// <summary>
        ///     maximum record length
        /// </summary>
        public ushort reclen
        {
            get => BitConverter.ToUInt16(Data, 132);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 132, 2);
        }

        /// <summary>
        ///     key for searching, etc.
        /// </summary>
        public IntPtr16 key
        {
            get
            {
                ReadOnlySpan<byte> btvFileStructSpan = Data;
                return new IntPtr16(btvFileStructSpan.Slice(134, 4));
            }
            set => Array.Copy(value.Data, 0, Data, 134, 4);
        }

        /// <summary>
        ///     actual record contents
        /// </summary>
        public IntPtr16 data
        {
            get
            {
                ReadOnlySpan<byte> btvFileStructSpan = Data;
                return new IntPtr16(btvFileStructSpan.Slice(138, 4));
            }
            set => Array.Copy(value.Data, 0, Data, 138, 4);
        }

        public readonly byte[] Data = new byte[192];

        public const ushort Size = 192;

        public BtvFileStruct() {}

        public BtvFileStruct(ReadOnlySpan<byte> btvFileStruct)
        {
            Data = btvFileStruct.ToArray();
        }

    }
}

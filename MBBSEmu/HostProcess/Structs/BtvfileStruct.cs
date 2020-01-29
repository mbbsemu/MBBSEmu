using MBBSEmu.Memory;
using System;
using System.Net.NetworkInformation;

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
                    result[i] = BitConverter.ToInt32(_btvFileStructData, (i * 4));
                }

                return result;
            }

            set
            {
                for (var i = 0; i < 32; i++)
                {
                    Array.Copy(BitConverter.GetBytes(value[i]), 0, _btvFileStructData, (i * 4), 4);
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
                ReadOnlySpan<byte> _btvFileStructSpan = _btvFileStructData;
                return new IntPtr16(_btvFileStructSpan.Slice(128, 4));
            }
            set => Array.Copy(value.ToArray(), 0, _btvFileStructData, 128, 4);
        }

        /// <summary>
        ///     maximum record length
        /// </summary>
        public ushort reclen
        {
            get => BitConverter.ToUInt16(_btvFileStructData, 132);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _btvFileStructData, 132, 2);
        }

        /// <summary>
        ///     key for searching, etc.
        /// </summary>
        public IntPtr16 key
        {
            get
            {
                ReadOnlySpan<byte> _btvFileStructSpan = _btvFileStructData;
                return new IntPtr16(_btvFileStructSpan.Slice(134, 4));
            }
            set => Array.Copy(value.ToArray(), 0, _btvFileStructData, 134, 4);
        }

        /// <summary>
        ///     actual record contents
        /// </summary>
        public IntPtr16 data
        {
            get
            {
                ReadOnlySpan<byte> _btvFileStructSpan = _btvFileStructData;
                return new IntPtr16(_btvFileStructSpan.Slice(138, 4));
            }
            set => Array.Copy(value.ToArray(), 0, _btvFileStructData, 138, 4);
        }

        private byte[] _btvFileStructData = new byte[192];

        public const ushort Size = 192;

        public BtvFileStruct()
        {
            
        }

        public BtvFileStruct(ReadOnlySpan<byte> _btvFileStruct)
        {
            _btvFileStructData = _btvFileStruct.ToArray();
        }

        public ReadOnlySpan<byte> ToSpan() => _btvFileStructData;

        public void FromSpan(ReadOnlySpan<byte> btvFileSpan) => _btvFileStructData = btvFileSpan.ToArray();

    }
}

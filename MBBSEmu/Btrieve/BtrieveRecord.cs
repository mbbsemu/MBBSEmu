using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a single Btrieve record within a given Btrieve FIle
    /// </summary>
    public class BtrieveRecord
    {
        /// <summary>
        ///     Physical Offset in the Btrieve File of this record
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        ///     Record Data
        /// </summary>
        public byte[] Data { get; set; }

        public BtrieveRecord(uint offset, byte[] data)
        {
            Offset = offset;
            Data = data;
        }

        public ReadOnlySpan<byte> ToSpan() => Data;
    }
}

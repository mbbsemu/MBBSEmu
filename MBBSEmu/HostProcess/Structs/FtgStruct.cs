using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     File Tagspec Handling Structure (FTG.H)
    /// </summary>
    public class FtgStruct
    {

        /// <summary>
        ///     Application-specific tagspec
        /// </summary>
        public byte[] tagspc
        {
            get
            {
                ReadOnlySpan<byte> dataSpan = Data;
                return dataSpan.Slice(0, 17).ToArray();
            }
            set => Array.Copy(value, 0, Data, 0, 17);
        }

        /// <summary>
        ///     tagspec flags
        /// </summary>
        public byte flags
        {
            get => Data[17];
            set => Data[17] = value;
        }

        /// <summary>
        ///     application's tagspec handler routine
        /// </summary>
        public FarPtr tshndl
        {
            get
            {
                ReadOnlySpan<byte> dataSpan = Data;
                return new FarPtr(dataSpan.Slice(18,4));
            }
            set => Array.Copy(value.Data, 0, Data, 18, FarPtr.Size);
        }

        public readonly byte[] Data;

        public const ushort Size = 22;

        public FtgStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }

        [Flags]
        public enum TagSpecFlags
        {
            /// <summary>
            ///     wild? yes=multi-file no=single file
            /// </summary>
            FTGWLD = 1,

            /// <summary>
            ///     taggable? no=download now or never
            /// </summary>
            FTGABL = 1 << 1

        }

        public enum TagSpecFunctionCodes
        {
            /// <summary>
            /// describe tagspec in English (store in tshmsg)
            /// </summary>
            TSHDSC = 1,

            /// <summary>
            /// is individual file visible? (rc=1 yes, rc=0 no)
            /// </summary>
            TSHVIS = 2,

            /// <summary>
            /// initiate scan
            /// </summary>
            TSHSCN = 3,

            /// <summary>
            ///  break down into next non-wild, visible, tagspec
            /// </summary>
            TSHNXT = 4,

            /// <summary>
            /// check download permission & begin (1=ok 0=denied)
            /// </summary>
            TSHBEG = 5,

            /// <summary>
            /// end complete download
            /// </summary>
            TSHEND = 6,

            /// <summary>
            /// abort incomplete download, unreserve etc
            /// </summary>
            TSHSKP = 7,

            /// <summary>
            /// finished downloading or tagging files
            /// </summary>
            TSHFIN = 8,

            /// <summary>
            /// user disconnected (called in place of TSHFIN)
            /// </summary>
            TSHHUP = 9,

            /// <summary>
            /// untag a file that was tagged
            /// </summary>
            TSHUNT = 10
        }
    }
}

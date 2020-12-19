using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     User-id cross reference structure
    ///
    ///     MAJORBBS.H
    /// </summary>
    public class UidxrefStruct
    {
        public const int XRFSIZ = 15; // user-id search string size
        public const int UIDSIZ = 30; // user-id size (including trailing zero)
        

        public byte[] xrfstg
        {
            get
            {
                ReadOnlySpan<byte> xrfstgSpan = Data;
                return xrfstgSpan.Slice(0, XRFSIZ + 1).ToArray();
            }
            set => Array.Copy(value, 0, Data, 0, value.Length);
        }

        public byte[] userid
        {
            get
            {
                ReadOnlySpan<byte> useridSpan = Data;
                return useridSpan.Slice(0, UIDSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 16, value.Length);
        }

        public byte[] xrfspare
        {
            get
            {
                ReadOnlySpan<byte> xrfspareSpan = Data;
                return xrfspareSpan.Slice(0, UIDSIZ).ToArray();
            }
            set => Array.Copy(value, 0, Data, 46, value.Length);
        }

        public const ushort Size = 52;

        public readonly byte[] Data = new byte[Size];

        public UidxrefStruct(ReadOnlySpan<byte> UidxrefStruct)
        {
            Data = UidxrefStruct.ToArray();
        }
    }
}

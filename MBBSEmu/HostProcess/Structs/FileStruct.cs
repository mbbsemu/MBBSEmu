using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Representation of the C++ FILE Struct
    ///
    ///     More Info: See STDIO.H in Borland C++ INCLUDE folder
    /// </summary>
    public class FileStruct
    {
        /// <summary>
        ///     File Access Flags passed into C++ FOPEN method
        ///
        ///     Reference: http://www.cplusplus.com/reference/cstdio/fopen/
        /// </summary>
        [Flags]
        public enum EnumFileAccessFlags : byte
        {
            Read = 1,
            Write = 1 << 1,
            Append = 1 << 2,
            Update = 1 << 3,
            Binary = 1 << 4,
            Text = 1 << 5
        }

        [Flags]
        public enum EnumFileFlags : ushort
        {
            Read = 1,
            Write = 1 << 1,
            ReadWrite = 1 << 2,
            Buffered = 1 << 3,
            Error = 1 << 4,
            EOF = 1 << 5,
            Binary = 1 << 6,
            DataIncoming = 1 << 7,
            DataOutgoing = 1 << 8,
            FileIsATerminal = 1 << 9
        }

        //fill/empty level of buffer [0-1]
        public ushort level
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, 2);
        }

        //File status flags [2-3]
        public ushort flags
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, 2);
        }

        //File descriptor [4]
        public byte fd
        {
            get => Data[4];
            set => Data[4] = value;
        }

        //Ungetc char if no buffer [5]
        public byte hold {
            get => Data[5];
            set => Data[5] = value;
        }

        //Buffer size [6-7]
        public ushort bsize {
            get => BitConverter.ToUInt16(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, 2);
        }

        //Data transfer buffer [8-11]
        public FarPtr buffer
        {
            get
            {
                ReadOnlySpan<byte> fileSpan = Data;
                return new FarPtr(fileSpan.Slice(8,4));
            }
            set => Array.Copy(value.Data, 0, Data, 8, 4);
        }

        //Current active pointer [12-15]
        public FarPtr curp {
            get
            {
                ReadOnlySpan<byte> fileSpan = Data;
                return new FarPtr(fileSpan.Slice(12, 4));
            }
            set => Array.Copy(value.Data, 0, Data, 12, 4);
        }

        //Temporary file indicator [16]
        public byte istemp {
            get => Data[16];
            set => Data[16] = value;
        }

        //Used for validity checking [17-18]
        public short token
        {
            get => BitConverter.ToInt16(Data, 17);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 17, 2);
        }

        public const ushort Size = 19;

        public byte[] Data = new byte[Size];

        public FileStruct() { }

        public FileStruct(ReadOnlySpan<byte> structData) => Data = structData.ToArray();

        /// <summary>
        ///     Sets the Initial FLAGS value on the FILE struct using the flags passed into FOPEN
        /// </summary>
        /// <param name="filesAccessFlags"></param>
        public void SetFlags(EnumFileAccessFlags filesAccessFlags)
        {
            if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Read) &&
                filesAccessFlags.HasFlag(EnumFileAccessFlags.Write))
                flags |= (ushort) EnumFileFlags.ReadWrite;
            else if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Read))
                flags |= (ushort) EnumFileFlags.Read;
            else if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Write))
                flags |= (ushort) EnumFileFlags.Write;
        }

        /// <summary>
        ///     Parses File Access characters passed into FOPEN
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static EnumFileAccessFlags CreateFlagsEnum(ReadOnlySpan<byte> flags)
        {
            var result = EnumFileAccessFlags.Text;

            foreach (var f in flags)
            {
                switch ((char)f)
                {
                    case 'r':
                        result |= EnumFileAccessFlags.Read;
                        break;
                    case 'w':
                        result |= EnumFileAccessFlags.Write;
                        break;
                    case 'a':
                        result |= EnumFileAccessFlags.Append;
                        break;
                    case '+':
                        result |= EnumFileAccessFlags.Update;
                        break;
                    case 'b':
                    {
                        result &= ~EnumFileAccessFlags.Text;
                        result |= EnumFileAccessFlags.Binary;
                        break;
                    }
                    case 't':
                    {
                        result &= ~EnumFileAccessFlags.Binary;
                        result |= EnumFileAccessFlags.Text;
                        break;
                    }
                    case ' ':
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown File Access Flag: {(char)f}");
                }
            }
            return result;
        }
    }
}

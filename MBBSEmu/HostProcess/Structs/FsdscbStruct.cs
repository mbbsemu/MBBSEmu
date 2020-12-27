using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Full-Screen Display Session Control Block
    ///
    ///     FSD.H
    /// </summary>
    public class FsdscbStruct
    {
        /// <summary>
        ///     Field Specifications String
        /// </summary>
        public FarPtr fldspc
        {
            get => new FarPtr(Data);
            set => Array.Copy(value.Data, 0, Data, 0, FarPtr.Size);
        }

        /// <summary>
        ///     Room for fsdppc() to put array of field info
        /// </summary>
        public FarPtr flddat
        {
            get => new FarPtr(Data, 4);
            set => Array.Copy(value.Data, 0, Data, 4, FarPtr.Size);
        }

        /// <summary>
        ///     room for fsdppc() to put embedded punctuation templts
        /// </summary>
        public FarPtr mbpunc
        {
            get => new FarPtr(Data, 8);
            set => Array.Copy(value.Data, 0, Data, 8, FarPtr.Size);
        }

        /// <summary>
        ///     Room for fsdans(), etc. to put answer output
        /// </summary>
        public FarPtr newans
        {
            get => new FarPtr(Data, 12);
            set => Array.Copy(value.Data, 0, Data, 12, FarPtr.Size);
        }

        /// <summary>
        ///     Field verify routine, or NULL
        /// </summary>
        public FarPtr fldvfy
        {
            get => new FarPtr(Data, 16);
            set => Array.Copy(value.Data, 0, Data, 16, FarPtr.Size);
        }

        /// <summary>
        ///     Attribute for field cursor is on
        /// </summary>
        public byte crsatr
        {
            get => Data[20];
            set => Data[20] = value;
        }

        /// <summary>
        ///     Number of fields in field spec
        /// </summary>
        public ushort numfld
        {
            get => BitConverter.ToUInt16(Data, 21);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 21, sizeof(ushort));
        }

        /// <summary>
        ///     Number of fields in template (numtpl &lt;= numfld)
        /// </summary>
        public ushort numtpl
        {
            get => BitConverter.ToUInt16(Data, 23);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 23, sizeof(ushort));
        }

        /// <summary>
        ///     Actual length of mbpunc array, in bytes, incl NULs
        /// </summary>
        public ushort mbleng
        {
            get => BitConverter.ToUInt16(Data, 25);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 25, sizeof(ushort));
        }

        /// <summary>
        ///     Max possible length of answer string (not incl NUL)
        /// </summary>
        public ushort maxans
        {
            get => BitConverter.ToUInt16(Data, 27);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 27, sizeof(ushort));
        }

        /// <summary>
        ///     Help field length, or 0 if no help field
        /// </summary>
        public byte hlplen
        {
            get => Data[29];
            set => Data[29] = value;
        }

        /// <summary>
        ///     Help field position command
        /// </summary>
        public byte[] hlpgto
        {
            get => new ReadOnlySpan<byte>(Data).Slice(30, 9).ToArray();
            set => Array.Copy(value, 0, Data, 27, hlpgto.Length);
        }

        /// <summary>
        ///     Help field attribute
        /// </summary>
        public byte hlpatr
        {
            get => Data[39];
            set => Data[39] = value;
        }

        /// <summary>
        ///     Help field offset in template
        /// </summary>
        public ushort hlpoff
        {
            get => BitConverter.ToUInt16(Data, 40);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 40, sizeof(ushort));
        }

        /// <summary>
        ///     Actual length of entire current answer string
        /// </summary>
        public ushort allans
        {
            get => BitConverter.ToUInt16(Data, 42);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 42, sizeof(ushort));
        }

        /// <summary>
        ///     FSD State Code
        /// </summary>
        public byte state
        {
            get => Data[44];
            set => Data[44] = value;
        }

        /// <summary>
        ///     Current Answer
        /// </summary>
        public byte[] ansbuf
        {
            get => new ReadOnlySpan<byte>(Data).Slice(45, 81).ToArray();
            set => Array.Copy(value, 0, Data, 45, value.Length);
        }

        /// <summary>
        ///     Length of Current Answer + Buffered Chars
        /// </summary>
        public byte anslen
        {
            get => Data[126];
            set => Data[126] = value;
        }

        /// <summary>
        ///     Cursor position in current answer field
        /// </summary>
        public byte ansptr
        {
            get => Data[127];
            set => Data[127] = value;
        }

        /// <summary>
        ///     Type-ahead buffer
        /// </summary>
        public byte[] typahd
        {
            get => new ReadOnlySpan<byte>(Data).Slice(128, 20).ToArray();
            set => Array.Copy(value, 0, Data, 128, typahd.Length);
        }

        /// <summary>
        ///     # bytes stuffed into typahd[] by fsdinc()
        /// </summary>
        public byte ahdptr
        {
            get => Data[148];
            set => Data[148] = value;
        }

        /// <summary>
        ///     # bytes in typahd[] processed by fsdprc()
        /// </summary>
        public byte hdladh
        {
            get => Data[149];
            set => Data[149] = value;
        }

        /// <summary>
        ///     Index of field just entered (when state --> FSDBUF)
        /// </summary>
        public byte entfld
        {
            get => Data[150];
            set => Data[150] = value;
        }

        /// <summary>
        ///     fsdinc()'s idea of the current cursor field
        /// </summary>
        public byte crsfld
        {
            get => Data[151];
            set => Data[151] = value;
        }

        /// <summary>
        ///     shuffled field, see cursor shuffling, below
        /// </summary>
        public byte shffld
        {
            get => Data[152];
            set => Data[152] = value;
        }

        /// <summary>
        ///     Pointer into field template of current (entfld) field
        /// </summary>
        public FarPtr ftmptr
        {
            get => new FarPtr(Data, 153);
            set => Array.Copy(value.Data, 0, Data, 153, FarPtr.Size);
        }

        /// <summary>
        ///     Flags for the FSD
        /// </summary>
        public byte flags
        {
            get => Data[157];
            set => Data[157] = value;
        }

        /// <summary>
        ///     Keeps track of multiple choice options
        /// </summary>
        public FarPtr altptr
        {
            get => new FarPtr(Data, 158);
            set => Array.Copy(value.Data, 0, Data, 158, FarPtr.Size);
        }

        /// <summary>
        ///     Keystroke that initiated exit of field
        /// </summary>
        public ushort xitkey
        {
            get => BitConverter.ToUInt16(Data, 162);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 162, sizeof(ushort));
        }

        /// <summary>
        ///     Count of changes during session
        /// </summary>
        public byte chgcnt
        {
            get => Data[164];
            set => Data[164] = value;
        }

        /// <summary>
        ///     Maximum "Y" coordinate of session/display
        /// </summary>
        public byte maxy
        {
            get => Data[165];
            set => Data[165] = value;
        }

        /// <summary>
        ///     Size of the Struct
        /// </summary>
        public const ushort Size = 166;

        /// <summary>
        ///     Raw Struct Data
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        ///     Constructor with initial value parameter
        /// </summary>
        /// <param name="structData"></param>
        public FsdscbStruct(ReadOnlySpan<byte> structData)
        {
            if (structData.Length != Size)
                throw new Exception($"Invalid size for Fsdscb ({Data.Length} bytes, expected {Size} length)");

            Data = structData.ToArray();
        }

    }
}

using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     FSDFLD Struct
    ///
    ///     FSD internal field information structure, per field
    ///
    ///     FSD.H
    /// </summary>
    public class Fsdfld
    {
        /// <summary>
        ///     ANSI command to go to field start
        /// </summary>
        public byte[] ansgto
        {
            get => new ReadOnlySpan<byte>(Data).Slice(0, 9).ToArray();
            set => Array.Copy(value, 0, Data, 0, value.Length);
        }

        /// <summary>
        ///     max chars in this answer (width <= ANSLEN)
        /// </summary>
        public byte width
        {
            get => Data[9];
            set => Data[9] = value;
        }

        /// <summary>
        ///     width incl embedded punct (xwidth &lt;= ANSLEN)
        /// </summary>
        public byte xwidth
        {
            get => Data[10];
            set => Data[10] = value;
        }

        /// <summary>
        ///     display attribute (when cursor not on field)
        /// </summary>
        public byte attr
        {
            get => Data[11];
            set => Data[11] = value;
        }

        /// <summary>
        ///     fsdfld flags
        /// </summary>
        public byte flags
        {
            get => Data[12];
            set => Data[12] = value;
        }

        /// <summary>
        ///     ield type: ? $ # Y
        /// </summary>
        public byte fldtyp
        {
            get => Data[13];
            set => Data[13] = value;
        }

        /// <summary>
        ///     offset of field name in field specifications string
        /// </summary>
        public ushort fspoff
        {
            get => BitConverter.ToUInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 14, sizeof(ushort));
        }

        /// <summary>
        ///     offset of first char of field in template string
        /// </summary>
        public ushort tmpoff
        {
            get => BitConverter.ToUInt16(Data, 16);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 16, sizeof(ushort));
        }

        /// <summary>
        ///     offset field template in mbpunc array (or -1=no mbpunc)
        /// </summary>
        public ushort mbpoff
        {
            get => BitConverter.ToUInt16(Data, 18);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 18, sizeof(ushort));
        }

        /// <summary>
        ///     offset of answer (after '=') in new answer string
        /// </summary>
        public ushort ansoff
        {
            get => BitConverter.ToUInt16(Data, 20);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 20, sizeof(ushort));
        }

        /// <summary>
        ///     length of answer in answer string
        /// </summary>
        public byte anslen
        {
            get => Data[22];
            set => Data[22] = value;
        }

        public readonly byte[] Data;

        public const ushort Size = 23;

        public Fsdfld()
        {
            Data = new byte[Size];
        }

        public Fsdfld(ReadOnlySpan<byte> data)
        {
            Data = data;
        }
    }
}

using System;

namespace MBBSEmu.HostProcess.Structs
{
    public class ExtUser
    {
        /// <summary>
        ///     user language, 0 to nlingo-1 (LINGO.H)
        /// </summary>
        public short lingo
        {
            get => BitConverter.ToInt16(Data, 0);
        }

        /// <summary>
        ///     current column for secret char echo
        /// </summary>
        public byte col
        {
            get => Data[2];
        }

        /// <summary>
        ///     line width for secret char echo
        /// </summary>
        public byte wid
        {
            get => Data[3];
        }

        /// <summary>
        ///     character to echo for secret char echo
        /// </summary>
        public byte ech
        {
            get => Data[4];
        }

        /// <summary>
        ///     chan baud rate (obsoletes usrptr->baud)
        /// </summary>
        public int baud
        {
            get => BitConverter.ToInt32(Data, 5);
        }

        /// <summary>
        ///     ticker time when SPX terminated
        /// </summary>
        public byte tspxt
        {
            get => Data[9];
        }

        /// <summary>
        ///     lngtck when channel reset (rstchn())
        /// </summary>
        public uint tckrst
        {
            get => BitConverter.ToUInt32(Data, 10);
        }

        /// <summary>
        ///     lngtck when logging on (lonstf())
        /// </summary>
        public uint tckonl
        {
            get => BitConverter.ToUInt32(Data, 14);
        }

        /// <summary>
        ///     count-down for byenow() time-out
        /// </summary>
        public byte byecnt
        {
            get => Data[18];
        }

        /// <summary>
        ///     entered A/A mode to go to this state
        /// </summary>
        public short entstt
        {
            get => BitConverter.ToInt16(Data, 19);
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 21;
    }
}

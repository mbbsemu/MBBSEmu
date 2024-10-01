using System;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     USER Struct as defined in MAJORBBS.H
    /// </summary>
    public class User : MemoryResidentStructBase
    {
        /// <summary>
        ///     Class (offline, or flavor of online) 
        /// </summary>
        public short UserClass
        {
            get => BitConverter.ToInt16(Data);
            set => BitConverter.GetBytes(value).CopyTo(Data[..sizeof(short)]);
        }

        /// <summary>
        ///     Dynamically allocated array of key bits 
        /// </summary>
        public FarPtr Keys
        {
            get => new(Data.Slice(2, 4));
            set => value.Data.CopyTo(Data.Slice(2, FarPtr.Size));
        }

        /// <summary>
        ///     State (Module Number in effect)
        /// </summary>
        public short State
        {
            get => BitConverter.ToInt16(Data[6..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(6, sizeof(short)));
        }

        /// <summary>
        ///     Substate (for convenience of Module)
        /// </summary>
        public short Substt
        {
            get => BitConverter.ToInt16(Data[8..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(8, sizeof(short)));
        }

        /// <summary>
        ///     State which has final lofrou() routine
        /// </summary>
        public short Lofstt
        {
            get => BitConverter.ToInt16(Data[10..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(10, 2));
        }

        /// <summary>
        ///     Usage Timer (for nonlive timeouts etc)
        /// </summary>
        public short Usetmr
        {
            get => BitConverter.ToInt16(Data[12..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(12, sizeof(short)));
        }

        /// <summary>
        ///     Total minutes of use, times 4
        /// </summary>
        public short Minut4
        {
            get => BitConverter.ToInt16(Data[14..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(14, sizeof(short)));
        }

        /// <summary>
        ///     General purpose counter
        /// </summary>
        public short Countr
        {
            get => BitConverter.ToInt16(Data[16..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(16, sizeof(short)));
        }


        /// <summary>
        ///     Profanity accumulator
        /// </summary>
        public short Pfnacc
        {
            get => BitConverter.ToInt16(Data[18..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(18, sizeof(short)));
        }

        /// <summary>
        ///     Runtime Flags
        /// </summary>
        public uint Flags
        {
            get => BitConverter.ToUInt32(Data[20..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(20, sizeof(uint)));
        }

        /// <summary>
        ///     Baud Rate currently in effect 
        /// </summary>
        public ushort Baud
        {
            get => BitConverter.ToUInt16(Data[24..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(24, sizeof(ushort)));
        }

        /// <summary>
        ///     Credit-Consumption Rate
        /// </summary>
        public short Crdrat
        {
            get => BitConverter.ToInt16(Data[26..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(26, sizeof(short)));
        }

        /// <summary>
        ///     No-Activity Auto-Logoff Counter
        /// </summary>
        public short Nazapc
        {
            get => BitConverter.ToInt16(Data[28..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(28, sizeof(short)));
        }

        /// <summary>
        ///     "logged in" module loop limit
        /// </summary>
        public short Linlim
        {
            get => BitConverter.ToInt16(Data[30..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(30, sizeof(short)));
        }

        /// <summary>
        ///     Pointer to users current class in table
        /// </summary>
        public FarPtr Clsptr
        {
            get => new(Data.Slice(32, 4));
            set => value.Data.CopyTo(Data.Slice(32, FarPtr.Size));
        }

        /// <summary>
        ///     Pointer to current poll routine
        /// </summary>
        public FarPtr Polrou
        {
            get => new FarPtr(Data.Slice(36, 4));
            set => value.Data.CopyTo(Data.Slice(36, FarPtr.Size));
        }

        /// <summary>
        ///     LAN chan state (IPX.H) 0=nonlan/nonhdw
        /// </summary>
        public byte lcstat
        {
            get => Data[40];
            set => Data[40] = value;
        }

        /// <summary>
        ///    Size of the User Struct in Bytes
        /// </summary>
        public const ushort Size = 41;

        public User(ushort channelNumber) : base(nameof(User), Size)
        {
            ChannelNumber = (short)channelNumber;
            
            //Set Default Values for this channel
            UserClass = 6;
            Minut4 = 0xA00;
            Baud = 38400;
        }
    }
}

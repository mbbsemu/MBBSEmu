using System;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     USER Struct as defined in MAJORBBS.H
    /// </summary>
    public class User
    {
        public short UserClass { get; set; }
        public IntPtr16 Keys { get; set; }
        public short State { get; set; }
        public short Substt { get; set; }
        public short Lofstt { get; set; }
        public short Usetmr { get; set; }
        public short Minut4 { get; set; }
        public short Countr { get; set; }
        public short Pfnacc { get; set; }
        public uint Flags { get; set; }
        public ushort Baud { get; set; }
        public short Crdrat { get; set; }
        public short Nazapc { get; set; }
        public short Linlim { get; set; }
        public IntPtr16 Clsptr { get; set; }
        public IntPtr16 Polrou { get; set; }
        public char lcstat { get; set; }

        private byte[] _userStructBytes;

        public const ushort Size = 41;

        public User()
        {
            Keys = new IntPtr16(0,0);
            Clsptr = new IntPtr16(0,0);
            Polrou = new IntPtr16(0,0);

            //Set Initial Values
            var output = new MemoryStream();
            output.Write(BitConverter.GetBytes((short)6)); //class (ACTUSR)
            output.Write(new IntPtr16().ToSpan()); //keys:segment
            output.Write(BitConverter.GetBytes((short)0)); //state (register_module return)
            output.Write(BitConverter.GetBytes((short)0)); //substt (always starts at 0)
            output.Write(BitConverter.GetBytes((short)0)); //lofstt
            output.Write(BitConverter.GetBytes((short)0)); //usetmr
            output.Write(BitConverter.GetBytes((short)0x0A00)); //minut4
            output.Write(BitConverter.GetBytes((short)0)); //countr
            output.Write(BitConverter.GetBytes((short)0)); //pfnacc
            output.Write(BitConverter.GetBytes((int)0x0000000)); //flags
            output.Write(BitConverter.GetBytes((ushort)0)); //baud
            output.Write(BitConverter.GetBytes((short)0)); //crdrat
            output.Write(BitConverter.GetBytes((short)0)); //nazapc
            output.Write(BitConverter.GetBytes((short)0)); //linlim
            output.Write(new IntPtr16().ToSpan()); //clsptr:segment
            output.Write(new IntPtr16().ToSpan()); //polrou:segment
            output.Write(BitConverter.GetBytes('0')); //lcstat

            FromSpan(output.ToArray());
        }

        public void FromSpan(ReadOnlySpan<byte> userSpan)
        {
            UserClass = BitConverter.ToInt16(userSpan.Slice(0, 2));
            Keys.FromSpan(userSpan.Slice(2,4));
            State = BitConverter.ToInt16(userSpan.Slice(6, 2));
            Substt = BitConverter.ToInt16(userSpan.Slice(8, 2));
            Lofstt = BitConverter.ToInt16(userSpan.Slice(10, 2));
            Usetmr = BitConverter.ToInt16(userSpan.Slice(12, 2));
            Minut4 = BitConverter.ToInt16(userSpan.Slice(14, 2));
            Countr = BitConverter.ToInt16(userSpan.Slice(16, 2));
            Pfnacc = BitConverter.ToInt16(userSpan.Slice(18, 2));
            Flags = BitConverter.ToUInt32(userSpan.Slice(20, 4));
            Baud = BitConverter.ToUInt16(userSpan.Slice(24, 2));
            Crdrat = BitConverter.ToInt16(userSpan.Slice(26, 2));
            Nazapc = BitConverter.ToInt16(userSpan.Slice(28, 2));
            Linlim = BitConverter.ToInt16(userSpan.Slice(30, 2));
            Clsptr.FromSpan(userSpan.Slice(32, 4));
            Polrou.FromSpan(userSpan.Slice(36, 4));
            lcstat = (char)userSpan[40];
        }

        public ReadOnlySpan<byte> ToSpan()
        {
            using var output = new MemoryStream();
            output.Write(BitConverter.GetBytes(UserClass)); //0-1
            output.Write(Keys.ToSpan()); //2-3-4-5
            output.Write(BitConverter.GetBytes(State)); //6-7
            output.Write(BitConverter.GetBytes(Substt)); //8-9
            output.Write(BitConverter.GetBytes(Lofstt)); //10-11
            output.Write(BitConverter.GetBytes(Usetmr)); //12-13
            output.Write(BitConverter.GetBytes(Minut4)); //14-15
            output.Write(BitConverter.GetBytes(Countr)); //16-17
            output.Write(BitConverter.GetBytes(Pfnacc)); //18-19
            output.Write(BitConverter.GetBytes(Flags)); //20-21-22-23
            output.Write(BitConverter.GetBytes(Baud)); //24-25
            output.Write(BitConverter.GetBytes(Crdrat)); //26-27
            output.Write(BitConverter.GetBytes(Nazapc)); //28-29
            output.Write(BitConverter.GetBytes(Linlim)); //30-31
            output.Write(Clsptr.ToSpan()); //32-33-34-35
            output.Write(Polrou.ToSpan()); //36-37-38-39
            output.WriteByte(0xFF); //40
            _userStructBytes = output.ToArray();
            return _userStructBytes;
        }
    }
}

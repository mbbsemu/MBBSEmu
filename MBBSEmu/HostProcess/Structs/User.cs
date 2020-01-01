using System;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /* From MAJORBBS.H:
        struct user {                 // volatile per-user info maintained        
            int class;               //  [0,1]  class (offline, or flavor of online)  
            int *keys;               //  [2,3,4,5]  dynamically alloc'd array of key bits 
            int state;               //  [6,7]  state (module number in effect)       
            int substt;              //  [8,9]  substate (for convenience of module)  
            int lofstt;              //  state which has final lofrou() routine
            int usetmr;              //  usage timer (for nonlive timeouts etc)
            int minut4;              //  total minutes of use, times 4         
            int countr;              //  general purpose counter               
            int pfnacc;              //  profanity accumulator                 
            unsigned long flags;     //  runtime flags                         
            unsigned baud;           //  baud rate currently in effect         
            int crdrat;              //  credit-consumption rate               
            int nazapc;              //  no-activity auto-logoff counter       
            int linlim;              //  "logged in" module loop limit         
            struct clstab *cltptr;   //  ??  pointer to guys current class in table
            void (*polrou)();        //  ??  pointer to current poll routine       
            char lcstat;             //  ??  LAN chan state (IPX.H) 0=nonlan/nonhdw
        };        
    */
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

        public User()
        {
            Keys = new IntPtr16(0,0);
            Clsptr = new IntPtr16(0,0);
            Polrou = new IntPtr16(0,0);

            //Set Initial Values
            var output = new MemoryStream();
            output.Write(BitConverter.GetBytes((short)6)); //class (ACTUSR)
            output.Write(new IntPtr16().ToSpan()); //keys:segment
            output.Write(BitConverter.GetBytes((short)1)); //state (register_module always returns 1)
            output.Write(BitConverter.GetBytes((short)0)); //substt (always starts at 0)
            output.Write(BitConverter.GetBytes((short)0)); //lofstt
            output.Write(BitConverter.GetBytes((short)0)); //usetmr
            output.Write(BitConverter.GetBytes((short)0)); //minut4
            output.Write(BitConverter.GetBytes((short)0)); //countr
            output.Write(BitConverter.GetBytes((short)0)); //pfnacc
            output.Write(BitConverter.GetBytes((int)0)); //flags
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
            output.Write(BitConverter.GetBytes(UserClass)); //class (ACTUSR)
            output.Write(Keys.ToSpan()); //keys:segment
            output.Write(BitConverter.GetBytes(State)); //state (register_module always returns 1)
            output.Write(BitConverter.GetBytes(Substt)); //substt (always starts at 0)
            output.Write(BitConverter.GetBytes(Lofstt)); //lofstt
            output.Write(BitConverter.GetBytes(Usetmr)); //usetmr
            output.Write(BitConverter.GetBytes(Minut4)); //minut4
            output.Write(BitConverter.GetBytes(Countr)); //countr
            output.Write(BitConverter.GetBytes(Pfnacc)); //pfnacc
            output.Write(BitConverter.GetBytes(Flags)); //flags
            output.Write(BitConverter.GetBytes(Baud)); //baud
            output.Write(BitConverter.GetBytes(Crdrat)); //crdrat
            output.Write(BitConverter.GetBytes(Nazapc)); //nazapc
            output.Write(BitConverter.GetBytes(Linlim)); //linlim
            output.Write(Clsptr.ToSpan()); //clsptr
            output.Write(Polrou.ToSpan()); //polrou
            output.Write(BitConverter.GetBytes('0')); //lcstat
            _userStructBytes = output.ToArray();
            return _userStructBytes;
        }
    }
}

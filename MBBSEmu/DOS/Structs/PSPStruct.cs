using MBBSEmu.Memory;
using System;

namespace MBBSEmu.DOS.Structs
{
    /// <summary>
    ///     Program Segment Prefix
    ///
    ///     This struct is created for each program that DOS Executes
    /// </summary>
    public class PSPStruct
    {
        /// <summary>
        ///     Int 20h Instruction (0xCD20) -- Old Way to Exit
        ///
        ///     Offset: 0x0
        /// </summary>
        public ushort Int20 { get; set; }

        /// <summary>
        ///     Segment Address just beyond the end of the Program Image
        ///
        ///     Offset: 0x2
        /// </summary>
        public ushort NextSegOffset { get; set; }

        /// <summary>
        ///     Reserved
        ///
        ///     Offset: 0x4
        /// </summary>
        public byte Reserved1;

        /// <summary>
        ///     FAR CALL to DOS Function Dispatcher (obsolete)
        ///
        ///     Offset: 0x5
        /// </summary>
        public readonly byte[] Dispatcher = new byte[5];

        /// <summary>
        ///     Terminate Address (See Int 22h)
        ///     Offset: 0xA
        /// </summary>
        public FarPtr TerminateAddress { get; set; }

        /// <summary>
        ///     Ctrl-Break Handler Address (See Int 23h)
        ///
        ///     Offset: 0xE
        /// </summary>
        public FarPtr CtrlBrkAddress { get; set; }

        /// <summary>
        ///     Critical Error Handler Address (See Int 24h)
        ///
        ///     Offset: 0x12
        /// </summary>
        public FarPtr CritErrorAddress { get; set; }

        /// <summary>
        ///     DOS Reserved Area
        ///
        ///     Offset: 0x16
        /// </summary>
        public readonly byte[] Reserved2 = new byte[22];

        /// <summary>
        ///     Segment Address of the DOS Environment
        ///
        ///     Offset: 0x2C
        /// </summary>
        public ushort EnvSeg { get; set; }

        /// <summary>
        ///     DOS Reserved Area (Handle Table, etc.)
        ///
        ///     Offset: 0x2E
        /// </summary>
        public readonly byte[] Reserved3 = new byte[46];

        /// <summary>
        ///     An Unopened File Control Block (FCB) for the 1st Command Parameter
        ///
        ///     Offset: 0x5C
        /// </summary>
        public byte[] FCB_1 = new byte[16];

        /// <summary>
        ///     An Unopened File Control Block (FCB) for the 2nd Command Parameter
        ///
        ///     Offset: 0x6C
        /// </summary>
        public byte[] FCB_2 = new byte[20];

        /// <summary>
        ///     Count of Characters in the Command Tail at 81h (also default setting for the Disk Transfer Address - DTA)
        ///
        ///     Offset: 0x80
        /// </summary>
        public byte CmdTailLength { get; set; }

        /// <summary>
        ///     Characters from DOS Command Line
        ///
        ///     Offset: 0x81
        /// </summary>
        public byte[] CommandTail = new byte[127];

        public const ushort Size = 256;

        private readonly byte[] _data = new byte[Size];

        public byte[] Data
        {
            get
            {
                Array.Copy(BitConverter.GetBytes(Int20), 0, _data, 0, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(NextSegOffset), 0, _data, 2, sizeof(ushort));
                Array.Copy(Dispatcher, 0, _data, 5, Dispatcher.Length);
                Array.Copy(TerminateAddress?.Data ?? FarPtr.Empty.Data, 0, _data, 0xA, FarPtr.Size);
                Array.Copy(CtrlBrkAddress?.Data ?? FarPtr.Empty.Data, 0, _data, 0xE, FarPtr.Size);
                Array.Copy(CritErrorAddress?.Data ?? FarPtr.Empty.Data, 0, _data, 0x12, FarPtr.Size);
                Array.Copy(BitConverter.GetBytes(EnvSeg), 0, _data, 0x2C, sizeof(ushort));
                Array.Copy(FCB_1, 0, _data, 0x5C, FCB_1.Length);
                Array.Copy(FCB_2, 0, _data, 0x6C, FCB_2.Length);
                _data[0x80] = CmdTailLength;
                Array.Copy(CommandTail, 0, _data, 0x81, CommandTail.Length);
                return _data;
            }
        }
    }
}

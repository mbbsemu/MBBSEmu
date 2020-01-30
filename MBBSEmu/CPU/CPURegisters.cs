using Iced.Intel;
using System;

namespace MBBSEmu.CPU
{
    public class CpuRegisters
    {
        /// <summary>
        ///     Halt Flag - halts CPU operation
        /// </summary>
        public bool Halt { get; set; }

        /// <summary>
        ///     x87 FPU Status Register
        /// </summary>
        public FpuStatusRegister Fpu { get; set; }

        /// <summary>
        ///     Flags
        /// </summary>
        public CpuFlags F { get; set; }

        /*
         * General Registers
         */

        /// <summary>
        ///     AX Register
        /// </summary>
        public ushort AX { get; set; }

        /// <summary>
        ///     AX Low Byte
        /// </summary>
        public byte AL
        {
            get => (byte) (AX & 0xFF);
            set
            {
                AX &= 0xFF00;
                AX |= value;
            }
        }

        /// <summary>
        ///     AX High Byte
        /// </summary>
        public byte AH
        {
            get => (byte) (AX >> 8);
            set
            {
                AX &= 0x00FF;
                AX |= (ushort)(value << 8);
            }
        }

        /// <summary>
        ///     Base Register
        /// </summary>
        public ushort BX { get; set; }

        /// <summary>
        ///     BX Low Byte
        /// </summary>
        public byte BL
        {
            get => (byte) (BX & 0xFF);
            set
            {
                BX &= 0xFF00;
                BX |= value;
            }
        }

        /// <summary>
        ///     BX High Byte
        /// </summary>
        public byte BH
        {
            get => (byte) (BX >> 8);
            set
            {
                BX &= 0x00FF;
                BX |= (ushort)(value << 8);
            }
        }



        public ushort CX { get; set; }

        /// <summary>
        ///     CX Low Byte
        /// </summary>
        public byte CL
        {
            get => (byte) (CX & 0xFF);
            set
            {
                CX &= 0xFF00;
                CX |= value;
            }
        }

        /// <summary>
        ///     CX High Byte
        /// </summary>
        public byte CH
        {
            get => (byte) (CX >> 8);
            set
            {
                CX &= 0x00FF;
                CX |= (ushort)(value << 8);
            }
        }

        public ushort DX { get; set; }

        /// <summary>
        ///     DX Low Byte
        /// </summary>
        public byte DL
        {
            get => (byte) (DX & 0xFF);
            set
            {
                DX &= 0xFF00;
                DX |= value;
            }
        }

        /// <summary>
        ///     DX High Byte
        /// </summary>
        public byte DH
        {
            get => (byte) (DX >> 8);
            set
            {
                DX &= 0x00FF;
                DX |= (ushort)(value << 8);
            }
        }

        /// <summary>
        ///     Pointer Register
        /// </summary>
        public ushort SP { get; set; }
        /// <summary>
        ///     Base Register
        /// </summary>
        public ushort BP { get; set; }
        /// <summary>
        ///     Index Register
        /// </summary>
        public ushort SI { get; set; }
        /// <summary>
        ///     Index Register
        /// </summary>
        public ushort DI { get; set; }

        /*
         * Memory Segmentation and Segment Registers
         */

        /// <summary>
        ///     Data Segment
        /// </summary>
        public ushort DS { get; set; }

        /// <summary>
        ///     Extra Segment
        /// </summary>
        public ushort ES { get; set; }

        /// <summary>
        ///     Stack Segment
        ///     Default Destination for String Operations
        /// </summary>
        public ushort SS { get; set; }

        /// <summary>
        ///     Code Segment
        /// </summary>
        public ushort CS { get; set; }

        /// <summary>
        ///     Instruction Pointer
        /// </summary>
        public ushort IP { get; set; }

        public CpuRegisters()
        {
            F = new CpuFlags();
            Fpu = new FpuStatusRegister();
        }

        /// <summary>
        ///     Gets Value based on the specified Register
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public ushort GetValue(Register register)
        {
            switch (register)
            {
                case Register.AX:
                    return AX;
                case Register.AL:
                    return AL;
                case Register.AH:
                    return AH;
                case Register.CL:
                    return CL;
                case Register.DL:
                    return DL;
                case Register.BL:
                    return BL;
                case Register.CH:
                    return CH;
                case Register.DH:
                    return DH;
                case Register.BH:
                    return BH;
                case Register.CX:
                    return CX;
                case Register.DX:
                    return DX;
                case Register.BX:
                    return BX;
                case Register.SP:
                    return SP;
                case Register.BP:
                    return BP;
                case Register.SI:
                    return SI;
                case Register.DI:
                    return DI;
                case Register.ES:
                    return ES;
                case Register.CS:
                    return CS;
                case Register.SS:
                    return SS;
                case Register.DS:
                    return DS;
                case Register.EIP:
                    return IP;
                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }

        public void SetValue(Register register, ushort value)
        {
            switch (register)
            {
                case Register.AX:
                    AX = value;
                    break;
                case Register.AL:
                    AL = (byte) value;
                    break;
                case Register.AH:
                    AH = (byte) value;
                    break;
                case Register.CL:
                    CL = (byte) value;
                    break;
                case Register.DL:
                    DL = (byte) value;
                    break;
                case Register.BL:
                    BL = (byte) value;
                    break;
                case Register.CH:
                    CH = (byte) value;
                    break;
                case Register.DH:
                    DH = (byte) value;
                    break;
                case Register.BH:
                    BH = (byte) value;
                    break;
                case Register.CX:
                    CX = value;
                    break;
                case Register.DX:
                    DX = value;
                    break;
                case Register.BX:
                    BX = value;
                    break;
                case Register.SP:
                    SP = value;
                    break;
                case Register.BP:
                    BP = value;
                    break;
                case Register.SI:
                    SI = value;
                    break;
                case Register.DI:
                    DI = value;
                    break;
                case Register.ES:
                    ES = value;
                    break;
                case Register.CS:
                    CS = value;
                    break;
                case Register.SS:
                    SS = value;
                    break;
                case Register.DS:
                    DS = value;
                    break;
                case Register.EIP:
                    IP = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }

        public byte GetSize(Register register)
        {
            switch (register)
            {
                
                case Register.AL:
                case Register.AH:
                case Register.BL:
                case Register.BH:
                case Register.CL:
                case Register.CH:
                case Register.DL:
                case Register.DH:
                    return 8;

                case Register.AX:
                case Register.BX:
                case Register.CX:
                case Register.DX:
                case Register.SP:
                case Register.BP:
                case Register.SI:
                case Register.DI:
                case Register.ES:
                case Register.CS:
                case Register.SS:
                case Register.DS:
                case Register.EIP:
                    return 16;

                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }

        public int GetLong(Register highBytes, Register lowBytes)
        {
            return (GetValue(highBytes) << 16) | GetValue(lowBytes);
        }

        /// <summary>
        ///     Returns a DOS.H compatible struct for register values (WORDREGS, BYTEREGS)
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> ToRegs()
        {
            var output = new byte[16];
            Array.Copy(BitConverter.GetBytes(AX), 0, output, 0, 2);
            Array.Copy(BitConverter.GetBytes(BX), 0, output, 2, 2);
            Array.Copy(BitConverter.GetBytes(CX), 0, output, 4, 2);
            Array.Copy(BitConverter.GetBytes(DX), 0, output, 6, 2);
            Array.Copy(BitConverter.GetBytes(SI), 0, output, 8, 2);
            Array.Copy(BitConverter.GetBytes(DI), 0, output, 10, 2);
            //TODO -- Determine if we needs FLAGS
            return output;
        }

        /// <summary>
        ///     Loads this CpuRegisters instance with values from a DOS.H compatible struct for register values
        /// </summary>
        /// <param name="regs"></param>
        public void FromRegs(ReadOnlySpan<byte> regs)
        {
            AX = BitConverter.ToUInt16(regs.Slice(0, 2));
            BX = BitConverter.ToUInt16(regs.Slice(2, 2));
            CX = BitConverter.ToUInt16(regs.Slice(4, 2));
            DX = BitConverter.ToUInt16(regs.Slice(6, 2));
            SI = BitConverter.ToUInt16(regs.Slice(8, 2));
            DI = BitConverter.ToUInt16(regs.Slice(10, 2));
        }

        /// <summary>
        ///     Sets all Register values to Zero
        /// </summary>
        public void Zero()
        {
            AX = 0;
            BX = 0;
            CX = 0;
            DX = 0;
            SI = 0;
            DI = 0;
            IP = 0;
            CS = 0;
            DS = 0;
            ES = 0;
            SS = 0;
        }
    }
}

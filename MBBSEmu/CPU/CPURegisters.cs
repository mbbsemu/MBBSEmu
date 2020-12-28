using Iced.Intel;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Holds the CPU Registers for the emulated x86 Core
    ///
    ///     While the majority of these are basic 16-bit values, several special
    ///     registers are abstracted out into their own class (Flags, FPU)
    /// </summary>
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
        public ushort F { get; set; }

        /*
         * General Registers
         */

        public uint EAX { get; set; }
        
        /// <summary>
        ///     AX Register
        /// </summary>
        public ushort AX
        {
            get => (ushort)(EAX & 0xFFFF);
            set
            {
                EAX &= 0xFFFF0000;
                EAX |= value;
            }
        }

        /// <summary>
        ///     AX Low Byte
        /// </summary>
        public byte AL
        {
            get => (byte) (EAX & 0xFF);
            set
            {
                EAX &= 0xFFFFFF00;
                EAX |= value;
            }
        }

        /// <summary>
        ///     AX High Byte
        /// </summary>
        public byte AH
        {
            get => (byte) (EAX >> 8);
            set
            {
                EAX &= 0xFFFF00FF;
                EAX |= (ushort)(value << 8);
            }
        }

        public uint EBX { get; set; }
        
        /// <summary>
        ///     Base Register
        /// </summary>
        public ushort BX
        {
            get => (ushort)(EBX & 0xFFFF);
            set
            {
                EBX &= 0xFFFF0000;
                EBX |= value;
            }
        }

        /// <summary>
        ///     BX Low Byte
        /// </summary>
        public byte BL
        {
            get => (byte) (EBX & 0xFF);
            set
            {
                EBX &= 0xFFFFFF00;
                EBX |= value;
            }
        }

        /// <summary>
        ///     BX High Byte
        /// </summary>
        public byte BH
        {
            get => (byte) (EBX >> 8);
            set
            {
                EBX &= 0xFFFF00FF;
                EBX |= (ushort)(value << 8);
            }
        }

        public uint ECX { get; set; }
        
        /// <summary>
        ///     Counter Register
        /// </summary>
        public ushort CX
        {
            get => (ushort)(ECX & 0xFFFF);
            set
            {
                ECX &= 0xFFFF0000;
                ECX |= value;
            }
        }

        /// <summary>
        ///     CX Low Byte
        /// </summary>
        public byte CL
        {
            get => (byte) (ECX & 0xFF);
            set
            {
                ECX &= 0xFFFFFF00;
                ECX |= value;
            }
        }

        /// <summary>
        ///     CX High Byte
        /// </summary>
        public byte CH
        {
            get => (byte) (ECX >> 8);
            set
            {
                ECX &= 0xFFFF00FF;
                ECX |= (ushort)(value << 8);
            }
        }

        public uint EDX { get; set; }
        
        /// <summary>
        ///     Data Register
        /// </summary>
        public ushort DX
        {
            get => (ushort)(EDX & 0xFFFF);
            set
            {
                EDX &= 0xFFFF0000;
                EDX |= value;
            }
        }

        /// <summary>
        ///     DX Low Byte
        /// </summary>
        public byte DL
        {
            get => (byte) (EDX & 0xFF);
            set
            {
                EDX &= 0xFFFFFF00;
                EDX |= value;
            }
        }

        /// <summary>
        ///     DX High Byte
        /// </summary>
        public byte DH
        {
            get => (byte) (EDX >> 8);
            set
            {
                EDX &= 0xFFFF00FF;
                EDX |= (ushort)(value << 8);
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
            Fpu = new FpuStatusRegister();
        }

        /// <summary>
        ///     Gets Value based on the specified Register
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public ushort GetValue(Register register)
        {
            return register switch
            {
                Register.AX => AX,
                Register.AL => AL,
                Register.AH => AH,
                Register.CL => CL,
                Register.DL => DL,
                Register.BL => BL,
                Register.CH => CH,
                Register.DH => DH,
                Register.BH => BH,
                Register.CX => CX,
                Register.DX => DX,
                Register.BX => BX,
                Register.SP => SP,
                Register.BP => BP,
                Register.SI => SI,
                Register.DI => DI,
                Register.ES => ES,
                Register.CS => CS,
                Register.SS => SS,
                Register.DS => DS,
                Register.EIP => IP,
                _ => throw new ArgumentOutOfRangeException(nameof(register), register, null)
            };
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

        /// <summary>
        ///     Returns a 32-bit long from DX:AX
        /// </summary>
        public int GetLong()
        {
            return DX << 16 | AX;
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
            Array.Copy(BitConverter.GetBytes(F.IsFlagSet((ushort)EnumFlags.CF) ? 1 : 0), 0, output, 12, 2);
            Array.Copy(BitConverter.GetBytes(F), 0, output, 14, 2);
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
            F  = BitConverter.ToUInt16(regs.Slice(14, 2));
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

        /// <summary>
        ///     Returns an IntPtr16 populated from DX:AX
        /// </summary>
        public FarPtr GetPointer()
        {
            return new FarPtr(segment: DX, offset: AX);
        }

        /// <summary>
        ///     Sets DX:AX to the value from ptr
        /// </summary>
        public void SetPointer(FarPtr ptr)
        {
            DX = ptr.Segment;
            AX = ptr.Offset;
        }
    }
}

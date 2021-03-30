using Iced.Intel;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using System;
using System.Runtime.InteropServices;

namespace MBBSEmu.CPU
{
    public struct FastFpuStatusRegister
    {
        public ushort StatusWord { get; set; }

        public ushort ControlWord { get; set; }

        public void SetFlag(EnumFpuStatusFlags statusFlag)
        {
            StatusWord = (ushort) (StatusWord | (ushort) statusFlag);
        }

        public void ClearFlag(EnumFpuStatusFlags statusFlag)
        {
            StatusWord = (ushort) (StatusWord & ~(ushort) statusFlag);
        }

        public byte GetStackTop() => (byte) ((StatusWord >> 11) & 0x7);

        public void SetStackTop(byte value)
        {
            StatusWord &= unchecked((ushort)(~0x3800)); //Zero out the previous value
            StatusWord |= (ushort)((value & 0x7) << 11); //Write new one
        }

        /// <summary>
        ///     Returns the Pointer in the Stack Array for the Specified Index
        ///
        ///     Example: If there are three values in the FPU stack and ST(0) is FpuStack[3],
        ///              GetStackPointer(Registers.ST1) will return FpuStack[2] for the value of ST(1).
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public int GetStackPointer(Register register)
        {
            var registerOffset = register switch
            {
                Register.ST0 => 0,
                Register.ST1 => 1,
                Register.ST2 => 2,
                Register.ST3 => 3,
                Register.ST4 => 4,
                Register.ST5 => 5,
                Register.ST6 => 6,
                Register.ST7 => 7,
                _ => throw new Exception($"Unsupported FPU Register: {register}")
            };

            return GetStackTop() - registerOffset;
        }

        public void PopStackTop()
        {
            var stackTop = GetStackTop();
            unchecked
            {
                stackTop--;
            }

            if (stackTop > 7)
                stackTop = 7;

            SetStackTop(stackTop);
        }

        public void PushStackTop()
        {
            var stackTop = GetStackTop();

            //If this causes the stack to overflow, set it back to the base (top) address
            unchecked
            {
                stackTop++;
            }

            if (stackTop > 7)
                stackTop = 0;

            SetStackTop(stackTop);
        }

        /// <summary>
        ///     Clears Exception Flags from Status and Control Words
        /// </summary>
        public void ClearExceptions()
        {
            StatusWord &= 0xFFC0;
            ControlWord &= 0xFFC0;
        }

        public static FastFpuStatusRegister Create()
        {
            var t = new FastFpuStatusRegister();
            t.ControlWord = 0x37F;
            t.StatusWord = 0;
            t.SetStackTop(7);
            return t;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct FastCpuRegisters
    {
        public static FastCpuRegisters Create()
        {
            var t = new FastCpuRegisters();
            t.Fpu.ControlWord = 0x37F;
            t.Fpu.StatusWord = 0;
            t.Fpu.SetStackTop(7);
            return t;
        }

        [FieldOffset(0)]
        public uint EAX;

        [FieldOffset(0)]
        public ushort AX;

        [FieldOffset(1)]
        public byte AH;

        [FieldOffset(0)]
        public byte AL;

        // EBX
        [FieldOffset(4)]
        public uint EBX;

        [FieldOffset(4)]
        public ushort BX;

        [FieldOffset(5)]
        public byte BH;

        [FieldOffset(4)]
        public byte BL;

        // ECX
        [FieldOffset(8)]
        public uint ECX;

        [FieldOffset(8)]
        public ushort CX;

        [FieldOffset(9)]
        public byte CH;

        [FieldOffset(8)]
        public byte CL;

        // EDX
        [FieldOffset(12)]
        public uint EDX;

        [FieldOffset(12)]
        public ushort DX;

        [FieldOffset(13)]
        public byte DH;

        [FieldOffset(12)]
        public byte DL;

        // ESP
        [FieldOffset(16)]
        public uint ESP;

        [FieldOffset(16)]
        public ushort SP;

        // EBP
        [FieldOffset(20)]
        public uint EBP;

        [FieldOffset(20)]
        public ushort BP;

        // ESI
        [FieldOffset(24)]
        public uint ESI;

        [FieldOffset(24)]
        public ushort SI;

        // EDI
        [FieldOffset(28)]
        public uint EDI;

        [FieldOffset(28)]
        public ushort DI;

        [FieldOffset(32)]
        public ushort DS;
        [FieldOffset(34)]
        public ushort ES;
        [FieldOffset(36)]
        public ushort SS;
        [FieldOffset(38)]
        public ushort CS;
        [FieldOffset(40)]
        public ushort IP;

        [FieldOffset(42)]
        public bool CarryFlag;
        [FieldOffset(43)]
        public bool SignFlag;
        [FieldOffset(44)]
        public bool OverflowFlag;
        [FieldOffset(45)]
        public bool ZeroFlag;
        [FieldOffset(46)]
        public bool DirectionFlag;
        [FieldOffset(47)]
        public bool AuxiliaryCarryFlag;
        [FieldOffset(48)]
        public bool InterruptFlag;
        [FieldOffset(49)]
        public bool Halt;

        [FieldOffset(50)]
        public FastFpuStatusRegister Fpu;

        public ushort F()
        {
            ushort f = 0;

            if (CarryFlag) f |= (ushort)EnumFlags.CF;
            if (OverflowFlag) f |= (ushort)EnumFlags.OF;
            if (AuxiliaryCarryFlag) f |= (ushort)EnumFlags.AF;
            if (DirectionFlag) f |= (ushort)EnumFlags.DF;
            if (SignFlag) f |= (ushort)EnumFlags.SF;
            if (ZeroFlag) f |= (ushort)EnumFlags.ZF;
            if (InterruptFlag) f |= (ushort)EnumFlags.IF;

            return f;
        }

        public uint EF() => F();

        public void SetF(ushort flags)
        {
            CarryFlag = flags.IsFlagSet((ushort)EnumFlags.CF);
            OverflowFlag = flags.IsFlagSet((ushort)EnumFlags.OF);
            AuxiliaryCarryFlag = flags.IsFlagSet((ushort)EnumFlags.AF);
            DirectionFlag = flags.IsFlagSet((ushort)EnumFlags.DF);
            SignFlag = flags.IsFlagSet((ushort)EnumFlags.SF);
            ZeroFlag = flags.IsFlagSet((ushort)EnumFlags.ZF);
            InterruptFlag = flags.IsFlagSet((ushort)EnumFlags.IF);
        }

        public void SetEF(uint flags) => SetF((ushort)flags);

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

        public uint GetValue32(Register register)
        {
            return register switch
            {
                Register.EAX => EAX,
                Register.EBX => EBX,
                Register.ECX => ECX,
                Register.EDX => EDX,
                Register.ESP => ESP,
                Register.EBP => EBP,
                Register.EDI => EDI,
                Register.ESI => ESI,
                _ => throw new ArgumentOutOfRangeException(nameof(register), register, null)
            };
        }

        /// <summary>
        ///     Sets the specified Register to the specified 8-bit value
        /// </summary>
        /// <param name="register"></param>
        /// <param name="value"></param>
        public void SetValue(Register register, byte value)
        {
            switch (register)
            {
                case Register.AL:
                    AL = value;
                    break;
                case Register.AH:
                    AH = value;
                    break;
                case Register.BH:
                    BH = value;
                    break;
                case Register.BL:
                    BL = value;
                    break;
                case Register.CH:
                    CH = value;
                    break;
                case Register.CL:
                    CL = value;
                    break;
                case Register.DH:
                    DH = value;
                    break;
                case Register.DL:
                    DL = value;
                    break;
                default:
                    SetValue(register, (ushort)value);
                    break;
            }
        }

        /// <summary>
        ///     Sets the specified Register to the specified 16-bit value
        /// </summary>
        /// <param name="register"></param>
        /// <param name="value"></param>
        public void SetValue(Register register, ushort value)
        {
            switch (register)
            {
                case Register.AX:
                    AX = value;
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
                    SetValue(register, (uint)value);
                    break;
            }
        }

        /// <summary>
        ///     Sets the specified Register to the specified 32-bit value
        /// </summary>
        /// <param name="register"></param>
        /// <param name="value"></param>
        public void SetValue(Register register, uint value)
        {
            switch (register)
            {
                case Register.EAX:
                    EAX = value;
                    break;
                case Register.EBX:
                    EBX = value;
                    break;
                case Register.ECX:
                    ECX = value;
                    break;
                case Register.EDX:
                    EDX = value;
                    break;
                case Register.ESP:
                    ESP = value;
                    break;
                case Register.EBP:
                    EBP = value;
                    break;
                case Register.ESI:
                    ESI = value;
                    break;
                case Register.EDI:
                    EDI = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }

        /// <summary>
        ///     Returns a 32-bit long from DX:AX
        /// </summary>
        public int GetLong() => DX << 16 | AX;

        /// <summary>
        ///     Returns a FarPtr from DX:AX
        /// </summary>
        public FarPtr GetPointer() => new FarPtr(segment: DX, offset: AX);

        public void SetPointer(FarPtr ptr)
        {
            DX = ptr.Segment;
            AX = ptr.Offset;
        }

        /// <summary>
        ///     Sets all Register values to Zero
        /// </summary>
        public void Zero()
        {
            EAX = 0;
            EBX = 0;
            ECX = 0;
            EDX = 0;
            ESI = 0;
            EDI = 0;
            ESP = 0;
            EBP = 0;
            IP = 0;
            CS = 0;
            DS = 0;
            ES = 0;
            SS = 0;
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
            Array.Copy(BitConverter.GetBytes(CarryFlag ? 1 : 0), 0, output, 12, 2);
            Array.Copy(BitConverter.GetBytes(F()), 0, output, 14, 2);
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
            SetF(BitConverter.ToUInt16(regs.Slice(14, 2)));
        }
    }
}

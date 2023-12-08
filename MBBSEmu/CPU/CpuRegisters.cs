using Iced.Intel;
using MBBSEmu.Memory;
using System;
using System.Text;

namespace MBBSEmu.CPU
{
    public class CpuRegisters : ICpuRegisters, IFpuRegisters
    {
        public CpuRegistersStruct Registers = CpuRegistersStruct.Create();

        public IFpuRegisters Fpu => this;

        public uint EAX { get => Registers.EAX; set => Registers.EAX = value; }
        public ushort AX { get => Registers.AX; set => Registers.AX = value; }
        public byte AH  { get => Registers.AH; set => Registers.AH = value; }
        public byte AL  { get => Registers.AL; set => Registers.AL = value; }
        public uint EBX  { get => Registers.EBX; set => Registers.EBX = value; }
        public ushort BX  { get => Registers.BX; set => Registers.BX = value; }
        public byte BH { get => Registers.BH; set => Registers.BH = value; }
        public byte BL  { get => Registers.BL; set => Registers.BL = value; }
        public uint ECX  { get => Registers.ECX; set => Registers.ECX = value; }
        public ushort CX  { get => Registers.CX; set => Registers.CX = value; }
        public byte CH  { get => Registers.CH; set => Registers.CH = value; }
        public byte CL  { get => Registers.CL; set => Registers.CL = value; }
        public uint EDX { get => Registers.EDX; set => Registers.EDX = value; }
        public ushort DX { get => Registers.DX; set => Registers.DX = value; }
        public byte DH { get => Registers.DH; set => Registers.DH = value; }
        public byte DL { get => Registers.DL; set => Registers.DL = value; }
        public uint ESP { get => Registers.ESP; set => Registers.ESP = value; }
        public ushort SP { get => Registers.SP; set => Registers.SP = value; }
        public uint EBP { get => Registers.EBP; set => Registers.EBP = value; }
        public ushort BP { get => Registers.BP; set => Registers.BP = value; }
        public uint ESI { get => Registers.ESI; set => Registers.ESI = value; }
        public ushort SI { get => Registers.SI; set => Registers.SI = value; }
        public uint EDI { get => Registers.EDI; set => Registers.EDI = value; }
        public ushort DI { get => Registers.DI; set => Registers.DI = value; }
        public ushort DS { get => Registers.DS; set => Registers.DS = value; }
        public ushort ES { get => Registers.ES; set => Registers.ES = value; }
        public ushort SS { get => Registers.SS; set => Registers.SS = value; }
        public ushort CS { get => Registers.CS; set => Registers.CS = value; }
        public ushort IP { get => Registers.IP; set => Registers.IP = value; }
        public ushort F { get => Registers.F(); set => Registers.SetF(value); }

        public bool CarryFlag { get => Registers.CarryFlag; set => Registers.CarryFlag = value; }
        public bool SignFlag { get => Registers.SignFlag; set => Registers.SignFlag = value; }
        public bool OverflowFlag { get => Registers.OverflowFlag; set => Registers.OverflowFlag = value; }
        public bool ZeroFlag { get => Registers.ZeroFlag; set => Registers.ZeroFlag = value; }
        public bool DirectionFlag { get => Registers.DirectionFlag; set => Registers.DirectionFlag = value; }
        public bool AuxiliaryCarryFlag { get => Registers.AuxiliaryCarryFlag; set => Registers.AuxiliaryCarryFlag = value; }
        public bool InterruptFlag { get => Registers.InterruptFlag; set => Registers.InterruptFlag = value; }
        public bool Halt { get => Registers.Halt; set => Registers.Halt = value; }

        public void Zero() => Registers.Zero();

        public void SetPointer(FarPtr ptr)
        {
          Registers.DX = ptr.Segment;
          Registers.AX = ptr.Offset;
        }

        public FarPtr GetPointer() => new FarPtr(Registers.DX, Registers.AX);

        public int GetLong() => (Registers.DX << 16) | Registers.AX;

        public void FromRegs(ReadOnlySpan<byte> regs) => Registers.FromRegs(regs);
        public ReadOnlySpan<byte> ToRegs() => Registers.ToRegs();

        public ushort StatusWord { get => Registers.Fpu.StatusWord;  set => Registers.Fpu.StatusWord = value; }

        public ushort ControlWord { get => Registers.Fpu.ControlWord; set => Registers.Fpu.ControlWord = value; }

        public void SetFlag(EnumFpuStatusFlags statusFlag) => Registers.Fpu.SetFlag(statusFlag);

        public void ClearFlag(EnumFpuStatusFlags statusFlag) => Registers.Fpu.ClearFlag(statusFlag);

        public byte GetStackTop() => Registers.Fpu.GetStackTop();

        public void SetStackTop(byte value) => Registers.Fpu.SetStackTop(value);

        /// <summary>
        ///     Returns the Pointer in the Stack Array for the Specified Index
        ///
        ///     Example: If there are three values in the FPU stack and ST(0) is FpuStack[3],
        ///              GetStackPointer(Registers.ST1) will return FpuStack[2] for the value of ST(1).
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public int GetStackPointer(Register register) => Registers.Fpu.GetStackPointer(register);
        public void PopStackTop() => Registers.Fpu.PopStackTop();
        public void PushStackTop() => Registers.Fpu.PushStackTop();
        public void ClearExceptions() => Registers.Fpu.ClearExceptions();

        /// <summary>
        ///     Overridden ToString() to display the current state of the CPU Registers
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Registers.ToString();
        }
    }
}

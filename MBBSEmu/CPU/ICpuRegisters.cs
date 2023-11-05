using Iced.Intel;
using MBBSEmu.Memory;
using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Mutable FPU Registers for the emulated x86 core.
    /// </summary>
    public interface IFpuRegisters
    {
        ushort StatusWord { get; set; }

        ushort ControlWord { get; set; }

        void SetFlag(EnumFpuStatusFlags statusFlag);
        void ClearFlag(EnumFpuStatusFlags statusFlag);

        byte GetStackTop();

        void SetStackTop(byte value);

        int GetStackPointer(Register register);
        void PopStackTop();
        void PushStackTop();
        void ClearExceptions();
    }

    /// <summary>
    ///     Mutable CPU Registers for the emulated x86 Core
    ///
    ///     While the majority of these are basic 16-bit values, several special
    ///     registers are abstracted out into their own class (Flags, FPU)
    /// </summary>
    public interface ICpuRegisters
    {
        IFpuRegisters Fpu { get; }

        uint EAX { get; set; }
        ushort AX { get; set; }
        byte AH  { get; set; }
        byte AL  { get; set; }
        uint EBX  { get; set; }
        ushort BX  { get; set; }
        byte BH  { get; set; }
        byte BL  { get; set; }
        uint ECX  { get; set; }
        ushort CX  { get; set; }
        byte CH  { get; set; }
        byte CL  { get; set; }
        uint EDX { get; set; }
        ushort DX { get; set; }
        byte DH { get; set; }
        byte DL { get; set; }
        uint ESP { get; set; }
        ushort SP { get; set; }
        uint EBP { get; set; }
        ushort BP { get; set; }
        uint ESI { get; set; }
        ushort SI { get; set; }
        uint EDI { get; set; }
        ushort DI { get; set; }
        ushort DS { get; set; }
        ushort ES { get; set; }
        ushort SS { get; set; }
        ushort CS { get; set; }
        ushort IP { get; set; }
        ushort F { get; set; }
        bool CarryFlag { get; set; }
        bool SignFlag { get; set; }
        bool OverflowFlag { get; set; }
        bool ZeroFlag { get; set; }
        bool DirectionFlag { get; set; }
        bool AuxiliaryCarryFlag { get; set; }
        bool InterruptFlag { get; set; }
        bool Halt { get; set; }

        void Zero();

        void SetPointer(FarPtr ptr);
        FarPtr GetPointer();

        int GetLong();

        void FromRegs(ReadOnlySpan<byte> regs);
        ReadOnlySpan<byte> ToRegs();
    }
}

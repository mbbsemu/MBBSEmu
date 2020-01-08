using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess;
using System;

namespace MBBSEmu.Memory
{
    public interface IMemoryCore
    {
        //Segment Mangement
        void AddSegment(ushort segmentNumber, int size = 0x10000);
        void AddSegment(EnumHostSegments segment);
        void AddSegment(Segment segment);
        Segment GetSegment(ushort segmentNumber);
        bool HasSegment(ushort segmentNumber);

        //Getters
        Instruction GetInstruction(ushort segment, ushort instructionPointer);
        byte GetByte(IntPtr16 pointer);
        byte GetByte(ushort segment, ushort offset);
        ushort GetWord(IntPtr16 pointer);
        ushort GetWord(ushort segment, ushort offset);
        ReadOnlySpan<byte> GetArray(IntPtr16 pointer, ushort count);
        ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count);
        ReadOnlySpan<byte> GetString(IntPtr16 pointer, bool stripNull = false);
        ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false);

        //Setters
        void SetByte(IntPtr16 pointer, byte value);
        void SetByte(ushort segment, ushort offset, byte value);
        void SetWord(IntPtr16 pointer, ushort value);
        void SetWord(ushort segment, ushort offset, ushort value);
        void SetArray(IntPtr16 pointer, ReadOnlySpan<byte> array);
        void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array);
    }
}
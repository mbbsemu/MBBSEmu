using System;
using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;

namespace MBBSEmu.Memory
{
    public interface IMemoryCore
    {
        void AddSegment(ushort segmentNumber);
        void AddSegment(Segment segment);
        Segment GetSegment(ushort segmentNumber);
        bool HasSegment(ushort segmentNumber);
        Instruction GetInstruction(ushort segment, int instructionPointer);
        byte GetByte(ushort segment, ushort offset);
        ushort GetWord(ushort segment, ushort offset);
        byte[] GetArray(ushort segment, ushort offset, ushort count);
        ReadOnlySpan<byte> GetSpan(ushort segment, ushort offset, ushort count);
        byte[] GetString(ushort segment, ushort offset);
        void SetByte(ushort segment, ushort offset, byte value);
        void SetWord(ushort segment, ushort offset, ushort value);
        void SetArray(ushort segment, ushort offset, byte[] array);
        void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array);

        ushort AllocateHostMemory(ushort size);
        ushort AllocateRoutineMemorySegment();
        void FreeRoutineMemorySegment(ushort segment);
    }
}
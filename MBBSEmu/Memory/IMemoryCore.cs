using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess;
using System;

namespace MBBSEmu.Memory
{
    public interface IMemoryCore
    {
        ushort GetPointerSegment();
        ushort GetFileSegment(int fileSize);
        void AddSegment(ushort segmentNumber, int size = 0x10000);
        void RemoveSegment(ushort segmentNumber);
        void AddSegment(EnumHostSegments segment);
        void AddSegment(Segment segment);
        Segment GetSegment(ushort segmentNumber);
        bool HasSegment(ushort segmentNumber);
        Instruction GetInstruction(ushort segment, ushort instructionPointer);
        byte GetByte(ushort segment, ushort offset);
        ushort GetWord(ushort segment, ushort offset);
        byte[] GetArray(ushort segment, ushort offset, ushort count);
        ReadOnlySpan<byte> GetSpan(ushort segment, ushort offset, ushort count);
        ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false);
        void SetByte(ushort segment, ushort offset, byte value);
        void SetWord(ushort segment, ushort offset, ushort value);
        void SetArray(ushort segment, ushort offset, byte[] array);
        void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array);
        ushort AllocateHostMemory(ushort size);
    }
}
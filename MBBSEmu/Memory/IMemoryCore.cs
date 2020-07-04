using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using System;
using Microsoft.Extensions.Primitives;

namespace MBBSEmu.Memory
{
    public interface IMemoryCore
    {
        //Variable Memory Management
        IntPtr16 GetVariablePointer(string name);
        bool TryGetVariablePointer(string name, out IntPtr16 pointer);
        void SetVariable(string name, byte value);
        void SetVariable(string name, ushort value);
        void SetVariable(string name, uint value);
        void SetVariable(string name, ReadOnlySpan<byte> value);
        void SetVariable(string name, IntPtr16 value);
        IntPtr16 AllocateVariable(string name, ushort size, bool declarePointer = false);
        IntPtr16 AllocateBigMemoryBlock(ushort quantity, ushort size);
        IntPtr16 GetBigMemoryBlock(IntPtr16 block, ushort index);

        //Segment Mangement
        void AddSegment(ushort segmentNumber, int size = 0x10000);
        void AddSegment(ushort segmentNumber, InstructionList segmentInstructionList);
        void AddSegment(Segment segment);
        Segment GetSegment(ushort segmentNumber);
        bool HasSegment(ushort segmentNumber);

        //Getters
        Instruction GetInstruction(ushort segment, ushort instructionPointer);
        byte GetByte(IntPtr16 pointer);
        byte GetByte(ushort segment, ushort offset);
        ushort GetWord(IntPtr16 pointer);
        ushort GetWord(ushort segment, ushort offset);
        IntPtr16 GetPointer(IntPtr16 pointer);
        IntPtr16 GetPointer(ushort segment, ushort offset);
        ReadOnlySpan<byte> GetArray(IntPtr16 pointer, ushort count);
        ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count);
        ReadOnlySpan<byte> GetArray(string variableName, ushort count);
        ReadOnlySpan<byte> GetString(IntPtr16 pointer, bool stripNull = false);
        ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false);
        ReadOnlySpan<byte> GetString(string variableName, bool stripNull = false);
        ReadOnlySpan<byte> GetStringDoubleNull(string variableName);

        //Setters
        void SetByte(IntPtr16 pointer, byte value);
        void SetByte(ushort segment, ushort offset, byte value);
        void SetWord(IntPtr16 pointer, ushort value);
        void SetWord(ushort segment, ushort offset, ushort value);
        void SetArray(IntPtr16 pointer, ReadOnlySpan<byte> array);
        void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array);
        void SetPointer(IntPtr16 pointer, IntPtr16 value);
        void SetPointer(string variableName, IntPtr16 value);
        void SetZero(IntPtr16 pointer, int length);

        //Nuke the world
        void Clear();
    }
}
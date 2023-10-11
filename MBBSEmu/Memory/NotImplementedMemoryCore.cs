using Iced.Intel;
using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    /// An IMemoryCore which throws NotImplementedException for everything
    /// </summary>
    public class NotImplementedMemoryCore : IMemoryCore
    {
        public virtual FarPtr AllocateBigMemoryBlock(ushort quantity, ushort size)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr AllocateRealModeSegment(ushort segmentSize)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr AllocateVariable(string name, ushort size, bool declarePointer)
        {
            throw new NotImplementedException();
        }

        public virtual void Clear()
        {
            throw new NotImplementedException();
        }

        public virtual void FillArray(ushort segment, ushort offset, int count, byte value)
        {
            throw new NotImplementedException();
        }

        public virtual void Free(FarPtr ptr)
        {
            throw new NotImplementedException();
        }

        public virtual int GetAllocatedMemorySize(FarPtr ptr)
        {
            throw new NotImplementedException();
        }

        public ReadOnlySpan<byte> GetMemorySegment(ushort segment)
        {
            throw new NotImplementedException();
        }

        public virtual ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr GetBigMemoryBlock(FarPtr block, ushort index)
        {
            throw new NotImplementedException();
        }

        public virtual byte GetByte(ushort segment, ushort offset)
        {
            throw new NotImplementedException();
        }

        public virtual uint GetDWord(ushort segment, ushort offset)
        {
            throw new NotImplementedException();
        }

        public virtual Instruction GetInstruction(ushort segment, ushort instructionPointer)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr GetOrAllocateVariablePointer(string name, ushort size, bool declarePointer)
        {
            throw new NotImplementedException();
        }

        public virtual ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr GetVariablePointer(string name)
        {
            throw new NotImplementedException();
        }

        public virtual ushort GetWord(ushort segment, ushort offset)
        {
            throw new NotImplementedException();
        }

        public virtual FarPtr Malloc(uint size)
        {
            throw new NotImplementedException();
        }

        public virtual Instruction Recompile(ushort segment, ushort instructionPointer)
        {
            throw new NotImplementedException();
        }

        public virtual void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {
            throw new NotImplementedException();
        }

        public virtual void SetByte(ushort segment, ushort offset, byte value)
        {
            throw new NotImplementedException();
        }

        public virtual void SetDWord(ushort segment, ushort offset, uint value)
        {
            throw new NotImplementedException();
        }

        public virtual void SetWord(ushort segment, ushort offset, ushort value)
        {
            throw new NotImplementedException();
        }

        public virtual bool TryGetVariablePointer(string name, out FarPtr pointer)
        {
            throw new NotImplementedException();
        }
        
        void IDisposable.Dispose()
        {

        }
    }
}

using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    /// An IMemoryCore that hides ReadOnlySpan in favor of byte arrays
    /// 
    /// Used by C++/CLI which doesn't support ReadOnlySpan (ref structs)
    /// </summary>
    public abstract class ArrayBasedMemoryCore : NotImplementedMemoryCore
    {
        public abstract byte[] GetByteArray(ushort segment, ushort offset, ushort count);
        public abstract void SetByteArray(ushort segment, ushort offset, byte[] array);
        public abstract byte[] GetByteString(ushort segment, ushort offset, bool stripNull);


        public override sealed Span<byte> GetArray(ushort segment, ushort offset, ushort count)
        {
            return GetByteArray(segment, offset, count).AsSpan();
        }

        public sealed override void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {
            SetByteArray(segment, offset, array.ToArray());
        }

        public sealed override ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull)
        {
            return GetByteString(segment, offset, stripNull).AsSpan();
        }
    }
}

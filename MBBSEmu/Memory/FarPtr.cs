using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents a far Int16:Int16 Pointer, containing segment + offset.
    /// </summary>
    public class FarPtr : IEquatable<FarPtr>
    {
        public ushort Segment { get; set; }
        public ushort Offset { get; set; }

        public byte[] Data
        {
            get => BitConverter.GetBytes((Segment << 16) | Offset);
            set
            {
                Offset = BitConverter.ToUInt16(value, 0);
                Segment = BitConverter.ToUInt16(value, 2);
            }
        }

        public const ushort Size = 4;

        public FarPtr() { }

        public FarPtr(ReadOnlySpan<byte> farPtrSpan)
        {
            FromSpan(farPtrSpan);
        }

        public FarPtr(ReadOnlySpan<byte> farPtrSpan, int startIndex)
        {
            FromSpan(farPtrSpan.Slice(startIndex, 4));
        }

        public FarPtr(ushort segment, ushort offset)
        {
            Segment = segment;
            Offset = offset;
        }

        public FarPtr(FarPtr pointer)
        {
            Segment = pointer.Segment;
            Offset = pointer.Offset;
        }

        public void FromSpan(ReadOnlySpan<byte> farPtrSpan)
        {
            Data = farPtrSpan.ToArray();
        }

        public bool IsNull() => FarPtr.Empty.Equals(this);

        /// <summary>
        ///     Returns the int16:int16 pointer as a 32-bit value
        /// </summary>
        /// <returns></returns>
        public uint ToUInt32() => (uint)((Segment << 16) | Offset);

        public bool IsAligned(ushort alignment) => (Offset & (alignment - 1)) == 0;

        /// <summary>
        ///     Returns the int16:int16 pointer as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Segment:X4}:{Offset:X4}";

        public bool Equals(FarPtr other)
        {
            if (other == null)
                return false;

            return (Segment == other.Segment && Offset == other.Offset);
        }

        public override bool Equals(object obj) => Equals(obj as FarPtr);

        public override int GetHashCode() => (int)ToUInt32();

        public static bool operator ==(FarPtr left, FarPtr right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(FarPtr left, FarPtr right)
        {
            if (left is null && right is null)
                return false;

            if (left is null || right is null)
                return true;

            return !left.Equals(right);
        }

        public static FarPtr Empty => new FarPtr(0, 0);
        public static FarPtr Null => Empty;

        public static FarPtr operator +(FarPtr i, FarPtr j)
        {
            int segment = i.Segment + j.Segment;
            int offset = i.Offset + j.Offset;
            if (offset >= 0x10000)
                ++segment;

#if DEBUG
            if (segment > 0xFFFF)
                throw new ArgumentException("Segment addition overflowed");
#endif
            return new FarPtr((ushort)segment, (ushort)(offset & 0xFFFF));
        }

        public static FarPtr operator +(FarPtr i, ushort v) => new FarPtr(i.Segment, (ushort)(i.Offset + v));
        public static FarPtr operator +(FarPtr i, int v) => new FarPtr(i.Segment, (ushort)(i.Offset + v));
        public static FarPtr operator -(FarPtr i, ushort v) => new FarPtr(i.Segment, (ushort)(i.Offset - v));
        public static FarPtr operator -(FarPtr i, int v) => new FarPtr(i.Segment, (ushort)(i.Offset - v));

        public static FarPtr operator ++(FarPtr i) => new FarPtr(i.Segment, (ushort)(i.Offset + 1));
        public static FarPtr operator --(FarPtr i) => new FarPtr(i.Segment, (ushort)(i.Offset - 1));

        public static bool operator >(FarPtr l, FarPtr r) => l.ToUInt32() > r.ToUInt32();
        public static bool operator >=(FarPtr l, FarPtr r) => l.ToUInt32() >= r.ToUInt32();

        public static bool operator <(FarPtr l, FarPtr r) => l.ToUInt32() < r.ToUInt32();
        public static bool operator <=(FarPtr l, FarPtr r) => l.ToUInt32() <= r.ToUInt32();
    }
}

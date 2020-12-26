using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents an Int16:Int16 Pointer
    /// </summary>
    public class IntPtr16 : IEquatable<IntPtr16>
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

        public IntPtr16() { }

        public IntPtr16(ReadOnlySpan<byte> intPtr16Span)
        {
            FromSpan(intPtr16Span);
        }

        public IntPtr16(ReadOnlySpan<byte> intPtr16Span, int startIndex)
        {
            FromSpan(intPtr16Span.Slice(startIndex, 4));
        }

        public IntPtr16(ushort segment, ushort offset)
        {
            Segment = segment;
            Offset = offset;
        }

        public IntPtr16(IntPtr16 pointer)
        {
            Segment = pointer.Segment;
            Offset = pointer.Offset;
        }

        public void FromSpan(ReadOnlySpan<byte> intPtr16Span)
        {
            Data = intPtr16Span.ToArray();
        }

        public bool IsNull() => IntPtr16.Empty.Equals(this);

        /// <summary>
        ///     Returns the int16:int16 pointer as a 32-bit value
        /// </summary>
        /// <returns></returns>
        public int ToInt32() => (Segment << 16) | Offset;

        /// <summary>
        ///     Returns the int16:int16 pointer as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Segment:X4}:{Offset:X4}";

        public bool Equals(IntPtr16 other)
        {
            if (other == null)
                return false;

            if (other.Segment != Segment)
                return false;

            if (other.Offset != Offset)
                return false;

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as IntPtr16);

        public override int GetHashCode()
        {
            return (Segment << 16) | Offset;
        }

        public static bool operator ==(IntPtr16 left, IntPtr16 right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            if (left.Segment != right.Segment)
                return false;

            if (left.Offset != right.Offset)
                return false;

            return true;
        }

        public static bool operator !=(IntPtr16 left, IntPtr16 right)
        {
            if (left is null && right is null)
                return false;

            if (left is null || right is null)
                return true;

            if (left.Segment != right.Segment)
                return true;

            if (left.Offset != right.Offset)
                return true;

            return false;
        }

        public static IntPtr16 Empty => new IntPtr16(0, 0);

        public static IntPtr16 operator +(IntPtr16 i, ushort v) => new IntPtr16(i.Segment, (ushort)(i.Offset + v));
        public static IntPtr16 operator +(IntPtr16 i, int v) => new IntPtr16(i.Segment, (ushort)(i.Offset + v));
        public static IntPtr16 operator -(IntPtr16 i, ushort v) => new IntPtr16(i.Segment, (ushort)(i.Offset - v));
        public static IntPtr16 operator -(IntPtr16 i, int v) => new IntPtr16(i.Segment, (ushort)(i.Offset - v));

        public static IntPtr16 operator ++(IntPtr16 i) => new IntPtr16(i.Segment, (ushort)(i.Offset + 1));
        public static IntPtr16 operator --(IntPtr16 i) => new IntPtr16(i.Segment, (ushort)(i.Offset - 1));

        public static bool operator >(IntPtr16 l, IntPtr16 r) => l.ToInt32() > r.ToInt32();
        public static bool operator >=(IntPtr16 l, IntPtr16 r) => l.ToInt32() >= r.ToInt32();

        public static bool operator <(IntPtr16 l, IntPtr16 r) => l.ToInt32() < r.ToInt32();
        public static bool operator <=(IntPtr16 l, IntPtr16 r) => l.ToInt32() <= r.ToInt32();
    }
}

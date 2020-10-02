using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents an Int16:Int16 Pointer
    /// </summary>
    public class IntPtr16 : IEquatable<IntPtr16>
    {
        public ushort Segment
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, 2);

        }
        public ushort Offset
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, 2);
        }

        public readonly byte[] Data = new byte[Size];

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
            Array.Copy(intPtr16Span.ToArray(), 0, Data, 0, 4);
        }

        /// <summary>
        ///     Returns the int16:int16 pointer as a 32-bit value
        /// </summary>
        /// <returns></returns>
        public int ToInt32() => BitConverter.ToInt32(ToSpan());

        /// <summary>
        ///     Returns the int16:int16 pointer as a byte[]
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray() => Data;

        /// <summary>
        ///     Returns a reference to the int16:int16 pointer
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> ToSpan() => Data;

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

            if (!(left is null) && right is null)
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

            if (!(left is null) && right is null)
                return true;

            if (left.Segment != right.Segment)
                return true;

            if (left.Offset != right.Offset)
                return true;

            return false;
        }

        public static IntPtr16 Empty => new IntPtr16(0, 0);

        public static IntPtr16 operator +(IntPtr16 i, ushort v) => new IntPtr16(i.Segment, (ushort)(i.Offset + v));
        public static IntPtr16 operator -(IntPtr16 i, ushort v) => new IntPtr16(i.Segment, (ushort)(i.Offset - v));

        public static IntPtr16 operator ++(IntPtr16 i) => new IntPtr16(i.Segment, (ushort)(i.Offset + 1));
        public static IntPtr16 operator --(IntPtr16 i) => new IntPtr16(i.Segment, (ushort)(i.Offset - 1));

        public static bool operator > (IntPtr16 l, IntPtr16 r) => l.ToInt32() > r.ToInt32();
        public static bool operator >= (IntPtr16 l, IntPtr16 r) => l.ToInt32() >= r.ToInt32();

        public static bool operator < (IntPtr16 l, IntPtr16 r) => l.ToInt32() < r.ToInt32();
        public static bool operator <= (IntPtr16 l, IntPtr16 r) => l.ToInt32() <= r.ToInt32();
    }
}

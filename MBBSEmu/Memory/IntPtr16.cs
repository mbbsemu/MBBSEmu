using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents an Int16:Int16 Pointer
    /// </summary>
    public class IntPtr16
    {
        public ushort Segment { get; set; }
        public ushort Offset { get; set; }

        public IntPtr16()
        {
            Segment = 0;
            Offset = 0;
        }

        public IntPtr16(ReadOnlySpan<byte> intPtr16Span)
        {
            FromSpan(intPtr16Span);
        }

        public IntPtr16(ushort segment, ushort offset)
        {
            Segment = segment;
            Offset = offset;
        }

        public void FromSpan(ReadOnlySpan<byte> intPtr16Span)
        {
            Offset = BitConverter.ToUInt16(intPtr16Span.Slice(0, 2));
            Segment = BitConverter.ToUInt16(intPtr16Span.Slice(2, 2));
        }

        public int ToInt() => BitConverter.ToInt32(ToSpan());

        public byte[] ToArray() => ToSpan().ToArray();

        public ReadOnlySpan<byte> ToSpan()
        {
            var output = new byte[4];
            Array.Copy(BitConverter.GetBytes(Offset), 0, output, 0, 2);
            Array.Copy(BitConverter.GetBytes(Segment), 0, output, 2, 2);
            return output;
        }
    }
}

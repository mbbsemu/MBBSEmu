using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents an Int16:Int16 Pointer
    /// </summary>
    public class IntPtr16
    {
        public ushort Segment
        {
            get => BitConverter.ToUInt16(_pointerData, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _pointerData, 2, 2);

        }
        public ushort Offset
        {
            get => BitConverter.ToUInt16(_pointerData, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _pointerData, 0, 2);

        }

        private readonly byte[] _pointerData = new byte[4];

        public IntPtr16() { }

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
            Array.Copy(intPtr16Span.ToArray(), 0, _pointerData, 0, 4);
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
        public byte[] ToArray() => _pointerData;

        /// <summary>
        ///     Returns a reference to the int16:int16 pointer
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> ToSpan() => _pointerData;

        /// <summary>
        ///     Returns the int16:int16 pointer as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Segment:X4}:{Offset:X4}";
    }
}

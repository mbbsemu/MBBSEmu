using System;

namespace MBBSEmu.Extensions
{
    public static class ReadOnlySpanExtensions
    {
        public static ReadOnlySpan<char> ToCharSpan(this ReadOnlySpan<byte> readOnlySpan)
        {
            var output = new char[readOnlySpan.Length];
            for (var i = 0; i < readOnlySpan.Length; i++)
            {
                output[i] = (char)readOnlySpan[i];
            }
            return output;
        }

        /// <summary>
        ///     Returns true if readOnlySpan contains only value.
        /// </summary>
        public static bool ContainsOnly(this ReadOnlySpan<byte> readOnlySpan, byte value)
        {
            for (var i = 0; i < readOnlySpan.Length; ++i)
            {
                if (readOnlySpan[i] != value)
                    return false;
            }
            return true;
        }
    }
}

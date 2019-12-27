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
    }
}
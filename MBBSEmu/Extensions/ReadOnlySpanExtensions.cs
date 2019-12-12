using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Extensions
{
    public static class ReadOnlySpanExtensions
    {
        public static ReadOnlySpan<char> ToCharSpan(this ReadOnlySpan<byte> readOnlySpan)
        {
            var output = new char[readOnlySpan.Length];
            for (int i = 0; i < readOnlySpan.Length; i++)
            {
                output[i] = (char)readOnlySpan[i];
            }
            return output;
        }
    }
}

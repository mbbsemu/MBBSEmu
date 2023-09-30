using Microsoft.Extensions.Logging;
using System;
using System.Text;

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

        /// <summary>
        ///     Creates a human readable hex string from a ReadOnlySpan of bytes
        ///
        ///     The header contains the total number of bytes, the start and end address
        ///     as well as columns for each byte within an 8-bit boundary.
        /// </summary>
        /// <param name="readOnlySpan"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string ToHexString(this ReadOnlySpan<byte> readOnlySpan, ushort start, ushort length)
        {
                var output = new StringBuilder();

                //Print Header
                output.AppendLine(new string('-', 73));
                output.AppendLine($"{readOnlySpan.Length} bytes, 0x{start:X4} -> 0x{start + length:X4}");
                output.AppendLine(new string('-', 73));

                //Handle Zero Length
                if (length == 0)
                {
                    output.AppendLine("No Data to Display");
                    return output.ToString();
                }

                output.Append("      ");
                for (var i = 0; i < 0x10; i++)
                {
                    output.Append($" {i:X2}");
                }
                output.AppendLine();
                var hexString = new StringBuilder(47);
                var literalString = new StringBuilder(15);

                //Print Hex Values
                for (var i = start; i < start + length; i++)
                {
                    hexString.Append($" {readOnlySpan[i]:X2}");
                    literalString.Append(readOnlySpan[i] < 32 ? ' ' : (char)readOnlySpan[i]);

                    //New Memory Page
                    if ((i | 0x0F) == i)
                    {
                        output.AppendLine($"{(i & ~0xF):X4} [{hexString} ] {literalString}");
                        hexString.Clear();
                        literalString.Clear();
                    }
                }

                //Flush any data remaining in the buffer
                if (hexString.Length > 0)
                {
                    output.AppendLine($"{(start + length) & ~0xF:X4} [{hexString.ToString().PadRight(48)} ] {literalString}");
                    hexString.Clear();
                    literalString.Clear();
                }

                return output.ToString();
        }
    }
}

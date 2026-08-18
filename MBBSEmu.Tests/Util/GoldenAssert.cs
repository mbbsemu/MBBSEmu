using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit.Sdk;

namespace MBBSEmu.Tests.Util
{
    /// <summary>
    ///     Byte-exact assertions against golden captures (raw server byte streams
    ///     recorded from real MajorBBS or from MBBSEmu itself). Failures render a
    ///     hex dump around the first difference so mismatches are diagnosable
    ///     without re-running under a debugger.
    /// </summary>
    public static class GoldenAssert
    {
        /// <summary>
        ///     Asserts actual matches the golden file byte-for-byte, except within
        ///     maskedRanges (offset/length spans covering volatile data such as
        ///     dates, usernames, or random rolls), which are zeroed on both sides
        ///     before comparison.
        /// </summary>
        public static void MatchesGolden(byte[] actual, string goldenPath, IEnumerable<(int Offset, int Length)> maskedRanges = null)
        {
            if (!File.Exists(goldenPath))
                throw new XunitException($"Golden file not found: {goldenPath}");

            var expected = File.ReadAllBytes(goldenPath);
            var maskedExpected = ApplyMasks(expected, maskedRanges);
            var maskedActual = ApplyMasks(actual, maskedRanges);

            var compareLength = Math.Min(maskedExpected.Length, maskedActual.Length);
            for (var i = 0; i < compareLength; i++)
            {
                if (maskedExpected[i] != maskedActual[i])
                    throw new XunitException(
                        $"Differs from golden {Path.GetFileName(goldenPath)} at offset 0x{i:X} " +
                        $"(expected 0x{maskedExpected[i]:X2}, actual 0x{maskedActual[i]:X2})\n" +
                        $"--- expected ---\n{HexDumpAround(maskedExpected, i)}\n" +
                        $"--- actual ---\n{HexDumpAround(maskedActual, i)}");
            }

            if (maskedExpected.Length != maskedActual.Length)
                throw new XunitException(
                    $"Length differs from golden {Path.GetFileName(goldenPath)}: " +
                    $"expected {maskedExpected.Length} bytes, actual {maskedActual.Length} bytes " +
                    $"(streams match through offset 0x{compareLength:X})");
        }

        /// <summary>
        ///     Asserts none of the forbidden bytes appear in actual. On failure,
        ///     reports per-byte counts and the first few offsets of each.
        /// </summary>
        public static void ContainsNone(byte[] actual, params byte[] forbiddenBytes)
        {
            var failures = new StringBuilder();
            foreach (var forbidden in forbiddenBytes)
            {
                var offsets = new List<int>();
                for (var i = 0; i < actual.Length; i++)
                {
                    if (actual[i] == forbidden)
                        offsets.Add(i);
                }

                if (offsets.Count > 0)
                {
                    var shown = string.Join(", ", offsets.Take(5).Select(o => $"0x{o:X}"));
                    failures.AppendLine(
                        $"0x{forbidden:X2} found {offsets.Count}x (first at {shown})\n{HexDumpAround(actual, offsets[0])}");
                }
            }

            if (failures.Length > 0)
                throw new XunitException($"Forbidden bytes present in {actual.Length}-byte stream:\n{failures}");
        }

        private static byte[] ApplyMasks(byte[] data, IEnumerable<(int Offset, int Length)> maskedRanges)
        {
            if (maskedRanges == null)
                return data;

            var masked = (byte[])data.Clone();
            foreach (var (offset, length) in maskedRanges)
            {
                for (var i = offset; i < offset + length && i < masked.Length; i++)
                    masked[i] = 0;
            }

            return masked;
        }

        /// <summary>
        ///     xxd-style dump of the 16-byte lines surrounding offset (one line of
        ///     context on each side), with the offending byte bracketed.
        /// </summary>
        private static string HexDumpAround(byte[] data, int offset)
        {
            var startLine = Math.Max(0, offset / 16 - 1);
            var endLine = Math.Min((data.Length - 1) / 16, offset / 16 + 1);
            var sb = new StringBuilder();

            for (var line = startLine; line <= endLine; line++)
            {
                var lineStart = line * 16;
                sb.Append($"{lineStart:X8}: ");

                var ascii = new StringBuilder();
                for (var i = lineStart; i < lineStart + 16; i++)
                {
                    if (i >= data.Length)
                    {
                        sb.Append("   ");
                        continue;
                    }

                    sb.Append(i == offset ? $"[{data[i]:X2}]" : $" {data[i]:X2}");
                    ascii.Append(data[i] >= 0x20 && data[i] < 0x7F ? (char)data[i] : '.');
                }

                sb.Append("  ").Append(ascii).AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}

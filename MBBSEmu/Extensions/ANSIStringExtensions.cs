using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MBBSEmu.Extensions
{
    public static class ANSIStringExtensions
    {
        private static readonly Dictionary<string, string> ColorCodeDictionary;

        static ANSIStringExtensions()
        {
            ColorCodeDictionary = new Dictionary<string, string>
            {
                {"|RESET|", "\x1B[0m"},
                {"|B|", "\x1B[1m"},
                {"|!B|", "\x1B[22m"},
                {"|I|", "\x1B[3m"},
                {"|!I|", "\x1B[23m"},
                {"|U|", "\x1B[4m"},
                {"|!U|", "\x1B[24m"},
                {"|BLINK|", "\x1B[5m"},
                {"|!BLINK|", "\x1B[25m"},
                {"|BLINKFAST|", "\x1B[6m"},
                {"|BLACK|", "\x1B[30m"},
                {"|!BLACK|", "\x1B[40m"},
                {"|RED|", "\x1B[31m"},
                {"|!RED|", "\x1B[41m"},
                {"|GREEN|", "\x1B[32m"},
                {"|!GREEN|", "\x1B[42m"},
                {"|YELLOW|", "\x1B[33m"},
                {"|!YELLOW|", "\x1B[43m"},
                {"|BLUE|", "\x1B[34m"},
                {"|!BLUE|", "\x1B[44m"},
                {"|MAGENTA|", "\x1B[35m"},
                {"|!MAGENTA|", "\x1B[45m"},
                {"|CYAN|", "\x1B[36m"},
                {"|!CYAN|", "\x1B[46m"},
                {"|WHITE|", "\x1B[37m"},
                {"|!WHITE|", "\x1B[47m"}
            };

        }

        public static string EncodeToANSIString(this string s)
        {
            return ColorCodeDictionary.Aggregate(s, (current, c) => current.Replace(c.Key, c.Value));
        }

        public static ReadOnlySpan<byte> EncodeToANSISpan(this string s)
        {
            return Encoding.ASCII.GetBytes(EncodeToANSIString(s));
        }

        public static byte[] EncodeToANSIArray(this string s)
        {
            return Encoding.ASCII.GetBytes(EncodeToANSIString(s));
        }

        public static byte[] GetNullTerminatedBytes(this string s)
        {
            return Encoding.ASCII.GetBytes(s + '\0');
        }
    }
}

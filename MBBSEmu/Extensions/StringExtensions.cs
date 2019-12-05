using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;

namespace MBBSEmu.Extensions
{
    public static class StringExtensions
    {
        private static readonly List<char> _controlCharacters = new List<char> { 'c', 'd', 'i', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 's', 'P', 'N', '%'};

        /// <summary>
        ///     Returns the string converted from printf format to a String.Format string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string FormatPrintf(this string s)
        {
            var currentControlCharacter = 0;
            var sbOutput = new StringBuilder();
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && _controlCharacters.Any(x => x == s[i + 1]))
                {
                    sbOutput.Append($"{{{currentControlCharacter}}}");
                    currentControlCharacter++;
                    i++;
                    continue;
                }
                sbOutput.Append(s[i]);
            }

            return sbOutput.ToString();
        }

        /// <summary>
        ///     Returns the number of Control Characters observed in the printf string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int CountPrintf(this string s)
        {
            var controlCharacterCount = 0;
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && _controlCharacters.Any(x => x == s[i + 1]))
                {
                    controlCharacterCount++;
                    i++;
                }
            }
            return controlCharacterCount;
        }

        /// <summary>
        ///     Gets the control character at the specified control character ordinal
        /// </summary>
        /// <param name="s"></param>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public static char GetPrintf(this string s, int ordinal)
        {
            var controlCharacterCount = 0;
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && _controlCharacters.Any(x => x == s[i + 1]))
                {
                    if (controlCharacterCount == ordinal)
                        return s[i + 1];

                    controlCharacterCount++;
                    i++;
                }
            }

            return '\0';
        }
    }
}

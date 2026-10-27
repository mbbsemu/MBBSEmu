using System;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Main menu keys for modules added without one
    /// </summary>
    public static class ModuleMenuKeys
    {
        /// <summary>
        ///     X is the main menu's log off option, so a module can never have it
        /// </summary>
        public const string LogOffKey = "X";

        /// <summary>
        ///     A to Z (except X), then 1 to 9, then 10 to 99
        /// </summary>
        private static readonly IReadOnlyList<string> Sequence = Enumerable.Range('A', 26)
            .Select(x => ((char)x).ToString())
            .Where(x => x != LogOffKey)
            .Concat(Enumerable.Range(1, 99).Select(x => x.ToString()))
            .ToList();

        /// <summary>
        ///     The first key in the sequence that isn't used, compared case-insensitively
        /// </summary>
        /// <returns>Null if every key is taken</returns>
        public static string Next(IEnumerable<string> used)
        {
            var taken = new HashSet<string>(used.Where(x => !string.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
            return Sequence.FirstOrDefault(x => !taken.Contains(x));
        }
    }
}

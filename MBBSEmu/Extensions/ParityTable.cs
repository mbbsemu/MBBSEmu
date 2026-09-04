namespace MBBSEmu.Extensions
{
    /// <summary>
    ///     Precomputed even-parity lookup for a single byte, matching the x86 PF flag:
    ///     Table[b] is true if the number of set bits in b is even.
    /// </summary>
    internal static class ParityTable
    {
        public static readonly bool[] Table = BuildTable();

        private static bool[] BuildTable()
        {
            var table = new bool[256];
            for (var i = 0; i < table.Length; i++)
            {
                var setBits = 0;
                for (var bit = 0; bit <= 7; bit++)
                {
                    if ((i & (1 << bit)) != 0)
                        setBits++;
                }
                table[i] = setBits % 2 == 0;
            }
            return table;
        }
    }
}

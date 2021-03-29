using System.Runtime.CompilerServices;

namespace MBBSEmu.Extensions
{
    public static class UintExtensions
    {
        /// <summary>
        ///     Helper Method just to see if Bit 7 is set denoting a negative value of a signed byte
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(this uint b) => (b & 0x80000000) != 0;

        /// <summary>
        ///     Returns if the specified bit was set
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitNumber"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(this uint b, int bitNumber) => (b & (1 << bitNumber)) != 0;

        /// <summary>
        ///     Returns if the specified flag is set in the byte
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlagSet(this uint b, uint bitMask) => (b & bitMask) != 0;

        /// <summary>
        ///     Sets the specified bitmask to for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort SetFlag(this uint b, uint bitMask) => (ushort)(b | bitMask);

        /// <summary>
        ///     Sets the specified bitmask to 0 for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ClearFlag(this uint b, uint bitMask) => (ushort)(b & ~bitMask);

        /// <summary>
        ///     Gets the Parity for the specified byte
        ///
        ///     On x86, even if the value is 16-bits, parity is only calculated on the least significant
        ///     8 bits.
        /// </summary>
        /// <param name="b"></param>
        /// <returns>1 == Even, 0 == Odd</returns>
        public static bool Parity(this uint b)
        {
            var setBits = 0;
            for (var i = 0; i <= 7; i++)
            {
                if (b.IsBitSet(i))
                    setBits++;
            }

            return setBits != 0 && setBits % 2 == 0;
        }
    }
}

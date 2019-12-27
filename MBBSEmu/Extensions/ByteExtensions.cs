using System.Runtime.CompilerServices;

namespace MBBSEmu.Extensions
{
    public static class ByteExtensions
    {
        /// <summary>
        ///     Helper Method just to see if Bit 7 is set denoting a negative value of a signed byte
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(this byte b) => (b & (1 << 7)) != 0;

        /// <summary>
        ///     Returns if the specified bit was set
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitNumber"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(this byte b, int bitNumber) => (b & (1 << bitNumber)) != 0;

        /// <summary>
        ///     Returns if the specified flag is set in the byte
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlagSet(this byte b, byte bitMask) => (b & bitMask) != 0;

        /// <summary>
        ///     Sets the specified bitmask to for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte SetFlag(this byte b, byte bitMask) => (byte)(b | bitMask);

        /// <summary>
        ///     Sets the specified bitmask to 0 for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte RemoveFlag(this byte b, byte bitMask) => (byte)(b & ~bitMask);

        /// <summary>
        ///     Gets the Parity for the specified byte
        /// </summary>
        /// <param name="b"></param>
        /// <returns>1 == Even, 0 == Odd</returns>
        public static bool Parity(this byte b)
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

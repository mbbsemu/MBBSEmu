using System.Runtime.CompilerServices;

namespace MBBSEmu.Extensions
{
    public static class UshortExtensions
    {
        /// <summary>
        ///     Helper Method just to see if Bit 7 is set denoting a negative value of a signed byte
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(this ushort b) => (b & (1 << 15)) != 0;

        /// <summary>
        ///     Returns if the specified bit was set
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitNumber"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(this ushort b, int bitNumber) => (b & (1 << bitNumber)) != 0;

        /// <summary>
        ///     Returns if the specified flag is set in the byte
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlagSet(this ushort b, ushort bitMask) => (b & bitMask) != 0;

        /// <summary>
        ///     Sets the specified bitmask to for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort SetFlag(this ushort b, ushort bitMask) => (ushort)(b | bitMask);

        /// <summary>
        ///     Sets the specified bitmask to 0 for the specified bits
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bitMask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ClearFlag(this ushort b, ushort bitMask) => (ushort)(b & ~bitMask);
    }
}

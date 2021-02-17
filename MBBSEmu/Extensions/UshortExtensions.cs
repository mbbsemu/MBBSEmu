using System;
using System.Runtime.CompilerServices;
using MBBSEmu.Date;

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
        public static bool IsNegative(this ushort b) => (b >> 15) == 1;

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

        /// <summary>
        ///     Gets the Parity for the specified byte
        ///
        ///     On x86, even if the value is 16-bits, parity is only calculated on the least significant
        ///     8 bits.
        /// </summary>
        /// <param name="b"></param>
        /// <returns>1 == Even, 0 == Odd</returns>
        public static bool Parity(this ushort b)
        {
            var setBits = 0;
            for (var i = 0; i <= 7; i++)
            {
                if (b.IsBitSet(i))
                    setBits++;
            }

            return setBits != 0 && setBits % 2 == 0;
        }

        /// <summary>
        ///     Sign extends 16bit -> 32bit
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUintSignExtended(this ushort b) => (uint)(short) b;

        /// <summary>
        ///     Pack DOS date
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToDosDate(this DateTime b)
        {
            return (ushort) ((b.Month << 5) + b.Day + ((b.Year - 1980) << 9));
        }

        /// <summary>
        ///     Unpack DOS date
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime FromDosDate(this ushort b)
        {
            if (b == 0)
                b = 0x5152;
            
            var outputDate = new DateTime(((b >> 9) & 0x007F) + 1980, (b >> 5) & 0x000F, b & 0x001F);

            return outputDate;
        }
    }
}

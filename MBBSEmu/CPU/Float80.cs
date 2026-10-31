using System;
using System.Buffers.Binary;
using System.Numerics;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     An x87 80-bit extended-precision value, as the 8087/80287/80387 holds it in its registers and in a 10-byte
    ///     (TBYTE) memory operand: a sign, a 15-bit biased exponent (bias 16383) and a 64-bit significand whose integer
    ///     bit is explicit (bit 63).
    ///
    ///     Every arithmetic operation is computed exactly and rounded once, as the coprocessor does: to the precision
    ///     control of the control word (bits 8-9: 24, 53 or 64 significant bits) where the instruction obeys it (FADD,
    ///     FSUB, FMUL, FDIV and their variants, FSQRT), and always to the rounding control (bits 10-11: nearest-even,
    ///     down, up, chop). The exponent range stays the extended range under every precision control. Stores to
    ///     single and double precision round to their format by the rounding control, and stores to integers round by
    ///     the rounding control and give the "integer indefinite" (the most negative value) when the value does not fit.
    ///
    ///     The operations report what the instruction reports in the status word: they OR the exception flags into it
    ///     (IE invalid, DE denormal operand, ZE zero divide, OE overflow, UE underflow, PE precision) and set C1 ("rounded
    ///     up": the magnitude of the result was increased) or the condition codes (FCOM, FPREM). They deliver the masked
    ///     response of every exception (the default results: infinities, the largest finite value, denormals, the real
    ///     indefinite), whatever the exception masks of the control word say: an unmasked exception would interrupt the
    ///     program on a real coprocessor, which this emulation does not do.
    ///
    ///     Where the 80287 and the 80387 differ, this follows the 80387 (and later x87 units): unnormals,
    ///     pseudo-infinities and pseudo-NaNs are invalid operands (IE, the result is the real indefinite), pseudo-denormals
    ///     are accepted as denormal operands (DE) and never produced, constants are rounded by the rounding control,
    ///     FSCALE accepts any scale factor, FXTRACT of zero gives -infinity (ZE), and FPREM reduces a large exponent
    ///     difference D in steps of 32 + (D mod 32) bits. Tininess is detected after rounding; the precision control's
    ///     width is counted from the smallest normal exponent for a denormal result. These rules were measured on
    ///     coprocessors, not taken from documentation alone.
    /// </summary>
    public readonly struct Float80 : IEquatable<Float80>
    {
        //Status word bits reported by the operations
        private const int IE = 0x0001;
        private const int DE = 0x0002;
        private const int ZE = 0x0004;
        private const int OE = 0x0008;
        private const int UE = 0x0010;
        private const int PE = 0x0020;
        private const int C0 = 0x0100;
        private const int C1 = 0x0200;
        private const int C2 = 0x0400;
        private const int C3 = 0x4000;
        private const int ConditionCodes = C0 | C1 | C2 | C3;

        private const int Bias = 16383;
        private const int MinExponent = -16382;
        private const int MaxExponent = 16383;
        private const ulong IntegerBit = 0x8000_0000_0000_0000;
        private const ulong QuietBit = 0x4000_0000_0000_0000;

        private const int RoundNearest = 0;
        private const int RoundDown = 1;
        private const int RoundUp = 2;

        /// <summary>
        ///     The 64-bit significand, with the integer bit explicit (bit 63)
        /// </summary>
        public readonly ulong Significand;

        /// <summary>
        ///     The sign (bit 15) and the biased exponent (bits 0-14)
        /// </summary>
        public readonly ushort SignExponent;

        public Float80(ushort signExponent, ulong significand)
        {
            SignExponent = signExponent;
            Significand = significand;
        }

        public static readonly Float80 Zero = new(0, 0);
        public static readonly Float80 NegativeZero = new(0x8000, 0);
        public static readonly Float80 One = new(Bias, IntegerBit);
        public static readonly Float80 PositiveInfinity = new(0x7FFF, IntegerBit);
        public static readonly Float80 NegativeInfinity = new(0xFFFF, IntegerBit);

        /// <summary>
        ///     The "real indefinite": the quiet NaN the coprocessor delivers for an invalid operation
        /// </summary>
        public static readonly Float80 Indefinite = new(0xFFFF, 0xC000_0000_0000_0000);

        private enum Kind : byte
        {
            Zero,
            Denormal, //Includes the pseudo-denormals (biased exponent 0 with the integer bit set)
            Normal,
            Infinity,
            QuietNaN,
            SignalingNaN,
            Unsupported //Unnormals, pseudo-infinities and pseudo-NaNs: invalid operands since the 80387
        }

        private Kind GetKind()
        {
            var exponent = SignExponent & 0x7FFF;
            if (exponent == 0x7FFF)
            {
                if ((Significand & IntegerBit) == 0)
                    return Kind.Unsupported;
                if (Significand == IntegerBit)
                    return Kind.Infinity;
                return (Significand & QuietBit) != 0 ? Kind.QuietNaN : Kind.SignalingNaN;
            }

            if (exponent == 0)
                return Significand == 0 ? Kind.Zero : Kind.Denormal;

            return (Significand & IntegerBit) != 0 ? Kind.Normal : Kind.Unsupported;
        }

        public bool IsNegative => (SignExponent & 0x8000) != 0;
        public bool IsZero => GetKind() == Kind.Zero;
        public bool IsDenormal => GetKind() == Kind.Denormal;
        public bool IsInfinity => GetKind() == Kind.Infinity;
        public bool IsNaN => GetKind() is Kind.QuietNaN or Kind.SignalingNaN;
        public bool IsSignalingNaN => GetKind() == Kind.SignalingNaN;
        public bool IsUnsupported => GetKind() == Kind.Unsupported;

        /// <summary>
        ///     The value with its sign inverted (FCHS): a bit operation, no exception even for a NaN
        /// </summary>
        public Float80 Negate() => new((ushort)(SignExponent ^ 0x8000), Significand);

        /// <summary>
        ///     The value with its sign cleared (FABS): a bit operation, no exception even for a NaN
        /// </summary>
        public Float80 Abs() => new((ushort)(SignExponent & 0x7FFF), Significand);

        /// <summary>
        ///     A finite non-zero value as (-1)^sign * significand * 2^exponent, the significand normalized (bit 63 set):
        ///     denormals and pseudo-denormals have the exponent of the smallest normal before normalization
        /// </summary>
        private void Unpack(out ulong significand, out int exponent)
        {
            significand = Significand;
            exponent = Math.Max(SignExponent & 0x7FFF, 1) - Bias - 63;
            var shift = BitOperations.LeadingZeroCount(significand);
            significand <<= shift;
            exponent -= shift;
        }

        private static Float80 Signed(bool negative, Float80 value) =>
            new((ushort)(value.SignExponent | (negative ? 0x8000 : 0)), value.Significand);

        private static Float80 Quiet(Float80 value) => new(value.SignExponent, value.Significand | QuietBit);

        private static int Precision(ushort controlWord) => ((controlWord >> 8) & 3) switch
        {
            0 => 24,
            2 => 53,
            _ => 64 //11 is extended; the reserved 01 acts as extended too (measured)
        };

        private static int Rounding(ushort controlWord) => (controlWord >> 10) & 3;

        private static int BitLength(UInt128 value) => 128 - (int)UInt128.LeadingZeroCount(value);

        /// <summary>
        ///     value >> shift, rounded by the rounding control. sticky: something non-zero lies below the lowest bit of
        ///     value (the caller keeps it at least two bits below the bits rounded away).
        /// </summary>
        private static UInt128 RoundShift(UInt128 value, int shift, bool sticky, bool negative, int roundingControl,
            out bool inexact, out bool roundedUp)
        {
            UInt128 result;
            bool half, below;
            if (shift <= 0)
            {
                result = value << -shift;
                half = false;
                below = sticky;
            }
            else if (shift > 128)
            {
                result = UInt128.Zero;
                half = false;
                below = value != UInt128.Zero || sticky;
            }
            else
            {
                result = shift == 128 ? UInt128.Zero : value >> shift;
                half = ((value >> (shift - 1)) & UInt128.One) != UInt128.Zero;
                below = sticky || (shift > 1 && (value & ((UInt128.One << (shift - 1)) - UInt128.One)) != UInt128.Zero);
            }

            inexact = half || below;
            roundedUp = roundingControl switch
            {
                RoundNearest => half && (below || (result & UInt128.One) != UInt128.Zero),
                RoundDown => inexact && negative,
                RoundUp => inexact && !negative,
                _ => false
            };

            return roundedUp ? result + UInt128.One : result;
        }

        /// <summary>
        ///     Rounds (-1)^negative * (value + sticky) * 2^exponent (value non-zero) to precision bits in a format whose
        ///     normal numbers have leading-bit exponents from minExponent to maxExponent. Returns false on overflow (the
        ///     caller delivers the masked response); otherwise the rounded significand and the exponent of its lowest
        ///     bit, with PE, UE and C1 added to flags.
        /// </summary>
        private static bool Round(bool negative, UInt128 value, int exponent, bool sticky, int precision,
            int minExponent, int maxExponent, int roundingControl, ref int flags, out UInt128 significand,
            out int lowExponent)
        {
            var bits = BitLength(value);
            var lead = exponent + bits - 1;

            //Tininess is detected after rounding: the value rounded to precision bits with an unbounded exponent is
            //still below the smallest normal
            var tiny = lead < minExponent - 1;
            if (lead == minExponent - 1)
            {
                var unbounded = RoundShift(value, bits - precision, sticky, negative, roundingControl, out _, out _);
                tiny = BitLength(unbounded) <= precision;
            }

            //The delivered rounding: precision bits, never below the denormal position (precision bits counted from
            //the smallest normal exponent)
            lowExponent = Math.Max(lead - precision + 1, minExponent - precision + 1);
            significand = RoundShift(value, lowExponent - exponent, sticky, negative, roundingControl,
                out var inexact, out var roundedUp);
            if (BitLength(significand) > precision)
            {
                significand >>= 1;
                lowExponent++;
            }

            if (inexact)
                flags |= tiny ? PE | UE : PE;

            if (significand != UInt128.Zero && lowExponent + BitLength(significand) - 1 > maxExponent)
                return false;

            if (roundedUp)
                flags |= C1;

            return true;
        }

        /// <summary>
        ///     The masked response to an overflow: infinity when the rounding goes away from zero (C1 set), otherwise
        ///     the largest finite value of the precision
        /// </summary>
        private static bool OverflowsToInfinity(bool negative, int roundingControl, ref int flags)
        {
            var toInfinity = roundingControl == RoundNearest || (roundingControl == RoundDown && negative) ||
                             (roundingControl == RoundUp && !negative);
            flags |= toInfinity ? OE | PE | C1 : OE | PE;
            return toInfinity;
        }

        /// <summary>
        ///     The extended result of an operation: (-1)^negative * (value + sticky) * 2^exponent, value non-zero
        /// </summary>
        private static Float80 RoundExtended(bool negative, UInt128 value, int exponent, bool sticky, int precision,
            int roundingControl, ref int flags)
        {
            var sign = negative ? 0x8000 : 0;
            if (!Round(negative, value, exponent, sticky, precision, MinExponent, MaxExponent, roundingControl,
                    ref flags, out var significand, out var lowExponent))
            {
                return OverflowsToInfinity(negative, roundingControl, ref flags)
                    ? new Float80((ushort)(sign | 0x7FFF), IntegerBit)
                    : new Float80((ushort)(sign | 0x7FFE), ulong.MaxValue << (64 - precision));
            }

            if (significand == UInt128.Zero)
                return new Float80((ushort)sign, 0);

            var bits = BitLength(significand);
            var lead = lowExponent + bits - 1;
            if (lead < MinExponent)
                return new Float80((ushort)sign, (ulong)significand << (lowExponent - (MinExponent - 63)));

            return new Float80((ushort)(sign | (lead + Bias)), (ulong)significand << (64 - bits));
        }

        /// <summary>
        ///     A single (precision 24) or double (precision 53) result: the IEEE bits
        /// </summary>
        private static ulong RoundIeee(bool negative, UInt128 value, int exponent, int precision, int maxExponent,
            int roundingControl, ref int flags)
        {
            var fractionBits = precision - 1;
            var sign = negative ? 1UL << (fractionBits + (precision == 24 ? 8 : 11)) : 0;
            if (!Round(negative, value, exponent, false, precision, 1 - maxExponent, maxExponent, roundingControl,
                    ref flags, out var significand, out var lowExponent))
            {
                return OverflowsToInfinity(negative, roundingControl, ref flags)
                    ? sign | ((ulong)(2 * maxExponent + 1) << fractionBits)
                    : sign | ((ulong)(2 * maxExponent) << fractionBits) | ((1UL << fractionBits) - 1);
            }

            if (significand == UInt128.Zero)
                return sign;

            var bits = BitLength(significand);
            var lead = lowExponent + bits - 1;
            if (lead < 1 - maxExponent)
                return sign | (ulong)significand;

            return sign | ((ulong)(lead + maxExponent) << fractionBits) |
                   (((ulong)significand << (precision - bits)) & ((1UL << fractionBits) - 1));
        }

        /// <summary>
        ///     Unsupported encodings first, then NaNs: true with the result when an operand is one of them. Of two NaNs
        ///     the one with the larger significand wins (the positive one when they are equal); the result is quiet, and
        ///     a signalling NaN raises IE.
        /// </summary>
        private static bool Screen(Float80 a, Float80 b, ref int flags, out Float80 result)
        {
            var ka = a.GetKind();
            var kb = b.GetKind();
            if (ka == Kind.Unsupported || kb == Kind.Unsupported)
            {
                flags |= IE;
                result = Indefinite;
                return true;
            }

            var nanA = ka is Kind.QuietNaN or Kind.SignalingNaN;
            var nanB = kb is Kind.QuietNaN or Kind.SignalingNaN;
            if (!nanA && !nanB)
            {
                result = default;
                return false;
            }

            if (ka == Kind.SignalingNaN || kb == Kind.SignalingNaN)
                flags |= IE;

            Float80 nan;
            if (nanA && nanB)
                nan = a.Significand != b.Significand ? (a.Significand > b.Significand ? a : b) : (a.IsNegative ? b : a);
            else
                nan = nanA ? a : b;

            result = Quiet(nan);
            return true;
        }

        private static bool Screen(Float80 a, ref int flags, out Float80 result)
        {
            switch (a.GetKind())
            {
                case Kind.Unsupported:
                    flags |= IE;
                    result = Indefinite;
                    return true;
                case Kind.SignalingNaN:
                    flags |= IE;
                    result = Quiet(a);
                    return true;
                case Kind.QuietNaN:
                    result = a;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static int DenormalFlag(Kind a, Kind b, bool memoryOperandDenormal) =>
            a == Kind.Denormal || b == Kind.Denormal || memoryOperandDenormal ? DE : 0;

        private static void Report(ref ushort statusWord, int flags) =>
            statusWord = (ushort)((statusWord & ~C1) | flags);

        /// <summary>
        ///     a + b (FADD). memoryOperandDenormal: one operand came from a single or double memory operand that is a
        ///     denormal there (it raises DE like a denormal register; the value itself is exact).
        /// </summary>
        public static Float80 Add(Float80 a, Float80 b, ushort controlWord, ref ushort statusWord,
            bool memoryOperandDenormal = false)
        {
            var flags = 0;
            var result = Add(a, b, false, controlWord, memoryOperandDenormal, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        /// <summary>
        ///     a - b (FSUB, and FSUBR with the operands exchanged)
        /// </summary>
        public static Float80 Subtract(Float80 a, Float80 b, ushort controlWord, ref ushort statusWord,
            bool memoryOperandDenormal = false)
        {
            var flags = 0;
            var result = Add(a, b, true, controlWord, memoryOperandDenormal, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 Add(Float80 a, Float80 b, bool subtract, ushort controlWord, bool memoryOperandDenormal,
            ref int flags)
        {
            if (Screen(a, b, ref flags, out var nan))
                return nan;

            var ka = a.GetKind();
            var kb = b.GetKind();
            var negativeA = a.IsNegative;
            var negativeB = b.IsNegative ^ subtract;
            if (ka == Kind.Infinity || kb == Kind.Infinity)
            {
                if (ka == kb && negativeA != negativeB)
                {
                    flags |= IE;
                    return Indefinite;
                }

                flags |= DenormalFlag(ka, kb, memoryOperandDenormal);
                return Signed(ka == Kind.Infinity ? negativeA : negativeB, PositiveInfinity);
            }

            flags |= DenormalFlag(ka, kb, memoryOperandDenormal);
            var precision = Precision(controlWord);
            var rounding = Rounding(controlWord);
            if (ka == Kind.Zero && kb == Kind.Zero)
                return negativeA == negativeB ? Signed(negativeA, Zero) : Signed(rounding == RoundDown, Zero);

            if (ka == Kind.Zero)
            {
                b.Unpack(out var onlyB, out var exponentB);
                return RoundExtended(negativeB, onlyB, exponentB, false, precision, rounding, ref flags);
            }

            if (kb == Kind.Zero)
            {
                a.Unpack(out var onlyA, out var exponentA);
                return RoundExtended(negativeA, onlyA, exponentA, false, precision, rounding, ref flags);
            }

            a.Unpack(out var significandA, out var lowA);
            b.Unpack(out var significandB, out var lowB);
            if (lowA < lowB)
            {
                (significandA, significandB) = (significandB, significandA);
                (lowA, lowB) = (lowB, lowA);
                (negativeA, negativeB) = (negativeB, negativeA);
            }

            //Both significands normalized; a has the larger exponent. Within 63 bits of each other the sum is exact
            //in 128 bits; further apart, a is the larger by far and b only matters as a sticky bit far below the
            //rounding position
            var difference = lowA - lowB;
            var x = (UInt128)significandA << 63;
            UInt128 y;
            if (difference <= 63)
                y = (UInt128)significandB << (63 - difference);
            else if (difference - 63 >= 64)
                y = UInt128.One;
            else
                y = (UInt128)(significandB >> (difference - 63)) |
                    ((significandB & ((1UL << (difference - 63)) - 1)) != 0 ? UInt128.One : UInt128.Zero);

            UInt128 sum;
            bool negative;
            if (negativeA == negativeB)
            {
                sum = x + y;
                negative = negativeA;
            }
            else if (x >= y)
            {
                sum = x - y;
                negative = negativeA;
            }
            else
            {
                sum = y - x;
                negative = negativeB;
            }

            if (sum == UInt128.Zero)
                return Signed(rounding == RoundDown, Zero);

            return RoundExtended(negative, sum, lowA - 63, false, precision, rounding, ref flags);
        }

        /// <summary>
        ///     a * b (FMUL)
        /// </summary>
        public static Float80 Multiply(Float80 a, Float80 b, ushort controlWord, ref ushort statusWord,
            bool memoryOperandDenormal = false)
        {
            var flags = 0;
            var result = Multiply(a, b, controlWord, memoryOperandDenormal, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 Multiply(Float80 a, Float80 b, ushort controlWord, bool memoryOperandDenormal,
            ref int flags)
        {
            if (Screen(a, b, ref flags, out var nan))
                return nan;

            var ka = a.GetKind();
            var kb = b.GetKind();
            var negative = a.IsNegative ^ b.IsNegative;
            if ((ka == Kind.Infinity && kb == Kind.Zero) || (ka == Kind.Zero && kb == Kind.Infinity))
            {
                flags |= IE;
                return Indefinite;
            }

            flags |= DenormalFlag(ka, kb, memoryOperandDenormal);
            if (ka == Kind.Infinity || kb == Kind.Infinity)
                return Signed(negative, PositiveInfinity);

            if (ka == Kind.Zero || kb == Kind.Zero)
                return Signed(negative, Zero);

            a.Unpack(out var significandA, out var exponentA);
            b.Unpack(out var significandB, out var exponentB);
            return RoundExtended(negative, (UInt128)significandA * significandB, exponentA + exponentB, false,
                Precision(controlWord), Rounding(controlWord), ref flags);
        }

        /// <summary>
        ///     a / b (FDIV, and FDIVR with the operands exchanged)
        /// </summary>
        public static Float80 Divide(Float80 a, Float80 b, ushort controlWord, ref ushort statusWord,
            bool memoryOperandDenormal = false)
        {
            var flags = 0;
            var result = Divide(a, b, controlWord, memoryOperandDenormal, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 Divide(Float80 a, Float80 b, ushort controlWord, bool memoryOperandDenormal,
            ref int flags)
        {
            if (Screen(a, b, ref flags, out var nan))
                return nan;

            var ka = a.GetKind();
            var kb = b.GetKind();
            var negative = a.IsNegative ^ b.IsNegative;
            if ((ka == Kind.Zero && kb == Kind.Zero) || (ka == Kind.Infinity && kb == Kind.Infinity))
            {
                flags |= IE;
                return Indefinite;
            }

            if (ka == Kind.Infinity)
            {
                flags |= DenormalFlag(kb, kb, memoryOperandDenormal);
                return Signed(negative, PositiveInfinity);
            }

            //A finite dividend over zero: ZE, and no DE even for a denormal dividend
            if (kb == Kind.Zero)
            {
                flags |= ZE;
                return Signed(negative, PositiveInfinity);
            }

            flags |= DenormalFlag(ka, kb, memoryOperandDenormal);
            if (kb == Kind.Infinity || ka == Kind.Zero)
                return Signed(negative, Zero);

            //Normalized significands: the quotient of a << 64 by b has 64 or 65 bits; two more bits of the long
            //division and the remainder as a sticky bit round correctly at any precision
            a.Unpack(out var significandA, out var exponentA);
            b.Unpack(out var significandB, out var exponentB);
            var dividend = (UInt128)significandA << 64;
            var quotient = dividend / significandB;
            var remainder = dividend % significandB;
            quotient = (quotient << 2) | ((remainder << 2) / significandB);
            remainder = (remainder << 2) % significandB;
            return RoundExtended(negative, quotient, exponentA - exponentB - 66, remainder != UInt128.Zero,
                Precision(controlWord), Rounding(controlWord), ref flags);
        }

        /// <summary>
        ///     The square root (FSQRT): -0 gives -0, a negative operand is invalid
        /// </summary>
        public static Float80 SquareRoot(Float80 a, ushort controlWord, ref ushort statusWord)
        {
            var flags = 0;
            var result = SquareRoot(a, controlWord, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 SquareRoot(Float80 a, ushort controlWord, ref int flags)
        {
            if (Screen(a, ref flags, out var nan))
                return nan;

            var kind = a.GetKind();
            if (kind == Kind.Zero)
                return a;

            if (a.IsNegative)
            {
                flags |= IE;
                return Indefinite;
            }

            if (kind == Kind.Infinity)
                return a;

            if (kind == Kind.Denormal)
                flags |= DE;

            //An even exponent, then the root of the significand followed by 70 zero bits, two bits at a time: 67 or
            //68 bits of root and the remainder as a sticky bit
            a.Unpack(out var significand, out var exponent);
            var radicand = (UInt128)significand;
            if ((exponent & 1) != 0)
            {
                radicand <<= 1;
                exponent--;
            }

            const int extraPairs = 35;
            var radicandPairs = (BitLength(radicand) + 1) / 2;
            UInt128 root = UInt128.Zero, remainder = UInt128.Zero;
            for (var i = radicandPairs - 1; i >= -extraPairs; i--)
            {
                var pair = i >= 0 ? (radicand >> (2 * i)) & 3u : UInt128.Zero;
                remainder = (remainder << 2) | pair;
                root <<= 1;
                var trial = (root << 1) | UInt128.One;
                if (remainder >= trial)
                {
                    remainder -= trial;
                    root |= UInt128.One;
                }
            }

            return RoundExtended(false, root, exponent / 2 - extraPairs, remainder != UInt128.Zero,
                Precision(controlWord), Rounding(controlWord), ref flags);
        }

        /// <summary>
        ///     Rounds to an integral value by the rounding control (FRNDINT); not affected by the precision control
        /// </summary>
        public static Float80 RoundToInteger(Float80 a, ushort controlWord, ref ushort statusWord)
        {
            var flags = 0;
            var result = RoundToInteger(a, controlWord, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 RoundToInteger(Float80 a, ushort controlWord, ref int flags)
        {
            if (Screen(a, ref flags, out var nan))
                return nan;

            var kind = a.GetKind();
            if (kind is Kind.Zero or Kind.Infinity)
                return a;

            if (kind == Kind.Denormal)
                flags |= DE;

            a.Unpack(out var significand, out var exponent);
            if (exponent >= 0)
                return a;

            var negative = a.IsNegative;
            var integer = RoundShift(significand, -exponent, false, negative, Rounding(controlWord),
                out var inexact, out var roundedUp);
            if (inexact)
                flags |= PE;
            if (roundedUp)
                flags |= C1;

            return FromMagnitude(negative, (ulong)integer);
        }

        /// <summary>
        ///     The partial remainder of a / b (FPREM: ST(0) by ST(1)) with the quotient truncated toward zero; the result
        ///     has the sign of a and is exact. With an exponent difference D below 64 the reduction is complete: C2 is
        ///     cleared and C0, C3, C1 receive bits 2, 1, 0 of the quotient. Otherwise a is reduced by D - N bits only,
        ///     N = 32 + (D mod 32), and C2 is set (run FPREM again) with C0, C3, C1 cleared.
        /// </summary>
        public static Float80 PartialRemainder(Float80 a, Float80 b, ref ushort statusWord)
        {
            var flags = 0;
            var result = PartialRemainder(a, b, ref flags);
            statusWord = (ushort)((statusWord & ~ConditionCodes) | flags);
            return result;
        }

        private static Float80 PartialRemainder(Float80 a, Float80 b, ref int flags)
        {
            if (Screen(a, b, ref flags, out var nan))
                return nan;

            var ka = a.GetKind();
            var kb = b.GetKind();
            if (ka == Kind.Infinity || kb == Kind.Zero)
            {
                flags |= IE;
                return Indefinite;
            }

            flags |= DenormalFlag(ka, kb, false);
            if (ka == Kind.Zero)
                return a;

            //When b is infinite or larger in magnitude, the quotient is zero and a comes back (in its canonical
            //encoding: a pseudo-denormal is normalized)
            var negative = a.IsNegative;
            a.Unpack(out var significandA, out var exponentA);
            UInt128 remainder = significandA;
            var exponent = exponentA;
            if (kb != Kind.Infinity)
            {
                b.Unpack(out var significandB, out var exponentB);
                var difference = exponentA - exponentB;
                if (difference >= 64)
                {
                    var step = 32 + difference % 32;
                    remainder = ((UInt128)significandA << step) % significandB;
                    exponent = exponentA - step;
                    flags |= C2;
                }
                else if (difference >= 0)
                {
                    var dividend = (UInt128)significandA << difference;
                    var quotient = dividend / significandB;
                    remainder = dividend % significandB;
                    exponent = exponentB;
                    if ((quotient & 4u) != 0u)
                        flags |= C0;
                    if ((quotient & 2u) != 0u)
                        flags |= C3;
                    if ((quotient & 1u) != 0u)
                        flags |= C1;
                }
            }

            if (remainder == UInt128.Zero)
                return Signed(negative, Zero);

            return RoundExtended(negative, remainder, exponent, false, 64, RoundNearest, ref flags);
        }

        /// <summary>
        ///     a * 2^trunc(b) (FSCALE: ST(0) scaled by ST(1) truncated toward zero), rounded by the rounding control
        ///     where it overflows or underflows; not affected by the precision control
        /// </summary>
        public static Float80 Scale(Float80 a, Float80 b, ushort controlWord, ref ushort statusWord)
        {
            var flags = 0;
            var result = Scale(a, b, controlWord, ref flags);
            Report(ref statusWord, flags);
            return result;
        }

        private static Float80 Scale(Float80 a, Float80 b, ushort controlWord, ref int flags)
        {
            if (Screen(a, b, ref flags, out var nan))
                return nan;

            var ka = a.GetKind();
            var kb = b.GetKind();
            if (kb == Kind.Infinity)
            {
                //+infinity: zero is invalid, anything else becomes an infinity; -infinity: an infinity is invalid,
                //anything else becomes a zero
                if (b.IsNegative ? ka == Kind.Infinity : ka == Kind.Zero)
                {
                    flags |= IE;
                    return Indefinite;
                }

                flags |= DenormalFlag(ka, ka, false);
                return Signed(a.IsNegative, b.IsNegative ? Zero : PositiveInfinity);
            }

            flags |= DenormalFlag(ka, kb, false);
            if (ka is Kind.Zero or Kind.Infinity)
                return a;

            //trunc(b), limited to a range that overflows or underflows any operand
            var scale = 0;
            if (kb != Kind.Zero)
            {
                b.Unpack(out var significandB, out var exponentB);
                var magnitude = exponentB >= -46 ? 100_000 : exponentB <= -64 ? 0 : (int)(significandB >> -exponentB);
                scale = b.IsNegative ? -magnitude : magnitude;
            }

            a.Unpack(out var significand, out var exponent);
            return RoundExtended(a.IsNegative, significand, exponent + scale, false, 64, Rounding(controlWord),
                ref flags);
        }

        /// <summary>
        ///     Splits a into its significand (the result: the same sign, exponent 0, in [1, 2)) and its unbiased
        ///     exponent (FXTRACT: the exponent goes to ST(1), the significand to ST(0)). Zero gives -infinity as the
        ///     exponent (ZE) and keeps its signed zero; an infinity gives +infinity.
        /// </summary>
        public static Float80 Extract(Float80 a, out Float80 exponent, ref ushort statusWord)
        {
            var flags = 0;
            Float80 significand;
            switch (a.GetKind())
            {
                case Kind.Unsupported:
                case Kind.QuietNaN:
                case Kind.SignalingNaN:
                    Screen(a, ref flags, out significand);
                    exponent = significand;
                    break;
                case Kind.Zero:
                    flags |= ZE;
                    exponent = NegativeInfinity;
                    significand = a;
                    break;
                case Kind.Infinity:
                    exponent = PositiveInfinity;
                    significand = a;
                    break;
                default:
                    if (a.IsDenormal)
                        flags |= DE;
                    a.Unpack(out var normalized, out var low);
                    exponent = FromInt64(low + 63);
                    significand = new Float80((ushort)((a.SignExponent & 0x8000) | Bias), normalized);
                    break;
            }

            Report(ref statusWord, flags);
            return significand;
        }

        /// <summary>
        ///     Compares a with b (FCOM, FTST with b = +0): C3 C2 C0 = 000 when a is greater, 001 when a is less, 100
        ///     when they are equal and 111 when unordered (a NaN or an unsupported operand, IE); C1 is cleared. -0
        ///     equals +0.
        /// </summary>
        public static void Compare(Float80 a, Float80 b, ref ushort statusWord, bool memoryOperandDenormal = false)
        {
            var ka = a.GetKind();
            var kb = b.GetKind();
            int flags;
            if (ka >= Kind.QuietNaN || kb >= Kind.QuietNaN)
            {
                flags = IE | C3 | C2 | C0;
            }
            else
            {
                flags = DenormalFlag(ka, kb, memoryOperandDenormal);
                var order = Order(a, ka, b, kb);
                flags |= order == 0 ? C3 : order < 0 ? C0 : 0;
            }

            statusWord = (ushort)((statusWord & ~ConditionCodes) | flags);
        }

        /// <summary>
        ///     -1, 0 or 1 as a is below, equal to or above b (neither a NaN nor unsupported)
        /// </summary>
        private static int Order(Float80 a, Kind ka, Float80 b, Kind kb)
        {
            var signA = ka == Kind.Zero ? 0 : a.IsNegative ? -1 : 1;
            var signB = kb == Kind.Zero ? 0 : b.IsNegative ? -1 : 1;
            if (signA != signB)
                return signA < signB ? -1 : 1;

            if (signA == 0)
                return 0;

            int magnitude;
            if (ka == Kind.Infinity || kb == Kind.Infinity)
            {
                magnitude = ka == kb ? 0 : ka == Kind.Infinity ? 1 : -1;
            }
            else
            {
                a.Unpack(out var significandA, out var exponentA);
                b.Unpack(out var significandB, out var exponentB);
                magnitude = exponentA != exponentB ? exponentA.CompareTo(exponentB) : significandA.CompareTo(significandB);
            }

            return signA * Math.Sign(magnitude);
        }

        /// <summary>
        ///     The constants of FLD1, FLDZ and of the five transcendental constants. The coprocessor rounds the latter by
        ///     the rounding control (not the precision control): nearest rounds the first 64 bits of pi, log2(e),
        ///     log10(2) and ln(2) up (the bits that follow are above one half), and those of log2(10) down.
        /// </summary>
        public static Float80 Pi(ushort controlWord) => Constant(0x4000, 0xC90F_DAA2_2168_C234, true, controlWord);
        public static Float80 Log2E(ushort controlWord) => Constant(0x3FFF, 0xB8AA_3B29_5C17_F0BB, true, controlWord);
        public static Float80 Log2Ten(ushort controlWord) => Constant(0x4000, 0xD49A_784B_CD1B_8AFE, false, controlWord);
        public static Float80 Log10Two(ushort controlWord) => Constant(0x3FFD, 0x9A20_9A84_FBCF_F798, true, controlWord);
        public static Float80 LnTwo(ushort controlWord) => Constant(0x3FFE, 0xB172_17F7_D1CF_79AB, true, controlWord);

        private static Float80 Constant(ushort signExponent, ulong truncated, bool nearestRoundsUp, ushort controlWord)
        {
            var rounding = Rounding(controlWord);
            var up = rounding == RoundNearest ? nearestRoundsUp : rounding == RoundUp;
            return new Float80(signExponent, up ? truncated + 1 : truncated);
        }

        /// <summary>
        ///     An integer (FILD of 16, 32 or 64 bits): always exact
        /// </summary>
        public static Float80 FromInt64(long value)
        {
            var negative = value < 0;
            return FromMagnitude(negative, negative ? unchecked((ulong)-value) : (ulong)value);
        }

        private static Float80 FromMagnitude(bool negative, ulong magnitude)
        {
            var sign = negative ? 0x8000 : 0;
            if (magnitude == 0)
                return new Float80((ushort)sign, 0);

            var shift = BitOperations.LeadingZeroCount(magnitude);
            return new Float80((ushort)(sign | (Bias + 63 - shift)), magnitude << shift);
        }

        /// <summary>
        ///     The exact value of a single-precision memory operand as an arithmetic instruction (FADD m32 ...) sees it:
        ///     a signalling NaN stays signalling for the operation's own NaN rules, a denormal is normalized
        ///     (denormal: it was a denormal in memory, for the operation's DE)
        /// </summary>
        public static Float80 FromSingle(uint bits, out bool denormal)
        {
            var sign = (ushort)((bits >> 16) & 0x8000);
            var exponent = (int)((bits >> 23) & 0xFF);
            var fraction = bits & 0x7F_FFFF;
            denormal = exponent == 0 && fraction != 0;
            return FromIeee(sign, exponent, fraction, 23, 0xFF, 127);
        }

        /// <summary>
        ///     FLD m32: the exact value; a signalling NaN is made quiet (IE), a denormal raises DE; C1 is cleared
        /// </summary>
        public static Float80 FromSingle(uint bits, ref ushort statusWord) =>
            Load(FromSingle(bits, out var denormal), denormal, ref statusWord);

        /// <summary>
        ///     The exact value of a double-precision memory operand as an arithmetic instruction (FADD m64 ...) sees it
        /// </summary>
        public static Float80 FromDouble(ulong bits, out bool denormal)
        {
            var sign = (ushort)((bits >> 48) & 0x8000);
            var exponent = (int)((bits >> 52) & 0x7FF);
            var fraction = bits & 0xF_FFFF_FFFF_FFFF;
            denormal = exponent == 0 && fraction != 0;
            return FromIeee(sign, exponent, fraction, 52, 0x7FF, 1023);
        }

        /// <summary>
        ///     FLD m64: the exact value; a signalling NaN is made quiet (IE), a denormal raises DE; C1 is cleared
        /// </summary>
        public static Float80 FromDouble(ulong bits, ref ushort statusWord) =>
            Load(FromDouble(bits, out var denormal), denormal, ref statusWord);

        private static Float80 FromIeee(ushort sign, int exponent, ulong fraction, int fractionBits, int maxBiased,
            int bias)
        {
            if (exponent == maxBiased)
                return new Float80((ushort)(sign | 0x7FFF), IntegerBit | (fraction << (63 - fractionBits)));

            if (exponent == 0)
            {
                if (fraction == 0)
                    return new Float80(sign, 0);

                var shift = BitOperations.LeadingZeroCount(fraction);
                return new Float80((ushort)(sign | (63 - shift - fractionBits + 1 - bias + Bias)), fraction << shift);
            }

            return new Float80((ushort)(sign | (exponent - bias + Bias)),
                IntegerBit | (fraction << (63 - fractionBits)));
        }

        private static Float80 Load(Float80 value, bool denormal, ref ushort statusWord)
        {
            var flags = denormal ? DE : 0;
            if (value.IsSignalingNaN)
            {
                flags |= IE;
                value = Quiet(value);
            }

            Report(ref statusWord, flags);
            return value;
        }

        /// <summary>
        ///     The 10-byte memory format (FLD m80): a bit copy, no exception
        /// </summary>
        public static Float80 FromBytes(ReadOnlySpan<byte> bytes) =>
            new(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2)),
                BinaryPrimitives.ReadUInt64LittleEndian(bytes));

        /// <summary>
        ///     The 10-byte memory format (FSTP m80): a bit copy, no exception
        /// </summary>
        public void ToBytes(Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, Significand);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), SignExponent);
        }

        /// <summary>
        ///     FST m32: the bits of the value rounded to single precision by the rounding control (overflow, underflow
        ///     and denormal results as the coprocessor delivers them; a NaN keeps the top of its significand)
        /// </summary>
        public uint ToSingle(ushort controlWord, ref ushort statusWord) =>
            (uint)ToIeee(24, 0xFF, 127, controlWord, ref statusWord);

        /// <summary>
        ///     FST m64: the bits of the value rounded to double precision by the rounding control
        /// </summary>
        public ulong ToDouble(ushort controlWord, ref ushort statusWord) =>
            ToIeee(53, 0x7FF, 1023, controlWord, ref statusWord);

        private ulong ToIeee(int precision, int maxBiased, int bias, ushort controlWord, ref ushort statusWord)
        {
            var fractionBits = precision - 1;
            var signBit = 1UL << (fractionBits + (precision == 24 ? 8 : 11));
            var sign = IsNegative ? signBit : 0;
            var infinity = (ulong)maxBiased << fractionBits;
            var quiet = 1UL << (fractionBits - 1);
            var flags = 0;
            ulong result;
            switch (GetKind())
            {
                case Kind.Unsupported:
                    flags |= IE;
                    result = signBit | infinity | quiet;
                    break;
                case Kind.SignalingNaN:
                case Kind.QuietNaN:
                    if (IsSignalingNaN)
                        flags |= IE;
                    result = sign | infinity | quiet | ((Significand >> (63 - fractionBits)) & ((1UL << fractionBits) - 1));
                    break;
                case Kind.Infinity:
                    result = sign | infinity;
                    break;
                case Kind.Zero:
                    result = sign;
                    break;
                default:
                    Unpack(out var significand, out var exponent);
                    result = RoundIeee(IsNegative, significand, exponent, precision, bias, Rounding(controlWord),
                        ref flags);
                    break;
            }

            Report(ref statusWord, flags);
            return result;
        }

        /// <summary>
        ///     FIST/FISTP m16: rounded by the rounding control; the integer indefinite (0x8000) and IE when the value
        ///     does not fit, is infinite or a NaN
        /// </summary>
        public short ToInt16(ushort controlWord, ref ushort statusWord) =>
            (short)ToInteger(16, controlWord, ref statusWord);

        /// <summary>
        ///     FIST/FISTP m32: the integer indefinite is 0x80000000
        /// </summary>
        public int ToInt32(ushort controlWord, ref ushort statusWord) =>
            (int)ToInteger(32, controlWord, ref statusWord);

        /// <summary>
        ///     FISTP m64: the integer indefinite is 0x8000000000000000
        /// </summary>
        public long ToInt64(ushort controlWord, ref ushort statusWord) =>
            ToInteger(64, controlWord, ref statusWord);

        private long ToInteger(int bits, ushort controlWord, ref ushort statusWord)
        {
            var indefinite = unchecked(-(1L << (bits - 1)));
            var kind = GetKind();
            if (kind == Kind.Zero)
            {
                Report(ref statusWord, 0);
                return 0;
            }

            if (kind is Kind.Unsupported or Kind.QuietNaN or Kind.SignalingNaN or Kind.Infinity)
            {
                Report(ref statusWord, IE);
                return indefinite;
            }

            Unpack(out var significand, out var exponent);
            var negative = IsNegative;
            UInt128 magnitude;
            var inexact = false;
            var roundedUp = false;
            if (exponent > 0)
                magnitude = UInt128.MaxValue; //At least 2^64: out of range
            else
                magnitude = RoundShift(significand, -exponent, false, negative, Rounding(controlWord), out inexact,
                    out roundedUp);

            var limit = (UInt128)1 << (bits - 1);
            if (negative ? magnitude > limit : magnitude >= limit)
            {
                Report(ref statusWord, IE);
                return indefinite;
            }

            Report(ref statusWord, (inexact ? PE : 0) | (roundedUp ? C1 : 0));
            return negative ? unchecked(-(long)(ulong)magnitude) : (long)(ulong)magnitude;
        }

        /// <summary>
        ///     A double converted exactly (the bit pattern of a 64-bit memory operand)
        /// </summary>
        public static implicit operator Float80(double value) =>
            FromDouble((ulong)BitConverter.DoubleToInt64Bits(value), out _);

        /// <summary>
        ///     The value rounded to the nearest double (FST m64 with rounding to nearest)
        /// </summary>
        public static explicit operator double(Float80 value)
        {
            ushort statusWord = 0;
            return BitConverter.Int64BitsToDouble((long)value.ToDouble(0x037F, ref statusWord));
        }

        public bool Equals(Float80 other) => SignExponent == other.SignExponent && Significand == other.Significand;

        public override bool Equals(object obj) => obj is Float80 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SignExponent, Significand);

        /// <summary>
        ///     The bits (sign and exponent, then the significand, in hexadecimal) and the nearest double
        /// </summary>
        public override string ToString() => $"{SignExponent:X4}{Significand:X16} ({(double)this:R})";
    }
}

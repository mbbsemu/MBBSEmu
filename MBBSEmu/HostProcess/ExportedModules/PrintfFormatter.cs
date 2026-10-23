using MBBSEmu.Memory;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Reads printf variable arguments sequentially from module memory, the way Borland's va_arg() does:
    ///     int/char/near pointer = 1 word, long/far pointer = 2 words, double = 8 bytes, long double = 10 bytes
    /// </summary>
    public class PrintfArgumentReader
    {
        private readonly IMemoryCore _memory;
        private readonly ushort _dataSegment;
        private FarPtr _position;

        /// <param name="memory">Module memory</param>
        /// <param name="firstArgument">Address of the first variable argument (stack or va_list)</param>
        /// <param name="dataSegment">Segment used for near (N) pointer arguments</param>
        public PrintfArgumentReader(IMemoryCore memory, FarPtr firstArgument, ushort dataSegment)
        {
            _memory = memory;
            _position = new FarPtr(firstArgument.Segment, firstArgument.Offset);
            _dataSegment = dataSegment;
        }

        public ushort NextWord()
        {
            var value = _memory.GetWord(_position);
            _position += 2;
            return value;
        }

        public uint NextDWord()
        {
            var low = NextWord();
            var high = NextWord();
            return (uint)high << 16 | low;
        }

        public double NextDouble()
        {
            var value = BitConverter.ToDouble(_memory.GetArray(_position, 8));
            _position += 8;
            return value;
        }

        public double NextLongDouble()
        {
            var value = PrintfFormatter.ExtendedToDouble(_memory.GetArray(_position, 10));
            _position += 10;
            return value;
        }

        public FarPtr NextPointer(bool near)
        {
            var offset = NextWord();
            return new FarPtr(near ? _dataSegment : NextWord(), offset);
        }
    }

    /// <summary>
    ///     printf() family formatting, following the Borland C++ (large memory model) Library Reference
    ///
    ///     Format specification: %[flags][width][.prec][F|N|h|l|L]type
    ///         flags:  '-' left justify, '+' always print a sign, ' ' blank before positive values,
    ///                 '#' alternate form, '0' pad with zeros
    ///         width:  minimum field width, or '*' to take it from the argument list (negative = left justify)
    ///         .prec:  d i o u x X -- minimum digits (.0 is the default of 1)
    ///                 e E f       -- digits after the decimal point (default 6)
    ///                 g G         -- significant digits (default 6)
    ///                 s           -- maximum characters printed
    ///                 '*' takes it from the argument list (negative = omitted)
    ///         size:   F far pointer, N near pointer, h short, l long (or double), L long double
    ///         type:   d i o u x X f e E g G c s % n p
    /// </summary>
    public static class PrintfFormatter
    {
        private const int MaxFieldSize = short.MaxValue;

        private enum ArgumentSize
        {
            Default,
            Short,
            Long,
            LongDouble
        }

        private struct Specification
        {
            public EnumPrintfFlags Flags;
            public int Width;
            public int Precision; //-1 when omitted
            public ArgumentSize Size;
            public bool Near;

            public readonly bool Has(EnumPrintfFlags flag) => (Flags & flag) != 0;
        }

        /// <summary>
        ///     Formats the specified printf format string
        /// </summary>
        /// <param name="format">Format string. If it contains a NUL, formatting stops there and the result is NUL terminated</param>
        /// <param name="arguments">Variable arguments</param>
        /// <param name="memory">Module memory, for %s and %n</param>
        /// <param name="segmentExists">Returns true if the segment is mapped in module memory</param>
        /// <param name="invalidPointer">Optional callback when %s references an unmapped segment</param>
        public static byte[] Format(ReadOnlySpan<byte> format, PrintfArgumentReader arguments, IMemoryCore memory,
            Func<ushort, bool> segmentExists, Action<FarPtr> invalidPointer = null)
        {
            var terminator = format.IndexOf((byte)0);
            if (terminator >= 0)
                format = format[..terminator];

            using var output = new MemoryStream(format.Length + 32);
            var i = 0;
            while (i < format.Length)
            {
                if (format[i] != '%')
                {
                    output.WriteByte(format[i++]);
                    continue;
                }

                var specificationStart = i++;
                var spec = new Specification { Precision = -1 };

                //Flags
                for (var parsingFlags = true; parsingFlags && i < format.Length;)
                {
                    switch (format[i])
                    {
                        case (byte)'-':
                            spec.Flags |= EnumPrintfFlags.LeftJustify;
                            i++;
                            break;
                        case (byte)'+':
                            spec.Flags |= EnumPrintfFlags.Signed;
                            i++;
                            break;
                        case (byte)' ':
                            spec.Flags |= EnumPrintfFlags.Space;
                            i++;
                            break;
                        case (byte)'#':
                            spec.Flags |= EnumPrintfFlags.AlternateForm;
                            i++;
                            break;
                        case (byte)'0':
                            spec.Flags |= EnumPrintfFlags.LeftPadZero;
                            i++;
                            break;
                        default:
                            parsingFlags = false;
                            break;
                    }
                }

                //Width
                if (i < format.Length && format[i] == '*')
                {
                    int width = (short)arguments.NextWord();
                    if (width < 0)
                    {
                        spec.Flags |= EnumPrintfFlags.LeftJustify;
                        width = -width;
                    }

                    spec.Width = Math.Min(width, MaxFieldSize);
                    i++;
                }
                else
                {
                    spec.Width = ParseNumber(format, ref i);
                }

                //Precision
                if (i < format.Length && format[i] == '.')
                {
                    i++;
                    if (i < format.Length && format[i] == '*')
                    {
                        int precision = (short)arguments.NextWord();
                        spec.Precision = precision < 0 ? -1 : Math.Min(precision, MaxFieldSize);
                        i++;
                    }
                    else
                    {
                        spec.Precision = ParseNumber(format, ref i);
                    }
                }

                //Input size modifiers
                for (var parsingSize = true; parsingSize && i < format.Length;)
                {
                    switch (format[i])
                    {
                        case (byte)'F':
                            spec.Near = false;
                            i++;
                            break;
                        case (byte)'N':
                            spec.Near = true;
                            i++;
                            break;
                        case (byte)'h':
                            spec.Size = ArgumentSize.Short;
                            i++;
                            break;
                        case (byte)'l':
                            spec.Size = ArgumentSize.Long;
                            i++;
                            break;
                        case (byte)'L':
                            spec.Size = ArgumentSize.LongDouble;
                            i++;
                            break;
                        default:
                            parsingSize = false;
                            break;
                    }
                }

                //Specification ran off the end of the string (e.g. "50%"), output it as-is
                if (i >= format.Length)
                {
                    output.Write(format[specificationStart..]);
                    break;
                }

                var type = (char)format[i++];
                switch (type)
                {
                    case '%':
                        output.WriteByte((byte)'%');
                        break;
                    case 'd':
                    case 'i':
                        {
                            long value = spec.Size == ArgumentSize.Long ? (int)arguments.NextDWord() : (short)arguments.NextWord();
                            WriteInteger(output, (ulong)Math.Abs(value), value < 0, 10, upperCase: false, signed: true, spec);
                            break;
                        }
                    case 'u':
                    case 'o':
                    case 'x':
                    case 'X':
                        {
                            ulong value = spec.Size == ArgumentSize.Long ? arguments.NextDWord() : arguments.NextWord();
                            var radix = type switch { 'u' => 10, 'o' => 8, _ => 16 };
                            WriteInteger(output, value, negative: false, radix, upperCase: type == 'X', signed: false, spec);
                            break;
                        }
                    case 'c':
                        WriteField(output, "", new[] { (byte)arguments.NextWord() }, spec, zeroPadAllowed: false);
                        break;
                    case 's':
                        {
                            var pointer = arguments.NextPointer(spec.Near);
                            WriteField(output, "", ReadString(memory, segmentExists, invalidPointer, pointer, spec.Precision), spec,
                                zeroPadAllowed: false);
                            break;
                        }
                    case 'f':
                    case 'e':
                    case 'E':
                    case 'g':
                    case 'G':
                        {
                            var value = spec.Size == ArgumentSize.LongDouble ? arguments.NextLongDouble() : arguments.NextDouble();
                            WriteFloat(output, value, type, spec);
                            break;
                        }
                    case 'p':
                        {
                            //Large memory model pointers print as SSSS:OOOO, near pointers as OOOO
                            var pointer = arguments.NextPointer(spec.Near);
                            var text = spec.Near ? $"{pointer.Offset:X4}" : $"{pointer.Segment:X4}:{pointer.Offset:X4}";
                            WriteField(output, "", Encoding.ASCII.GetBytes(text), spec, zeroPadAllowed: false);
                            break;
                        }
                    case 'n':
                        {
                            //Stores the number of characters written so far
                            var pointer = arguments.NextPointer(spec.Near);
                            if (pointer.IsNull() || !segmentExists(pointer.Segment))
                                break;

                            if (spec.Size == ArgumentSize.Long)
                                memory.SetDWord(pointer, (uint)output.Length);
                            else
                                memory.SetWord(pointer, (ushort)output.Length);
                            break;
                        }
                    default:
                        //Unknown conversion character, output the specification as-is
                        output.Write(format[specificationStart..i]);
                        break;
                }
            }

            if (terminator >= 0)
                output.WriteByte(0);

            return output.ToArray();
        }

        /// <summary>
        ///     Converts an 80-bit x87 extended precision value (long double) to a double
        /// </summary>
        public static double ExtendedToDouble(ReadOnlySpan<byte> value)
        {
            var mantissa = BitConverter.ToUInt64(value[..8]);
            var signAndExponent = BitConverter.ToUInt16(value.Slice(8, 2));
            var negative = (signAndExponent & 0x8000) != 0;
            var exponent = signAndExponent & 0x7FFF;

            double result;
            if (exponent == 0x7FFF)
                result = (mantissa << 1) == 0 ? double.PositiveInfinity : double.NaN;
            else
                result = Math.ScaleB(mantissa, exponent - 16383 - 63);

            return negative ? -result : result;
        }

        private static int ParseNumber(ReadOnlySpan<byte> format, ref int i)
        {
            var value = 0;
            while (i < format.Length && format[i] >= '0' && format[i] <= '9')
            {
                value = Math.Min(value * 10 + (format[i] - '0'), MaxFieldSize);
                i++;
            }

            return value;
        }

        private static string SignPrefix(bool negative, Specification spec)
        {
            if (negative)
                return "-";

            if (spec.Has(EnumPrintfFlags.Signed))
                return "+";

            return spec.Has(EnumPrintfFlags.Space) ? " " : "";
        }

        private static void WriteInteger(Stream output, ulong magnitude, bool negative, int radix, bool upperCase, bool signed,
            Specification spec)
        {
            var digits = radix switch
            {
                8 => Convert.ToString((long)magnitude, 8),
                16 => magnitude.ToString(upperCase ? "X" : "x", CultureInfo.InvariantCulture),
                _ => magnitude.ToString(CultureInfo.InvariantCulture)
            };

            //Precision is the minimum number of digits; Borland treats .0 the same as the default of 1
            var minimumDigits = Math.Max(spec.Precision, 1);
            if (digits.Length < minimumDigits)
                digits = digits.PadLeft(minimumDigits, '0');

            if (radix == 8 && spec.Has(EnumPrintfFlags.AlternateForm) && digits[0] != '0')
                digits = "0" + digits;

            string prefix;
            if (signed)
                prefix = SignPrefix(negative, spec);
            else if (radix == 16 && spec.Has(EnumPrintfFlags.AlternateForm) && magnitude != 0)
                prefix = upperCase ? "0X" : "0x";
            else
                prefix = "";

            //The 0 flag is ignored when a precision is given for integer conversions
            WriteField(output, prefix, Encoding.ASCII.GetBytes(digits), spec, zeroPadAllowed: spec.Precision < 0);
        }

        private static void WriteFloat(Stream output, double value, char type, Specification spec)
        {
            var negative = double.IsNegative(value);

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                //Borland prints +INF, -INF, +NAN and -NAN
                var special = (negative ? "-" : "+") + (double.IsNaN(value) ? "NAN" : "INF");
                WriteField(output, "", Encoding.ASCII.GetBytes(special), spec, zeroPadAllowed: false);
                return;
            }

            var magnitude = Math.Abs(value);
            var precision = spec.Precision < 0 ? 6 : spec.Precision;
            var alternate = spec.Has(EnumPrintfFlags.AlternateForm);
            var upperCase = char.IsUpper(type);

            var body = char.ToLowerInvariant(type) switch
            {
                'f' => FixedNotation(magnitude, precision, alternate),
                'e' => ExponentNotation(magnitude, precision, upperCase, alternate),
                _ => GeneralNotation(magnitude, precision, upperCase, alternate)
            };

            WriteField(output, SignPrefix(negative, spec), Encoding.ASCII.GetBytes(body), spec, zeroPadAllowed: true);
        }

        private static string FixedNotation(double magnitude, int precision, bool alternate)
        {
            var text = magnitude.ToString("F" + precision, CultureInfo.InvariantCulture);
            return precision == 0 && alternate ? text + "." : text;
        }

        private static string ExponentNotation(double magnitude, int precision, bool upperCase, bool alternate)
        {
            var (mantissa, exponent) = SplitExponent(magnitude, precision);
            if (precision == 0 && alternate)
                mantissa += ".";

            return JoinExponent(mantissa, exponent, upperCase);
        }

        private static string GeneralNotation(double magnitude, int precision, bool upperCase, bool alternate)
        {
            var significantDigits = precision == 0 ? 1 : precision;

            //The exponent decides between %e and %f style, after rounding to the significant digits
            var (mantissa, exponent) = SplitExponent(magnitude, significantDigits - 1);

            if (exponent < -4 || exponent >= significantDigits)
            {
                mantissa = alternate ? EnsureDecimalPoint(mantissa) : TrimFraction(mantissa);
                return JoinExponent(mantissa, exponent, upperCase);
            }

            var text = magnitude.ToString("F" + (significantDigits - 1 - exponent), CultureInfo.InvariantCulture);
            return alternate ? EnsureDecimalPoint(text) : TrimFraction(text);
        }

        /// <summary>
        ///     Rounds to the given number of digits after the decimal point in exponent notation
        /// </summary>
        private static (string mantissa, int exponent) SplitExponent(double magnitude, int precision)
        {
            //.NET formats as "1.234560E+001"
            var text = magnitude.ToString("E" + precision, CultureInfo.InvariantCulture);
            var exponentStart = text.IndexOf('E');
            return (text[..exponentStart],
                int.Parse(text[(exponentStart + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        }

        /// <summary>
        ///     Borland prints at least two exponent digits ("1.5e+01"), where .NET prints three
        /// </summary>
        private static string JoinExponent(string mantissa, int exponent, bool upperCase) =>
            $"{mantissa}{(upperCase ? 'E' : 'e')}{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent):D2}";

        private static string TrimFraction(string number) =>
            number.Contains('.') ? number.TrimEnd('0').TrimEnd('.') : number;

        private static string EnsureDecimalPoint(string number) =>
            number.Contains('.') ? number : number + ".";

        private static byte[] ReadString(IMemoryCore memory, Func<ushort, bool> segmentExists, Action<FarPtr> invalidPointer,
            FarPtr pointer, int maxLength)
        {
            if (pointer.IsNull())
                return Encoding.ASCII.GetBytes("(null)");

            if (!segmentExists(pointer.Segment))
            {
                invalidPointer?.Invoke(pointer);
                return Encoding.ASCII.GetBytes("Invalid Pointer");
            }

            if (maxLength < 0)
                return memory.GetString(pointer, stripNull: true).ToArray();

            //With a precision the string doesn't need to be terminated, so only read up to maxLength bytes
            using var result = new MemoryStream(maxLength);
            for (var n = 0; n < maxLength && pointer.Offset + n <= ushort.MaxValue; n++)
            {
                byte b;
                try
                {
                    b = memory.GetByte(pointer + n);
                }
                catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException)
                {
                    break;
                }

                if (b == 0)
                    break;

                result.WriteByte(b);
            }

            return result.ToArray();
        }

        private static void WriteField(Stream output, string prefix, byte[] body, Specification spec, bool zeroPadAllowed)
        {
            var padding = Math.Max(spec.Width - (prefix.Length + body.Length), 0);
            var prefixBytes = Encoding.ASCII.GetBytes(prefix);

            if (spec.Has(EnumPrintfFlags.LeftJustify))
            {
                output.Write(prefixBytes);
                output.Write(body);
                WriteRepeated(output, (byte)' ', padding);
            }
            else if (zeroPadAllowed && spec.Has(EnumPrintfFlags.LeftPadZero))
            {
                output.Write(prefixBytes);
                WriteRepeated(output, (byte)'0', padding);
                output.Write(body);
            }
            else
            {
                WriteRepeated(output, (byte)' ', padding);
                output.Write(prefixBytes);
                output.Write(body);
            }
        }

        private static void WriteRepeated(Stream output, byte value, int count)
        {
            for (var n = 0; n < count; n++)
                output.WriteByte(value);
        }
    }
}

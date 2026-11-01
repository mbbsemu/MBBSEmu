using FluentAssertions;
using MBBSEmu.CPU;
using MBBSEmu.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    /// <summary>
    ///     Float80 against results of real x87 coprocessors, line by line. Every expected value in the two files was
    ///     produced by a coprocessor; none was computed by this code.
    ///
    ///     borland30_coprocessor.txt.br: 28,923 results of a Borland C++ 3.0 program run on a coprocessor (extended
    ///     precision, rounding to nearest: the run-time library's defaults): the four operations on random 64-bit
    ///     operands and on exact ties, conversions to unsigned long and long (Borland's conversion: FISTP of a 64-bit
    ///     integer with rounding control "chop", the low 32 bits), stores into double and float, unsigned long into
    ///     extended, and a chained expression (an integer times a double, then floats subtracted, multiplied and added)
    ///     with random operands and with constants written in the program.
    ///
    ///     x87_coprocessor_cases.txt.br: 16,059 single instructions, each run from FNINIT under its own control word:
    ///     FADD, FSUB, FSUBR, FMUL, FDIV, FDIVR and FSQRT under every rounding control and precision control
    ///     (24, 53 and 64 bits), with ties, cancellation, overflow, underflow and denormal results; FRNDINT, FPREM,
    ///     FSCALE, FXTRACT, FCOM, FTST, FCHS, FABS; the seven constants under every control word; FLD and FILD of
    ///     every memory size; FST of single and double and FIST of 16, 32 and 64 bits under every rounding control,
    ///     in and out of range; the arithmetic instructions with single and double memory operands; and every pair
    ///     of special operands (zeros, denormals, pseudo-denormals, the largest and smallest normals, infinities,
    ///     quiet and signalling NaNs, the real indefinite, unnormals, pseudo-infinities and pseudo-NaNs). The status
    ///     word recorded is the bits the instruction defines: the exception flags and C1, and C0, C2, C3 for FCOM,
    ///     FTST and FPREM.
    /// </summary>
    public class Float80_Coprocessor_Tests
    {
        private const ushort ExtendedNearest = 0x037F;
        private const ushort ExtendedChop = 0x0F7F;

        private static IEnumerable<string> Lines(string resource)
        {
            var compressed = ResourceManager.GetTestResourceManager().GetResource($"MBBSEmu.Tests.Assets.x87.{resource}").ToArray();
            using var reader = new StreamReader(new BrotliStream(new MemoryStream(compressed), CompressionMode.Decompress), Encoding.ASCII);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length > 0 && line[0] != '#')
                    yield return line;
            }
        }

        private static Float80 Extended(string hex) =>
            new(ushort.Parse(hex.AsSpan(0, 4), NumberStyles.HexNumber), ulong.Parse(hex.AsSpan(4, 16), NumberStyles.HexNumber));

        private static string Hex(Float80 value) => $"{value.SignExponent:X4}{value.Significand:X16}";

        private static uint Hex32(string hex) => uint.Parse(hex, NumberStyles.HexNumber);

        private static ulong Hex64(string hex) => ulong.Parse(hex, NumberStyles.HexNumber);

        [Fact]
        public void Borland30_Coprocessor_Results()
        {
            var failures = new List<string>();
            var counts = new Dictionary<char, int>();
            var header = false;
            foreach (var line in Lines("borland30_coprocessor.txt.br"))
            {
                var f = line.Split(' ');
                if (f[0] == "H")
                {
                    //The run-time library found coprocessor type 3 (an 80387)
                    f[1].Should().Be("3");
                    header = true;
                    continue;
                }

                counts[f[0][0]] = counts.GetValueOrDefault(f[0][0]) + 1;
                ushort statusWord = 0;
                string actual, expected;
                switch (f[0])
                {
                    case "O":
                        {
                            //r = x op y in long double
                            var x = Extended(f[2]);
                            var y = Extended(f[3]);
                            var r = f[1] switch
                            {
                                "A" => Float80.Add(x, y, ExtendedNearest, ref statusWord),
                                "S" => Float80.Subtract(x, y, ExtendedNearest, ref statusWord),
                                "M" => Float80.Multiply(x, y, ExtendedNearest, ref statusWord),
                                _ => Float80.Divide(x, y, ExtendedNearest, ref statusWord)
                            };
                            actual = Hex(r);
                            expected = f[4];
                            break;
                        }
                    case "C":
                        {
                            //(unsigned long)x and (long)x: the low 32 bits of FISTP m64 with rounding control "chop"
                            var low = (uint)Extended(f[1]).ToInt64(ExtendedChop, ref statusWord);
                            actual = $"{low:X8} {low:X8}";
                            expected = $"{f[2]} {f[3]}";
                            break;
                        }
                    case "R":
                        {
                            //x stored into a double and into a float
                            var x = Extended(f[1]);
                            actual = $"{x.ToDouble(ExtendedNearest, ref statusWord):X16} {x.ToSingle(ExtendedNearest, ref statusWord):X8}";
                            expected = $"{f[2]} {f[3]}";
                            break;
                        }
                    case "I":
                        //An unsigned long into the coprocessor
                        actual = Hex(Float80.FromInt64(Hex32(f[1])));
                        expected = f[2];
                        break;
                    case "E":
                        //(unsigned long)((regno * dc * f1 - f2) * f3 - f4 + f5): a double, then single memory operands
                        actual = $"{Expression(Hex32(f[1]), Hex64(f[2]), Hex32(f[3]), Hex32(f[4]), Hex32(f[5]), Hex32(f[6]), Hex32(f[7])):X8}";
                        expected = f[8];
                        break;
                    case "Q":
                        //The same with constants written in the program: 0.03 (a double) and 7, 2000, 6, 300, 11 (floats)
                        actual = $"{Expression(Hex32(f[1]), (ulong)BitConverter.DoubleToInt64Bits(0.03), BitConverter.SingleToUInt32Bits(7f), BitConverter.SingleToUInt32Bits(2000f), BitConverter.SingleToUInt32Bits(6f), BitConverter.SingleToUInt32Bits(300f), BitConverter.SingleToUInt32Bits(11f)):X8}";
                        expected = f[2];
                        break;
                    default:
                        throw new InvalidDataException(line);
                }

                if (actual != expected)
                    failures.Add($"{line} => {actual}");
            }

            header.Should().BeTrue();
            counts.Should().BeEquivalentTo(new Dictionary<char, int> { { 'O', 4448 }, { 'C', 3042 }, { 'R', 2128 }, { 'I', 304 }, { 'E', 3000 }, { 'Q', 16001 } });
            failures.Should().BeEmpty($"{failures.Count} of 28923 results differ from the coprocessor's");
        }

        private static uint Expression(uint regno, ulong dc, uint f1, uint f2, uint f3, uint f4, uint f5)
        {
            ushort statusWord = 0;
            var t = Float80.FromInt64(regno);
            t = Float80.Multiply(t, Float80.FromDouble(dc, out _), ExtendedNearest, ref statusWord);
            t = Float80.Multiply(t, Float80.FromSingle(f1, out _), ExtendedNearest, ref statusWord);
            t = Float80.Subtract(t, Float80.FromSingle(f2, out _), ExtendedNearest, ref statusWord);
            t = Float80.Multiply(t, Float80.FromSingle(f3, out _), ExtendedNearest, ref statusWord);
            t = Float80.Subtract(t, Float80.FromSingle(f4, out _), ExtendedNearest, ref statusWord);
            t = Float80.Add(t, Float80.FromSingle(f5, out _), ExtendedNearest, ref statusWord);
            return (uint)t.ToInt64(ExtendedChop, ref statusWord);
        }

        [Fact]
        public void Coprocessor_Instruction_Cases()
        {
            var failures = new List<string>();
            var count = 0;
            foreach (var line in Lines("x87_coprocessor_cases.txt.br"))
            {
                var f = line.Split(' ');
                var op = f[0];
                var controlWord = ushort.Parse(f[1], NumberStyles.HexNumber);
                var expectedStatus = ushort.Parse(f[^1], NumberStyles.HexNumber);
                ushort statusWord = 0;
                var (inputs, results) = Execute(op, controlWord, f, ref statusWord);

                //The status bits the instruction defines; an invalid FPREM leaves the condition codes undefined
                var mask = op is "COM" or "COMS" or "COMD" or "TST" or "PREM" ? 0x473F : 0x023F;
                if (op == "PREM" && (expectedStatus & 1) != 0)
                    mask = 0x003F;

                var expected = string.Join(' ', f[(2 + inputs)..^1]);
                if (results != expected || (statusWord & mask) != expectedStatus)
                    failures.Add($"{line} => {results} {statusWord & mask:X4}");
                count++;
            }

            count.Should().Be(16059);
            failures.Should().BeEmpty($"{failures.Count} of {count} cases differ from the coprocessor's");
        }

        /// <summary>
        ///     Runs one case line through Float80: the number of input fields and the results as the line writes them
        /// </summary>
        private static (int inputs, string results) Execute(string op, ushort cw, string[] f, ref ushort sw)
        {
            switch (op)
            {
                case "ADD": return (2, Hex(Float80.Add(Extended(f[2]), Extended(f[3]), cw, ref sw)));
                case "SUB": return (2, Hex(Float80.Subtract(Extended(f[2]), Extended(f[3]), cw, ref sw)));
                case "SUBR": return (2, Hex(Float80.Subtract(Extended(f[3]), Extended(f[2]), cw, ref sw)));
                case "MUL": return (2, Hex(Float80.Multiply(Extended(f[2]), Extended(f[3]), cw, ref sw)));
                case "DIV": return (2, Hex(Float80.Divide(Extended(f[2]), Extended(f[3]), cw, ref sw)));
                case "DIVR": return (2, Hex(Float80.Divide(Extended(f[3]), Extended(f[2]), cw, ref sw)));
                case "PREM": return (2, Hex(Float80.PartialRemainder(Extended(f[2]), Extended(f[3]), ref sw)));
                case "SCALE": return (2, Hex(Float80.Scale(Extended(f[2]), Extended(f[3]), cw, ref sw)));
                case "COM":
                    Float80.Compare(Extended(f[2]), Extended(f[3]), ref sw);
                    return (2, string.Empty);
                case "TST":
                    Float80.Compare(Extended(f[2]), Float80.Zero, ref sw);
                    return (1, string.Empty);
                case "SQRT": return (1, Hex(Float80.SquareRoot(Extended(f[2]), cw, ref sw)));
                case "RNDINT": return (1, Hex(Float80.RoundToInteger(Extended(f[2]), cw, ref sw)));
                case "CHS": return (1, Hex(Extended(f[2]).Negate()));
                case "ABS": return (1, Hex(Extended(f[2]).Abs()));
                case "XTRACT":
                    {
                        var significand = Float80.Extract(Extended(f[2]), out var exponent, ref sw);
                        return (1, $"{Hex(significand)} {Hex(exponent)}");
                    }
                case "LD1": return (0, Hex(Float80.One));
                case "LDZ": return (0, Hex(Float80.Zero));
                case "LDPI": return (0, Hex(Float80.Pi(cw)));
                case "LDL2E": return (0, Hex(Float80.Log2E(cw)));
                case "LDL2T": return (0, Hex(Float80.Log2Ten(cw)));
                case "LDLG2": return (0, Hex(Float80.Log10Two(cw)));
                case "LDLN2": return (0, Hex(Float80.LnTwo(cw)));
                case "LDS": return (1, Hex(Float80.FromSingle(Hex32(f[2]), ref sw)));
                case "LDD": return (1, Hex(Float80.FromDouble(Hex64(f[2]), ref sw)));
                case "ILDW": return (1, Hex(Float80.FromInt64((short)ushort.Parse(f[2], NumberStyles.HexNumber))));
                case "ILDD": return (1, Hex(Float80.FromInt64((int)Hex32(f[2]))));
                case "ILDQ": return (1, Hex(Float80.FromInt64((long)Hex64(f[2]))));
                case "STS": return (1, $"{Extended(f[2]).ToSingle(cw, ref sw):X8}");
                case "STD": return (1, $"{Extended(f[2]).ToDouble(cw, ref sw):X16}");
                case "ISTW": return (1, $"{(ushort)Extended(f[2]).ToInt16(cw, ref sw):X4}");
                case "ISTD": return (1, $"{(uint)Extended(f[2]).ToInt32(cw, ref sw):X8}");
                case "ISTQ": return (1, $"{(ulong)Extended(f[2]).ToInt64(cw, ref sw):X16}");
            }

            //An arithmetic instruction or FCOM with a single (S) or double (D) memory operand: the operand as the
            //instruction sees it, and whether it was a denormal there
            var a = Extended(f[2]);
            bool denormal;
            var b = op[^1] == 'S' ? Float80.FromSingle(Hex32(f[3]), out denormal) : Float80.FromDouble(Hex64(f[3]), out denormal);
            switch (op[..^1])
            {
                case "ADD": return (2, Hex(Float80.Add(a, b, cw, ref sw, denormal)));
                case "SUB": return (2, Hex(Float80.Subtract(a, b, cw, ref sw, denormal)));
                case "MUL": return (2, Hex(Float80.Multiply(a, b, cw, ref sw, denormal)));
                case "DIV": return (2, Hex(Float80.Divide(a, b, cw, ref sw, denormal)));
                case "COM":
                    Float80.Compare(a, b, ref sw, denormal);
                    return (2, string.Empty);
                default:
                    throw new InvalidDataException(op);
            }
        }
    }
}

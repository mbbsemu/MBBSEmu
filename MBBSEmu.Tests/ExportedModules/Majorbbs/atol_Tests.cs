using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class atol_Tests : ExportedModuleTestBase
    {
        private const int ATOL_ORDINAL = 77;

        [Theory]
        [InlineData("123", 123)]
        [InlineData("-123", -123)]
        [InlineData("+123", 123)]
        [InlineData("   -123", -123)]
        [InlineData("   +123", 123)]
        [InlineData("   -123    ", -123)]
        [InlineData("   +123    ", 123)]
        [InlineData("0", 0)]
        [InlineData("crap", 0)]
        [InlineData("   crap", 0)]
        [InlineData("2147483647", 2147483647)]
        [InlineData("-2147483648", 2147483648)]
        [InlineData("4000000000", 4000000000)]
        [InlineData("-4000000000", -4000000000)]
        public void ATOLTest(string input, long expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            var inputStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRING", (ushort)(input.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStringPointer, Encoding.ASCII.GetBytes(input));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ATOL_ORDINAL, new List<FarPtr> { inputStringPointer });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        /// <summary>
        ///     Expected values are the 32 bits (DX:AX) returned by the original kernels' ATOL export, executed on
        ///     the same texts (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical results, and so does the
        ///     Borland C++ 3.0-4.52 run-time library's atol): beyond the range of a long the value wraps modulo 2^32.
        /// </summary>
        [Theory]
        [InlineData("2147483648", 0x80000000u)]
        [InlineData("2147483649", 0x80000001u)]
        [InlineData("3000000000", 0xB2D05E00u)]
        [InlineData("4294000000", 0xFFF13D80u)]
        [InlineData("4294967295", 0xFFFFFFFFu)]
        [InlineData("4294967296", 0x00000000u)]
        [InlineData("4294967297", 0x00000001u)]
        [InlineData("5000000000", 0x2A05F200u)]
        [InlineData("9999999999", 0x540BE3FFu)]
        [InlineData("10000000000", 0x540BE400u)]
        [InlineData("12345678901234567890", 0xEB1F0AD2u)]
        [InlineData("18446744073709551615", 0xFFFFFFFFu)]
        [InlineData("-2147483647", 0x80000001u)]
        [InlineData("-2147483649", 0x7FFFFFFFu)]
        [InlineData("-4294967295", 0x00000001u)]
        [InlineData("-4294967296", 0x00000000u)]
        [InlineData("-18446744073709551616", 0x00000000u)]
        [InlineData("+2147483648", 0x80000000u)]
        [InlineData("+4294967295", 0xFFFFFFFFu)]
        [InlineData(" 4294967295", 0xFFFFFFFFu)]
        [InlineData("04294967295", 0xFFFFFFFFu)]
        [InlineData("0004294967295", 0xFFFFFFFFu)]
        [InlineData("4294967295x", 0xFFFFFFFFu)]
        [InlineData("\t\n\v\f\r 42", 0x0000002Au)]
        [InlineData("123abc", 0x0000007Bu)]
        [InlineData("99999999", 0x05F5E0FFu)]
        [InlineData("007", 0x00000007u)]
        [InlineData("-0", 0x00000000u)]
        [InlineData("", 0x00000000u)]
        [InlineData(" ", 0x00000000u)]
        [InlineData("+", 0x00000000u)]
        [InlineData("-", 0x00000000u)]
        [InlineData("+-5", 0x00000000u)]
        [InlineData("-+5", 0x00000000u)]
        [InlineData("--5", 0x00000000u)]
        [InlineData("- 5", 0x00000000u)]
        public void ATOL_MatchesKernel(string input, uint expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            var inputStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRING", (ushort)(input.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStringPointer, Encoding.ASCII.GetBytes(input));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ATOL_ORDINAL, new List<FarPtr> { inputStringPointer });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}

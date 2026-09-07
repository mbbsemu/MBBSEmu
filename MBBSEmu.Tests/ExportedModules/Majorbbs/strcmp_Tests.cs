using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strcmp_Tests : ExportedModuleTestBase
    {
        private const int STRCMP_ORDINAL = 573;

        [Theory]
        [InlineData("", "", 0)]
        [InlineData("", "a", 0xFFFF)]
        [InlineData("a", "", 1)]
        [InlineData("abc", "cbc", 0xFFFF)]
        [InlineData("cbc", "abc", 1)]
        [InlineData("This is great!", "this is great!", 1)]
        [InlineData("This is great!", "This is great!", 0)]
        [InlineData("super", "supe", 1)]
        [InlineData("supe", "super", 0xFFFF)]
        public void compareTest(string a, string b, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(a.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(a));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(b.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(b));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCMP_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void emptyCompareTest()
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCMP_ORDINAL,
                new List<FarPtr> { FarPtr.Empty, FarPtr.Empty });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

        }

        [Fact]
        public void strcmp_DistinguishesDifferentNonAsciiBytes()
        {
            //Reset State
            Reset();

            // 0x81 and 0xA5 are both invalid single-byte UTF-8 sequences, and both get
            // replaced with '?' if decoded via Encoding.ASCII/UTF8 -- they must still
            // compare as different strings.
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", 2);
            mbbsEmuMemoryCore.SetArray(str1Pointer, new byte[] { 0x81, 0x0 });

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", 2);
            mbbsEmuMemoryCore.SetArray(str2Pointer, new byte[] { 0xA5, 0x0 });

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCMP_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void strcmp_EqualNonAsciiBytesCompareEqual()
        {
            //Reset State
            Reset();

            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", 2);
            mbbsEmuMemoryCore.SetArray(str1Pointer, new byte[] { 0xAD, 0x0 });

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", 2);
            mbbsEmuMemoryCore.SetArray(str2Pointer, new byte[] { 0xAD, 0x0 });

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCMP_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}

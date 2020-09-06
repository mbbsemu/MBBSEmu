using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strcmp_Tests : MajorbbsTestBase
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
            ExecuteApiTest(STRCMP_ORDINAL, new List<IntPtr16> {str1Pointer, str2Pointer});

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}

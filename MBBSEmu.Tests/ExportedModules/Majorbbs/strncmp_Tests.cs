using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int STRNCMP_ORDINAL = 581;

        [Theory]
        [InlineData("", "", 32, 0)]
        [InlineData("", "a", 1, 0xFFFF)]
        [InlineData("a", "", 1, 1)]
        [InlineData("abc", "cbc", 3, 0xFFFF)]
        [InlineData("cbc", "Abc", 4, 1)]
        [InlineData("This is great!", "this is great!", 32, 1)]
        [InlineData("This is great!", "This is great!", 32, 0)]
        [InlineData("sUper", "supe", 4, 1)]
        [InlineData("super", "supe", 4, 0)]
        [InlineData("supe", "Super", 5, 0xFFFF)]
        public void strncmp_Test(string a, string b, ushort length, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(a.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(a));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(b.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(b));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRNCMP_ORDINAL,
                new List<ushort>
                {
                    str1Pointer.Offset, 
                    str1Pointer.Segment, 
                    str2Pointer.Offset, 
                    str2Pointer.Segment, 
                    length
                });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}

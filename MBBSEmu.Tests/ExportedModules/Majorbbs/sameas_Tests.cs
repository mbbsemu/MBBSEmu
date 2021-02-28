using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int SAMEAS_ORDINAL = 520;

        [Theory]
        [InlineData("", "", 1)]
        [InlineData("", "a", 0)]
        [InlineData("a", "", 0)]
        [InlineData("abc", "cbc", 0)]
        [InlineData("cbc", "Abc", 0)]
        [InlineData("This is great!", "this is great!", 1)]
        [InlineData("This is great!", "This is great!", 1)]
        [InlineData("super", "supe", 0)]
        [InlineData("supe", "super", 0)]
        public void sameas_Test(string a, string b, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(a.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(a));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(b.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(b));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SAMEAS_ORDINAL, new List<FarPtr> {str1Pointer, str2Pointer});

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}

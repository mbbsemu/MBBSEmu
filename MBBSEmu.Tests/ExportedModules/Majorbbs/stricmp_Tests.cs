using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stricmp_Tests : ExportedModuleTestBase
    {
        private const int STRICMP_ORDINAL = 576;

        [Theory]
        [InlineData("", "", 0)]
        [InlineData("", "a", 0xFFFF)]
        [InlineData("a", "", 1)]
        [InlineData("abc", "cbc", 0xFFFF)]
        [InlineData("cbc", "Abc", 1)]
        [InlineData("This is great!", "this is great!", 0)]
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
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRICMP_ORDINAL, new List<FarPtr> {str1Pointer, str2Pointer});

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void emptyCompareTest()
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRICMP_ORDINAL,
                new List<FarPtr> { FarPtr.Empty, FarPtr.Empty });

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

        }
    }
}

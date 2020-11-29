using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strtol_Tests : ExportedModuleTestBase
    {
        private const int STRTOL_ORDINAL = 586;

        [Theory]
        [InlineData("123456 Hello some text", 6, 10, 123456)]
        [InlineData("2345678 YESSS", 7, 10, 2345678)]
        [InlineData("+2345678 YESSS", 7, 10, 2345678)]
        [InlineData("   2345678 YESSS", 7, 10, 2345678)]
        [InlineData("78 A", 7, 10, 78)]
        [InlineData("0x23CACE A very big number", 1, 16, 2345678)]
        [InlineData("-2345678 A very big number", 1, 10, -2345678)]
        [InlineData("", 0, 10, 0)]
        [InlineData("ThereIsNoNumberHere", 1, 10, 0)]
        public void strtol_Test(string srcString, ushort ushortPtr, ushort numBase, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            var sourceStringSuffixPointer = mbbsEmuMemoryCore.AllocateVariable("SUFFIXPP", (ushort)(srcString.Length));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL, new List<ushort> { sourceStringPointer.Offset, sourceStringPointer.Segment, sourceStringSuffixPointer.Offset, sourceStringSuffixPointer.Segment, numBase });

            var returnedSuffixPointer = mbbsEmuMemoryCore.GetVariablePointer("SUFFIXPP");

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
            //Assert.Equal(sourceStringPointer.Offset + ushortPtr, returnedSuffixPointer.Offset);
        }
    }
}

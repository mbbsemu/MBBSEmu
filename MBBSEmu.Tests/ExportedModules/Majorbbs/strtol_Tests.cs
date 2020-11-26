using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strtol_Tests : ExportedModuleTestBase
    {
        private const int STRTOL_ORDINAL = 586;

        [Theory]
        [InlineData("123456 Hello some text", 7, 10, 123456)]
        [InlineData("2345678 YESSS", 7, 10, 2345678)]
        [InlineData("78 A", 7, 10, 78)]
        [InlineData("0x23CACE A very big number", 1, 16, 2345678)]
        public void strtol_Test(string srcString, ushort ushortPtr, ushort numBase, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));
            var sourceStringSuffixPointer = mbbsEmuMemoryCore.GetPointer(sourceStringPointer.Segment, ushortPtr);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL, new List<ushort> { sourceStringPointer.Offset, sourceStringPointer.Segment, sourceStringSuffixPointer.Offset, sourceStringSuffixPointer.Segment, numBase });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}

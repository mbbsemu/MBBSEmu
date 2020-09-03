using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strncpy_Tests : MajorbbsTestBase
    {
        private const int STRNCPY_ORDINAL = 582;

        // DST buffer is "AAAAAAAA"
        [Theory]
        [InlineData(8, "Test", 4, "TestAAAA")]
        [InlineData(8, "Test", 0, "AAAAAAAA")]
        [InlineData(8, "Test", 1, "TAAAAAAA")]
        [InlineData(8, "Test", 5, "Test")]
        public void strncpy_Test(ushort dstLength, string srcString, ushort copyLength, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", dstLength);
            mbbsEmuMemoryCore.SetArray("DST", Enumerable.Repeat((byte)'A', dstLength).ToArray());

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length+1));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Execute Test
            ExecuteApiTest(STRNCPY_ORDINAL, new List<ushort> { destinationStringPointer.Offset, destinationStringPointer.Segment, sourceStringPointer.Offset, sourceStringPointer.Segment, copyLength });

            //Verify Results
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DST", true)));
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
        }
    }
}

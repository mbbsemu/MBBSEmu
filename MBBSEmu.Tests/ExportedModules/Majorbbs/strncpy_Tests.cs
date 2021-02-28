using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strncpy_Tests : ExportedModuleTestBase
    {
        private const int STRNCPY_ORDINAL = 582;

        // DST buffer is "AAAAAAA"
        [Theory]
        [InlineData(8, "Test", 4, "TestAAA")]
        [InlineData(8, "Test", 0, "AAAAAAA")]
        [InlineData(8, "Test", 1, "TAAAAAA")]
        [InlineData(8, "Test", 5, "Test")]
        public void strncpy_Test(ushort dstLength, string srcString, ushort copyLength, string expected)
        {
            //Reset State
            Reset();

            // Fills destination with all AAAAAAAAAAAAA, up to dstLength - 1 and then a NULL terminator
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", dstLength);
            mbbsEmuMemoryCore.SetArray(destinationStringPointer, Enumerable.Repeat((byte)'A', dstLength).ToArray());
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + dstLength - 1, 0);

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length+1));
            mbbsEmuMemoryCore.SetArray(sourceStringPointer, Encoding.ASCII.GetBytes(srcString));
            mbbsEmuMemoryCore.SetByte(sourceStringPointer + srcString.Length, 0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRNCPY_ORDINAL,
                new List<ushort>
                {
                    destinationStringPointer.Offset,
                    destinationStringPointer.Segment,
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    copyLength
                });

            //Verify Results
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DST", true)));
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
        }
    }
}

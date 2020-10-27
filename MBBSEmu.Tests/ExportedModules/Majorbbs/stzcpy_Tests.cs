using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stzcpy_Tests : ExportedModuleTestBase
    {
        private const int STZCPY_ORDINAL = 589;

        [Theory]
        [InlineData(1, "T", "\0")]
        [InlineData(4, "TestPhrase\0", "Tes\0")]
        [InlineData(8, "Test\0", "Test\0\0\0\0")]
        [InlineData(4, "Test\0", "Tes\0")]
        [InlineData(12, "TestTestTest\0", "TestTestTest\0")]
        public void stzcpy_Test(ushort dstLength, string srcString, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", dstLength);
            //mbbsEmuMemoryCore.SetArray("DST", Enumerable.Repeat((byte) 'A', dstLength).ToArray());

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort) (srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STZCPY_ORDINAL,
                new List<ushort>
                {
                    destinationStringPointer.Offset,
                    destinationStringPointer.Segment,
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    dstLength
                });

            //Verify Results
            //Assert.Equal(expected, Encoding.ASCII.GetArray(mbbsEmuMemoryCore.GetString("DST", false)));
            Assert.Equal(expected, Encoding.ASCII.GetBytes(mbbsEmuMemoryCore.GetArray(destinationStringPointer, dstLength)));
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
        }
    }
}

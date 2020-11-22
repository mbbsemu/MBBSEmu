using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class movmem_Tests : ExportedModuleTestBase
    {
        private const int MOVMEM_ORDINAL = 420;

        [Theory]
        [InlineData(3, new byte[] { 0x4, 0x4, 0x2 })]
        [InlineData(1, new byte[] { 0xE })]
        [InlineData(2, new byte[] { 0xD, 0xA })]
        [InlineData(5, new byte[] { 0xD, 0xA, 0xD, 0xD, 0x2 })]
        [InlineData(0, new byte[] { })]
        public void movmem_Test(ushort moveLength, byte[] expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var srcPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", moveLength);
            mbbsEmuMemoryCore.SetArray("SRC", expected);

            var dstPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)(moveLength + 1));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MOVMEM_ORDINAL,
                new List<ushort>
                {
                    srcPointer.Offset,
                    srcPointer.Segment,
                    dstPointer.Offset,
                    dstPointer.Segment,
                    moveLength
                });

            //Verify Results
            var dstArray = mbbsEmuMemoryCore.GetArray("DST", moveLength);

            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}

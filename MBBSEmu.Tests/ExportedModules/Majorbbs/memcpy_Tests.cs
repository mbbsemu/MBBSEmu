using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class memcpy_Tests : ExportedModuleTestBase
    {
        private const int MEMCPY_ORDINAL = 409;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(64000)]
        public void MEMCPY_Test(ushort copiedLength)
        {
            //Reset State
            Reset();

            byte[] data = Enumerable.Repeat((byte)0x7F, copiedLength).ToArray();

            //Set Argument Values to be Passed In
            var dstPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)(copiedLength + 1));

            var srcPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", copiedLength);
            mbbsEmuMemoryCore.SetArray("SRC", data);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MEMCPY_ORDINAL,
                new List<ushort>
                {
                    dstPointer.Offset, 
                    dstPointer.Segment, 
                    srcPointer.Offset, 
                    srcPointer.Segment, 
                    copiedLength
                });

            //Verify Results
            var dstArray = mbbsEmuMemoryCore.GetArray("DST", copiedLength);

            Assert.Equal(data, dstArray.ToArray());
            // validates last item to be 0x7F
            if (copiedLength > 0)
            {
              Assert.Equal(0x7F, mbbsEmuMemoryCore.GetByte(dstPointer + copiedLength - 1));
            }
            // validates the item AFTER the last item is still 0 (doesn't get overwritten)
            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(dstPointer + copiedLength));

            Assert.Equal(dstPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(dstPointer.Offset, mbbsEmuCpuRegisters.AX);
        }
    }
}

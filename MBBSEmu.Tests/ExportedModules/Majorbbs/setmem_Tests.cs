using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class setmem_Tests : ExportedModuleTestBase
    {
        private const int SETMEM_ORDINAL = 544;

        [Theory]
        [InlineData(new byte[] { 0x4, 0x4 }, 0x3, 2)]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2 }, 0x3, 4)]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2, 0x1, 0x0 }, 0xE, 6)]
        [InlineData(new byte[] { 0x4 }, 0x0, 1)]
        [InlineData(new byte[] { }, 0x0, 0)]
        [InlineData(new byte[] { 0xE, 0xF, 0x2, 0x8 }, 0xA, 4)]
        public void memset_Test(byte[] bufMem, ushort valueToFill, ushort memSize)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In -- Signature: void setmem(char *destination, unsigned nbytes, char value)
            var bufPointer = mbbsEmuMemoryCore.AllocateVariable("SETMEMORY", (ushort)bufMem.Length);
            mbbsEmuMemoryCore.SetArray(bufPointer, bufMem);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment,
                SETMEM_ORDINAL,
                new List<ushort>
                {
                    bufPointer.Offset,
                    bufPointer.Segment,
                    memSize,
                    valueToFill
                });

            //Verify Results
            var expected = new byte[memSize];

            for (var i = 0; i < memSize; i++)
                expected[i] = (byte)valueToFill;

            var dstArray = mbbsEmuMemoryCore.GetArray("SETMEMORY", memSize);

            Assert.Equal(bufPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(bufPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}

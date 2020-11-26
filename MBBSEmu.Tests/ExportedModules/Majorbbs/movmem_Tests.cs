using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class movmem_Tests : ExportedModuleTestBase
    {
        private const int MOVMEM_ORDINAL = 420;

        [Theory]
        [InlineData(new byte[] { 0x4, 0x4, 0x2 })]
        [InlineData(new byte[] { 0xE })]
        [InlineData(new byte[] { 0xD, 0xA })]
        [InlineData(new byte[] { 0xD, 0xA, 0xD, 0xD, 0x2 })]
        [InlineData(new byte[] { })]
        public void movmem_Test(byte[] expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var srcPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort) expected.Length);
            mbbsEmuMemoryCore.SetArray("SRC", expected);

            var dstPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)(expected.Length + 1));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MOVMEM_ORDINAL,
                new List<ushort>
                {
                    srcPointer.Offset,
                    srcPointer.Segment,
                    dstPointer.Offset,
                    dstPointer.Segment,
                    (ushort) expected.Length
                });

            //Verify Results
            var dstArray = mbbsEmuMemoryCore.GetArray("DST", (ushort) expected.Length);

            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expected, dstArray.ToArray());
        }

        [Theory]
        [InlineData(3, 1, new byte[] { 0x54, 0x45, 0x53, 0x54 }, new byte[] { 0x45, 0x53, 0x54, 0x54 })]
        [InlineData(3, 2, new byte[] { 0x54, 0x45, 0x53, 0x54 }, new byte[] { 0x53, 0x54, 0x0, 0x54 })]
        [InlineData(1, 2, new byte[] { 0x40, 0x41, 0x42, 0x41 }, new byte[] { 0x42, 0x41, 0x42, 0x41 })]
        [InlineData(2, 1, new byte[] { 0x56, 0x40, 0x40, 0x58 }, new byte[] { 0x40, 0x40, 0x40, 0x58 })]
        [InlineData(0, 0, new byte[] { }, new byte[] { })]
        public void movmem_samepointer_Test(ushort moveLength, ushort bufferShift, byte[] source, byte[] expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var srcPointer = mbbsEmuMemoryCore.AllocateVariable("POINTER", (ushort)source.Length);
            mbbsEmuMemoryCore.SetArray("POINTER", source);

            var dstPointer = mbbsEmuMemoryCore.GetVariablePointer("POINTER");
            
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MOVMEM_ORDINAL,
                new List<ushort>
                {
                    (ushort) (srcPointer.Offset + bufferShift),
                    srcPointer.Segment,
                    dstPointer.Offset,
                    dstPointer.Segment,
                    moveLength
                });

            //Verify Results
            var dstArray = mbbsEmuMemoryCore.GetArray("POINTER", (ushort)expected.Length);

            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}

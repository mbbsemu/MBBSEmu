using FluentAssertions;
using MBBSEmu.Memory;
using System;
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

            dstArray.ToArray().Should().BeEquivalentTo(data);

            // validates last item to be 0x7F
            if (copiedLength > 0)
            {
              mbbsEmuMemoryCore.GetByte(dstPointer + copiedLength - 1).Should().Be(0x7F);
            }
            // validates the item AFTER the last item is still 0 (doesn't get overwritten)
            mbbsEmuMemoryCore.GetByte(dstPointer + copiedLength).Should().Be(0);

            mbbsEmuCpuRegisters.GetPointer().Should().Be(dstPointer);
        }

        [Fact]
        public void memcpy_overlap()
        {
            var dst = mbbsEmuMemoryCore.Malloc(128);
            mbbsEmuMemoryCore.SetZero(dst, 128);

            var src = dst + 64;
            mbbsEmuMemoryCore.FillArray(src, 64, 0xAA);

            // now we have a block of 128 bytes, 0 for the first 64, 0xAA for the last 64
            // we'll copy into the first quarter (32) bytes in from the last, this should
            // cause memory like  0|0xAA|0xAA|0xAA|
            var cpyDst = dst + 32;
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MEMCPY_ORDINAL,
                new List<ushort>
                {
                    cpyDst.Offset,
                    cpyDst.Segment,
                    src.Offset,
                    src.Segment,
                    64,
                });

            var expectedArray = new byte[128];
            Array.Fill(expectedArray, value: (byte) 0, startIndex: 0, count: 32);
            Array.Fill(expectedArray, value: (byte) 0xAA, startIndex: 32, count: 96);

            mbbsEmuMemoryCore.GetArray(dst, 128).ToArray().Should().BeEquivalentTo(expectedArray);

            mbbsEmuMemoryCore.Free(dst);
        }

        [Fact]
        public void memcpy_writeToExportedModuleAddress()
        {
            var dst = new FarPtr(0xFFFF, 0x1);

            var src = mbbsEmuMemoryCore.AllocateVariable("SRC", 1);

            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MEMCPY_ORDINAL,
                new List<ushort>
                {
                    dst.Offset,
                    dst.Segment,
                    src.Offset,
                    src.Segment,
                    1
                });

            //No Exception, just returns the pointer
            dst.Segment.Should().Be(0xFFFF);
            dst.Offset.Should().Be(0x1);
        }

        [Fact]
        public void memcpy_writeFromExportedModuleAddress()
        {
            var src = new FarPtr(0xFFFF, 0x1);

            var dst = mbbsEmuMemoryCore.AllocateVariable("DST", 1);

            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, MEMCPY_ORDINAL,
                new List<ushort>
                {
                    dst.Offset,
                    dst.Segment,
                    src.Offset,
                    src.Segment,
                    1
                });

            //Verify it just returns the original source as pointer
            mbbsEmuCpuRegisters.GetPointer().Should().Be(dst);

            //Verify Nothing Was Written
            mbbsEmuMemoryCore.GetByte(dst).Should().Be(0);
        }
    }
}

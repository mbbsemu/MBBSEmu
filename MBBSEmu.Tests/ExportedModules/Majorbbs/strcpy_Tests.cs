using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strcpy_Tests : ExportedModuleTestBase
    {
        private const int STRCPY_ORDINAL = 574;

        [Theory]
        [InlineData("TEST1\0", "TEST1\0")]
        [InlineData("TEST1\0TEST2\0", "TEST1\0")]
        [InlineData("\0", "\0")]
        [InlineData("", "\0")]
        public void strcpy_Test(string sourceString, string expectedDestination)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DESTINATION_STRING", 0xFF);
            mbbsEmuMemoryCore.SetArray("DESTINATION_STRING", Encoding.ASCII.GetBytes(new string('X', 0xFF)));

            var sourceStringPointer = FarPtr.Empty;
            if (!string.IsNullOrEmpty(sourceString))
            {
                sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SOURCE_STRING", (ushort)(sourceString.Length + 1));
                mbbsEmuMemoryCore.SetArray("SOURCE_STRING", Encoding.ASCII.GetBytes(sourceString));
            }


            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCPY_ORDINAL, new List<FarPtr> { destinationStringPointer, sourceStringPointer });

            //Verify Results
            Assert.Equal(expectedDestination, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DESTINATION_STRING")));
        }

        [Fact]
        public void strcpy_Truncates_WhenDestinationTooSmall()
        {
            // Reset State
            Reset();

            if (!mbbsEmuProtectedModeMemoryCore.HasSegment(STACK_SEGMENT))
                mbbsEmuProtectedModeMemoryCore.AddSegment(STACK_SEGMENT);

            var destinationStringPointer = new FarPtr(STACK_SEGMENT, 0xFFFC);
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + 0, (byte)'X');
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + 1, (byte)'X');
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + 2, (byte)'X');
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + 3, (byte)'X');

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SOURCE_STRING", 9);
            mbbsEmuMemoryCore.SetArray("SOURCE_STRING", Encoding.ASCII.GetBytes("ABCDEFG\0"));

            ExecuteApiTest(
                HostProcess.ExportedModules.Majorbbs.Segment,
                STRCPY_ORDINAL,
                new List<FarPtr> { destinationStringPointer, sourceStringPointer });

            Assert.Equal(
                Encoding.ASCII.GetBytes("ABC\0"),
                mbbsEmuMemoryCore.GetArray(destinationStringPointer, 4).ToArray());
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
        }

        /// <summary>
        ///     Regression test for the live MajorMUD crash where a module passed a destination
        ///     pointer into a segment that was never allocated. SetArray throws before a single
        ///     byte is written, so strcpy has nothing to truncate -- it must still return dest
        ///     in DX:AX rather than letting the exception escape into the CPU loop.
        ///
        ///     Both offsets matter: a non-zero offset faults inside AsSpan, while offset 0 yields
        ///     an empty span that faults on the indexer instead, raising a different exception.
        /// </summary>
        [Theory]
        [InlineData(0x022A)] // offset != 0 -> AsSpan(offset) throws ArgumentOutOfRangeException
        [InlineData(0x0000)] // offset == 0 -> empty span, indexer throws IndexOutOfRangeException
        public void strcpy_DoesNotThrow_WhenDestinationSegmentUnallocated(int destinationOffset)
        {
            // Reset State
            Reset();

            const ushort UNALLOCATED_SEGMENT = 0x1001;
            Assert.False(mbbsEmuProtectedModeMemoryCore.HasSegment(UNALLOCATED_SEGMENT));

            var destinationStringPointer = new FarPtr(UNALLOCATED_SEGMENT, (ushort)destinationOffset);

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SOURCE_STRING", 9);
            mbbsEmuMemoryCore.SetArray("SOURCE_STRING", Encoding.ASCII.GetBytes("ABCDEFG\0"));

            ExecuteApiTest(
                HostProcess.ExportedModules.Majorbbs.Segment,
                STRCPY_ORDINAL,
                new List<FarPtr> { destinationStringPointer, sourceStringPointer });

            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuProtectedModeMemoryCore.HasSegment(UNALLOCATED_SEGMENT));
        }
    }
}

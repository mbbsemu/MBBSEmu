using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class shift_Tests : ExportedModuleTestBase
    {
        private const int ULONG_R_SHIFT_ORDINAL = 661;
        private const int LONG_R_SHIFT_ORDINAL = 660;
        private const int LONG_L_SHIFT_ORDINAL = 658;

        [Theory]
        [InlineData(123, 5, ((int) 123) >> 5)]
        [InlineData(123, 0, 123)]
        [InlineData(-123, 0, -123)]
        [InlineData(25_021_000, 7, ((int) 25_021_000) >> 7)]
        [InlineData(-25_021_000, 7, ((int) -25_021_000) >> 7)]
        [InlineData(25_021_000, 31, ((int) 25_021_000) >> 31)]
        public void long_shiftRightTest(int value, byte right_shifts, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            mbbsEmuCpuRegisters.AX = (ushort) (((uint) value) & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort) (((uint) value) >> 16);
            mbbsEmuCpuRegisters.CL = right_shifts;
            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment, LONG_R_SHIFT_ORDINAL, new List<ushort> {});

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Theory]
        [InlineData(123, 5, ((uint) 123) >> 5)]
        [InlineData(123, 0, 123)]
        [InlineData(25_021_000, 20, ((uint) 25_021_000) >> 20)]
        [InlineData(0xFE8235B8, 20, ((uint) 0xFE8235B8) >> 20)]
        [InlineData(0xFFFFFFFF, 1, 0x7FFFFFFF)]
        [InlineData(0x7FFFFFFF, 31, 0)]
        public void ulong_shiftRightTest(uint value, byte right_shifts, uint expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            mbbsEmuCpuRegisters.AX = (ushort) (((uint) value) & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort) (((uint) value) >> 16);
            mbbsEmuCpuRegisters.CL = right_shifts;
            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment, ULONG_R_SHIFT_ORDINAL, new List<ushort> {});

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Theory]
        [InlineData(123, 5, ((int) 123) << 5)]
        [InlineData(123, 0, 123)]
        [InlineData(-123, 0, -123)]
        [InlineData(25_021_000, 4, ((int) 25_021_000) << 4)]
        [InlineData(-25_021_000, 4, ((int) -25_021_000) << 4)]
        [InlineData(0x7FFFFFFF, 2, -4)] // overflows, 2 lower bits become 00 = 0xFFFFFFFC
        public void long_shiftLeftTest(int value, byte left_shifts, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            mbbsEmuCpuRegisters.AX = (ushort) (((uint) value) & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort) (((uint) value) >> 16);
            mbbsEmuCpuRegisters.CL = left_shifts;
            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment, LONG_L_SHIFT_ORDINAL, new List<ushort> {});

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}

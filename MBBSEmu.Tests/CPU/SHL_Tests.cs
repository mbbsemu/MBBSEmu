using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class SHL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFFFF, 0xFFFE)]
        public void SHL_AX_CarryFlag_SignFlag(ushort axValue, ushort axExpectedValue)
        {
            Reset();

            //SHL AX, 1
            CreateCodeSegment(new byte[] { 0xD1, 0xE0 });
            mbbsEmuCpuRegisters.AX = axValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x7FFF, 0xFFFE)]
        public void SHL_AX_OverflowFlag_SignFlag(ushort axValue, ushort axExpectedValue)
        {
            Reset();

            //SHL AX, 1
            CreateCodeSegment(new byte[] { 0xD1, 0xE0 });
            mbbsEmuCpuRegisters.AX = axValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}

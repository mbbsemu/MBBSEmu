using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class ADC_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0, 0x0, 0x1)]
        [InlineData(1, 0xFFFF, 0x1)]
        public void ADC_AX_IMM16_CarryFlagSet(ushort adcValue, ushort axStartingValue, ushort axExpectedValue)
        {
            Reset();
            CreateCodeSegment(new byte[]
            {
                //ADC AX, adcValue
                0x15, BitConverter.GetBytes(adcValue)[0], BitConverter.GetBytes(adcValue)[1]
            });
            mbbsEmuCpuRegisters.AX = axStartingValue;
            mbbsEmuCpuRegisters.F.SetFlag(EnumFlags.CF);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(1, 0, 0xFFFF, 0xFFFF, 0x0, 0x0)]
        public void ADD_AX_IMM16_ADC_DX_IMM16(ushort addValue, ushort adcValue, ushort axStartingValue, ushort dxStartingValue, ushort axExpectedValue, ushort dxExpectedValue)
        {
            Reset();
            CreateCodeSegment(new byte[]
            {
                0x05, BitConverter.GetBytes(addValue)[0], BitConverter.GetBytes(addValue)[1],

                //ADC DX, adcValue
                0x81, 0xD2, BitConverter.GetBytes(adcValue)[0], BitConverter.GetBytes(adcValue)[1]
            });
            mbbsEmuCpuRegisters.AX = axStartingValue;
            mbbsEmuCpuRegisters.DX = dxStartingValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(dxExpectedValue, mbbsEmuCpuRegisters.DX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}

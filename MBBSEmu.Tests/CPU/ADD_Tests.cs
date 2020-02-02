using System.Runtime.InteropServices;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class ADD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        public void ADD_AL_IMM8_ClearFlags(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] {0x04, value});

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AL);
            Assert.Equal(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x80)]
        [InlineData(0xFF)]
        public void ADD_AL_IMM8_SignFlag(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x04, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AL);
            Assert.Equal(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x0)]
        public void ADD_AL_IMM8_ZeroFlag(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x04, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFF)]
        public void ADD_AL_IMM8_CarryFlag_ZeroFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 1;
            CreateCodeSegment(new byte[] { 0x04, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFF)]
        public void ADD_AL_IMM8_CarryFlag_OverflowFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;
            CreateCodeSegment(new byte[] { 0x04, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x7F, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x80)]
        public void ADD_AL_IMM8_CarryFlag_ZeroFlag_OverflowFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;
            CreateCodeSegment(new byte[] { 0x04, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        [InlineData(0xFF)]
        [InlineData(0x7FFF)]
        public void ADD_AX_IMM16_ClearFlags(ushort value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x8000)]
        [InlineData(0xFFFF)]
        public void ADD_AX_IMM16_SignFlag(ushort value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_AX_IMM16_CarryFlag_ZeroFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 1;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_AX_IMM16_CarryFlag_SignFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0xFFFF;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value - 1, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_AX_IMM16_CarryFlag_OverflowFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0x8000;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0x7FFF, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x8000)]
        public void ADD_AX_IMM16_CarryFlag_ZeroFlag_OverflowFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x05, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0x8000;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x0, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }


        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        public void ADD_RM8_AL_IMM8_ClearFlags(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AL);
            Assert.Equal(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x80)]
        [InlineData(0xFF)]
        public void ADD_RM8_AL_IMM8_SignFlag(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AL);
            Assert.Equal(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x0)]
        public void ADD_RM8_AL_IMM8_ZeroFlag(byte value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFF)]
        public void ADD_RM8_AL_IMM8_CarryFlag_ZeroFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 1;
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFF)]
        public void ADD_RM8_AL_IMM8_CarryFlag_OverflowFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x7F, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x80)]
        public void ADD_RM8_AL_IMM8_CarryFlag_ZeroFlag_OverflowFlag(byte value)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;
            CreateCodeSegment(new byte[] { 0x80, 0xC0, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AL);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        [InlineData(0xFF)]
        [InlineData(0x7FFF)]
        public void ADD_RM16_AX_IMM16_ClearFlags(ushort value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x8000)]
        [InlineData(0xFFFF)]
        public void ADD_RM16_AX_IMM16_SignFlag(ushort value)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_RM16_AX_IMM16_CarryFlag_ZeroFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 1;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
            Assert.NotEqual(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_RM16_AX_IMM16_CarryFlag_SignFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0xFFFF;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value - 1, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0xFFFF)]
        public void ADD_RM16_AX_IMM16_CarryFlag_OverflowFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0x8000;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0x7FFF, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x8000)]
        public void ADD_RM16_AX_IMM16_CarryFlag_ZeroFlag_OverflowFlag(ushort value)
        {
            //Setup
            Reset();
            CreateCodeSegment(new byte[] { 0x81, 0xC0, (byte)(value & 0x00FF), (byte)(value >> 8) });
            mbbsEmuCpuRegisters.AX = 0x8000;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x0, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        [InlineData(0xFF)]
        [InlineData(0x7FFF)]
        public void ADD_RM16_AX_M16_ClearFlags(ushort value)
        {
            Reset();
            CreateDataSegment(new byte[] {(byte)(value & 0x00FF), (byte)(value >> 8)}, 2);
            CreateCodeSegment(new byte[] { 0x03, 0x47, 0x00 });
            mbbsEmuCpuRegisters.DS = 2;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x1)]
        [InlineData(0x7F)]
        [InlineData(0xFF)]
        [InlineData(0x7FFF)]
        public void ADD_RM16_AX_M16_Offset_ClearFlags(ushort value)
        {
            Reset();
            CreateDataSegment(new byte[] { (byte)(value & 0x00FF), (byte)(value >> 8) }, 2);
            CreateCodeSegment(new byte[] { 0x03, 0x47, 0xFF });
            mbbsEmuCpuRegisters.BX = 1;
            mbbsEmuCpuRegisters.DS = 2;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(value, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}

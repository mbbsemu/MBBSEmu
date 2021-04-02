using FluentAssertions;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class Registers_Tests : CpuTestBase
    {
        [Fact]
        public void Regs_AX()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.EAX = 0x12345678;

          regs.AX.Should().Be(0x5678);
          regs.AH.Should().Be(0x56);
          regs.AL.Should().Be(0x78);

          regs.AL = 0x11;
          regs.EAX.Should().Be(0x12345611);
          regs.AX.Should().Be(0x5611);

          regs.EBX.Should().Be(0);
        }

        [Fact]
        public void Regs_BX()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.EBX = 0x12345678;

          regs.BX.Should().Be(0x5678);
          regs.BH.Should().Be(0x56);
          regs.BL.Should().Be(0x78);

          regs.BL = 0x11;
          regs.EBX.Should().Be(0x12345611);
          regs.BX.Should().Be(0x5611);

          regs.EAX.Should().Be(0);
          regs.ECX.Should().Be(0);
        }

        [Fact]
        public void Regs_CX()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.ECX = 0x12345678;

          regs.CX.Should().Be(0x5678);
          regs.CH.Should().Be(0x56);
          regs.CL.Should().Be(0x78);

          regs.CL = 0x11;
          regs.ECX.Should().Be(0x12345611);
          regs.CX.Should().Be(0x5611);

          regs.EBX.Should().Be(0);
          regs.EDX.Should().Be(0);
        }

        [Fact]
        public void Regs_DX()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.EDX = 0x12345678;

          regs.DX.Should().Be(0x5678);
          regs.DH.Should().Be(0x56);
          regs.DL.Should().Be(0x78);

          regs.DL = 0x11;
          regs.EDX.Should().Be(0x12345611);
          regs.DX.Should().Be(0x5611);

          regs.ECX.Should().Be(0);
          regs.ESP.Should().Be(0);
        }

        [Fact]
        public void Regs_SP()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.ESP = 0x12345678;
          regs.SP.Should().Be(0x5678);

          regs.SP = 0x1111;
          regs.ESP.Should().Be(0x12341111);

          regs.EDX.Should().Be(0);
          regs.EBP.Should().Be(0);
        }

        [Fact]
        public void Regs_BP()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.EBP = 0x12345678;
          regs.BP.Should().Be(0x5678);

          regs.BP = 0x1111;
          regs.EBP.Should().Be(0x12341111);

          regs.ESP.Should().Be(0);
          regs.ESI.Should().Be(0);
        }

        [Fact]
        public void Regs_SI()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.ESI = 0x12345678;
          regs.SI.Should().Be(0x5678);

          regs.SI = 0x1111;
          regs.ESI.Should().Be(0x12341111);

          regs.EBP.Should().Be(0);
          regs.EDI.Should().Be(0);
        }

        [Fact]
        public void Regs_DI()
        {
          CpuRegistersStruct regs = CpuRegistersStruct.Create();
          regs.EDI = 0x12345678;
          regs.DI.Should().Be(0x5678);

          regs.DI = 0x1111;
          regs.EDI.Should().Be(0x12341111);

          regs.ESI.Should().Be(0);
        }
    }
}

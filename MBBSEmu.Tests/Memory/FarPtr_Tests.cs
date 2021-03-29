using FluentAssertions;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
  public class FarPtr_Tests : TestBase
  {
    [Fact]
    public void Regs_AX()
    {
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
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
      MBBSEmu.CPU.FastCpuRegisters regs = MBBSEmu.CPU.FastCpuRegisters.Create();
      regs.EDI = 0x12345678;
      regs.DI.Should().Be(0x5678);

      regs.DI = 0x1111;
      regs.EDI.Should().Be(0x12341111);

      regs.ESI.Should().Be(0);
      //regs.EDI.Should().Be(0);
    }

    [Fact]
    public void ConstructsFromArray()
    {
      var farPtr = new FarPtr((new byte[] {0x10,0x20,0x30,0x40}));
      farPtr.Segment.Should().Be(0x4030);
      farPtr.Offset.Should().Be(0x2010);
    }

    [Fact]
    public void ToArray()
    {
      var farPtr = new FarPtr((new byte[] {0x10,0x20,0x30,0x40}));
      farPtr.Data.Should().ContainInOrder(new[] { 0x10, 0x20, 0x30, 0x40 });
    }

    [Fact]
    public void IsNull()
    {
      FarPtr.Empty.IsNull().Should().BeTrue();

      new FarPtr(0, 0).IsNull().Should().BeTrue();

      new FarPtr((new byte[] {0,0,0,0})).IsNull().Should().BeTrue();

      new FarPtr(0, 1).IsNull().Should().BeFalse();
      new FarPtr(1, 0).IsNull().Should().BeFalse();
    }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(0,1,1)]
    [InlineData(1,1,0x00010001)]
    [InlineData(0xFFFF, 0xFFFF, 0xFFFFFFFF)]
    public void ToInt32(ushort segment, ushort offset, uint expected)
    {
      var farPtr = new FarPtr(segment, offset);

      farPtr.ToUInt32().Should().Be(expected);
      farPtr.GetHashCode().Should().Be((int)expected);
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(32, 16, true)]
    [InlineData(31, 16, false)]
    [InlineData(33, 16, false)]
    public void Alignment(ushort offset, ushort alignment, bool expectedAlignment)
    {
      new FarPtr(0, offset).IsAligned(alignment).Should().Be(expectedAlignment);
    }

    [Theory]
    [InlineData(0,0,0,0,true)]
    [InlineData(0x10,0x20,0x10,0x20,true)]
    [InlineData(0xFFFF,0xFFFF,0xFFFF,0xFFFF,true)]
    [InlineData(0,0,0,1,false)]
    [InlineData(0,0,1,0,false)]
    [InlineData(0,1,0,0,false)]
    [InlineData(1,0,0,0,false)]
    [InlineData(0x10,0x20,0x10,0xFF,false)]
    public void Equality(ushort leftSegment, ushort leftOffset, ushort rightSegment, ushort rightOffset, bool expectedEquality)
    {
      var left = new FarPtr(leftSegment, leftOffset);
      var right = new FarPtr(rightSegment, rightOffset);

      left.Equals(right).Should().Be(expectedEquality);
      right.Equals(left).Should().Be(expectedEquality);
      (left == right).Should().Be(expectedEquality);
      (right == left).Should().Be(expectedEquality);
      (left != right).Should().Be(!expectedEquality);
      (right != left).Should().Be(!expectedEquality);
    }

    [Fact]
    public void EqualityNull()
    {
      FarPtr.Empty.Equals(null).Should().BeFalse();
      (FarPtr.Empty == null).Should().BeFalse();
      (null == FarPtr.Empty).Should().BeFalse();

      (FarPtr.Empty != null).Should().BeTrue();
      (null != FarPtr.Empty).Should().BeTrue();

      FarPtr left = null;
      FarPtr right = null;

      (left == right).Should().BeTrue();
      (left != right).Should().BeFalse();
    }

    [Theory]
    [InlineData(50, 50, 45, 95)]
    [InlineData(50, 0x7FFF, 0x8000, 0xFFFF)]
    [InlineData(50, 0xFFFF, 2, 1)]
    public void AdditionUShort(ushort leftSegment, ushort leftOffset, ushort adder, ushort expectedOffset)
    {
      (new FarPtr(leftSegment, leftOffset) + adder).Should().Be(new FarPtr(leftSegment, expectedOffset));
    }

    [Theory]
    [InlineData(50, 50, 45, 95)]
    [InlineData(50, 0x7FFF, 0x8000, 0xFFFF)]
    [InlineData(50, 0xFFFF, 2, 1)]
    [InlineData(50, 50, -10, 40)]
    public void AdditionInt(ushort leftSegment, ushort leftOffset, int adder, ushort expectedOffset)
    {
      (new FarPtr(leftSegment, leftOffset) + adder).Should().Be(new FarPtr(leftSegment, expectedOffset));
    }

    [Theory]
    [InlineData(50, 50, 45, 5)]
    [InlineData(50, 0x7FFF, 0x7FFF, 0)]
    [InlineData(50, 1, 2, 0xFFFF)]
    public void SubtractionUShort(ushort leftSegment, ushort leftOffset, ushort subtrahend, ushort expectedOffset)
    {
      (new FarPtr(leftSegment, leftOffset) - subtrahend).Should().Be(new FarPtr(leftSegment, expectedOffset));
    }

    [Theory]
    [InlineData(50, 50, 45, 5)]
    [InlineData(50, 0x7FFF, 0x7FFF, 0)]
    [InlineData(50, 1, 2, 0xFFFF)]
    [InlineData(50, 50, -2, 52)]
    public void SubtractionInt(ushort leftSegment, ushort leftOffset, int subtrahend, ushort expectedOffset)
    {
      (new FarPtr(leftSegment, leftOffset) - subtrahend).Should().Be(new FarPtr(leftSegment, expectedOffset));
    }

    [Fact]
    public void PreUnaryAddition()
    {
      var farPtr = new FarPtr(50, 0xFFFE);
      (++farPtr).Should().Be(new FarPtr(50, 0xFFFF));
      (++farPtr).Should().Be(new FarPtr(50, 0));
      (++farPtr).Should().Be(new FarPtr(50, 1));
    }

    [Fact]
    public void PostUnaryAddition()
    {
      var farPtr = new FarPtr(50, 0xFFFE);
      (farPtr++).Should().Be(new FarPtr(50, 0xFFFE));
      (farPtr++).Should().Be(new FarPtr(50, 0xFFFF));
      (farPtr++).Should().Be(new FarPtr(50, 0));
    }

    [Fact]
    public void PreUnarySubtraction()
    {
      var farPtr = new FarPtr(50, 1);
      (--farPtr).Should().Be(new FarPtr(50, 0));
      (--farPtr).Should().Be(new FarPtr(50, 0xFFFF));
      (--farPtr).Should().Be(new FarPtr(50, 0xFFFE));
    }

    [Fact]
    public void PostUnarySubtraction()
    {
      var farPtr = new FarPtr(50, 1);
      (farPtr--).Should().Be(new FarPtr(50, 1));
      (farPtr--).Should().Be(new FarPtr(50, 0));
      (farPtr--).Should().Be(new FarPtr(50, 0xFFFF));
    }

    [Theory]
    [InlineData(50, 50, 50, 0, true)]
    [InlineData(50, 50, 0, 50, true)]
    public void GreaterOrLess(ushort leftSegment, ushort leftOffset, ushort rightSegment, ushort rightOffset, bool greater)
    {
      var left = new FarPtr(leftSegment, leftOffset);
      var right = new FarPtr(rightSegment, rightOffset);

      (left > right).Should().Be(greater);
      (left >= right).Should().Be(greater);
      (right > left).Should().Be(!greater);
      (right >= left).Should().Be(!greater);

      (left < right).Should().Be(!greater);
      (left <= right).Should().Be(!greater);
      (right < left).Should().Be(greater);
      (right <= left).Should().Be(greater);
    }

    [Fact]
    public void GreaterOrLessEqualPointers()
    {
      (FarPtr.Empty > FarPtr.Empty).Should().BeFalse();
      (FarPtr.Empty < FarPtr.Empty).Should().BeFalse();

      (FarPtr.Empty >= FarPtr.Empty).Should().BeTrue();
      (FarPtr.Empty <= FarPtr.Empty).Should().BeTrue();
    }
  }
}

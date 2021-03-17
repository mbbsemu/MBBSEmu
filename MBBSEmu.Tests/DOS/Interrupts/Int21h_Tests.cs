using FluentAssertions;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using NLog;
using System.Collections.Generic;
using System;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
  public class Int21h_Tests : TestBase
  {
    private readonly CpuRegisters _registers = new CpuRegisters();
    private readonly FakeClock _fakeClock = new FakeClock();
    private readonly ServiceResolver _serviceResolver;
    private readonly IMemoryCore _memory;
    private readonly Int21h _int21;

    public Int21h_Tests()
    {
        _serviceResolver = new ServiceResolver(_fakeClock);
        _memory = new RealModeMemoryCore(_serviceResolver.GetService<ILogger>());
        _int21 = new Int21h(_registers, _memory, _fakeClock, _serviceResolver.GetService<ILogger>());
    }

    [Fact]
    public void SetHandleCount_0x67()
    {
      _registers.AH = 0x67;
      _registers.BX = 255;
      _registers.F.SetFlag((ushort)EnumFlags.CF);

      _int21.Handle();

      _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().Be(false);
    }

    [Fact]
    public void GetDefaultDiskNumber_0x19()
    {
      _registers.AL = 0;
      _registers.AH = 0x19;

      _int21.Handle();

      _registers.AL.Should().Be(2);
    }
  }
}

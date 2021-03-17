using FluentAssertions;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using NLog;
using System.Collections.Generic;
using System.IO;
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
    private readonly MemoryStream _consoleOutput = new MemoryStream();
    private readonly MemoryStream _consoleInput = new MemoryStream();
    private readonly Int21h _int21;

    public Int21h_Tests()
    {
        var streamWriter = new StreamWriter(_consoleOutput) { AutoFlush = true };

        _serviceResolver = new ServiceResolver(_fakeClock);
        _memory = new RealModeMemoryCore(_serviceResolver.GetService<ILogger>());
        _int21 = new Int21h(_registers, _memory, _fakeClock, _serviceResolver.GetService<ILogger>(), new StreamReader(_consoleInput), streamWriter, streamWriter);
    }

    [Fact]
    public void KeyboardInput_0x01()
    {
      _registers.AH = 0x01;
      _registers.AL = 0;

      _consoleInput.WriteByte((byte)'a');
      _consoleInput.WriteByte((byte)'b');
      _consoleInput.Seek(0, SeekOrigin.Begin);

      _int21.Handle();
      _registers.AL.Should().Be((byte)'a');

      _int21.Handle();
      _registers.AL.Should().Be((byte)'b');

      _consoleOutput.Seek(0, SeekOrigin.Begin);
      _consoleOutput.ReadByte().Should().Be((byte)'a');
      _consoleOutput.ReadByte().Should().Be((byte)'b');
      _consoleOutput.ReadByte().Should().Be(-1);
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

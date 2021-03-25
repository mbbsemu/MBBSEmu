using System.Collections.Concurrent;
using FluentAssertions;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.DOS;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Extensions;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using Xunit;

namespace MBBSEmu.Tests.DOS.Interrupts
{
    public class Int21h_Tests : TestBase
    {
        private readonly CpuRegisters _registers = new();
        private readonly FakeClock _fakeClock = new FakeClock();
        private readonly IMemoryCore _memory;
        private readonly BlockingCollection<byte> _consoleInput = new(new ConcurrentQueue<byte>());
        private readonly BlockingCollection<byte[]> _consoleOutput = new(new ConcurrentQueue<byte[]>());
        private readonly Int21h _int21;

        public Int21h_Tests()
        {
            var serviceResolver = new ServiceResolver(_fakeClock);
            _memory = new RealModeMemoryCore(serviceResolver.GetService<ILogger>());
            _int21 = new Int21h(_registers, _memory, _fakeClock, serviceResolver.GetService<ILogger>(), serviceResolver.GetService<IFileUtility>(), _consoleInput, _consoleOutput, null);
        }

        [Fact]
        public void KeyboardInput_0x01()
        {
            _registers.AH = 0x01;
            _registers.AL = 0;

            _consoleInput.Add((byte)'a');
            _consoleInput.Add((byte)'b');

            _int21.Handle();
            _registers.AL.Should().Be((byte)'a');

            _int21.Handle();
            _registers.AL.Should().Be((byte)'b');

            _consoleOutput.Take().Should().Contain(new[] { (byte)'a' });
            _consoleOutput.Take().Should().Contain(new[] { (byte)'b' });
        }

        [Fact]
        public void SetHandleCount_0x67()
        {
            _registers.AH = 0x67;
            _registers.BX = 255;
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
        }

        [Fact]
        public void GetDefaultDiskNumber_0x19()
        {
            _registers.AL = 0;
            _registers.AH = 0x19;

            _int21.Handle();

            _registers.AL.Should().Be(2);
        }

        [Fact]
        public void AllocateMemory_0x48()
        {
            _registers.AL = 0;
            _registers.AH = 0x48;
            _registers.BX = 2;
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be(RealModeMemoryCore.HEAP_BASE_SEGMENT);

            _registers.AL = 0;
            _registers.AH = 0x48;

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be(RealModeMemoryCore.HEAP_BASE_SEGMENT + 2);
        }

        [Fact]
        public void AllocateMemory_0x48_TooMuch()
        {
            _registers.AL = 0;
            _registers.AH = 0x48;
            _registers.BX = 0xFFFF;
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeTrue();
            _registers.AX.Should().Be((ushort)DOSErrorCode.INSUFFICIENT_MEMORY);
        }

        [Fact]
        public void FreeMemory_0x49()
        {
            _registers.AL = 0;
            _registers.AH = 0x48;
            _registers.BX = 2;
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be(RealModeMemoryCore.HEAP_BASE_SEGMENT);

            _registers.AL = 0;
            _registers.AH = 0x49;
            _registers.ES = RealModeMemoryCore.HEAP_BASE_SEGMENT;
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();

            _memory.Malloc(0).Should().Be(new FarPtr(RealModeMemoryCore.HEAP_BASE_SEGMENT, 0));
        }

        [Fact]
        public void GetDefaultAllocationStrategy_0x58()
        {
            _registers.AL = 0;
            _registers.AH = 0x58;

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be((ushort)Int21h.AllocationStrategy.BEST_FIT);
        }

        [Theory]
        [InlineData((byte)Int21h.AllocationStrategy.BEST_FIT, (byte)Int21h.AllocationStrategy.BEST_FIT)]
        [InlineData((byte)Int21h.AllocationStrategy.FIRST_FIT, (byte)Int21h.AllocationStrategy.FIRST_FIT)]
        [InlineData((byte)Int21h.AllocationStrategy.LAST_FIT, (byte)Int21h.AllocationStrategy.LAST_FIT)]
        [InlineData((byte)3, (byte)Int21h.AllocationStrategy.LAST_FIT)]
        [InlineData((byte)0xFF, (byte)Int21h.AllocationStrategy.LAST_FIT)]
        public void SetDefaultAllocationStrategy_0x58(byte allocationStrategy, byte expectedStrategy)
        {
            _registers.AL = 1;
            _registers.AH = 0x58;
            _registers.BL = allocationStrategy;

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be(expectedStrategy);

            _registers.AX = _registers.BX = 0;
            _registers.AH = 0x58;

            _int21.Handle();
            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeFalse();
            _registers.AX.Should().Be(expectedStrategy);
        }

        [Fact]
        public void GetDefaultAllocationStrategyBadCommand_0x58()
        {
            _registers.AL = 2;
            _registers.AH = 0x58;

            _int21.Handle();

            _registers.F.IsFlagSet((ushort)EnumFlags.CF).Should().BeTrue();
            _registers.AX.Should().Be((ushort)DOSErrorCode.UNKNOWN_COMMAND);
        }
    }
}

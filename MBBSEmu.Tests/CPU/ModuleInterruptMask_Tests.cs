using Iced.Intel;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class ModuleInterruptMask_Tests : CpuTestBase
    {
        private readonly QueueTarget _messages = new();

        private ExecutionUnit CreateExecutionUnit()
        {
            if (!mbbsEmuProtectedModeMemoryCore.HasSegment(0))
                mbbsEmuProtectedModeMemoryCore.AddSegment(0);

            return new ExecutionUnit(mbbsEmuMemoryCore, fakeClock, FileUtility.CreateForTest(),
                new Dictionary<ushort, IExportedModule>(), new MessageLogger(_messages), "/tmp");
        }

        [Fact]
        public void MaskCanBeSavedBlockedAndRestored()
        {
            Reset();
            var instructions = new Assembler(16);
            instructions.mov(al, 0x5a);
            instructions.@out(0x21, al);
            instructions.cli();
            instructions.@in(al, 0x21);
            instructions.mov(ah, al);
            instructions.mov(al, 0xff);
            instructions.@out(0x21, al);
            instructions.mov(al, ah);
            instructions.@out(0x21, al);
            instructions.sti();
            instructions.xor(ax, ax);
            instructions.@in(al, 0x21);
            instructions.hlt();
            CreateCodeSegment(instructions);
            using var unit = CreateExecutionUnit();

            var registers = unit.Execute(new FarPtr(1, 0), ushort.MaxValue, bypassState: true);

            Assert.Equal(0x5a, registers.AL);
            Assert.Empty(_messages.DequeueAll());
        }

        [Fact]
        public void MaskSurvivesRegisterResetBetweenCallbacks()
        {
            Reset();
            var instructions = new Assembler(16);
            instructions.@in(al, 0x21);
            instructions.mov(ah, al);
            instructions.mov(al, 0x36);
            instructions.@out(0x21, al);
            instructions.hlt();
            CreateCodeSegment(instructions);
            using var unit = CreateExecutionUnit();

            Assert.Equal(0xff, unit.Execute(new FarPtr(1, 0), ushort.MaxValue, bypassState: true).AH);
            Assert.Equal(0x36, unit.Execute(new FarPtr(1, 0), ushort.MaxValue, bypassState: true).AH);
            using var other = CreateExecutionUnit();
            Assert.Equal(0xff, other.Execute(new FarPtr(1, 0), ushort.MaxValue, bypassState: true).AH);
            Assert.Empty(_messages.DequeueAll());
        }

        [Fact]
        public void OtherUnimplementedPortsStillWarn()
        {
            Reset();
            var instructions = new Assembler(16);
            instructions.@in(al, 0x22);
            instructions.hlt();
            CreateCodeSegment(instructions);
            using var unit = CreateExecutionUnit();

            unit.Execute(new FarPtr(1, 0), ushort.MaxValue, bypassState: true);

            Assert.Contains(_messages.DequeueAll(), entry =>
                entry[3] is string message && message.Contains("unimplemented IO Port: 34"));
        }
    }
}

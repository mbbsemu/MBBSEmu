using FluentAssertions;
using System.Collections.Generic;
using System;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class farmalloc_Tests : ExportedModuleTestBase
    {
        private const int FARMALLOC_ORDINAL = 203;
        private const int FARFREE_ORDINAL = 202;

        [Fact]
        public void FARMALLOC_AllocatesTooMuch()
        {
            //Reset State
            Reset();

            Action action = () => ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARMALLOC_ORDINAL, new List<ushort> { 10, 10 });
            action.Should().Throw<OutOfMemoryException>();
        }

        [Fact]
        public void FARMALLOC_HappyPath()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARMALLOC_ORDINAL, new List<ushort> { 256, 0 });

            mbbsEmuCpuRegisters.GetPointer().Should().NotBe(FarPtr.Empty);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARFREE_ORDINAL, new List<FarPtr> { mbbsEmuCpuRegisters.GetPointer() });
        }
    }
}

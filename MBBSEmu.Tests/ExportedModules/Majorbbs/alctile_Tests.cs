using System.Collections.Generic;
using System;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class alctile_Tests : ExportedModuleTestBase
    {
        private const int ALCTILE_ORDINAL = 832;
        private const int PTRTILE_ORDINAL = 833;

        [Fact]
        public void acltile_Test()
        {
            //Reset State
            Reset();

            ushort quantity = 256;
            ushort size = 1024;

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCTILE_ORDINAL, new List<ushort> { quantity, size });

            Assert.NotEqual(0, mbbsEmuCpuRegisters.DX);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            var allocatedPointer = mbbsEmuCpuRegisters.GetPointer();
            // Get pointers
            for (ushort i = 0; i < quantity; ++i)
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PTRTILE_ORDINAL, new List<ushort> { allocatedPointer.Offset, allocatedPointer.Segment, i });

                Assert.Equal(allocatedPointer.Segment + i, mbbsEmuCpuRegisters.DX); // needs a valid segment
                Assert.Equal(0, mbbsEmuCpuRegisters.AX); // offset always 0
            }
        }

        [Fact]
        public void alctile_invalidArgument_Test()
        {
            //Reset State
            Reset();

            ushort quantity = 0;
            ushort size = 0;

            Assert.Throws<ArgumentException>(() => ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCTILE_ORDINAL, new List<ushort> { quantity, size }));
        }
    }
}

using System.Collections.Generic;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int ALCBLOK_ORDINAL = 879;
        private const int PTRBLOK_ORDINAL = 880;

        [Fact]
        public void alcblok_Test()
        {
            //Reset State
            Reset();

            ushort quantity = 256;
            ushort size = 1024;

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCBLOK_ORDINAL, new List<ushort> { quantity, size });

            //Verify Results
            Assert.NotEqual(0, mbbsEmuCpuRegisters.DX);

            var allocatedPointer = mbbsEmuCpuRegisters.GetPointer();

            // Get pointers
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PTRBLOK_ORDINAL, new List<ushort> { allocatedPointer.Offset, allocatedPointer.Segment, 0 });
            Assert.NotEqual(0, mbbsEmuCpuRegisters.DX);

            var lastPtr = mbbsEmuCpuRegisters.GetPointer();
            for (ushort i = 1; i < quantity; ++i)
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PTRBLOK_ORDINAL, new List<ushort> { allocatedPointer.Offset, allocatedPointer.Segment, i });

                Assert.True(mbbsEmuCpuRegisters.GetPointer() > lastPtr);

                lastPtr = mbbsEmuCpuRegisters.GetPointer();
            }
        }

        [Fact]
        public void alcblok_invalidArgument_Test()
        {
            //Reset State
            Reset();

            ushort quantity = 0;
            ushort size = 0;

            Assert.Throws<ArgumentException>(() => ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCBLOK_ORDINAL, new List<ushort> { quantity, size }));
        }
    }
}

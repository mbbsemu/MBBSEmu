using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Phapi
{
    /// <summary>
    ///     Covers PHAPI's DosAllocRealSeg (ordinal 16), which modules use to allocate a Real-Mode
    ///     memory segment they can later target with a Real-Mode interrupt (e.g. DosRealIntr).
    /// </summary>
    public class DosAllocRealSeg_Tests : PhapiTestBase
    {
        [Fact]
        public void AllocatesARealModeSegmentAndWritesItToBothOutputPointers()
        {
            var selectorPointer = mbbsEmuMemoryCore.Malloc(2);
            var segmentPointer = mbbsEmuMemoryCore.Malloc(2);
            const uint size = 4096;

            ExecuteApiTest(HostProcess.ExportedModules.Phapi.Segment, DOSALLOCREALSEG_ORDINAL, new List<ushort>
            {
                selectorPointer.Offset,
                selectorPointer.Segment,
                segmentPointer.Offset,
                segmentPointer.Segment,
                (ushort)size,
                (ushort)(size >> 16),
            });

            var selectorValue = mbbsEmuMemoryCore.GetWord(selectorPointer);
            var segmentValue = mbbsEmuMemoryCore.GetWord(segmentPointer);

            Assert.NotEqual(0, selectorValue);
            Assert.Equal(selectorValue, segmentValue);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}

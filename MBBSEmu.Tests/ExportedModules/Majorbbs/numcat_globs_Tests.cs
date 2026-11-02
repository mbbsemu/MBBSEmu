using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class numcat_globs_Tests : ExportedModuleTestBase
    {
        private const ushort NUMCAT_ORDINAL = 717;
        private const ushort GLOBS_ORDINAL = 734;

        [Fact]
        public void numcat_IsWritableVariable()
        {
            Reset();

            // Trade Wars 2002 clears numcat before running the global command handlers, as hdlinp() does
            var numcat = GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, NUMCAT_ORDINAL);
            Assert.NotEqual(HostProcess.ExportedModules.Majorbbs.Segment, numcat.Segment);

            mbbsEmuMemoryCore.SetWord(numcat, 3);
            Assert.Equal(3, mbbsEmuMemoryCore.GetWord(new FarPtr(ExecutePropertyTest(NUMCAT_ORDINAL))));
        }

        [Fact]
        public void globs_IsEmptyHandlerTable()
        {
            Reset();

            var globs = GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, GLOBS_ORDINAL);
            Assert.NotEqual(HostProcess.ExportedModules.Majorbbs.Segment, globs.Segment);

            for (var i = 0; i < HostProcess.ExportedModules.Majorbbs.GLBMAX; i++)
                Assert.True(mbbsEmuMemoryCore.GetPointer(globs + (ushort)(i * FarPtr.Size)).IsNull());
        }
    }
}

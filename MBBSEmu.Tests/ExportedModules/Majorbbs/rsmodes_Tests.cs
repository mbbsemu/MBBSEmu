using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rsmodes_Tests : ExportedModuleTestBase
    {
        private const ushort RSMODES_ORDINAL = 504;

        [Fact]
        public void RSMODES_ExportsInitializedPerChannelArray()
        {
            var pointer = new FarPtr(majorbbs.Invoke(RSMODES_ORDINAL));
            var expectedPointer = mbbsEmuMemoryCore.GetVariablePointer("RSMODES");

            Assert.Equal(expectedPointer, pointer);
            Assert.Equal(Majorbbs.NORMRS, mbbsEmuMemoryCore.GetWord(pointer));
            Assert.Equal(Majorbbs.NORMRS, mbbsEmuMemoryCore.GetWord(pointer + sizeof(ushort)));

            majorbbs.SetResetModes(Majorbbs.NANSRS);

            Assert.Equal(Majorbbs.NANSRS, mbbsEmuMemoryCore.GetWord(pointer));
            Assert.Equal(Majorbbs.NANSRS, mbbsEmuMemoryCore.GetWord(pointer + sizeof(ushort)));
        }
    }
}

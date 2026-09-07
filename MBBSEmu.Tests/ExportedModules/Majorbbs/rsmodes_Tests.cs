using MBBSEmu.Memory;
using Xunit;
using MajorbbsModule = MBBSEmu.HostProcess.ExportedModules.Majorbbs;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rsmodes_Tests : ExportedModuleTestBase
    {
        private const ushort RSMODES_ORDINAL = 504;

        [Fact]
        public void RSMODES_ExportsInitializedPerChannelArray()
        {
            var exportedPointer = new FarPtr(majorbbs.Invoke(RSMODES_ORDINAL));
            var pointer = mbbsEmuMemoryCore.GetPointer("*RSMODES");

            Assert.Equal(mbbsEmuMemoryCore.GetVariablePointer("*RSMODES"), exportedPointer);
            Assert.Equal(mbbsEmuMemoryCore.GetVariablePointer("RSMODES"), pointer);
            Assert.Equal(MajorbbsModule.NORMRS, mbbsEmuMemoryCore.GetWord(pointer));
            Assert.Equal(MajorbbsModule.NORMRS, mbbsEmuMemoryCore.GetWord(pointer + sizeof(ushort)));

            majorbbs.SetResetModes(MajorbbsModule.NANSRS);

            Assert.Equal(MajorbbsModule.NANSRS, mbbsEmuMemoryCore.GetWord(pointer));
            Assert.Equal(MajorbbsModule.NANSRS, mbbsEmuMemoryCore.GetWord(pointer + sizeof(ushort)));
        }
    }
}

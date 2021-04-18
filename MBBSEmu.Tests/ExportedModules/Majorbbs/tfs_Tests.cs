using FluentAssertions;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class tfs_Tests: FileTestBase
    {
        private const int TFSTATE_ORDINAL = 768;
        private const int TFSOPN_ORDINAL = 773;
        private const int TFSABT_ORDINAL = 892;
        private short tfsopn(string filespec)
        {
            var ptr = mbbsEmuMemoryCore.Malloc((ushort)(filespec.Length + 1));
            mbbsEmuMemoryCore.SetArray(ptr, Encoding.ASCII.GetBytes(filespec));
            mbbsEmuMemoryCore.SetByte(ptr + filespec.Length, 0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSOPN_ORDINAL, new List<FarPtr>() {ptr});

            mbbsEmuMemoryCore.Free(ptr);
            return (short)mbbsEmuCpuRegisters.AX;
        }

        private void tfsabt()
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSABT_ORDINAL, new List<ushort> ());
        }

        private ushort tfstate => mbbsEmuMemoryCore.GetWord(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSTATE_ORDINAL));

        [Fact]
        public void tfsopen_test()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "test\r\nline\r\nanother\r\n";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            tfstate.Should().Be((ushort)HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            var nFiles = tfsopn("file.txt");
            nFiles.Should().Be(1);
            tfstate.Should().Be((ushort)HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);

            tfsabt();

            tfstate.Should().Be((ushort)HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);
        }
    }
}

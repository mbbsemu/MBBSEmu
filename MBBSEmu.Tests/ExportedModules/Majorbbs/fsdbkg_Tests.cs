using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class fsdbkg_Tests : ExportedModuleTestBase
    {
        private const int FSDBKG_ORDINAL = 238;

        [Fact]
        public void FSDBKG_TurnsWordWrapOff_Test()
        {
            Reset();

            //FSDBBS.C: fsdbkg() paints with btutsw(usrnum,0), and wrap stays off until fsdcof()
            testSessions[0].WordWrapWidth = 79;
            testSessions[0].OutputEnabled = false;

            var templatePointer = mbbsEmuMemoryCore.AllocateVariable("TEMPLATE", 0x20);
            mbbsEmuMemoryCore.SetArray(templatePointer, Encoding.ASCII.GetBytes("Name: ????\0"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSDBKG_ORDINAL,
                new List<FarPtr> { templatePointer });

            Assert.Equal(0, testSessions[0].WordWrapWidth);
        }
    }
}

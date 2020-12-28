using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class errcod_Tests : ExportedModuleTestBase
    {
        private const int ERRCOD_ORDINAL = 193;

        [Fact]
        public void ClingoTests()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ERRCOD_ORDINAL, new List<FarPtr>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(ERRCOD_ORDINAL);

            var actualValue = mbbsEmuMemoryCore.GetWord(new FarPtr(returnedPointer));

            Assert.Equal(0, actualValue);
        }
    }
}

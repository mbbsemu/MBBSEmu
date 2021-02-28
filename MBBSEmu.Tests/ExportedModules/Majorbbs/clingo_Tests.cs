using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int CLINGO_ORDINAL = 761;

        [Fact]
        public void ClingoTests()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CLINGO_ORDINAL, new List<FarPtr>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(CLINGO_ORDINAL);

            var actualValue = mbbsEmuMemoryCore.GetWord(new FarPtr(returnedPointer));

            Assert.Equal(0, actualValue);
        }
    }
}

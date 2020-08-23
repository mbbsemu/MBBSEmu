using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class clingo_Tests : MajorbbsTestBase
    {
        private const int CLINGO_ORDINAL = 761;

        [Fact]
        public void ClingoTests()
        {
            //Reset State
            Reset();

            ExecuteApiTest(CLINGO_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(CLINGO_ORDINAL);

            var actualValue = mbbsEmuMemoryCore.GetWord(new IntPtr16(returnedPointer));

            Assert.Equal(0, actualValue);
        }
    }
}

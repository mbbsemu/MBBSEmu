using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class clingo_Tests : MajorbbsTestBase
    {
        private const int CLINGO_ORDINAL = 761;

        [Theory]
        [InlineData(0x0)]
        public void ClingoTests(ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(CLINGO_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(CLINGO_ORDINAL);

            var actualValue = mbbsEmuMemoryCore.GetWord(new IntPtr16(returnedPointer));

            Assert.Equal(expectedValue, actualValue);
        }
    }
}

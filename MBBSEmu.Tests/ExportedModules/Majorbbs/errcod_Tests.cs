using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class errcod_Tests : MajorbbsTestBase
    {
        private const int ERRCOD_ORDINAL = 193;

        [Theory]
        [InlineData(0x0)]
        public void ClingoTests(ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(ERRCOD_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(ERRCOD_ORDINAL);

            var actualValue = mbbsEmuMemoryCore.GetWord(new IntPtr16(returnedPointer));

            Assert.Equal(expectedValue, actualValue);
        }
    }
}

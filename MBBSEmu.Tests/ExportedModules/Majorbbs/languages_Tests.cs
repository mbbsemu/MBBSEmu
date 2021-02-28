using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int LANGUAGES_ORDINAL = 762;

        [Fact]
        public void LanguagesTests()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LANGUAGES_ORDINAL, new List<FarPtr>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(LANGUAGES_ORDINAL);
            var pointerToLanguages = mbbsEmuMemoryCore.GetPointer(new FarPtr(returnedPointer));
            var actualValue = mbbsEmuMemoryCore.GetPointer(pointerToLanguages);

            var actualLanguageValue = mbbsEmuMemoryCore.GetString(actualValue, true);

            Assert.Equal("ansi", Encoding.ASCII.GetString(actualLanguageValue));
        }
    }
}

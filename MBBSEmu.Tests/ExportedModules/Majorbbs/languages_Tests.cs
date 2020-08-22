using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class languages_Tests : MajorbbsTestBase
    {
        private const int LANGUAGES_ORDINAL = 762;

        [Theory]
        [InlineData("ansi")]
        public void LanguagesTests(string expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(LANGUAGES_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(LANGUAGES_ORDINAL);
            var pointerToLanguages = mbbsEmuMemoryCore.GetPointer(new IntPtr16(returnedPointer));
            var actualValue = mbbsEmuMemoryCore.GetPointer(pointerToLanguages);

            var actualLanguageValue = mbbsEmuMemoryCore.GetString(actualValue, true);

            Assert.Equal(expectedValue, Encoding.ASCII.GetString(actualLanguageValue));
        }

        /// <summary>
        ///     Negative Tests
        /// </summary>
        /// <param name="unexpectedValue"></param>
        [Theory]
        [InlineData("")]
        [InlineData("rip")]
        [InlineData("english")]
        public void LanguagesTests_Negative(string unexpectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(LANGUAGES_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var returnedPointer = ExecutePropertyTest(LANGUAGES_ORDINAL);
            var pointerToLanguages = mbbsEmuMemoryCore.GetPointer(new IntPtr16(returnedPointer));
            var actualValue = mbbsEmuMemoryCore.GetPointer(pointerToLanguages);

            var actualLanguageValue = mbbsEmuMemoryCore.GetString(actualValue, true);

            Assert.NotEqual(unexpectedValue, Encoding.ASCII.GetString(actualLanguageValue));
        }
    }
}

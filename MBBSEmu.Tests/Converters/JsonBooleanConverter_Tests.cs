using MBBSEmu.Converters;
using System.Text.Json;
using Xunit;

namespace MBBSEmu.Tests.Converters
{
    public class JsonBooleanConverter_Tests
    {

        internal class TestResult
        {
            public bool TestValue { get; set; }
        }

        [Theory]
        [InlineData(1, true, false)]
        [InlineData(0, false, false)]
        [InlineData(-1, false, true)]
        [InlineData(2, false, true)]
        public void IntToBoolTest(int number, bool expectedResult, bool throwsException)
        {
            var jsonToDeserialize = $"{{ \"TestValue\" : {number} }}";

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter() }
            };

            if (!throwsException)
            {
                var actualResult = JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options);
                Assert.Equal(expectedResult, actualResult?.TestValue);
            }
            else
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options));
            }
        }

        [Theory]
        [InlineData("true", true, false)]
        [InlineData("yes", true, false)]
        [InlineData("1", true, false)]
        [InlineData("false", false, false)]
        [InlineData("no", false, false)]
        [InlineData("0", false, false)]
        [InlineData("test", false, true)]
        public void StringToBoolTest(string value, bool expectedResult, bool throwsException)
        {
            var jsonToDeserialize = $"{{ \"TestValue\" : \"{value}\" }}";

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter() }
            };

            if (!throwsException)
            {
                var actualResult = JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options);
                Assert.Equal(expectedResult, actualResult?.TestValue);
            }
            else
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options));
            }
        }

        [Theory]
        [InlineData("true", true, false)]
        [InlineData("false", false, false)]
        [InlineData("derp", false, true)]
        public void BooleanToBoolTest(string value, bool expectedResult, bool throwsException)
        {
            var jsonToDeserialize = $"{{ \"TestValue\" : {value} }}";

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter() }
            };

            if (!throwsException)
            {
                var actualResult = JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options);
                Assert.Equal(expectedResult, actualResult?.TestValue);
            }
            else
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options));
            }
        }
    }
}
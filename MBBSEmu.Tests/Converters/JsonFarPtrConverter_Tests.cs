using MBBSEmu.Converters;
using MBBSEmu.Memory;
using System.Text.Json;
using Xunit;

namespace MBBSEmu.Tests.Converters
{
    public class JsonFarPtrConverter_Tests
    {
        internal class TestResult
        {
            public FarPtr TestValue { get; set; }
        }

        [Theory]
        [InlineData("0001:0001", 1, 1, false)]
        [InlineData("FFFF:FFFF", ushort.MaxValue, ushort.MaxValue, false)]
        [InlineData("FFFF:0001", ushort.MaxValue, 1, false)]
        [InlineData("0000:FFFF", 0, ushort.MaxValue, false)]
        [InlineData("XXXX:FFFF", 0, ushort.MaxValue, true)]
        [InlineData("FFFF:XXXX", 0, ushort.MaxValue, true)]
        [InlineData("FFFFF:FFFFF", 0, 0, true)]
        [InlineData("", 0, 0, true)]
        [InlineData("X:X", 0, 0, true)]
        [InlineData("X:X:X", 0, 0, true)]
        public void StringToFarPtrTest(string farPtrString, ushort segment, ushort offset, bool throwsException)
        {
            var jsonToDeserialize = $"{{ \"TestValue\" : \"{farPtrString}\" }}";

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonFarPtrConverter() }
            };

            if (!throwsException)
            {
                var actualResult = JsonSerializer.Deserialize<TestResult>(jsonToDeserialize, options);
                Assert.Equal(segment, actualResult.TestValue.Segment);
                Assert.Equal(offset, actualResult.TestValue.Offset);
            }
            else
            {
                Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<JsonBooleanConverter_Tests.TestResult>(jsonToDeserialize, options));
            }
        }
    }
}

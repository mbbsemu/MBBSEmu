using FluentAssertions;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class ProtectedModeMemoryCore_Tests : TestBase
    {
        private readonly IMessageLogger _logger = new ServiceResolver().GetService<LogFactory>().GetLogger<MessageLogger>();

        [Fact]
        public void EndOfSegmentString()
        {
            ushort segment = 1;
            var memoryCore = new ProtectedModeMemoryCore(_logger);
            memoryCore.AddSegment(segment);

            var testString = new string('X', 5) + "\0";
            var testStringOffset = (ushort)(ushort.MaxValue - testString.Length + 1);

            (memoryCore as IMemoryCore).SetArray(1, testStringOffset, Encoding.ASCII.GetBytes(testString));

            var stringFromMemory = Encoding.ASCII.GetString((memoryCore as IMemoryCore).GetString(1, testStringOffset, stripNull: false));

            stringFromMemory.Should().Be(testString);
        }

        [Fact]
        public void MultiSegmentAllocation()
        {
            var memoryCore = new ProtectedModeMemoryCore(_logger);
            var data1 = memoryCore.Malloc(0xFF00);
            data1.Should().NotBeNull();

            var data2 = memoryCore.Malloc(0xFF00);
            data2.Should().NotBeNull();
            data2.Segment.Should().NotBe(data1.Segment);

            var data3 = memoryCore.Malloc(0xFF00);
            data3.Should().NotBeNull();
            data3.Segment.Should().NotBe(data2.Segment);
            data3.Segment.Should().NotBe(data1.Segment);
        }
    }
}

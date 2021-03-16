using MBBSEmu.DependencyInjection;
using MBBSEmu.Memory;
using NLog;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class MemoryCore_Tests : TestBase
    {
        private readonly ILogger _logger = new ServiceResolver().GetService<ILogger>();

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

            Assert.Equal(testString, stringFromMemory);

        }
    }
}

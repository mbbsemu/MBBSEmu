using FluentAssertions;
using MBBSEmu.Logging;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.Logging
{
    public class LogLineWriter_Tests
    {
        [Fact]
        public void LogLineWriter_EmitsCompleteLines()
        {
            var lines = new List<string>();
            var writer = new LogLineWriter(lines.Add);

            writer.Write("hello\nworld\r\npartial");
            lines.Should().Equal("hello", "world");

            writer.Flush();
            lines.Should().Equal("hello", "world", "partial");

            writer.Dispose();
            lines.Should().Equal("hello", "world", "partial");
        }

        [Fact]
        public void Dispose_EmitsBufferedPartialLine()
        {
            var lines = new List<string>();
            var writer = new LogLineWriter(lines.Add);

            writer.Write('A');
            writer.Write('B');
            writer.Dispose();

            lines.Should().Equal("AB");
        }
    }
}

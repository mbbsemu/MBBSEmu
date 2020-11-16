using FluentAssertions;
using MBBSEmu.Btrieve;
using System.IO;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class BtrieveUtil_Tests : TestBase
    {
        private class FakeMemoryStream : MemoryStream
        {
            internal int MaxBytesToRead { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, Math.Min(count, MaxBytesToRead));
            }
        }

        [Fact]
        public void FakeReadsChunksProperly()
        {
            using var stream = new FakeMemoryStream() { MaxBytesToRead = 2 };
            var bytes = Encoding.ASCII.GetBytes("123456789");

            stream.Write(bytes);
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[9];
            stream.Read(buffer, 0, 8).Should().Be(2);
            stream.Read(buffer, 2, 8).Should().Be(2);
            stream.Read(buffer, 4, 8).Should().Be(2);
            stream.Read(buffer, 6, 8).Should().Be(2);
            stream.Read(buffer, 8, 1).Should().Be(1);

            Encoding.ASCII.GetString(buffer).Should().Be("123456789");
        }

        [Theory]
        [InlineData("testing", 32)]
        [InlineData("testing", 8)]
        [InlineData("testing", 7)]
        [InlineData("testing", 6)]
        [InlineData("testing", 5)]
        [InlineData("testing", 4)]
        [InlineData("testing", 3)]
        [InlineData("testing", 2)]
        [InlineData("testing", 1)]
        public void ReadEntireStream(string input, int maxBytesToRead)
        {
            using var stream = new FakeMemoryStream() { MaxBytesToRead = maxBytesToRead };
            var bytes = Encoding.ASCII.GetBytes(input);

            stream.Write(bytes);
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            var read = BtrieveUtil.ReadEntireStream(stream);

            read.AsSpan().SequenceEqual(bytes).Should().BeTrue();
        }
    }
}

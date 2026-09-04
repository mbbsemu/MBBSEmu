using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Session.Telnet;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Session.Telnet
{
    public class IacFilter_Tests
    {
        private readonly IacFilter iacFilter = new(new ServiceResolver().GetService<LogFactory>().GetLogger<MessageLogger>());

        [Fact]
        public void PassThroughNoIAC()
        {
            var str = "testing 1234";
            var bytes = Encoding.ASCII.GetBytes(str);
            var (outBytes, len) = iacFilter.ProcessIncomingClientData(bytes, bytes.Length);

            Assert.Equal(bytes, new ReadOnlySpan<byte>(outBytes).Slice(0, len).ToArray());
        }

        [Fact]
        public void PassThroughNoIACDoubleProcess()
        {
            var str = "testing 1234";
            var bytes = Encoding.ASCII.GetBytes(str);

            var (outBytes, len) = iacFilter.ProcessIncomingClientData(bytes, bytes.Length);

            Assert.Equal(bytes, new ReadOnlySpan<byte>(outBytes).Slice(0, len).ToArray());

            (outBytes, len) = iacFilter.ProcessIncomingClientData(bytes, bytes.Length);

            Assert.Equal(bytes, new ReadOnlySpan<byte>(outBytes).Slice(0, len).ToArray());
        }

        [Fact]
        public void BasicTelnetStripping()
        {
            byte[] iac = {
                0xFF, 0xFB, 0x01,
                0xFF, 0xFC, 0x01,
                0xFF, 0xFD, 0x01,
                0xFF, 0xFE, 0x01,
            };
            var expectedString = "This is a test of the emergency system";

            var bytes = Concat(
                Encoding.ASCII.GetBytes("This is a test"),
                iac,
                Encoding.ASCII.GetBytes(" of the emergency system"),
                iac);

            var (outBytes, len) = iacFilter.ProcessIncomingClientData(bytes, bytes.Length);

            Assert.Equal(Encoding.ASCII.GetBytes(expectedString), new ReadOnlySpan<byte>(outBytes).Slice(0, len).ToArray());
        }

        [Fact]
        public void BasicTelnetOptionsStripping()
        {
            byte[] iacWithOptions = {
                0xFF, 0xFB, 0x01,
                0xFF, 0xFA, 0x1F, 0x00, 0x50, 0x00, 0x18, 0xFF, 0xF0
            };
            var expectedString = "This is a test of the emergency system";

            var bytes = Concat(
                Encoding.ASCII.GetBytes("This is a test"),
                iacWithOptions,
                Encoding.ASCII.GetBytes(" of the emergency system"),
                iacWithOptions);

            var (outBytes, len) = iacFilter.ProcessIncomingClientData(bytes, bytes.Length);

            Assert.Equal(Encoding.ASCII.GetBytes(expectedString), new ReadOnlySpan<byte>(outBytes).Slice(0, len).ToArray());
        }

        [Fact]
        public void BasicTelnetStrippingOverPackets()
        {
            var stream = new MemoryStream();
            byte[] start_iac = { 0xFF };
            byte[] end_iac = { 0xFB, 0x01 };

            var b = Concat(Encoding.ASCII.GetBytes("This is a test"), start_iac);
            var (bytes, length) = iacFilter.ProcessIncomingClientData(b, b.Length);
            stream.Write(bytes, 0, length);

            Assert.Equal(
                Encoding.ASCII.GetBytes("This is a test"),
                stream.ToArray());

            b = Concat(end_iac, Encoding.ASCII.GetBytes(" of the emergency system"));
            (bytes, length) = iacFilter.ProcessIncomingClientData(b, b.Length);
            stream.Write(bytes, 0, length);

            Assert.Equal(
                Encoding.ASCII.GetBytes("This is a test of the emergency system"),
                stream.ToArray());
        }

        [Theory]
        [InlineData(new byte[] { (byte)'A', 0x0D, 0x0A, (byte)'B' }, new byte[] { (byte)'A', 0x0D, (byte)'B' })]
        [InlineData(new byte[] { (byte)'A', 0x0D, 0x00, (byte)'B' }, new byte[] { (byte)'A', 0x0D, (byte)'B' })]
        [InlineData(new byte[] { (byte)'A', 0x0A, (byte)'B' }, new byte[] { (byte)'A', 0x0D, (byte)'B' })]
        public void NormalizesTerminalInput(byte[] input, byte[] expected)
        {
            var (bytes, length) = iacFilter.ProcessIncomingClientData(input, input.Length);

            Assert.Equal(expected, new ReadOnlySpan<byte>(bytes).Slice(0, length).ToArray());
        }

        [Theory]
        [InlineData(0x0A)]
        [InlineData(0x00)]
        public void NormalizesCarriageReturnSequenceAcrossPackets(byte trailingByte)
        {
            var (bytes, length) = iacFilter.ProcessIncomingClientData(new byte[] { (byte)'A', 0x0D }, 2);
            Assert.Equal(new byte[] { (byte)'A', 0x0D }, new ReadOnlySpan<byte>(bytes).Slice(0, length).ToArray());

            (bytes, length) = iacFilter.ProcessIncomingClientData(new byte[] { trailingByte, (byte)'B' }, 2);
            Assert.Equal(new byte[] { (byte)'B' }, new ReadOnlySpan<byte>(bytes).Slice(0, length).ToArray());
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            var length = 0;
            foreach (var a in arrays)
            {
                length += a.Length;
            }

            var ret = new byte[length];
            length = 0;
            foreach (var a in arrays)
            {
                Array.Copy(a, 0, ret, length, a.Length);
                length += a.Length;
            }

            return ret;
        }
    }
}

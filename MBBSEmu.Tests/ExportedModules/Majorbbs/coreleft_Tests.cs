using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class coreleft_Tests : ExportedModuleTestBase
    {
        private const int CORELEFT_ORDINAL = 138;
        private const int FARCORELEFT_ORDINAL = 201;

        [Theory]
        [InlineData(CORELEFT_ORDINAL)]
        [InlineData(FARCORELEFT_ORDINAL)]
        public void coreleft_ReturnsPositiveSignedLong(int ordinal)
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal, new List<ushort>());

            //Verify Results -- modules store the result in a signed long, so it must read as positive
            mbbsEmuCpuRegisters.GetLong().Should().BePositive();
            ((uint)mbbsEmuCpuRegisters.GetLong()).Should().Be(HostProcess.ExportedModules.Majorbbs.CORELEFT_BYTES);
        }

        /// <summary>
        ///     Mirrors MajorMUD 1.11p's allocate_buffers(): a cache of (buf + 1) records is shrunk by
        ///     step down to a floor while it doesn't fit in farcoreleft(), compared as signed longs.
        /// </summary>
        [Theory]
        [InlineData(300, 10, 25, 386)] // active monsters (stock MONSBUF, WCCACMSR record length)
        [InlineData(2000, 25, 50, 1528)] // rooms (stock ROOMBUF)
        public void farcoreleft_DoesNotShrinkBuffers(int buf, int step, int floor, int recordSize)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FARCORELEFT_ORDINAL, new List<ushort>());
            long avail = mbbsEmuCpuRegisters.GetLong();

            var original = buf;
            // the product is a 16-bit unsigned int in the original, promoted to long for the compare
            while ((ushort)((buf + 1) * recordSize) > avail && buf > floor)
                buf -= step;

            buf.Should().Be(original);
        }
    }
}

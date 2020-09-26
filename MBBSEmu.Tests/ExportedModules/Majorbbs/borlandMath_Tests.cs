using System.Collections.Generic;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class borlandMath_Tests : ExportedModuleTestBase
    {
        private const int LDIV_ORDINAL = 654;
        private const int LMOD_ORDINAL = 655;
        private const int LUDIV_ORDINAL = 656;
        private const int LUMOD_ORDINAL = 657;
        private const int LXMUL_ORDINAL = 659;

        [Theory]
        [InlineData(200, 5, 40)]
        [InlineData(200, -5, -40)]
        [InlineData(-200, -5, 40)]
        [InlineData(5, 200, 0)]
        public void ldivTest(int value1, int value2, int expectedValue)
        {
            Reset();

            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment,
              LDIV_ORDINAL,
              new List<ushort>
              {
                (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
              }
            );

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Theory]
        [InlineData(200, 5, 40)]
        [InlineData(200, 0xFFFFFFFF, 0)]
        [InlineData(0xFFFF, 200, 0xFFFF / 200)]
        [InlineData(5, 200, 0)]
        [InlineData(0xFFFFFFFF, 0x1111, 0xFFFFFFFF / 0x1111)]
        public void ludivTest(uint value1, uint value2, uint expectedValue)
        {
            Reset();

            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment,
              LUDIV_ORDINAL,
              new List<ushort>
              {
                (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
              }
            );

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Fact]
        public void ldivDivideByZero()
        {
            var value1 = 100;
            var value2 = 0;

            Reset();

            Assert.Throws<DivideByZeroException>(() =>
                ExecuteApiTest(
                  HostProcess.ExportedModules.Majorbbs.Segment,
                  LDIV_ORDINAL,
                  new List<ushort>
                  {
                    (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                    (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
                  }
                ));
        }

        [Fact]
        public void ludivDivideByZero()
        {
            var value1 = 100;
            var value2 = 0;

            Reset();

            Assert.Throws<DivideByZeroException>(() =>
                ExecuteApiTest(
                  HostProcess.ExportedModules.Majorbbs.Segment,
                  LUDIV_ORDINAL,
                  new List<ushort>
                  {
                    (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                    (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
                  }
                ));
        }

        [Theory]
        [InlineData(200, 51, 200 % 51)]
        [InlineData(200, -51, 200 % -51)]
        [InlineData(-200, -51, -200 % -51)]
        [InlineData(5, 200, 5)]
        [InlineData(5, -200, 5)]
        public void lmodTest(int value1, int value2, int expectedValue)
        {
            Reset();

            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment,
              LMOD_ORDINAL,
              new List<ushort>
              {
                (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
              }
            );

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Theory]
        [InlineData(200, 51, 200 % 51)]
        [InlineData(200, 0xFFFFFFFF, 200)]
        [InlineData(0xFFFF, 200, 0xFFFF % 200)]
        [InlineData(5, 200, 5)]
        [InlineData(0xFFFFFFFF, 0x1111, 0xFFFFFFFF % 0x1111)]
        public void lumodTest(uint value1, uint value2, uint expectedValue)
        {
            Reset();

            ExecuteApiTest(
              HostProcess.ExportedModules.Majorbbs.Segment,
              LUMOD_ORDINAL,
              new List<ushort>
              {
                (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
              }
            );

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }

        [Fact]
        public void lmodDivideByZero()
        {
            var value1 = 100;
            var value2 = 0;

            Reset();

            Assert.Throws<DivideByZeroException>(() =>
                ExecuteApiTest(
                  HostProcess.ExportedModules.Majorbbs.Segment,
                  LMOD_ORDINAL,
                  new List<ushort>
                  {
                    (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                    (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
                  }
                ));
        }

        [Fact]
        public void lumodDivideByZero()
        {
            var value1 = 100;
            var value2 = 0;

            Reset();

            Assert.Throws<DivideByZeroException>(() =>
                ExecuteApiTest(
                  HostProcess.ExportedModules.Majorbbs.Segment,
                  LUMOD_ORDINAL,
                  new List<ushort>
                  {
                    (ushort)(value1 & 0xFFFF), (ushort)(value1 >> 16),
                    (ushort)(value2 & 0xFFFF), (ushort)(value2 >> 16),
                  }
                ));
        }

        [Theory]
        [InlineData(200, 51, 200 * 51)]
        [InlineData(51, 200, 200 * 51)]
        [InlineData(0, 51, 0)]
        [InlineData(51, 0, 0)]
        [InlineData(-1, 1, -1)]
        [InlineData(-1, -1, 1)]
        [InlineData(100_000, 2_000, 200_000_000)]
        [InlineData(100_000, -2_000, -200_000_000)]
        [InlineData(100_000, 2_000_000, unchecked(100_000 * 2_000_000))]
        public void lxmulTest(int value1, int value2, int expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.DX = (ushort)((uint) value1 >> 16);
            mbbsEmuCpuRegisters.AX = (ushort)((uint) value1 & 0xFFFF);
            mbbsEmuCpuRegisters.CX = (ushort)((uint) value2 >> 16);
            mbbsEmuCpuRegisters.BX = (ushort)((uint) value2 & 0xFFFF);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LXMUL_ORDINAL, new List<ushort>());

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}

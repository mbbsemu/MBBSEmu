using MBBSEmu.HostProcess.Structs;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Phapi
{
    /// <summary>
    ///     Covers PHAPI's DosRealIntr (ordinal 49) handling of INT 21h AH=0x35 (Get Interrupt
    ///     Vector), which modules use to detect whether the Btrieve driver is loaded before
    ///     talking to it directly via INT 7Bh.
    /// </summary>
    public class DosRealIntr_GetInterruptVector_Tests : PhapiTestBase
    {
        private const byte GET_INTERRUPT_VECTOR_FUNCTION = 0x35;
        private const byte BTRIEVE_VECTOR = 0x7B;

        [Fact]
        public void GetInterruptVector_ForBtrieveVector_ReturnsSegment0x33InBX()
        {
            var regs = new Regs16Struct { AX = (ushort)((GET_INTERRUPT_VECTOR_FUNCTION << 8) | BTRIEVE_VECTOR) };

            var result = DosRealIntr(INT_21H, regs);

            Assert.Equal(0x33, result.BX);
        }

        [Fact]
        public void GetInterruptVector_ForUnknownVector_Throws()
        {
            var regs = new Regs16Struct { AX = (ushort)((GET_INTERRUPT_VECTOR_FUNCTION << 8) | 0xFF) };

            Assert.Throws<Exception>(() => DosRealIntr(INT_21H, regs));
        }

        [Fact]
        public void UnknownInt21Function_Throws()
        {
            var regs = new Regs16Struct { AX = 0x9900 };

            Assert.Throws<Exception>(() => DosRealIntr(INT_21H, regs));
        }

        [Fact]
        public void UnhandledInterrupt_Throws()
        {
            Assert.Throws<Exception>(() => DosRealIntr(0x10, new Regs16Struct()));
        }
    }
}

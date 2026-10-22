using MBBSEmu.CPU;

namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     Interrupt Vector 11h -- BIOS GET EQUIPMENT LIST
    ///
    ///     Borland C++ startup code calls this to detect a math coprocessor (bit 1)
    ///     before deciding whether to use the x87 or its floating point emulator.
    /// </summary>
    public class Int11h : IInterruptHandler
    {
        private ICpuRegisters _registers { get; init; }

        public byte Vector => 0x11;

        public Int11h(ICpuRegisters registers)
        {
            _registers = registers;
        }

        public void Handle()
        {
            /*
                INT 11 - BIOS - GET EQUIPMENT LIST
                Return: AX = BIOS equipment list word
                    bit 0     floppy disk(s) installed
                    bit 1     80x87 coprocessor installed
                    bits 5-4  initial video mode (10 = 80x25 color)

                The emulated CPU executes x87 instructions, so report a coprocessor.
            */
            _registers.AX = 0x0023;
        }
    }
}

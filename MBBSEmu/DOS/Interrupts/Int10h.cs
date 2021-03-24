using MBBSEmu.CPU;
using NLog;

namespace MBBSEmu.DOS.Interrupts
{
    public class Int10h : IInterruptHandler
    {
        private ILogger _logger { get; init; }

        private CpuRegisters _registers { get; init; }

        public byte Vector => 0x10;

        public Int10h(CpuRegisters registers, ILogger logger)
        {
            _registers = registers;
            _logger = logger;
        }

        public void Handle()
        {
            _logger.Error($"Interrupt AX {_registers.AX:X4} H:{_registers.AH:X2}");

            switch (_registers.AH)
            {
                case 0x0F:
                    GetCurrentVideoMode_0x0F();
                    return;
            }
        }


        private void GetCurrentVideoMode_0x0F()
        {
            _registers.AH = 80;
            _registers.BL = 0x03; //80x34
            _registers.BH = 0x00;
        }
    }
}
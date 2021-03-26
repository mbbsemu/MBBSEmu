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
                case 0x12:
                    AlternateFunctionSelect_0x12();
                    return;
                case 0x08:
                    ReadAttributes_CharacterAtCursorPosition();
                    return;
                    
            }
        }


        private void GetCurrentVideoMode_0x0F()
        {
            _registers.AH = 80; //Columns
            _registers.BL = 0x10; //640x480 16 colors 80x25 text resolution
            _registers.BH = 0x00; //Initial Page?
        }

        private void AlternateFunctionSelect_0x12()
        {
            switch (_registers.BL)
            {
                case 0x10: //Return EGA Information
                {
                    _registers.BH = 0; //Color Mode
                    _registers.BL = 3; //256k bytes Memory Installed
                    _registers.CH = 0; //TODO -- Feature Bits?
                    _registers.CL = 0; //TODO -- Switch Settings?
                    return;
                }
            }
        }

        private void ReadAttributes_CharacterAtCursorPosition()
        {
            // Input: _registers.BL == Display Page

            _registers.AL = 0x0; //TODO -- Null for now?
            _registers.AH = 0x0; //TODO -- Attributes of Character?
        }
    }
}
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
                    ReadAttributes_CharacterAtCursorPosition_0x08();
                    return;
                    
            }
        }

        /// <summary>
        /// INT 10 - AH = 0Fh VIDEO - GET CURRENT VIDEO MODE
        /// Return: AH = number of columns on screen
        /// AL = current video mode (see INT 10h/AH=00h)
        /// BH = current active display page
        /// Note: if mode was set with bit 7 set ("no blanking"), the returned mode will also have bit 7 set
        /// </summary>
        private void GetCurrentVideoMode_0x0F()
        {
            _registers.AH = 80; //Columns
            _registers.BL = 0x10; //640x480 16 colors 80x25 text resolution
            _registers.BH = 0x00; //Initial Page?
        }

        /// <summary>
        /// INT 10 - AH = 12h VIDEO - ALTERNATE FUNCTION SELECT (PS, EGA, VGA, MCGA
        /// </summary>
        private void AlternateFunctionSelect_0x12()
        {
            switch (_registers.BL)
            {
                case 0x10: //Return EGA Information
                {
                        /*
                         * BL = 10h: return EGA information
                           Return: BH = 0: color mode in effect (3Dx)
                           1: mono mode in effect (3Bx)
                           BL = 0: 64k bytes memory installed
                           1: 128k bytes memory installed
                           2: 192k bytes memory installed
                           3: 256k bytes memory installed
                           CH = feature bits
                           CL = switch settings
                         */
                        _registers.BH = 0; //Color Mode
                    _registers.BL = 3; //256k bytes Memory Installed
                    _registers.CH = 0; //TODO -- Feature Bits?
                    _registers.CL = 0; //TODO -- Switch Settings?
                    return;
                }
            }
        }

        /// <summary>
        /// INT 10 - AH = 08h VIDEO - READ ATTRIBUTES/CHARACTER AT CURSOR POSITION
        /// BH = display page
        /// Return: AL = character
        /// AH = attribute of character (alpha modes)
        /// </summary>
        private void ReadAttributes_CharacterAtCursorPosition_0x08()
        {
            // Input: _registers.BL == Display Page

            _registers.AL = 0x0; //TODO -- Null for now?
            _registers.AH = 0x0; //TODO -- Attributes of Character?
        }
    }
}
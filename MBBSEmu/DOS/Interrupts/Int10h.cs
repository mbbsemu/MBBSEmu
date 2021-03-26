using System.Collections.Concurrent;
using System.Text;
using MBBSEmu.CPU;
using NLog;

namespace MBBSEmu.DOS.Interrupts
{
    public class Int10h : IInterruptHandler
    {
        private const string ANSI_CLEAR_SCREEN = "\x1B[2J";
        private const string ANSI_SET_CURSOR_POSITION = "\x1B[{0};{1}H";

        private ILogger _logger { get; init; }
        private readonly BlockingCollection<byte[]> _stdout;

        private CpuRegisters _registers { get; init; }

        public byte Vector => 0x10;

        public Int10h(CpuRegisters registers, ILogger logger, BlockingCollection<byte[]> stdout)
        {
            _registers = registers;
            _logger = logger;
            _stdout = stdout;
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
                case 0x06:
                    ScrollPageUp_0x06();
                    return;
                case 0x02:
                    SetCursorPosition_0x02();
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

        /// <summary>
        /// INT 10 - AH = 06h VIDEO - SCROLL PAGE UP
        /// AL = number of lines to scroll window (0 = blank whole window)
        /// BH = attributes to be used on blanked lines
        /// CH,CL = row,column of upper left corner of window to scroll
        /// DH,DL = row,column of lower right corner of window
        /// </summary>
        private void ScrollPageUp_0x06()
        {
            //Blank Whole Screen?
            if (_registers.AL == 0)
            {
                _stdout.Add(Encoding.ASCII.GetBytes(ANSI_CLEAR_SCREEN));
                return;
            }

            _logger.Warn($"Scrolling up {_registers.AL} lines not currently supported");

        }

        /// <summary>
        /// INT 10 - AH = 02h VIDEO - SET CURSOR POSITION
        /// DH,DL = row, column (0,0 = upper left)
        /// BH = page number
        /// 0 in graphics modes
        /// 0-3 in modes 2&amp;3
        /// 0-7 in modes 0&amp;1
        /// </summary>
        private void SetCursorPosition_0x02()
        {
            _stdout.Add(Encoding.ASCII.GetBytes(string.Format(ANSI_SET_CURSOR_POSITION, _registers.DH, _registers.DL)));
        }
    }
}
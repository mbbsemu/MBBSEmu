using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Logging;

namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     Interrupt Vector 16h -- BIOS KEYBOARD SERVICES
    ///
    ///     Keys come from the same stdin stream INT 21h reads. IStream has no non-blocking
    ///     peek, so "check for keystroke" reads one byte ahead and holds it until it's consumed;
    ///     programs that poll the keyboard while doing other work will block on that check.
    /// </summary>
    public class Int16h : IInterruptHandler
    {
        private ICpuRegisters _registers { get; init; }
        private IMessageLogger _logger { get; init; }
        private IStream _stdin { get; init; }

        private int _pendingKey = -1;

        public byte Vector => 0x16;

        public Int16h(ICpuRegisters registers, IMessageLogger logger, IStream stdin)
        {
            _registers = registers;
            _logger = logger;
            _stdin = stdin;
        }

        public void Handle()
        {
            switch (_registers.AH)
            {
                case 0x00: //GET KEYSTROKE
                case 0x10: //GET ENHANCED KEYSTROKE
                    {
                        var key = _pendingKey >= 0 ? _pendingKey : _stdin.ReadByte();
                        _pendingKey = -1;

                        //No scan codes from a byte stream; AH = 0 marks "ASCII only"
                        _registers.AX = (ushort)(key < 0 ? 0 : key);
                        break;
                    }
                case 0x01: //CHECK FOR KEYSTROKE
                case 0x11: //CHECK FOR ENHANCED KEYSTROKE
                    {
                        if (_pendingKey < 0)
                            _pendingKey = _stdin.ReadByte();

                        //ZF clear = key available (AX = key, left in the buffer)
                        _registers.ZeroFlag = _pendingKey < 0;
                        _registers.AX = (ushort)(_pendingKey < 0 ? 0 : _pendingKey);
                        break;
                    }
                case 0x02: //GET SHIFT FLAGS
                case 0x12: //GET EXTENDED SHIFT FLAGS
                    _registers.AX = 0;
                    break;
                default:
                    _logger.Warn($"Unsupported INT 16h Function: 0x{_registers.AH:X2}");
                    break;
            }
        }
    }
}

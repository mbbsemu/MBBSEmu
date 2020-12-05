using System;
using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;

namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     Interrupt Vector 1Ah which handles BIOS calls for Clock Information
    /// </summary>
    public class Int1Ah : IInterruptHandler
    {
        private readonly CpuRegisters _registers;
        private readonly IMemoryCore _memory;

        public ushort Vector => 0x1A;

        public Int1Ah(CpuRegisters registers, IMemoryCore memory)
        {
            _registers = registers;
            _memory = memory;
        }

        public void Handle()
        {
            switch (_registers.AH)
            {
                case 0x0:
                {
                        /*
                            INT 1A - AH = 00h CLOCK - GET TIME OF DAY
                            Return: CX:DX = clock count
                            AL = 0 if clock was read or written (via AH=0,1)
                             within the current 24-hour period
                             Otherwise, AL > 0

                            There are 18.2 Clock Ticker Per Second, so this is:
                            Number of Seconds Since Midnight * 18.2
                         */

                        var secondsSinceMidnight =
                            (uint)((DateTime.Now - new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day)).TotalSeconds * 18.2);

                        _registers.CX = (ushort) (secondsSinceMidnight & 0xFFFF);
                        _registers.DX = (ushort)(secondsSinceMidnight >> 8);
                        break;
                }

                default:
                    throw new ArgumentOutOfRangeException($"Unsupported Int {Vector:X2} Function: 0x{_registers.AH:X2}");
            }
        }
    }
}

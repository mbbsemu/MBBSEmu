using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;

namespace MBBSEmu.CPU.Interrupts
{
    public class Int21h : IInterruptHandler
    {
        private CpuRegisters _registers;
        private IMemoryCore _memory;

        /// <summary>
        ///     INT 21h defined Disk Transfer Area
        ///
        ///     Buffer used to hold information on the current Disk / IO operation
        /// </summary>
        private IntPtr16 DiskTransferArea;

        public Int21h(CpuRegisters registers, IMemoryCore memory)
        {
            _registers = registers;
            _memory = memory;
        }

        public void Handle()
        {
            switch (_registers.AH)
            {
                case 0x19:
                    {
                        //DOS - GET DEFAULT DISK NUMBER
                        //Return: AL = Drive Number
                        _registers.AL = 2; //C:
                        return;
                    }
                case 0x1A:
                    {
                        //Specifies the memory area to be used for subsequent FCB operations.
                        //DS:DX = Segment:offset of DTA
                        DiskTransferArea = new IntPtr16(_registers.DS, _registers.DX);
                        return;
                    }
                case 0x2A:
                    {
                        //DOS - GET CURRENT DATE
                        //Return: DL = day, DH = month, CX = year
                        //AL = day of the week(0 = Sunday, 1 = Monday, etc.)
                        _registers.DL = (byte)DateTime.Now.Day;
                        _registers.DH = (byte)DateTime.Now.Month;
                        _registers.CX = (ushort)DateTime.Now.Year;
                        _registers.AL = (byte)DateTime.Now.DayOfWeek;
                        return;
                    }
                case 0x2F:
                    {
                        //Get DTA address
                        /*
                         *  Action:	Returns the segment:offset of the current DTA for read/write operations.
                            On entry:	AH = 2Fh
                            Returns:	ES:BX = Segment.offset of current DTA
                         */
                        if (DiskTransferArea == null && !_memory.TryGetVariablePointer("Int21h-DTA", out DiskTransferArea))
                            DiskTransferArea = _memory.AllocateVariable("Int21h-DTA", 0xFF);

                        _registers.ES = DiskTransferArea.Segment;
                        _registers.BX = DiskTransferArea.Offset;
                        return;
                    }
                case 0x47:
                    {
                        /*
                            DOS 2+ - GET CURRENT DIRECTORY
                            DL = drive (0=default, 1=A, etc.)
                            DS:DI points to 64-byte buffer area
                            Return: CF set on error
                            AX = error code
                            Note: the returned path does not include the initial backslash
                         */
                        _memory.SetArray(_registers.DS, _registers.SI, Encoding.ASCII.GetBytes("BBSV6\\\0"));
                        _registers.AX = 0;
                        _registers.DL = 0;
                        _registers.F = _registers.F.ClearFlag((ushort)EnumFlags.CF);
                        return;
                    }
                case 0x62:
                    {
                        /*
                            INT 21 - AH = 62h DOS 3.x - GET PSP ADDRESS
                            Return: BX = segment address of PSP
                            We allocate 0xFFFF to ensure it has it's own segment in memory
                         */
                        if (!_memory.TryGetVariablePointer("INT21h-PSP", out var pspPointer))
                            pspPointer = _memory.AllocateVariable("Int21h-PSP", 0xFFFF);

                        _registers.BX = pspPointer.Segment;
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: 0x{_registers.AH:X2}");
            }
        }
    }
}

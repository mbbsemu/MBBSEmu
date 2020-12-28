using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     Interrupt Vector 21h which handles the main DOS APIs
    ///
    ///     This is implemented within the DOS Kernel
    /// </summary>
    public class Int21h : IInterruptHandler
    {
        private CpuRegisters _registers { get; init; }
        private IMemoryCore _memory { get; init; }
        private IClock _clock { get; init; }

        /// <summary>
        ///     INT 21h defined Disk Transfer Area
        ///
        ///     Buffer used to hold information on the current Disk / IO operation
        /// </summary>
        private FarPtr DiskTransferArea;

        public byte Vector => 0x21;

        private readonly Dictionary<byte, FarPtr> _interruptVectors;

        public Int21h(CpuRegisters registers, IMemoryCore memory, IClock clock)
        {
            _registers = registers;
            _memory = memory;
            _clock = clock;
            _interruptVectors = new Dictionary<byte, FarPtr>();
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
                        DiskTransferArea = new FarPtr(_registers.DS, _registers.DX);
                        return;
                    }
                case 0x25:
                    {
                        /*
                            INT 21 - AH = 25h DOS - SET INTERRUPT VECTOR
                            AL = interrupt number
                            DS:DX = new vector to be used for specified interrupt
                         */

                        var interruptVector = _registers.AL;
                        var newVectorPointer = new FarPtr(_registers.DS, _registers.DX);

                        _interruptVectors[interruptVector] = newVectorPointer;

                        return;
                    }
                case 0x2A:
                    {
                        //DOS - GET CURRENT DATE
                        //Return: DL = day, DH = month, CX = year
                        //AL = day of the week(0 = Sunday, 1 = Monday, etc.)
                        _registers.DL = (byte)_clock.Now.Day;
                        _registers.DH = (byte)_clock.Now.Month;
                        _registers.CX = (ushort)_clock.Now.Year;
                        _registers.AL = (byte)_clock.Now.DayOfWeek;
                        return;
                    }
                case 0x2C:
                    {
                        //DOS - GET CURRENT TIME
                        //Return: CH = hour, CL = minute, DH = second, DL = 1/100 seconds
                        _registers.CH = (byte) _clock.Now.Hour;
                        _registers.CL = (byte) _clock.Now.Minute;
                        _registers.DH = (byte) _clock.Now.Second;
                        _registers.DL = (byte) (_clock.Now.Millisecond / 100);
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
                        DiskTransferArea = _memory.GetOrAllocateVariablePointer("Int21h-DTA", 0xFF);

                        _registers.ES = DiskTransferArea.Segment;
                        _registers.BX = DiskTransferArea.Offset;
                        return;
                    }
                case 0x30:
                    {
                        /*  DOS 2+ - GET DOS VERSION
                            AH = 30h
                            Return: AL = Major Version number (0 for DOS 1.x)
                            AH = Minor Version number
                            BH = OEM number
                             00h IBM
                             16h DEC
                            BL:CX = 24-bit user number
                         */
                        _registers.AL = 6;
                        _registers.AH = 22;
                        return;

                    }
                case 0x35:
                    {
                        /*
                           INT 21 - AH = 35h DOS 2+ - GET INTERRUPT VECTOR
                           AL = interrupt number
                           Return: ES:BX = value of interrupt vector
                         */

                        if (!_interruptVectors.TryGetValue(_registers.AL, out var resultVector))
                        {
                            _registers.ES = 0xFFFF;
                            _registers.BX = _registers.AL;
                        }
                        else
                        {
                            _registers.ES = resultVector.Segment;
                            _registers.BX = resultVector.Offset;
                        }
                        return;

                    }
                case 0x40:
                    {
                        /*
                          INT 21 - AH = 40h DOS 2+ - WRITE TO FILE WITH HANDLE
                            BX = file handle
                            CX = number of bytes to write
                            DS:DX -> buffer

                            Return: CF set on error
                             AX = error code

                            CF clear if successful
                             AX = number of bytes written

                            Note: if CX is zero, no data is written, and the file is truncated or extended
                             to the current position
                         */
                        var fileHandle = _registers.BX;
                        var numberOfBytes = _registers.CX;
                        var bufferPointer = new FarPtr(_registers.DS, _registers.DX);

                        var dataToWrite = _memory.GetArray(bufferPointer, numberOfBytes);

                        /*
                             DOS Default/Predefined Handles:
                             0 - Standard Input Device - can be redirected (STDIN)
	                         1 - Standard Output Device - can be redirected (STDOUT)
	                         2 - Standard Error Device - can be redirected (STDERR)
	                         3 - Standard Auxiliary Device (STDAUX)
	                         4 - Standard Printer Device (STDPRN)
                         */

                        if (fileHandle == 1 || fileHandle == 2)
                            Console.WriteLine(Encoding.ASCII.GetString(dataToWrite));

                        break;
                    }
                case 0x44:
                    {
                        /*
                            INT 21 - AH = 44H DOS Get Device Information

                            Sub-Function Definition is in AL
                         */
                        switch (_registers.AL)
                        {

                            case 0x0:
                                {
                                    /*
                                        INT 21 - AX = 4400h DOS 2+ - IOCTL - GET DEVICE INFORMATION
                                        BX = file or device handle
                                        Return: CF set on error
                                         AX = error code
                                        CF clear if successful
                                         DX = device info
                                     */

                                    //Device
                                    if (_registers.BX <= 2)
                                    {
                                        _registers.DX = 0;

                                        _registers.DX |= 1; //STD Input
                                        _registers.DX |= 1 << 1; //STD Output
                                        _registers.DX |= 1 << 4; //Reserved? DOSBox sets it
                                        _registers.DX |= 1 << 6; //Not EOF
                                        _registers.DX |= 1 << 7; //IS Device
                                        _registers.DX |= 1 << 15; //Reserved? DOSBox sets it
                                    }
                                }
                                break;
                        }

                        break;
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
                case 0x4A:
                    {
                        /*
                            INT 21 - AH = 4Ah DOS 2+ - ADJUST MEMORY BLOCK SIZE (SETBLOCK)
                            ES = Segment address of block to change
                            BX = New size in paragraphs
                            Return: CF set on error
                            AX = error code
                            BX = maximum size possible for the block

                            Because MBBSEmu allocates blocks as 0xFFFF in length, we ignore this and proceed
                         */

                        var segmentToAdjust = _registers.ES;
                        var newSize = _registers.BX;

                        if (!_memory.HasSegment(segmentToAdjust))
                            _memory.AddSegment(segmentToAdjust);

                        _registers.BX = 0xFFFF;
                        break;
                    }
                case 0x4C:
                    {
                        /*
                            INT 21 - AH = 4Ch DOS 2+ - QUIT WITH EXIT CODE (EXIT)
                            AL = exit code
                            Return: never returns
                         */
                        Console.WriteLine($"Exiting With Exit Code: {_registers.AL}");
                        _registers.Halt = true;
                        break;
                    }
                case 0x62:
                    {
                        /*
                            INT 21 - AH = 62h DOS 3.x - GET PSP ADDRESS
                            Return: BX = segment address of PSP

                            This is only set when an EXE is running, thus should only be called from
                            an EXE.
                         */
                        if (!_memory.TryGetVariablePointer("Int21h-PSP", out var pspPointer))
                            throw new Exception("No PSP has been defined");

                        _registers.BX = _memory.GetWord(pspPointer);
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: 0x{_registers.AH:X2}");
            }
        }
    }
}

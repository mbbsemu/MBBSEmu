using Iced.Intel;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MBBSEmu.CPU
{
    public class CpuCore : ICpuCore
    {
        protected static readonly ILogger _logger;

        public delegate ReadOnlySpan<byte> InvokeExternalFunctionDelegate(ushort importedNameTableOrdinal, ushort functionOrdinal);

        private InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public CpuRegisters Registers { get; set; }
        private IMemoryCore Memory { get; set; }

        private Instruction _currentInstruction { get; set; }

        //Debug Pointers

        /// <summary>
        ///     Current Location of the Instruction Pointer
        /// </summary>
        private IntPtr16 _currentInstructionPointer { get; set; }

        /// <summary>
        ///     Previous Location of the Instruction Pointer
        /// </summary>
        private IntPtr16 _previousInstructionPointer { get; set; }

        /// <summary>
        ///     Previous Location of a CALL into the current function
        /// </summary>
        private IntPtr16 _previousCallPointer { get; set; }

        private bool _showDebug;

        private ushort EXTRA_SEGMENT;

        /// <summary>
        ///     Default offset for the Stack Base
        /// </summary>
        public const ushort STACK_BASE = 0xFFFF;

        /// <summary>
        ///     Default segment for the Stack
        /// </summary>
        public const ushort STACK_SEGMENT = 0x0;

        public readonly List<byte[]> FpuStack = new List<byte[]>(8) { new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4] };

        private int _currentOperationSize = 0;

        public long InstructionCounter = 0;

        private IntPtr16 DiskTransferArea;

        static CpuCore()
        {
            _logger = ServiceResolver.GetService<ILogger>();
        }

        public CpuCore() { }

        /// <summary>
        ///     Resets the CPU back to a starting state
        /// </summary>
        /// <param name="memoryCore"></param>
        /// <param name="cpuRegisters"></param>
        /// <param name="invokeExternalFunctionDelegate"></param>
        public void Reset(IMemoryCore memoryCore, CpuRegisters cpuRegisters,
            InvokeExternalFunctionDelegate invokeExternalFunctionDelegate)
        {
            //Setup Debug Pointers
            _currentInstructionPointer = IntPtr16.Empty;
            _previousInstructionPointer = IntPtr16.Empty;
            _previousCallPointer = IntPtr16.Empty;

            //Setup Delegate Call   
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;

            //Setup Memory Space
            Memory = memoryCore;
            EXTRA_SEGMENT = ushort.MaxValue;

            //Setup Registers
            Registers = cpuRegisters;
            Registers.BP = STACK_BASE;
            Registers.SP = STACK_BASE;
            Registers.SS = STACK_SEGMENT;
            Registers.ES = EXTRA_SEGMENT;
            Registers.Halt = false;

            //These two values are the final values popped off on the routine's last RETF
            //Seeing a ushort.max for CS and IP tells the routine it's now done
            Push(ushort.MaxValue);
            Push(ushort.MaxValue);
        }

        public void Reset() => Reset(STACK_BASE);

        /// <summary>
        ///     Only Resets Register Values
        /// </summary>
        public void Reset(ushort stackBase)
        {
            EXTRA_SEGMENT = ushort.MaxValue;

            //Setup Registers
            Registers.BP = stackBase;
            Registers.SP = stackBase;
            Registers.SS = STACK_SEGMENT;
            Registers.ES = EXTRA_SEGMENT;
            Registers.Halt = false;

            //These two values are the final values popped off on the routine's last RETF
            //Seeing a ushort.max for CS and IP tells the routine it's now done
            Push(ushort.MaxValue);
            Push(ushort.MaxValue);

            InstructionCounter = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Pop()
        {
            var value = Memory.GetWord(Registers.SS, (ushort)(Registers.SP + 1));
#if DEBUG
            if(_showDebug)
                _logger.Info($"Popped {value:X4} from {Registers.SP+1:X4}");
#endif
            Registers.SP += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ushort value)
        {
            Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), value);

#if DEBUG
            if(_showDebug)
                _logger.Info($"Pushed {value:X4} to {Registers.SP - 1:X4}");
#endif
            Registers.SP -= 2;
        }

        public void Tick()
        {
            //Check for segment end
            if ((Registers.CS == ushort.MaxValue || Registers.CS == 0) && (Registers.IP == ushort.MaxValue || Registers.IP == 0))
            {
                Registers.Halt = true;
                return;
            }

            _currentInstructionPointer.Offset = Registers.IP;
            _currentInstructionPointer.Segment = Registers.CS;

            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);
            Registers.IP = _currentInstruction.IP16;
            _currentOperationSize = GetCurrentOperationSize();

#if DEBUG

            if(Registers.CS == 10 && Registers.IP == 0x383)
              Debugger.Break();

            if (Registers.CS == 16 && ((Registers.IP >= 0x1A-7 && Registers.IP <= 0x1B6A)))
            {

                _showDebug = true;
            }
            else
            {
                _showDebug = false;
                //bShowDebug = Registers.CS == 0x6 && Registers.IP >= 0x7B && Registers.IP <= 0x86;
            }

            if (_showDebug)
                _logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction.ToString()}");
#endif
            InstructionCounter++;

            //Jump Table
            switch (_currentInstruction.Mnemonic)
            {
                //Instructions that will set the IP -- we just return
                case Mnemonic.Retf:
                    Op_Retf();
                    return;
                case Mnemonic.Ret:
                    Op_Ret();
                    return;
                case Mnemonic.Je:
                    Op_Je();
                    return;
                case Mnemonic.Jne:
                    Op_Jne();
                    return;
                case Mnemonic.Jmp:
                    Op_Jmp();
                    return;
                case Mnemonic.Jb:
                    Op_Jb();
                    return;
                case Mnemonic.Jl:
                    Op_Jl();
                    return;
                case Mnemonic.Jg:
                    Op_Jg();
                    return;
                case Mnemonic.Jge:
                    Op_Jge();
                    return;
                case Mnemonic.Jle:
                    Op_Jle();
                    return;
                case Mnemonic.Jbe:
                    Op_Jbe();
                    return;
                case Mnemonic.Ja:
                    Op_Ja();
                    return;
                case Mnemonic.Jae:
                    Op_Jae();
                    return;
                case Mnemonic.Call:
                    Op_Call();
                    return;
                case Mnemonic.Loop:
                    Op_Loop();
                    return;
                case Mnemonic.Jcxz:
                    Op_Jcxz();
                    return;
                case Mnemonic.Loope:
                    Op_Loope();
                    break;

                //Instructions that do not set IP -- we'll just increment
                case Mnemonic.Clc:
                    Op_Clc();
                    break;
                case Mnemonic.Cwd:
                    Op_Cwd();
                    break;
                case Mnemonic.Neg:
                    Op_Neg();
                    break;
                case Mnemonic.Sbb:
                    Op_Sbb();
                    break;
                case Mnemonic.Add:
                    Op_Add();
                    break;
                case Mnemonic.Adc:
                    Op_Add(addCarry: true);
                    break;
                case Mnemonic.And:
                    Op_And();
                    break;
                case Mnemonic.Imul:
                    Op_Imul();
                    break;
                case Mnemonic.Idiv:
                    Op_Idiv();
                    break;
                case Mnemonic.Push:
                    Op_Push();
                    break;
                case Mnemonic.Pop:
                    Op_Pop();
                    break;
                case Mnemonic.Mov:
                    Op_Mov();
                    break;
                case Mnemonic.Cmp:
                    Op_Cmp();
                    break;
                case Mnemonic.Sub:
                    Op_Sub();
                    break;
                case Mnemonic.Xor:
                    Op_Xor();
                    break;
                case Mnemonic.Inc:
                    Op_Inc();
                    break;
                case Mnemonic.Dec:
                    Op_Dec();
                    break;
                case Mnemonic.Stosw:
                    Op_Stosw();
                    break;
                case Mnemonic.Wait:
                case Mnemonic.Nop:
                    break;
                case Mnemonic.Enter:
                    Op_Enter();
                    break;
                case Mnemonic.Leave:
                    Op_Leave();
                    break;
                case Mnemonic.Lea:
                    Op_Lea();
                    break;
                case Mnemonic.Shl:
                    Op_Shl();
                    break;
                case Mnemonic.Sar:
                    Op_Sar();
                    break;
                case Mnemonic.Shr:
                    Op_Shr();
                    break;
                case Mnemonic.Rcr:
                    Op_Rcr();
                    break;
                case Mnemonic.Or:
                    Op_Or();
                    break;
                case Mnemonic.Les:
                    Op_Les();
                    break;
                case Mnemonic.Lds:
                    Op_Lds();
                    break;
                case Mnemonic.Int:
                    Op_Int();
                    break;
                case Mnemonic.Test:
                    Op_Test();
                    break;
                case Mnemonic.Fld:
                    Op_Fld();
                    break;
                case Mnemonic.Fmul:
                    Op_Fmul();
                    break;
                case Mnemonic.Fstp:
                    Op_Fstp();
                    break;
                case Mnemonic.Fcompp:
                    Op_Fcompp();
                    break;
                case Mnemonic.Fild:
                    Op_Fild();
                    break;
                case Mnemonic.Fnstsw:
                case Mnemonic.Fstsw:
                    Op_Fstsw();
                    break;
                case Mnemonic.Sahf:
                    Op_Sahf();
                    break;
                case Mnemonic.Rcl:
                    Op_Rcl();
                    break;
                case Mnemonic.Not:
                    Op_Not();
                    break;
                case Mnemonic.Xchg:
                    Op_Xchg();
                    break;
                case Mnemonic.Div:
                    Op_Div();
                    break;
                case Mnemonic.Fsub:
                    Op_Fsub();
                    break;
                case Mnemonic.Fldz:
                    Op_Fldz();
                    break;
                case Mnemonic.Fnstcw:
                case Mnemonic.Fstcw:
                    Op_Fstcw();
                    break;
                case Mnemonic.Fldcw:
                    Op_Fldcw();
                    break;
                case Mnemonic.Fistp:
                    Op_Fistp();
                    break;
                case Mnemonic.Faddp:
                    Op_Faddp();
                    break;
                case Mnemonic.Cli:
                    Op_Cli();
                    break;
                case Mnemonic.Sti:
                    Op_Sti();
                    break;
                case Mnemonic.Fst:
                    Op_Fst();
                    break;
                case Mnemonic.Cbw:
                    Op_Cbw();
                    break;
                case Mnemonic.Cld:
                    Op_Cld();
                    break;
                case Mnemonic.Lodsb:
                    Op_Lodsb();
                    break;
                case Mnemonic.Stosb:
                    Op_Stosb();
                    break;
                case Mnemonic.Scasb:
                    Op_Scasb();
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

            _previousInstructionPointer.Offset = Registers.IP;
            _previousInstructionPointer.Segment = Registers.CS;

            Registers.IP += (ushort)_currentInstruction.Length;

#if DEBUG
            if (_showDebug)
                _logger.InfoRegisters(this);
#endif

        }

        /// <summary>
        ///     Returns the VALUE of Operand 0
        ///     Only use this for instructions where Operand 0 is a VALUE, not an address target
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        private byte GetOperandValueUInt8(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    {
                        //for ops like imul bx, etc. where AX is the implicit destination
                        if (operandType == EnumOperandType.Source && _currentInstruction.Op1Register == Register.None)
                        {
                            return Registers.AL;
                        }

                        return (byte)Registers.GetValue(operandType == EnumOperandType.Destination
                            ? _currentInstruction.Op0Register
                            : _currentInstruction.Op1Register);
                    }
                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;
                case OpKind.Memory:
                    {
                        //Relocation Record is applied to the offset of the value
                        //Because of this, we return out
                        var offset = GetOperandOffset(opKind);

                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.UInt8:
                                return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), offset);
                            default:
                                throw new Exception("Type Requested not UInt8");
                        }

                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown UInt8 Operand: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the VALUE of Operand 0
        ///     Only use this for instructions where Operand 0 is a VALUE, not an address target
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        private ushort GetOperandValueUInt16(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    {
                        //for ops like imul bx, etc. where AX is the implicit destination
                        if (operandType == EnumOperandType.Source && _currentInstruction.Op1Register == Register.None)
                        {
                            return Registers.AX;
                        }

                        return Registers.GetValue(operandType == EnumOperandType.Destination
                            ? _currentInstruction.Op0Register
                            : _currentInstruction.Op1Register);
                    }
                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;
                case OpKind.Immediate16:
                    return _currentInstruction.Immediate16;
                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;
                case OpKind.Memory:
                    {
                        //Relocation Record is applied to the offset of the value
                        //Because of this, we return out
                        var offset = GetOperandOffset(opKind);

                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Int16:
                            case MemorySize.UInt16:
                                return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offset);
                            default:
                                throw new Exception("Type Requested not UInt16");
                        }

                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown UInt16 Operand: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the VALUE of Operand 0
        ///     Only use this for instructions where Operand 0 is a VALUE, not an address target
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        private uint GetOperandValueUInt32(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Immediate8to32:
                    return (ushort)_currentInstruction.Immediate8to32;
                case OpKind.Immediate32:
                    return _currentInstruction.Immediate32;
                case OpKind.Memory:
                    {
                        //Relocation Record is applied to the offset of the value
                        //Because of this, we return out
                        var offset = GetOperandOffset(opKind);

                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.UInt32:
                                return BitConverter.ToUInt32(
                                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), offset, 4));
                            default:
                                throw new Exception("Type Requested not UInt32");
                        }

                    }

                default:
                    throw new Exception("Type Requested not UInt32");
            }
        }


        /// <summary>
        ///     Returns the OFFSET of Operand 0
        ///
        ///     Only use this for instructions where Operand 0 is an OFFSET within a memory Segment
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        private ushort GetOperandOffset(OpKind opKind)
        {
            ushort result;
            switch (opKind)
            {
                case OpKind.Memory:
                    {
                        switch (_currentInstruction.MemoryBase)
                        {
                            case Register.DS when _currentInstruction.MemoryIndex == Register.None:
                            case Register.None when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)_currentInstruction.MemoryDisplacement;
                                break;
                            case Register.BP when _currentInstruction.MemoryIndex == Register.None:
                            {

                                result = (ushort) (Registers.BP + _currentInstruction.MemoryDisplacement + 1);
                                break;
                            }

                            case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                            {

                                result = (ushort) (Registers.BP + Registers.SI +
                                                   _currentInstruction.MemoryDisplacement + 1);
                                break;
                            }

                            case Register.BP when _currentInstruction.MemoryIndex == Register.DI:
                            {
                                result = (ushort) (Registers.BP + Registers.DI +
                                                   _currentInstruction.MemoryDisplacement + 1);
                                break;
                            }

                            case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.BX + _currentInstruction.MemoryDisplacement);
                                break;
                            case Register.BX when _currentInstruction.MemoryIndex == Register.SI:
                                result = (ushort)(Registers.BX + _currentInstruction.MemoryDisplacement + Registers.SI);
                                break;
                            case Register.SI when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.SI + _currentInstruction.MemoryDisplacement);
                                break;
                            case Register.DI when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.DI + _currentInstruction.MemoryDisplacement);
                                break;
                            default:
                                throw new Exception("Unknown GetOperandOffset MemoryBase");
                        }
                    }
                    break;
                case OpKind.NearBranch16:
                    result = _currentInstruction.NearBranch16;
                    break;
                default:
                    throw new Exception($"Unknown OpKind for GetOperandOffset: {opKind}");
            }

            return result;
        }

        /// <summary>
        ///     Returns if the Current Instruction is an 8-bit or 16-bit operation
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCurrentOperationSize()
        {
            return _currentInstruction.Op0Kind switch
            {
                OpKind.Register => _currentInstruction.Op0Register.GetSize(),
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt8 || _currentInstruction.MemorySize == MemorySize.Int8 => 1,
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt16 || _currentInstruction.MemorySize == MemorySize.Int16 => 2,
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt32 || _currentInstruction.MemorySize == MemorySize.Int32 => 4,
                _ => -1
            };
        }

        /// <summary>
        ///     Saves the specified result of the current instruction into the current instructions destination
        /// </summary>
        /// <param name="result"></param>
        /// <param name="operationSize"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToDestination(ushort result)
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register, result);
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 1:
                    {
                        Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind), (byte)result);
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 2:
                    {
                        Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind), result);
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown Destination: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToSource(ushort result)
        {
            switch (_currentInstruction.Op1Kind)
            {
                case OpKind.Register:
                    {
                        Registers.SetValue(_currentInstruction.Op1Register, result);
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 1:
                    {
                        Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op1Kind), (byte)result);
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 2:
                    {
                        Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op1Kind), result);
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown Source: {_currentInstruction.Op0Kind}");
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Loop()
        {
            Registers.CX--;

            if (Registers.CX == 0)
            {
                //Continue with the next instruction
                Registers.IP += (ushort)_currentInstruction.Length;
                return;
            }

            Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Int()
        {
            switch (_currentInstruction.Immediate8)
            {
                case 0x21:
                    Op_Int_21h();
                    return;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown INT: {_currentInstruction.Immediate8:X2}");
            }
        }

        private void Op_Int_21h()
        {
            switch (Registers.AH)
            {
                case 0x19:
                    {
                        //DOS - GET DEFAULT DISK NUMBER
                        //Return: AL = Drive Number
                        Registers.AL = 2; //C:
                        return;
                    }
                case 0x1A:
                    {
                        //Specifies the memory area to be used for subsequent FCB operations.
                        //DS:DX = Segment:offset of DTA
                        DiskTransferArea = new IntPtr16(Registers.DS, Registers.DX);
                        return;
                    }
                case 0x2A:
                    {
                        //DOS - GET CURRENT DATE
                        //Return: DL = day, DH = month, CX = year
                        //AL = day of the week(0 = Sunday, 1 = Monday, etc.)
                        Registers.DL = (byte)DateTime.Now.Day;
                        Registers.DH = (byte)DateTime.Now.Month;
                        Registers.CX = (ushort)DateTime.Now.Year;
                        Registers.AL = (byte)DateTime.Now.DayOfWeek;
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
                        if (DiskTransferArea == null && !Memory.TryGetVariablePointer("Int21h-DTA", out DiskTransferArea))
                            DiskTransferArea = Memory.AllocateVariable("Int21h-DTA", 0xFF);

                        Registers.ES = DiskTransferArea.Segment;
                        Registers.BX = DiskTransferArea.Offset;
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
                        Memory.SetArray(Registers.DS, Registers.SI, Encoding.ASCII.GetBytes("BBSV6\\\0"));
                        Registers.AX = 0;
                        Registers.DL = 0;
                        Registers.F.ClearFlag(EnumFlags.CF);
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: 0x{Registers.AH:X2}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cwd()
        {
            Registers.DX = Registers.AX.IsBitSet(15) ? (ushort)0xFFFF : (ushort)0x0000;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Neg()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Neg_8(),
                2 => Op_Neg_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Neg_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var result = (byte)(0 - destination);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Neg_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var result = (ushort)(0 - destination);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sbb()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Sbb_8(),
                2 => Op_Sbb_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Sbb_8()
        {
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                if (Registers.F.IsFlagSet(EnumFlags.CF))
                    source += 1;

                var result = (byte)(destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sbb_16()
        {
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                if (Registers.F.IsFlagSet(EnumFlags.CF))
                    source += 1;

                var result = (ushort)(destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Clc() => Registers.F.ClearFlag(EnumFlags.CF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Or()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Or_8(),
                2 => Op_Or_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Or_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Or_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Rcr()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Rcr_8(),
                2 => Op_Rcr_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Rcr_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            unchecked
            {
                var result = (byte)destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(0);

                    //Perform Rotation
                    result = (byte)(result >> 1);

                    //If CF was set, rotate that value in
                    if (Registers.F.IsFlagSet(EnumFlags.CF))
                        result.SetFlag(1 << 7);

                    //Set new CF Value
                    if (newCFValue)
                    {
                        Registers.F.SetFlag(EnumFlags.CF);
                    }
                    else
                    {
                        Registers.F.ClearFlag(EnumFlags.CF);
                    }
                }

                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Rcr_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(0);

                    //Perform Rotation
                    result = (ushort)(result >> 1);

                    //If CF was set, rotate that value in
                    if (Registers.F.IsFlagSet(EnumFlags.CF))
                        result.SetFlag(1 << 15);

                    //Set new CF Value
                    if (newCFValue)
                    {
                        Registers.F.SetFlag(EnumFlags.CF);
                    }
                    else
                    {
                        Registers.F.ClearFlag(EnumFlags.CF);
                    }
                }

                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Rcl()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Rcl_8(),
                2 => Op_Rcl_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Rcl_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(7);

                    //Perform Rotation
                    result = (byte)(result << 1);

                    //If CF was set, rotate that value in
                    if (Registers.F.IsFlagSet(EnumFlags.CF))
                        result.SetFlag(1);

                    //Set new CF Value
                    if (newCFValue)
                    {
                        Registers.F.SetFlag(EnumFlags.CF);
                    }
                    else
                    {
                        Registers.F.ClearFlag(EnumFlags.CF);
                    }
                }

                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Rcl_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(15);

                    //Perform Rotation
                    result = (ushort)(result << 1);

                    //If CF was set, rotate that value in
                    if (Registers.F.IsFlagSet(EnumFlags.CF))
                        result.SetFlag(1);

                    //Set new CF Value
                    if (newCFValue)
                    {
                        Registers.F.SetFlag(EnumFlags.CF);
                    }
                    else
                    {
                        Registers.F.ClearFlag(EnumFlags.CF);
                    }
                }

                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sar()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Sar_8(),
                2 => Op_Sar_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sar_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)(destination >> (sbyte)source);

                //Maintain Sign Bit
                if (destination.IsNegative())
                    result.SetFlag(1 << 7);

                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sar_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)(destination >> source);

                //Maintain Sign Bit
                if (destination.IsNegative())
                    result.SetFlag(1 << 15);

                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Shr()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Shr_8(),
                2 => Op_Shr_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Shr_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)(destination >> (sbyte)source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Shr_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)(destination >> source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Shl()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Shl_8(),
                2 => Op_Shl_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Shl_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)(destination << (sbyte)source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Shl_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)(destination << source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.ES = dataPointer.Segment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Lds()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.DS = dataPointer.Segment;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Lea()
        {
            var sourceOffset = GetOperandOffset(_currentInstruction.Op1Kind);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, sourceOffset);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported Destination for LEA: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Enter()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Immediate16:
                    Push(Registers.BP);
                    Registers.BP = Registers.SP;
                    Registers.SP -= (ushort)(_currentInstruction.Immediate16 + 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown ENTER: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Leave()
        {
            Registers.SP = Registers.BP;
            Registers.BP = Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Retf()
        {
            Registers.IP = Pop();
            Registers.CS = Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Ret()
        {
            Registers.IP = Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Stosw()
        {
            while (Registers.CX > 0)
            {
                Memory.SetWord(Registers.ES, Registers.DI, Registers.AX);
                Registers.DI += 2;
                Registers.CX--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Inc()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Inc_8(),
                2 => Op_Inc_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Inc_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (byte)(destination + 1);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, 1);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Inc_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (ushort)(destination + 1);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, 1);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Dec()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Dec_8(),
                2 => Op_Dec_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Dec_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (byte)(destination - 1);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Dec_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (ushort)(destination - 1);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_And()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_And_8(),
                2 => Op_And_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_And_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_And_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Xor()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Xor_8(),
                2 => Op_Xor_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Xor_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (byte)(destination ^ source);
            Registers.F.Evaluate(EnumFlags.ZF, result);
            Registers.F.Evaluate(EnumFlags.SF, result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Xor_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (ushort)(destination ^ source);
            Registers.F.Evaluate(EnumFlags.ZF, result);
            Registers.F.Evaluate(EnumFlags.SF, result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Ja()
        {
            if (!Registers.F.IsFlagSet(EnumFlags.CF) && !Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jae()
        {
            if (!Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jbe()
        {
            if (Registers.F.IsFlagSet(EnumFlags.ZF) || Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jmp()
        {
            if (_currentInstruction.FlowControl == FlowControl.IndirectBranch)
            {
                //Get the destination offset
                var offsetToDestinationValue = GetOperandOffset(_currentInstruction.Op0Kind);

                if (_currentInstruction.IsJmpNearIndirect)
                {
                    var destinationOffset = Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offsetToDestinationValue);
                    Registers.IP = destinationOffset;
                    return;
                }

                if (_currentInstruction.IsJmpFarIndirect)
                {
                    var destinationPointerData = Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                        offsetToDestinationValue, 4);

                    var destinationPointer = new IntPtr16(destinationPointerData);

                    Registers.IP = destinationPointer.Offset;
                    Registers.CS = destinationPointer.Segment;
                    return;
                }

                throw new Exception($"Unhandled Indirect Jump in x86 Core: {_currentInstruction}");

            }

            //Near Jumps
            Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);

            if (_currentInstruction.IsJmpFar || _currentInstruction.IsJmpFarIndirect)
                Registers.CS = Registers.GetValue(_currentInstruction.MemorySegment);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jle()
        {
            //ZF == 1 OR SF <> OF
            if (Registers.F.IsFlagSet(EnumFlags.SF) != Registers.F.IsFlagSet(EnumFlags.OF)
                || Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jge()
        {
            // SF == OF
            if (Registers.F.IsFlagSet(EnumFlags.SF) == Registers.F.IsFlagSet(EnumFlags.OF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jne()
        {
            //ZF == 0
            if (!Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jg()
        {
            //ZF == 0 & SF == OF
            if (!Registers.F.IsFlagSet(EnumFlags.ZF) &&
                (Registers.F.IsFlagSet(EnumFlags.SF) == Registers.F.IsFlagSet(EnumFlags.OF)))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Je()
        {
            // ZF == 1
            if (Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jl()
        {
            //SF <> OF
            if (Registers.F.IsFlagSet(EnumFlags.SF) != Registers.F.IsFlagSet(EnumFlags.OF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jb()
        {
            //CF == 1
            if (Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Test()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Test_8();
                    break;
                case 2:
                    Op_Test_16();
                    break;
                default:
                    throw new Exception("Unsupported Operation Size");
            }

            //Clear Overflow & Carry
            Registers.F.Flags.ClearFlag((ushort)EnumFlags.OF);
            Registers.F.Flags.ClearFlag((ushort)EnumFlags.CF);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Test_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Test_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
        }

        /// <summary>
        ///     Identical to SUB, just doesn't save result of subtraction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cmp()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Sub_8();
                    return;
                case 2:
                    Op_Sub_16();
                    return;
                case 4:
                    Op_Sub_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sub()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Sub_8(),
                2 => Op_Sub_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Sub_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)(destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sub_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)(destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint Op_Sub_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);
                return result;
            }
        }

        /// <summary>
        ///     ADD and ADC are exactly the same, except that ADC also adds 1 if the carry flag is set
        ///     So we don't repeat the function, we just pass the ADC flag
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Add(bool addCarry = false)
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Add_8(addCarry),
                2 => Op_Add_16(addCarry),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Add_8(bool addCarry)
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                if (addCarry && Registers.F.IsFlagSet(EnumFlags.CF))
                    source++;

                var result = (byte)(source + destination);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Add_16(bool addCarry)
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                if (addCarry && Registers.F.IsFlagSet(EnumFlags.CF))
                    source++;

                var result = (ushort)(source + destination);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate(EnumFlags.SF, result);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Imul()
        {
            switch (_currentInstruction.OpCount)
            {
                case 1:
                    Op_Imul_1operand();
                    return;
                case 2:
                    throw new NotImplementedException("IMUL with 2 Opcodes not implemented");
                case 3:
                    Op_Imul_3operand();
                    return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Imul_1operand()
        {
            var operand2 = Registers.AX;
            var operand3 = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };
            ushort result;
            unchecked
            {
                result = (ushort)(operand2 * operand3);
            }

            Registers.AX = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Imul_3operand()
        {
            var operand2 = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };

            var operand3 = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op2Kind, EnumOperandType.Source),
                2 => GetOperandValueUInt16(_currentInstruction.Op2Kind, EnumOperandType.Source),
                _ => throw new Exception("Unsupported Operation Size")
            };
            ushort result;
            unchecked
            {
                result = (ushort)(operand2 * operand3);
            }

            Registers.SetValue(_currentInstruction.Op0Register, result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Idiv()
        {

            switch (GetCurrentOperationSize())
            {
                case 1:
                    Op_Idiv_8();
                    return;
                case 2:
                    Op_Idiv_16();
                    return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Idiv_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var quotient = Math.DivRem(Registers.AX, destination, out var remainder);
                Registers.AL = (byte)quotient;
                Registers.AH = (byte)remainder;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Idiv_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var quotient = Math.DivRem(Registers.GetLong(Register.DX, Register.AX), destination, out var remainder);
                Registers.AX = (ushort)quotient;
                Registers.DX = (ushort)remainder;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Pop()
        {
            var popValue = Pop();
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, popValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown POP: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Push Op Code
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Push()
        {
            switch (_currentInstruction.StackPointerIncrement)
            {
                //8Bit
                case -2 when _currentOperationSize == 1:
                    Push(GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination));
                    return;

                //166bit, also default size (-1)
                case -2 when _currentOperationSize == -1:
                case -2 when _currentOperationSize == 2:
                    Push(GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination));
                    return;

                //32Bit
                case -4:
                    Push((ushort)(_currentInstruction.Immediate8to32 >> 16));
                    Push((ushort)_currentInstruction.Immediate8to32);
                    return;
                default:
                    throw new Exception(
                        $"Unsupported Stack Pointer Increment: {_currentInstruction.StackPointerIncrement}");
            }
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Mov()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentOperationSize == 1:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register,
                            GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source));
                        return;
                    }
                case OpKind.Register when _currentOperationSize == 2:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register,
                            GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source));
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 1:
                    {
                        Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source));
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 2:
                    {
                        Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source));
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 4:
                    {
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            BitConverter.GetBytes(GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source)));
                        return;
                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown MOV: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Call()
        {
            _previousCallPointer.Segment = Registers.CS;
            _previousCallPointer.Offset = Registers.IP;

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Memory when _currentInstruction.IsCallFarIndirect:
                    {
                        //Far call with target offset at memory location
                        Push(Registers.CS);
                        Push((ushort)(Registers.IP + _currentInstruction.Length));

                        var offset = GetOperandOffset(OpKind.Memory);
                        var pointer = new IntPtr16(Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), offset, 4));
                        Registers.CS = pointer.Segment;
                        Registers.IP = pointer.Offset;
                        break;
                    }
                case OpKind.FarBranch16 when _currentInstruction.FarBranchSelector <= 0xFF:
                    {
                        //Far call to another Segment
                        Push(Registers.CS);
                        Push((ushort)(Registers.IP + _currentInstruction.Length));

                        Registers.CS = _currentInstruction.FarBranchSelector;
                        Registers.IP = _currentInstruction.FarBranch16;
                        break;
                    }
                case OpKind.FarBranch16 when _currentInstruction.FarBranchSelector > 0xFF00:
                    {
                        //We push CS:IP to the stack
                        Push(Registers.CS);
                        Push((ushort)(Registers.IP + _currentInstruction.Length));

                        //Simulate an ENTER
                        //Set BP to the current stack pointer
                        Push(Registers.BP);
                        Registers.BP = Registers.SP;

                        var ipBeforeCall = Registers.IP;

                        _invokeExternalFunctionDelegate(_currentInstruction.FarBranchSelector,
                            _currentInstruction.Immediate16);

                        //Control Transfer Occured in the CALL, so we clean up the stack and return
                        if (ipBeforeCall != Registers.IP)
                            return;

                        //Simulate a LEAVE & retf
                        Registers.SP = Registers.BP;
                        Registers.SetValue(Register.BP, Pop());
                        Registers.SetValue(Register.EIP, Pop());
                        Registers.SetValue(Register.CS, Pop());
                        break;
                    }
                case OpKind.NearBranch16:
                    {
                        //We push CS:IP to the stack
                        //Push the IP of the **NEXT** instruction to the stack
                        Push((ushort)(Registers.IP + _currentInstruction.Length));
                        Registers.IP = _currentInstruction.NearBranch16;
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }

        /// <summary>
        ///     Floating Point Load Operation (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fld()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var floatToLoad = Memory.GetArray(Registers.DS, offset, 4);
            FpuStack[Registers.Fpu.GetStackTop()] = floatToLoad.ToArray();
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Integer Load Operation (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fild()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var intToLoad = Memory.GetArray(Registers.DS, offset, 2);

            var convertedInt = Convert.ToSingle(BitConverter.ToInt16(intToLoad));

            FpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(convertedInt);
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Floating Point Multiplication (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fmul()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var floatToMultiply = Memory.GetArray(Registers.DS, offset, 4);

            Registers.Fpu.PopStackTop();
            var float1 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            var float2 = BitConverter.ToSingle(floatToMultiply);

            var result = float1 * float2;
            FpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(result);
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Floating Point Store & Pop (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fstp()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            Registers.Fpu.PopStackTop();
            var valueToSave = FpuStack[Registers.Fpu.GetStackTop()];

            Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), offset, valueToSave);
        }

        /// <summary>
        ///     Floating Point Compare (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fcompp()
        {
            Registers.Fpu.PopStackTop();
            var float1 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            Registers.Fpu.PopStackTop();
            var float2 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);

            if (float1 > float2)
            {
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code3);
            }
            else if (float1 < float2)
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code3);
            }
            else
            {
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code3);
            }

        }

        /// <summary>
        ///     Store Status Word to Memory or AX Register
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fstsw()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(Register.AX, Registers.Fpu.StatusWord);
                    break;
                case OpKind.Memory:
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), Registers.Fpu.StatusWord);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sahf()
        {

            if (Registers.AH.IsFlagSet((byte)EnumFlags.SF))
            {
                Registers.F.SetFlag(EnumFlags.SF);
            }
            else
            {
                Registers.F.ClearFlag(EnumFlags.SF);
            }

            if (Registers.AH.IsFlagSet((byte)EnumFlags.ZF))
            {
                Registers.F.SetFlag(EnumFlags.ZF);
            }
            else
            {
                Registers.F.ClearFlag(EnumFlags.ZF);
            }

            if (Registers.AH.IsFlagSet((byte)EnumFlags.AF))
            {
                Registers.F.SetFlag(EnumFlags.AF);
            }
            else
            {
                Registers.F.ClearFlag(EnumFlags.AF);
            }

            if (Registers.AH.IsFlagSet((byte)EnumFlags.PF))
            {
                Registers.F.SetFlag(EnumFlags.PF);
            }
            else
            {
                Registers.F.ClearFlag(EnumFlags.PF);
            }

            if (Registers.AH.IsFlagSet((byte)EnumFlags.CF))
            {
                Registers.F.SetFlag(EnumFlags.CF);
            }
            else
            {
                Registers.F.ClearFlag(EnumFlags.CF);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Not()
        {
            var destination = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };

            var result = (ushort)~destination;

            WriteToDestination(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Xchg()
        {
            var destination = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };

            var source = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Source),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Source),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(source);
            WriteToSource(destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Div()
        {
            var source = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };

            var operationSize = GetCurrentOperationSize();

            int destination = 0;
            switch (operationSize)
            {
                case 1:
                    destination = Registers.AX;
                    break;
                case 2:
                    destination = (Registers.DX << 16) | Registers.AX;
                    break;
            }

            var quotient = Math.DivRem(destination, source, out var remainder);

            Registers.AX = (ushort)quotient;
            Registers.DX = (ushort)remainder;
        }

        /// <summary>
        ///     Floating Point Subtraction (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fsub()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var floatToMultiply = Memory.GetArray(Registers.DS, offset, 4);

            Registers.Fpu.PopStackTop();
            var float1 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            var float2 = BitConverter.ToSingle(floatToMultiply);

            var result = float1 - float2;
            FpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(result);
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Floating Point Load Zero
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fldz()
        {
            FpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(0.0f);
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Floating Point Load Control Word
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fldcw()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var newControlWord = Memory.GetWord(Registers.DS, offset);

            Registers.Fpu.ControlWord = newControlWord;
        }

        /// <summary>
        ///     Floating Point Store Control Word
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fstcw()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(Register.AX, Registers.Fpu.ControlWord);
                    break;
                case OpKind.Memory:
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), Registers.Fpu.ControlWord);
                    break;
            }
        }

        /// <summary>
        ///     Floating Point Store Integer & Pop
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fistp()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            Registers.Fpu.PopStackTop();
            var valueToSave = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            Memory.SetWord(Registers.DS, offset, (ushort)valueToSave);
        }

        /// <summary>
        ///     Floating Point Add ST0 to ST1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Faddp()
        {
            Registers.Fpu.PopStackTop(); //ST1
            var float1 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            Registers.Fpu.PopStackTop(); //ST0
            var float2 = BitConverter.ToSingle(FpuStack[Registers.Fpu.GetStackTop()]);
            Registers.Fpu.PushStackTop(); //ST1

            var result = float1 + float2;

            //Store result at ST1
            FpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(result);

            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Clear Interrupt Flag
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cli()
        {
            Registers.F.ClearFlag(EnumFlags.IF);
        }

        /// <summary>
        ///     Set Interrupt Flag
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sti()
        {
            Registers.F.SetFlag(EnumFlags.IF);
        }

        /// <summary>
        ///     Floating Point Store (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fst()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            var valueToSave = FpuStack[Registers.Fpu.GetStackTop()];
            Memory.SetArray(Registers.DS, offset, valueToSave);
        }

        /// <summary>
        ///     Covert Byte to Word
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cbw()
        {
            Registers.AH = Registers.AL.IsNegative() ? (byte)0xFF : (byte)0x0;
        }

        /// <summary>
        ///     Clears Direction Flag
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cld()
        {
            Registers.F.ClearFlag(EnumFlags.DF);
        }

        /// <summary>
        ///     Load byte at address DS:(E)SI into AL.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Lodsb()
        {
            Registers.AL = Memory.GetByte(Registers.DS, Registers.SI);

            if (Registers.F.IsFlagSet(EnumFlags.DF))
            {
                Registers.SI--;
            }
            else
            {
                Registers.SI++;
            }
        }


        /// <summary>
        ///     Store AL at address ES:(E)DI.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Stosb()
        {
            Memory.SetByte(Registers.DS, Registers.SI, Registers.AL);

            if (Registers.F.IsFlagSet(EnumFlags.DF))
            {
                Registers.SI--;
                Registers.DI--;
            }
            else
            {
                Registers.SI++;
                Registers.DI++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Jcxz()
        {
            // CX == 0
            if (Registers.CX == 0)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Scasb()
        {
            var destination = Registers.AL;
            var source = Memory.GetByte(Registers.ES, Registers.DI);

#if DEBUG
            _logger.Info($"Comparing {(char)destination} to {(char)source}");
#endif

            unchecked
            {
                var result = (byte)(destination - source);
                Registers.F.EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate(EnumFlags.ZF, result);
                Registers.F.Evaluate(EnumFlags.SF, result);

                if (Registers.F.IsFlagSet(EnumFlags.DF))
                {
                    Registers.SI--;
                    Registers.DI--;
                }
                else
                {
                    Registers.SI++;
                    Registers.DI++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Loope()
        {
            Registers.CX--;

            if (Registers.CX == 0 && !Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                //Continue with the next instruction
                Registers.IP += (ushort)_currentInstruction.Length;
                return;
            }

            Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);
        }
    }
}
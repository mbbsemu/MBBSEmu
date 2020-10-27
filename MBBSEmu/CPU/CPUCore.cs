using Iced.Intel;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     MBBSEmu emulated 16-bit x86 Core used to execute decompiled x86 Assembly
    /// </summary>
    public class CpuCore : ICpuCore
    {
        protected readonly ILogger _logger;

        /// <summary>
        ///     Definition for delegate that is invoked with a CALL FAR references an Imported Method
        /// </summary>
        /// <param name="importedNameTableOrdinal"></param>
        /// <param name="functionOrdinal"></param>
        /// <returns></returns>
        public delegate ReadOnlySpan<byte> InvokeExternalFunctionDelegate(ushort importedNameTableOrdinal, ushort functionOrdinal);

        /// <summary>
        ///     Delegate that is invoked with a CALL FAR references an Imported Method
        /// </summary>
        private InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        /// <summary>
        ///     x86 Registers
        /// </summary>
        public CpuRegisters Registers { get; set; }

        /// <summary>
        ///     x86 Memory Core representing a full 16-bit Address Space
        /// </summary>
        private IMemoryCore Memory { get; set; }

        /// <summary>
        ///     Current Instruction being Executed
        /// </summary>
        private Instruction _currentInstruction { get; set; }

        //Debug Pointers

        /// <summary>
        ///     Pointer to the current instruction CS:IP (for debugging)
        /// </summary>
        private IntPtr16 _currentInstructionPointer { get; set; }

        /// <summary>
        ///     Pointer to the previous instruction CS:IP (for debugging)
        /// </summary>
        private IntPtr16 _previousInstructionPointer { get; set; }

        /// <summary>
        ///     Previous Location of a CALL into the current function
        /// </summary>
        private IntPtr16 _previousCallPointer { get; set; }

        /// <summary>
        ///     Defines if additional debug (disassembly, registers, etc.) is to be displayed with
        ///     each instruction being executed
        /// </summary>
        private bool _showDebug { get; set; }

        /// <summary>
        ///     Initial Value of the ES Register
        /// </summary>
        private ushort EXTRA_SEGMENT { get; set; }

        /// <summary>
        ///     Default Value for the SP Register (Stack Pointer)
        /// </summary>
        public const ushort STACK_BASE = 0xFFFF;

        /// <summary>
        ///     Default Segment for the Stack in Memory
        /// </summary>
        public const ushort STACK_SEGMENT = 0x0;

        /// <summary>
        ///     x87 FPU Stack
        ///
        ///     The x87 Stack contains eight slots for 32-bit (Single Precision) Floating Point values
        /// </summary>
        public readonly double[] FpuStack = new double[8];

        /// <summary>
        ///     Size of the current operation (in bytes)
        ///
        ///     1 == 8, 2 == 16, 4 == 32, etc.
        /// </summary>
        private int _currentOperationSize { get; set; }

        /// <summary>
        ///     Counter of the total number of Instructions executed since Entry
        /// </summary>
        public long InstructionCounter { get; set; }

        /// <summary>
        ///     INT 21h defined Disk Transfer Area
        ///
        ///     Buffer used to hold information on the current Disk / IO operation
        /// </summary>
        private IntPtr16 DiskTransferArea;

        /// <summary>
        ///     Default Compiler Hints for use on methods within the CPU
        ///
        ///     AggressiveOptimization == The method contains a hot path and should be optimized
        ///
        ///     Inlining actually *slows* down this code, to a great extent, most due to CPU L1
        ///     cache limits. Performance seemed best with AggressiveOptimization on and AggressiveInlining
        ///     off.
        ///
        ///     AggressiveOptimization will tell the JIT to spend more time during compilation generating better code
        /// </summary>
        private const MethodImplOptions CompilerOptimizations = MethodImplOptions.AggressiveOptimization;

        public CpuCore(ILogger logger)
        {
            _logger = logger;
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

        /// <summary>
        ///     Resets CPU to a default startup state (default Stack Pointer & Base Pointer)
        /// </summary>
        public void Reset() => Reset(STACK_BASE);

        /// <summary>
        ///     Resets the CPU to a default startup state
        /// </summary>
        /// <param name="stackBase">Offset of the Stack Pointer & Base Pointer after Reset</param>
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

        /// <summary>
        ///     Pops a Word from the Stack and increments the Stack Pointer
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Pop()
        {
            var value = Memory.GetWord(Registers.SS, (ushort)(Registers.SP + 1));
#if DEBUG
            if (_showDebug)
                _logger.Info($"Popped {value:X4} from {Registers.SP + 1:X4}");
#endif
            Registers.SP += 2;
            return value;
        }

        /// <summary>
        ///     Pushes a Word to the Stack and decrements the Stack Pointer
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ushort value)
        {
            Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), value);

#if DEBUG
            if (_showDebug)
                _logger.Info($"Pushed {value:X4} to {Registers.SP - 1:X4}");
#endif
            Registers.SP -= 2;
        }

        /// <summary>
        ///     Ticks the emulated x86 Core one instruction from the current CS:IP
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        public void Tick()
        {
            // TODO figure out how we can remove this check, such as filling the
            // memory core instruction set at 65535 to all halt instructions

            // Check for segment end
            if (Registers.CS == ushort.MaxValue)
            {
                Registers.Halt = true;
                return;
            }

#if DEBUG
            _currentInstructionPointer.Offset = Registers.IP;
            _currentInstructionPointer.Segment = Registers.CS;
#endif

            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);
            _currentOperationSize = GetCurrentOperationSize();

#if DEBUG

            //Breakpoint
            //if (Registers.CS == 0x1 && Registers.IP == 0xc93)
            //    Debugger.Break();

            //Show Debugging
            //_showDebug = Registers.CS == 0x2 && Registers.IP >= 0xA50 && Registers.IP <= 0x0E6D;
            //_showDebug = (Registers.CS == 0x6 && Registers.IP >= 0x352A && Registers.IP <= 0x3562);

            if (_showDebug)
                _logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction}");
#endif
            InstructionCounter++;

            //Jump Table
            Switch:
            switch (_currentInstruction.Mnemonic)
            {
                case Mnemonic.INVALID:
                    _currentInstruction = Memory.Recompile(Registers.CS, Registers.IP);
                    _currentOperationSize = GetCurrentOperationSize();
                    goto Switch;
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
                    return;

                //Instructions that do not set IP -- we'll just increment
                case Mnemonic.Wait:
                case Mnemonic.Nop:
                case Mnemonic.In:
                case Mnemonic.Out:
                case Mnemonic.Hlt: //Halt CPU until interrupt, there are none so keep going
                    break;
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
                case Mnemonic.Fld1:
                    Op_Fld1();
                    break;
                case Mnemonic.Fsqrt:
                    Op_Fsqrt();
                    break;
                case Mnemonic.Fdiv:
                    Op_Fdiv();
                    break;
                case Mnemonic.Fmulp:
                    Op_Fmulp();
                    break;
                case Mnemonic.Fcomp:
                    Op_Fcomp();
                    break;
                case Mnemonic.Pushf:
                    Op_Pushf();
                    break;
                case Mnemonic.Fadd:
                    Op_Fadd();
                    break;
                case Mnemonic.Fdivp:
                    Op_Fdivp();
                    break;
                case Mnemonic.Fsubr:
                    Op_Fsubr();
                    break;
                case Mnemonic.Fsubrp:
                    Op_Fsubrp();
                    break;
                case Mnemonic.Fclex:
                case Mnemonic.Fnclex:
                    Op_Fclex();
                    break;
                case Mnemonic.Frndint:
                    Op_Frndint();
                    break;
                case Mnemonic.Ror:
                    Op_Ror();
                    break;
                case Mnemonic.Ftst:
                    Op_Ftst();
                    break;
                case Mnemonic.Fcom:
                    Op_Fcom();
                    break;
                case Mnemonic.Fdivr:
                    Op_Fdivr();
                    break;
                case Mnemonic.Fdivrp:
                    Op_Fdivrp();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

#if DEBUG
            _previousInstructionPointer.Offset = Registers.IP;
            _previousInstructionPointer.Segment = Registers.CS;
#endif

            Registers.IP += (ushort)_currentInstruction.Length;

#if DEBUG
            if (_showDebug)
                _logger.InfoRegisters(this);
#endif

        }

        /// <summary>
        ///     Returns the Operand Value as a 8-bit Integer
        ///
        ///     Operand Sizes Equal to or Less than 8-Bit are Supported and returned as a 8-bit Integer
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
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
                        var offset = GetOperandOffset(opKind);

                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Int8:
                            case MemorySize.UInt8:
                                return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), offset);
                            default:
                                throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }

                default:
                    throw new Exception($"Unsupported OpKind: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the Operand Value as a 16-bit Integer
        ///
        ///     Operand Sizes Equal to or Less than 16-Bit are Supported and returned as a 16-bit Integer
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
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
                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Int8:
                            case MemorySize.UInt8:
                                return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            case MemorySize.Int16:
                            case MemorySize.UInt16:
                                return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            default:
                                throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }

                default:
                    throw new Exception($"Unsupported OpKind: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the Operand Value as a 32-bit Integer
        ///
        ///     Operand Sizes Equal to or Less than 32-Bit are Supported and returned as a 32-bit Integer
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private uint GetOperandValueUInt32(OpKind opKind)
        {
            switch (opKind)
            {
                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;
                case OpKind.Immediate16:
                    return _currentInstruction.Immediate16;
                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;
                case OpKind.Immediate32:
                    return _currentInstruction.Immediate32;
                case OpKind.Immediate8to32:
                    return (uint)_currentInstruction.Immediate8to32;
                case OpKind.Memory:
                    {
                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Int8:
                            case MemorySize.UInt8:
                                return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            case MemorySize.Int16:
                            case MemorySize.UInt16:
                                return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            case MemorySize.Int32:
                            case MemorySize.UInt32:
                                return BitConverter.ToUInt32(
                                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind), 4));
                            default:
                                throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }

                default:
                    throw new Exception($"Unsupported OpKind: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the Operand Value as a 64-bit Integer
        ///
        ///     Operand Sizes Equal to or Less than 64-Bit are Supported and returned as a 64-bit Integer
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private ulong GetOperandValueUInt64(OpKind opKind)
        {
            switch (opKind)
            {
                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;
                case OpKind.Immediate16:
                    return _currentInstruction.Immediate16;
                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;
                case OpKind.Immediate32:
                    return _currentInstruction.Immediate32;
                case OpKind.Immediate8to32:
                    return (uint)_currentInstruction.Immediate8to32;
                case OpKind.Immediate64:
                    return _currentInstruction.Immediate64;
                case OpKind.Immediate8to64:
                    return (ulong)_currentInstruction.Immediate8to64;
                case OpKind.Memory:
                    {
                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Int8:
                            case MemorySize.UInt8:
                                return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            case MemorySize.Int16:
                            case MemorySize.UInt16:
                                return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            case MemorySize.Int32:
                            case MemorySize.UInt32:
                                return BitConverter.ToUInt32(
                                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind), 4));
                            case MemorySize.Int64:
                            case MemorySize.UInt64:
                                return BitConverter.ToUInt64(
                                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind), 8));
                            default:
                                throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }

                default:
                    throw new Exception($"Unsupported OpKind: {opKind}");
            }
        }

        /// <summary>
        ///     Gets the Float Value of the specified Operand
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private float GetOperandValueFloat(OpKind opKind, EnumOperandType operandType)
        {
            return opKind switch
            {
                OpKind.Memory => BitConverter.ToSingle(
                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind),
                        4)),
                OpKind.Register when operandType == EnumOperandType.Destination => (float)FpuStack[
                    Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)],
                OpKind.Register when operandType == EnumOperandType.Source => (float)FpuStack[
                    Registers.Fpu.GetStackPointer(_currentInstruction.Op1Register)],
                _ => throw new Exception($"Unknown Float Operand: {opKind}")
            };
        }

        /// <summary>
        ///     Returns the Operand Value as a 64-bit Double
        ///
        ///     Operand Sizes Equal to or Less than 64-Bit are Supported and returned as a 64-bit Double
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private double GetOperandValueDouble(OpKind opKind, EnumOperandType operandType)
        {
            return opKind switch
            {
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float32 => BitConverter.ToSingle(
                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind),
                        4)),
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float64 => BitConverter.ToDouble(
                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind),
                        8)),
                OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float80 => BitConverter.ToDouble(
                    Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind),
                        8)),
                OpKind.Register when operandType == EnumOperandType.Destination => FpuStack[
                    Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)],
                OpKind.Register when operandType == EnumOperandType.Source => FpuStack[
                    Registers.Fpu.GetStackPointer(_currentInstruction.Op1Register)],
                _ => throw new Exception($"Unknown Double Operand: {opKind}")
            };
        }

        /// <summary>
        ///     Returns the OFFSET of Operand 0
        ///
        ///     Only use this for instructions where Operand 0 is an OFFSET within a Memory Segment
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
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

                                    result = (ushort)(Registers.BP + _currentInstruction.MemoryDisplacement + 1);
                                    break;
                                }

                            case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                                {

                                    result = (ushort)(Registers.BP + Registers.SI +
                                                       _currentInstruction.MemoryDisplacement + 1);
                                    break;
                                }

                            case Register.BP when _currentInstruction.MemoryIndex == Register.DI:
                                {
                                    result = (ushort)(Registers.BP + Registers.DI +
                                                       _currentInstruction.MemoryDisplacement + 1);
                                    break;
                                }

                            case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.BX + _currentInstruction.MemoryDisplacement);
                                break;
                            case Register.BX when _currentInstruction.MemoryIndex == Register.SI:
                                result = (ushort)(Registers.BX + _currentInstruction.MemoryDisplacement + Registers.SI);
                                break;
                            case Register.BX when _currentInstruction.MemoryIndex == Register.DI:
                                result = (ushort)(Registers.BX + _currentInstruction.MemoryDisplacement + Registers.DI);
                                break;
                            case Register.SI when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.SI + _currentInstruction.MemoryDisplacement);
                                break;
                            case Register.DI when _currentInstruction.MemoryIndex == Register.None:
                                result = (ushort)(Registers.DI + _currentInstruction.MemoryDisplacement);
                                break;
                            default:
                                throw new Exception($"Unknown GetOperandOffset MemoryBase: {_currentInstruction}");
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
        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
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
                    throw new ArgumentOutOfRangeException($"Unknown Destination: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Writes the specified result of the instruction into the specified destination
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(CompilerOptimizations)]
        private void WriteToDestination(float result)
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Memory:
                    Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes(result));
                    break;
                case OpKind.Register:
                    FpuStack[Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)] = result;
                    break;
            }
        }

        /// <summary>
        ///     Writes the specified result of the instruction into the specified destination
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(CompilerOptimizations)]
        private void WriteToDestination(double result)
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float32:
                    Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes((float)result));
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float64:
                    Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes(result));
                    break;
                case OpKind.Register:
                    FpuStack[Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)] = result;
                    break;
            }
        }

        /// <summary>
        ///     Saves the specified result of the current instruction into the current instructions source
        ///
        ///     This is only used by a couple specific opcodes
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(CompilerOptimizations)]
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
                    throw new ArgumentOutOfRangeException($"Unknown Source: {_currentInstruction.Op0Kind}");
            }
        }


        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Int()
        {
            switch (_currentInstruction.Immediate8)
            {
                case 0x21:
                    Op_Int_21h();
                    return;
                case 0x3E:
                    //Borland Interrupt -- ignored
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown INT: {_currentInstruction.Immediate8:X2}");
            }
        }

        [MethodImpl(CompilerOptimizations)]
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
                case 0x62:
                    {
                        /*
                            INT 21 - AH = 62h DOS 3.x - GET PSP ADDRESS
                            Return: BX = segment address of PSP
                            We allocate 0xFFFF to ensure it has it's own segment in memory
                         */
                        if (!Memory.TryGetVariablePointer("INT21h-PSP", out var pspPointer))
                            pspPointer = Memory.AllocateVariable("Int21h-PSP", 0xFFFF);

                        Registers.BX = pspPointer.Segment;
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: 0x{Registers.AH:X2}");
            }
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Cwd()
        {
            Registers.DX = Registers.AX.IsBitSet(15) ? (ushort)0xFFFF : (ushort)0x0000;
        }


        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private byte Op_Or_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(CompilerOptimizations)]
        private ushort Op_Or_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.ES = dataPointer.Segment;
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Lds()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.DS = dataPointer.Segment;
        }


        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Enter()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Immediate16:
                    Push(Registers.BP);
                    Registers.BP = Registers.SP;
                    Registers.SP -= _currentInstruction.Immediate16;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown ENTER: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Leave()
        {
            Registers.SP = Registers.BP;
            Registers.BP = Pop();
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Retf()
        {
            Registers.IP = Pop();
            Registers.CS = Pop();
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Ret()
        {
            Registers.IP = Pop();
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Stosw()
        {
            while (Registers.CX > 0)
            {
                Memory.SetWord(Registers.ES, Registers.DI, Registers.AX);
                Registers.DI += 2;
                Registers.CX--;
            }
        }

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private byte Op_And_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(CompilerOptimizations)]
        private ushort Op_And_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private byte Op_Xor_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (byte)(destination ^ source);
            Registers.F.Evaluate(EnumFlags.ZF, result);
            Registers.F.Evaluate(EnumFlags.SF, result);
            return result;
        }

        [MethodImpl(CompilerOptimizations)]
        private ushort Op_Xor_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (ushort)(destination ^ source);
            Registers.F.Evaluate(EnumFlags.ZF, result);
            Registers.F.Evaluate(EnumFlags.SF, result);
            return result;
        }

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Test_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Registers.F.Evaluate(EnumFlags.ZF, destination);
            Registers.F.Evaluate(EnumFlags.SF, destination);
        }

        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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


        [MethodImpl(CompilerOptimizations)]
        private uint Op_Sub_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind);

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
        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Idiv()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Idiv_8();
                    return;
                case 2:
                    Op_Idiv_16();
                    return;
            }
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Idiv_8()
        {
            var destination = (sbyte)GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            var quotient = Math.DivRem((short)Registers.AX, destination, out var remainder);

            if(quotient > sbyte.MaxValue || quotient < sbyte.MinValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AL = (byte)quotient;
            Registers.AH = (byte)remainder;

        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Idiv_16()
        {
            var destination = (short)GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            var quotient = Math.DivRem(Registers.GetLong(), destination, out var remainder);

            if (quotient > short.MaxValue || quotient < short.MinValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AX = (ushort)quotient;
            Registers.DX = (ushort)remainder;

        }

        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
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
                            BitConverter.GetBytes(GetOperandValueUInt32(_currentInstruction.Op1Kind)));
                        return;
                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown MOV: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(CompilerOptimizations)]
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
                        var pointer = Memory.GetPointer(Registers.GetValue(_currentInstruction.MemorySegment), offset);
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
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fld()
        {
            var floatToLoad = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);

            //Set Value as new ST(0)
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = floatToLoad;
        }

        /// <summary>
        ///     Integer Load Operation (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fild()
        {
            var valueToLoad = GetOperandValueUInt64(_currentInstruction.Op0Kind);

            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = valueToLoad;

        }

        /// <summary>
        ///     Floating Point Multiplication (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fmul()
        {
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];
            var source = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);

            var result = ST0 * source;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Store & Pop (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fstp()
        {
            Op_Fst();

            Registers.Fpu.PopStackTop(); //Pop the stack setting ST(1)->ST(0)
        }

        /// <summary>
        ///     Store Status Word to Memory or AX Register
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
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

        /// <summary>
        ///     Set Status Flag Values from Flags Saved in AH
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
        private void Op_Div()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Div_8();
                    return;
                case 2:
                    Op_Div_16();
                    return;
            }
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Div_8()
        {
            var divisor = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = Registers.AX;

            var quotient = Math.DivRem(dividend, divisor, out var remainder);

            if (quotient > byte.MaxValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AL = (byte)quotient;
            Registers.AH = (byte)remainder;
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Div_16()
        {
            var divisor = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = (uint)Registers.GetLong();

            var quotient = Math.DivRem(dividend, divisor, out var remainder);

            if (quotient > ushort.MaxValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AX = (ushort)quotient;
            Registers.DX = (ushort)remainder;
        }

        /// <summary>
        ///     Floating Point Subtraction (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fsub()
        {
            var floatToSubtract = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];

            var result = ST0 - floatToSubtract;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Reverse Subtraction (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fsubr()
        {
            var valueToSubtractFrom = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];

            var result = valueToSubtractFrom - ST0;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Reverse Subtraction (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fsubrp()
        {
            var valueToSubtractFrom = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var valueToSubtract = GetOperandValueDouble(_currentInstruction.Op1Kind, EnumOperandType.Destination);

            var result = valueToSubtractFrom - valueToSubtract;
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = result;
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Load Zero
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fldz()
        {
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = 0d; //set ST(0) to 0.0
        }

        /// <summary>
        ///     Floating Point Load Control Word
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fldcw()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);
            var newControlWord = Memory.GetWord(Registers.DS, offset);

            Registers.Fpu.ControlWord = newControlWord;
        }

        /// <summary>
        ///     Floating Point Store Control Word
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fistp()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            var valueToSave = FpuStack[Registers.Fpu.GetStackTop()];
            Registers.Fpu.PopStackTop();

            if (double.IsNaN(valueToSave))
            {
                Registers.Fpu.ControlWord = Registers.Fpu.ControlWord.SetFlag((ushort)EnumFpuControlWordFlags.InvalidOperation);
                return;
            }

            switch (_currentInstruction.MemorySize)
            {
                case MemorySize.Int16:
                    {
                        if (valueToSave > short.MaxValue || valueToSave < short.MinValue)
                        {
                            Registers.Fpu.ControlWord = Registers.Fpu.ControlWord.SetFlag((ushort)EnumFpuControlWordFlags.InvalidOperation);
                            break;
                        }

                        Memory.SetWord(Registers.DS, offset, (ushort)Convert.ToInt16(valueToSave));
                        break;
                    }
                case MemorySize.Int32:
                    {
                        if (valueToSave > uint.MaxValue || valueToSave < int.MinValue)
                        {
                            Registers.Fpu.ControlWord = Registers.Fpu.ControlWord.SetFlag((ushort)EnumFpuControlWordFlags.InvalidOperation);
                            break;
                        }

                        Memory.SetArray(Registers.DS, offset, BitConverter.GetBytes(Convert.ToInt32(valueToSave)));
                        break;
                    }
                case MemorySize.Int64:
                    {
                        if (valueToSave > long.MaxValue || valueToSave < long.MinValue)
                        {
                            Registers.Fpu.ControlWord = Registers.Fpu.ControlWord.SetFlag((ushort)EnumFpuControlWordFlags.InvalidOperation);
                            break;
                        }

                        Memory.SetArray(Registers.DS, offset, BitConverter.GetBytes(Convert.ToInt64(valueToSave)));
                        break;
                    }
                default:
                    throw new Exception($"Unknown Memory Size: {_currentInstruction.MemorySize}");
            }


        }

        /// <summary>
        ///     Floating Point Add ST0 to ST1
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Faddp()
        {
            var STdestination = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var STsource = GetOperandValueDouble(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = STdestination + STsource;

            //Store result at ST1
            WriteToDestination(result);

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Clear Interrupt Flag
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Cli()
        {
            Registers.F.ClearFlag(EnumFlags.IF);
        }

        /// <summary>
        ///     Set Interrupt Flag
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Sti()
        {
            Registers.F.SetFlag(EnumFlags.IF);
        }

        /// <summary>
        ///     Floating Point Store (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fst()
        {
            var valueToSave = FpuStack[Registers.Fpu.GetStackTop()]; //Save off ST(0)

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float32:
                    {
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes((float)valueToSave));
                        break;
                    }
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float80:
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float64:
                    {
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes(valueToSave));
                        break;
                    }
                case OpKind.Register:
                    {
                        FpuStack[Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)] = valueToSave;
                        break;
                    }
                default:
                    throw new Exception($"Unsupported Destination: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Covert Byte to Word
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Cbw()
        {
            Registers.AH = Registers.AL.IsNegative() ? (byte)0xFF : (byte)0x0;
        }

        /// <summary>
        ///     Clears Direction Flag
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Cld()
        {
            Registers.F.ClearFlag(EnumFlags.DF);
        }

        /// <summary>
        ///     Load byte at address DS:(E)SI into AL.
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
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
        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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

        [MethodImpl(CompilerOptimizations)]
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
                    Registers.DI--;
                }
                else
                {
                    Registers.DI++;
                }
            }
        }

        [MethodImpl(CompilerOptimizations)]
        private void Op_Loope()
        {
            Registers.CX--;

            if (Registers.F.IsFlagSet(EnumFlags.ZF) && Registers.CX != 0)
            {
                Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);
                return;
            }

            //Continue with the next instruction
            Registers.IP += (ushort)_currentInstruction.Length;
        }

        /// <summary>
        ///     Floating Point Load Operation (x87)
        ///
        ///     Pushes the value of "1" to ST(0)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fld1()
        {
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = 1d;
        }

        /// <summary>
        ///     Floating Point Square Root Operation (x87)
        ///
        ///     Computes square root of ST(0) and stores the result in ST(0).
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fsqrt()
        {
            //Handle FPU States for ST0 values that won't square root
            switch (FpuStack[Registers.Fpu.GetStackTop()])
            {
                case double.NaN:
                case double.PositiveInfinity:
                case 0:
                    return;
                case double.NegativeInfinity:
                case var v when v < 0:
                    Registers.Fpu.ControlWord =
                        Registers.Fpu.ControlWord.SetFlag((ushort)EnumFpuControlWordFlags.InvalidOperation);
                    return;
            }

            FpuStack[Registers.Fpu.GetStackTop()] = Math.Sqrt(FpuStack[Registers.Fpu.GetStackTop()]);
        }

        /// <summary>
        ///     Floating Point Division (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fdiv()
        {
            var source = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];

            var result = ST0 / source;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Multiply ST0 to ST1
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fmulp()
        {
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];
            var ST1 = FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)];

            var result = ST0 * ST1;

            //Store result at ST1
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = result;

            //Pop the Stack making ST(1)->ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Compares the contents of register ST(0) and source value and sets condition code flags C0, C2, and C3 in the FPU status word
        ///     according to the results. The source operand can be a data register or a memory location. If no source operand is given,
        ///     the value in ST(0) is compared with the value in ST(1). The sign of zero is ignored, so that –0.0 is equal to +0.0.
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fcom()
        {
            var source = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];

            if (double.IsNaN(ST0) || double.IsNaN(source))
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code3);
                return;
            }

            if (ST0 > source)
            {
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code3);
            }
            else if (ST0 < source)
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
        ///     Same as FCOM, but Pops the FPU Stack
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fcomp()
        {
            Op_Fcom();

            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Same as FCOM, but Pops the FPU Stack twice
        ///
        ///     Because FCOMPP doesn't have any source or destination operands, we have to manually retrieve
        ///     ST0 and ST1 values for comparison (can't just call FCOM).
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fcompp()
        {

            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];
            var ST1 = FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)];

            if (double.IsNaN(ST0) || double.IsNaN(ST1))
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code3);
            }
            else
            {
                if (ST0 > ST1)
                {
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code0);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code3);
                }
                else if (ST0 < ST1)
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

            Registers.Fpu.PopStackTop();
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Push Flags Register to Stack
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Pushf()
        {
            Push(Registers.F.Flags);
        }

        /// <summary>
        ///     Floating Point Addition (x87)
        ///
        ///     Add m32fp to ST(0) and store result in ST(0).
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fadd()
        {
            var floatToAdd = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var floatOnFpuStack = FpuStack[Registers.Fpu.GetStackTop()];

            var result = floatOnFpuStack + floatToAdd;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Divide ST1 by ST0 saving the result to ST(1) and Popping the FPU stack
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fdivp()
        {
            var STdestination = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var STsource = GetOperandValueDouble(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = STdestination / STsource;

            //Store result at ST1
            WriteToDestination(result);

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Clears Exception Flags from the x87 FPU Status Register
        ///
        ///     FCLEX is similar to FNCLEX, except it is preceded by a WAIT which is ignored
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fclex()
        {
            Registers.Fpu.ClearExceptions();
        }

        /// <summary>
        ///     Rounds the value at ST(0) to the nearest Integral Value and stores it in ST(0)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Frndint()
        {
            FpuStack[Registers.Fpu.GetStackTop()] = Math.Round(FpuStack[Registers.Fpu.GetStackTop()], MidpointRounding.AwayFromZero);
        }

        /// <summary>
        ///     Rotate Right
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Ror()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Ror_8(),
                2 => Op_Ror_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     8-bit Rotate Right
        /// </summary>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private byte Op_Ror_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)((destination >> (sbyte)source) | (destination << (8 - (sbyte)source)));

                //CF Set if Most Significant Bit set to 1
                if (result.IsNegative())
                    Registers.F.SetFlag(EnumFlags.CF);
                else
                    Registers.F.ClearFlag(EnumFlags.CF);

                //If Bits 7 & 6 are not the same, then we overflowed
                if (result.IsBitSet(7) != result.IsBitSet(6))
                    Registers.F.SetFlag(EnumFlags.OF);
                else
                    Registers.F.ClearFlag(EnumFlags.OF);

                return result;
            }
        }

        /// <summary>
        ///     16-bit Rotate Right
        /// </summary>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        private ushort Op_Ror_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)((destination >> (sbyte)source) | (destination << (16 - (sbyte)source)));

                //CF Set if Most Significant Bit set to 1
                if (result.IsNegative())
                    Registers.F.SetFlag(EnumFlags.CF);
                else
                    Registers.F.ClearFlag(EnumFlags.CF);

                //If Bits 15 & 14 are not the same, then we overflowed
                if (result.IsBitSet(15) != result.IsBitSet(14))
                    Registers.F.SetFlag(EnumFlags.OF);
                else
                    Registers.F.ClearFlag(EnumFlags.OF);

                return result;
            }
        }

        /// <summary>
        ///     Floating Point Compare ST(0) to 0.0 (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Ftst()
        {
            var ST0Value = FpuStack[Registers.Fpu.GetStackTop()];

            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);

            if (double.IsNaN(ST0Value))
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code3);
                return;
            }

            if (ST0Value > 0.0d)
            {
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code0);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code3);
            }
            else if (ST0Value < 0.0d)
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
        ///     Floating Point Reverse Division (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fdivr()
        {
            var dividend = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];

            var result = dividend / ST0;
            FpuStack[Registers.Fpu.GetStackTop()] = result;
        }

        /// <summary>
        ///     Floating Point Reverse Division (x87)
        /// </summary>
        [MethodImpl(CompilerOptimizations)]
        private void Op_Fdivrp()
        {
            var dividend = GetOperandValueDouble(_currentInstruction.Op0Kind, EnumOperandType.Source);
            var divisor = GetOperandValueDouble(_currentInstruction.Op1Kind, EnumOperandType.Destination);

            var result = dividend / divisor;
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = result;
            Registers.Fpu.PopStackTop();
        }
    }
}

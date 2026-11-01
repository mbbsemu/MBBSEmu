using Iced.Intel;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     MBBSEmu emulated 16-bit x86 Core used to execute decompiled x86 Assembly
    /// </summary>
    public class CpuCore : CpuRegisters, ICpuCore, IDisposable
    {
        protected readonly IMessageLogger _logger;

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
        private FarPtr _currentInstructionPointer { get; set; }

        /// <summary>
        ///     Pointer to the previous instruction CS:IP (for debugging)
        /// </summary>
        private FarPtr _previousInstructionPointer { get; set; }

        /// <summary>
        ///     Previous Location of a CALL into the current function
        /// </summary>
        private FarPtr _previousCallPointer { get; set; }

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
        public const ushort STACK_BASE = 0xFFFE;

        /// <summary>
        ///     Default Segment for the Stack in Memory
        /// </summary>
        public const ushort STACK_SEGMENT = 0x0;

        /// <summary>
        ///     x87 FPU Stack
        ///
        ///     The eight 80-bit extended-precision registers of the coprocessor
        /// </summary>
        public readonly Float80[] FpuStack = new Float80[8];

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
        private const MethodImplOptions OpcodeCompilerOptimizations = MethodImplOptions.AggressiveOptimization;


        /// <summary>
        ///     Default Compiler Hints for use on methods within the CPU
        ///
        ///     AggressiveOptimization == The method contains a hot path and should be optimized
        ///
        ///     Inlining actually is appropriate for the Opcode Subroutines (8, 16, 32 bit variations) as it inlines
        ///     those with their Opcode function.
        ///
        ///     AggressiveOptimization will tell the JIT to spend more time during compilation generating better code
        /// </summary>
        private const MethodImplOptions OpcodeSubroutineCompilerOptimizations = MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining;

        private readonly Dictionary<int, IInterruptHandler> _interruptHandlers = new();
        private readonly Dictionary<int, IIOPort> _ioPortHandlers = new();

        private List<List<FarPtr>> CPUDebugRanges = [];
        private List<FarPtr> CPUBreakpoints = [];
        private bool CPUDebugBreak = false;
        private Dictionary<(FarPtr, ushort), byte[]> WatchedVariables = new();

        public CpuCore(IMessageLogger logger)
        {
            _logger = logger;
        }

        public CpuCore()
        {
            _logger = new LogFactory().GetLogger<MessageLogger>();
        }

        public void Dispose()
        {
            foreach (var handler in _interruptHandlers.Values)
            {
                if (handler is IDisposable disposable)
                    disposable.Dispose();
            }

            foreach (var handler in _ioPortHandlers.Values)
            {
                if (handler is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        /// <summary>
        ///     Resets the CPU back to a starting state
        /// </summary>
        /// <param name="memoryCore"></param>
        /// <param name="invokeExternalFunctionDelegate"></param>
        /// <param name="interruptHandlers"></param>
        /// <param name="ioPortHandlers"></param>
        public void Reset(IMemoryCore memoryCore,
            InvokeExternalFunctionDelegate invokeExternalFunctionDelegate,
            IEnumerable<IInterruptHandler> interruptHandlers, IDictionary<int, IIOPort> ioPortHandlers)
        {
            //Setup Debug Pointers
            _currentInstructionPointer = FarPtr.Empty;
            _previousInstructionPointer = FarPtr.Empty;
            _previousCallPointer = FarPtr.Empty;

            //Setup Delegate Call
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;

            if (interruptHandlers != null)
            {
                _interruptHandlers.Clear();
                foreach (var h in interruptHandlers)
                    _interruptHandlers.Add(h.Vector, h);
            }

            if (ioPortHandlers != null)
            {
                _ioPortHandlers.Clear();
                foreach (var h in ioPortHandlers)
                    _ioPortHandlers.Add(h.Key, h.Value);
            }

            //Setup Memory Space
            Memory = memoryCore;
            EXTRA_SEGMENT = ushort.MaxValue;

            //Setup Registers
            Registers.Zero();
            Registers.BP = STACK_BASE;
            Registers.SP = STACK_BASE;
            Registers.SS = STACK_SEGMENT;
            Registers.ES = EXTRA_SEGMENT;
            Registers.Halt = false;

            //These two values are the final values popped off on the routine's last RETF
            //Seeing a ushort.max for CS and IP tells the routine it's now done
            Push(uint.MaxValue);

            //Setup Debug

            /* ---------------------------
             * ##    CPU Breakpoints    ##
             * ---------------------------
             * The below list specifies addresses where the CPU will be halted, allowing you to break operation and inspect.
             * You can specify any number of offsets in any order. Because this will only work in DEBUG, and is evaluated
             * during operation with each instruction, having a large number of breakpoints will slow down the CPU
             */
            CPUBreakpoints =
            [
                //Below would break the CPU at the CS:IP of 0x1:0x100
                //new(0x1, 0x100)
            ];

            /* ---------------------------
             * ##    CPU Debug Ranges   ##
             * ---------------------------
             * The below list specifies a list of address ranges where CPU debug information will be logged to the console
             * for each executed instruction (including register debug information as well). Values must be in START->END
             * order within their own pair, but pairs can be added to the list in any order.
             */
            CPUDebugRanges =
            [
                //[
                //    new FarPtr(0x1, 0xD23),
                //    new FarPtr(0x1, 0xD59)
                //],
                //[
                //    new FarPtr(0x1, 0x44B),
                //    new FarPtr(0x1, 0x475)
                //]
            ];

            /* ---------------------------
             * ##    Watched Memory     ##
             * ---------------------------
             * The below Dictionary specified the addresses that will be watched for changes between CPU ticks. This will
             * assist in isolating any issues that might be occurring with memory corruption or in other areas of the code
             * not currently being debugged.
             */
            WatchedVariables =
            [
                //{ (new FarPtr(0x9, 0x469), 2), null }
            ];

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
            Push(uint.MaxValue);

            InstructionCounter = 0;
        }

        /// <summary>
        ///     Pops a Word from the Stack and increments the Stack Pointer
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Pop()
        {

            var value = Memory.GetWord(Registers.SS, Registers.SP);

#if DEBUG
            if (_showDebug)
                _logger.Debug($"Popped {value:X4} from {Registers.SP:X4}");
#endif

            Registers.SP += 2;
            return value;
        }

        /// <summary>
        ///     Pops a DWord from the Stack and increments the Stack Pointer
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint PopDWord()
        {
            var value = Memory.GetDWord(Registers.SS, Registers.SP);
#if DEBUG
            if (_showDebug)
                _logger.Debug($"Popped {value:X8} from {Registers.SP:X4}");
#endif

            Registers.SP += 4;
            return value;
        }

        /// <summary>
        ///     Pushes a Word to the Stack and decrements the Stack Pointer
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ushort value)
        {
            Registers.SP -= 2;
            Memory.SetWord(Registers.SS, Registers.SP, value);

#if DEBUG
            if (_showDebug)
                _logger.Debug($"Pushed {value:X4} to {Registers.SP:X4}");
#endif

        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Interrupt(byte vectorNumber)
        {
            // TODO schedule the CPU interrupt
        }

        /// <summary>
        ///     Pushes a DWord to the Stack and decrements the Stack Pointer
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(uint value)
        {
            Registers.SP -= 4;
            Memory.SetDWord(Registers.SS, Registers.SP, value);

#if DEBUG
            if (_showDebug)
                _logger.Debug($"Pushed {value:X8} to {Registers.SP:X4}");
#endif
        }

        /// <summary>
        ///     Ticks the emulated x86 Core one instruction from the current CS:IP
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Tick()
        {
#if DEBUG
            _currentInstructionPointer.Offset = Registers.IP;
            _currentInstructionPointer.Segment = Registers.CS;
#endif

            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);
            _currentOperationSize = GetCurrentOperationSize();

#if DEBUG

            //Evaluate Breakpoints
            if (CPUBreakpoints.Contains(_currentInstructionPointer))
                Debugger.Break();

            //Evaluate Debug Ranges
            if (CPUDebugRanges.Any(x => x[0] <= _currentInstructionPointer && x[1] >= _currentInstructionPointer))
            {
                _showDebug = true; //Set to log Register values to console after execution
                _logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction}");
                foreach (var l in Registers.ToString().Split("\n"))
                    _logger.Debug(l);
                for (var i = 0; i < 8; i++)
                    _logger.Debug($"FPU[{i}]: {FpuStack[i]} {(i == Registers.Fpu.GetStackTop() ? " <--" : string.Empty)}");
            }
            else
            {
                _showDebug = false;
            }

            //Evaluate Watched Variables
            foreach (var (address, length) in WatchedVariables.Keys)
            {
                var value = Memory.GetArray(address.Segment, address.Offset, length);
                if (WatchedVariables[(address, length)] == null || !value.SequenceEqual(WatchedVariables[(address, length)]))
                {
                    _logger.Debug($"Value Change @ {address} :: Old: {WatchedVariables[(address, length)]} :: New: {value.ToHexString()}");
                    WatchedVariables[(address, length)] = value.ToArray();
                }
            }

            _currentInstructionPointer.Offset = Registers.IP;
            _currentInstructionPointer.Segment = Registers.CS;
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
                case Mnemonic.Jp:
                    Op_Jp();
                    return;
                case Mnemonic.Jnp:
                    Op_Jnp();
                    return;
                case Mnemonic.Js:
                    Op_Js();
                    return;
                case Mnemonic.Jns:
                    Op_Jns();
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
                case Mnemonic.Loopne:
                    Op_Loopne();
                    return;
                case Mnemonic.Iret:
                    Op_Iret();
                    return;

                //Instructions that do not set IP -- we'll just increment
                case Mnemonic.Wait:
                case Mnemonic.Nop:
                    break;
                case Mnemonic.In:
                    Op_In();
                    break;
                case Mnemonic.Out:
                    Op_Out();
                    break;
                case Mnemonic.Hlt: //Halt CPU until interrupt, there are none so keep going
                    Registers.Halt = true;
                    break;
                case Mnemonic.Aam:
                    Op_Aam();
                    break;
                case Mnemonic.Daa:
                    Op_Daa();
                    break;
                case Mnemonic.Clc:
                    Op_Clc();
                    break;
                case Mnemonic.Stc:
                    Op_Stc();
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
                case Mnemonic.Cmpsb:
                    Op_Cmpsb();
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
                case Mnemonic.Fldpi:
                    Op_Fldpi();
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
                case Mnemonic.Fist:
                    Op_Fist();
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
                case Mnemonic.Std:
                    Op_Std();
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
                case Mnemonic.Fxch:
                    Op_Fxch();
                    break;
                case Mnemonic.Fpatan:
                    Op_Fpatan();
                    break;
                case Mnemonic.Fsqrt:
                    Op_Fsqrt();
                    break;
                case Mnemonic.Fscale:
                    Op_Fscale();
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
                case Mnemonic.Pushfd:
                    Op_Pushfd();
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
                case Mnemonic.Rol:
                    Op_Rol();
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
                case Mnemonic.Fsin:
                    Op_Fsin();
                    break;
                case Mnemonic.Fcos:
                    Op_Fcos();
                    break;
                case Mnemonic.Lodsw:
                    Op_Lodsw();
                    break;
                case Mnemonic.Popf:
                    Op_Popf();
                    break;
                case Mnemonic.Popfd:
                    Op_Popfd();
                    break;
                case Mnemonic.Fsubp:
                    Op_Fsubp();
                    break;
                case Mnemonic.Fchs:
                    Op_Fchs();
                    break;
                case Mnemonic.Movsb:
                    Op_Movsb();
                    break;
                case Mnemonic.Movsw:
                    Op_Movsw();
                    break;
                case Mnemonic.Movsx:
                    Op_Movsx();
                    break;
                case Mnemonic.Movzx:
                    Op_Movzx();
                    break;
                case Mnemonic.Shld:
                    Op_Shld();
                    break;
                case Mnemonic.Shrd:
                    Op_Shrd();
                    break;
                case Mnemonic.Mul:
                    Op_Mul();
                    break;
                case Mnemonic.Cdq:
                    Op_Cdq();
                    break;
                case Mnemonic.Fabs:
                    Op_Fabs();
                    break;
                case Mnemonic.Fsincos:
                    Op_Fsincos();
                    break;
                case Mnemonic.Fxtract:
                    Op_Fxtract();
                    break;
                case Mnemonic.Fprem:
                    Op_Fprem();
                    break;
                case Mnemonic.F2xm1:
                    Op_F2xm1();
                    break;
                case Mnemonic.Fyl2x:
                    Op_Fyl2x();
                    break;
                case Mnemonic.Fyl2xp1:
                    Op_Fyl2xp1();
                    break;
                case Mnemonic.Fptan:
                    Op_Fptan();
                    break;
                case Mnemonic.Fldl2e:
                    Op_Fldl2e();
                    break;
                case Mnemonic.Fldln2:
                    Op_Fldln2();
                    break;
                case Mnemonic.Fldlg2:
                    Op_Fldlg2();
                    break;
                case Mnemonic.Fldl2t:
                    Op_Fldl2t();
                    break;
                case Mnemonic.Fincstp:
                    Op_Fincstp();
                    break;
                case Mnemonic.Fdecstp:
                    Op_Fdecstp();
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
            {
                //_logger.InfoRegisters(this);

                if (CPUDebugBreak)
                    Debugger.Break();
            }
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private byte GetOperandValueUInt8(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    {
                        //for ops like imul bx, etc. where AX is the implicit destination
                        if (operandType == EnumOperandType.Source && _currentInstruction.Op1Register == Register.None)
                            return Registers.AL;

                        return operandType switch
                        {
                            EnumOperandType.Destination => (byte)Registers.GetValue(_currentInstruction.Op0Register),
                            EnumOperandType.Source => (byte)Registers.GetValue(_currentInstruction.Op1Register),
                            EnumOperandType.Count => (byte)Registers.GetValue(_currentInstruction.Op2Register),
                            _ => throw new ArgumentOutOfRangeException(nameof(operandType), operandType, null)
                        };
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
                case OpKind.MemorySegSI:
                    {
                        return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), Registers.SI);
                    }
                case OpKind.MemorySegDI:
                    {
                        return Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), Registers.DI);
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
        [MethodImpl(OpcodeCompilerOptimizations)]
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
                case OpKind.MemorySegSI:
                    {
                        return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), Registers.SI);
                    }
                case OpKind.MemorySegDI:
                    {
                        return Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), Registers.DI);
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private uint GetOperandValueUInt32(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    {
                        //for ops like imul bx, etc. where AX is the implicit destination
                        if (operandType == EnumOperandType.Source && _currentInstruction.Op1Register == Register.None)
                        {
                            return Registers.EAX;
                        }

                        return Registers.GetValue32(operandType == EnumOperandType.Destination
                            ? _currentInstruction.Op0Register
                            : _currentInstruction.Op1Register);
                    }
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
                                return Memory.GetDWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(opKind));
                            default:
                                throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }
                case OpKind.MemorySegSI:
                    {
                        return Memory.GetDWord(Registers.GetValue(_currentInstruction.MemorySegment), Registers.SI);
                    }
                case OpKind.MemorySegDI:
                    {
                        return Memory.GetDWord(Registers.GetValue(_currentInstruction.MemorySegment), Registers.DI);
                    }
                default:
                    throw new Exception($"Unsupported OpKind: {opKind}");
            }
        }

        /// <summary>
        ///     This is a helper method which takes the resulting value from GetOperandValueUInt8 and signs it depending on the underlying
        ///     OpKind and MemorySize
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private sbyte GetOperandValueInt8(OpKind opKind, EnumOperandType operandType)
        {
            var value = GetOperandValueUInt8(opKind, operandType);

            return opKind switch
            {
                OpKind.Immediate8 => (sbyte)value,
                OpKind.Register => (sbyte)value,
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.Int8 => (sbyte)value,
                    _ => throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}")
                },
                _ => throw new Exception($"Unsupported OpKind: {opKind}")
            };
        }

        /// <summary>
        ///     This is a helper method which takes the resulting value from GetOperandValueUInt16 and signs it depending on the underlying
        ///     OpKind and MemorySize
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private short GetOperandValueInt16(OpKind opKind, EnumOperandType operandType)
        {
            var value = GetOperandValueUInt16(opKind, operandType);

            return opKind switch
            {
                OpKind.Immediate8 => (sbyte)value,
                OpKind.Immediate16 => (short)value,
                OpKind.Immediate8to16 => (short)value,
                OpKind.Register => (short)value,
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.Int8 => (sbyte)value,
                    MemorySize.UInt8 => (byte)value,
                    MemorySize.Int16 => (short)value,
                    _ => throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}")
                },
                _ => throw new Exception($"Unsupported OpKind: {opKind}")
            };
        }

        /// <summary>
        ///     This is a helper method which takes the resulting value from GetOperandValueUInt64 and signs it depending on the underlying
        ///     This is a helper method which takes the resulting value from GetOperandValueUInt32 and signs it depending on the underlying
        ///     OpKind and MemorySize
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private int GetOperandValueInt32(OpKind opKind, EnumOperandType operandType)
        {
            var value = GetOperandValueUInt32(opKind, operandType);

            return opKind switch
            {
                OpKind.Immediate8 => (sbyte)value,
                OpKind.Immediate16 => (short)value,
                OpKind.Immediate8to16 => (short)value,
                OpKind.Immediate32 => (int)value,
                OpKind.Immediate8to32 => (int)value,
                OpKind.Register => (int)value,
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.Int8 => (sbyte)value,
                    MemorySize.UInt8 => (byte)value,
                    MemorySize.Int16 => (short)value,
                    MemorySize.UInt16 => (ushort)value,
                    MemorySize.Int32 => (int)value,
                    _ => throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}")
                },
                _ => throw new Exception($"Unsupported OpKind: {opKind}")
            };
        }

        /// <summary>
        ///     This is a helper method which takes the resulting value from GetOperandValueUInt64 and signs it depending on the underlying
        ///     OpKind and MemorySize
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private long GetOperandValueInt64(OpKind opKind)
        {
            var value = GetOperandValueUInt64(opKind);

            return opKind switch
            {
                OpKind.Immediate8 => (sbyte)value,
                OpKind.Immediate16 => (short)value,
                OpKind.Immediate8to16 => (short)value,
                OpKind.Immediate32 => (int)value,
                OpKind.Immediate8to32 => (int)value,
                OpKind.Immediate64 => (long)value,
                OpKind.Immediate8to64 => (long)value,
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.Int8 => (sbyte)value,
                    MemorySize.UInt8 => (byte)value,
                    MemorySize.Int16 => (short)value,
                    MemorySize.UInt16 => (ushort)value,
                    MemorySize.Int32 => (int)value,
                    MemorySize.UInt32 => (uint)value,
                    MemorySize.Int64 => (long)value,
                    _ => throw new Exception($"Invalid Operand Size: {_currentInstruction.MemorySize}")
                },
                _ => throw new Exception($"Unsupported OpKind: {opKind}")
            };

        }

        /// <summary>
        ///     Returns the Operand Value as a 64-bit Integer
        ///
        ///     Operand Sizes Equal to or Less than 64-Bit are Supported and returned as a 64-bit Integer
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        ///     Returns the value of an x87 operand: a register of the FPU stack (the first operand's register for the
        ///     destination, the second's for the source) or a 32-, 64- or 80-bit memory operand, converted exactly.
        ///
        ///     A single or double memory operand is taken as the arithmetic instructions see it (a signalling NaN is
        ///     still signalling for the operation's NaN rules); memoryOperandDenormal tells whether it was a denormal
        ///     there, which the operation reports as DE.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private Float80 GetOperandValueFloat80(OpKind opKind, EnumOperandType operandType, out bool memoryOperandDenormal)
        {
            memoryOperandDenormal = false;
            switch (opKind)
            {
                case OpKind.Register:
                    return FpuStack[Registers.Fpu.GetStackPointer(operandType == EnumOperandType.Destination
                        ? _currentInstruction.Op0Register
                        : _currentInstruction.Op1Register)];
                case OpKind.Memory:
                    {
                        var segment = Registers.GetValue(_currentInstruction.MemorySegment);
                        var offset = GetOperandOffset(opKind);
                        switch (_currentInstruction.MemorySize)
                        {
                            case MemorySize.Float32:
                                return Float80.FromSingle(BitConverter.ToUInt32(Memory.GetArray(segment, offset, 4)), out memoryOperandDenormal);
                            case MemorySize.Float64:
                                return Float80.FromDouble(BitConverter.ToUInt64(Memory.GetArray(segment, offset, 8)), out memoryOperandDenormal);
                            case MemorySize.Float80:
                                return Float80.FromBytes(Memory.GetArray(segment, offset, 10));
                            default:
                                throw new Exception($"Unknown Float Operand Size: {_currentInstruction.MemorySize}");
                        }
                    }
                default:
                    throw new Exception($"Unknown Float Operand: {opKind}");
            }
        }

        /// <summary>
        ///     Returns the FPU stack register an x87 arithmetic instruction writes: ST(0) when its source is a memory
        ///     operand, otherwise its first operand (ST(0) for FADD ST(0),ST(i), ST(i) for FADD ST(i),ST(0) and FADDP)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private int GetFpuDestinationIndex() => Registers.Fpu.GetStackPointer(
            _currentInstruction.Op0Kind == OpKind.Memory ? Register.ST0 : _currentInstruction.Op0Register);

        /// <summary>
        ///     Returns the OFFSET of Operand 0
        ///
        ///     Only use this for instructions where Operand 0 is an OFFSET within a Memory Segment
        /// </summary>
        /// <param name="opKind"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private ushort GetOperandOffset(OpKind opKind)
        {
            ushort result;
            switch (opKind)
            {
                case OpKind.Memory:
                    {
                        result = (ushort)_currentInstruction.MemoryDisplacement;
                        switch (_currentInstruction.MemoryBase)
                        {
                            case Register.DS:
                            case Register.None:
                                {
#if DEBUG
                                    if (_currentInstruction.MemoryIndex != Register.None)
                                        throw new Exception($"Unknown GetOperandOffset MemoryBase: {_currentInstruction}");
#endif
                                    break;
                                }

                            case Register.BP:
                                {
                                    result += Registers.BP;

                                    if (_currentInstruction.MemoryIndex == Register.None)
                                        break;

                                    result += Registers.GetValue(_currentInstruction.MemoryIndex);
                                    break;
                                }

                            case Register.BX:
                                {
                                    result += Registers.BX;

                                    if (_currentInstruction.MemoryIndex == Register.None)
                                        break;

                                    result += Registers.GetValue(_currentInstruction.MemoryIndex);
                                    break;
                                }

                            case Register.SI:
                                {
#if DEBUG
                                    if (_currentInstruction.MemoryIndex != Register.None)
                                        throw new Exception($"Unknown GetOperandOffset MemoryBase: {_currentInstruction}");
#endif
                                    result += Registers.SI;
                                    break;
                                }

                            case Register.DI:
                                {
#if DEBUG
                                    if (_currentInstruction.MemoryIndex != Register.None)
                                        throw new Exception($"Unknown GetOperandOffset MemoryBase: {_currentInstruction}");
#endif
                                    result += Registers.DI;
                                    break;
                                }

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
        ///     Returns if the Current Instruction is an 8-bit, 16-bit, or 32-bit operation
        /// </summary>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void WriteToDestination(uint result)
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentOperationSize == 1:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register, (byte)result);
                        return;
                    }
                case OpKind.Register when _currentOperationSize == 2:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register, (ushort)result);
                        return;
                    }
                case OpKind.Register when _currentOperationSize == 4:
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
                            GetOperandOffset(_currentInstruction.Op0Kind), (ushort)result);
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 4:
                    {
                        Memory.SetDWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind), result);
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Destination: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Saves the specified result of the current instruction into the current instructions source
        ///
        ///     This is only used by a couple specific opcodes
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void WriteToSource(ushort result)
        {
            switch (_currentInstruction.Op1Kind)
            {
                case OpKind.Register when _currentOperationSize == 1:
                    {
                        Registers.SetValue(_currentInstruction.Op1Register, (byte)result);
                        return;
                    }
                case OpKind.Register when _currentOperationSize == 2:
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
                    throw new ArgumentOutOfRangeException($"Unknown Source: {_currentInstruction.Op1Kind}");
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_In()
        {
            var port = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            if (!_ioPortHandlers.TryGetValue(port, out var ioport))
            {
                _logger.Warn($"Attempted access of unimplemented IO Port: {port}");
                return;
            }

            var data = ioport.In(port);

            Registers.SetValue(_currentInstruction.Op0Register, data);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Out()
        {
            var port = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Source);

            if (!_ioPortHandlers.TryGetValue(port, out var ioport))
            {
                _logger.Warn($"Attempted access of unimplemented IO Port: {port}");
                return;
            }

            var data = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            ioport.Out(port, data);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Aam()
        {
            var al = Registers.AL;
            var imm = _currentInstruction.Immediate8;

            Registers.AH = (byte)(al / imm);
            Registers.AL = (byte)(al % imm);

            Flags_EvaluateSignZero(Registers.AL);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Daa()
        {
            int res = Registers.AL;
            if ((Registers.AL & 0xF) > 9 || Registers.AuxiliaryCarryFlag)
            {
                res += 6;
                Registers.AuxiliaryCarryFlag = true;
            }
            else
            {
                Registers.AuxiliaryCarryFlag = false;
            }

            if (res > 0x9F || Registers.CarryFlag)
            {
                res += 0x60;
                Registers.CarryFlag = true;
            }
            else
            {
                Registers.CarryFlag = false;
            }

            Registers.AL = (byte)res;

            Flags_EvaluateSignZero(Registers.AL);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Int()
        {
            _interruptHandlers[_currentInstruction.Immediate8].Handle();
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cwd()
        {
            Registers.DX = Registers.AX.IsNegative() ? (ushort)0xFFFF : (ushort)0x0000;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cdq()
        {
            Registers.EDX = Registers.EAX.IsNegative() ? 0xFFFFFFFF : 0;
        }


        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Neg()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Neg_8(),
                2 => Op_Neg_16(),
                4 => Op_Neg_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Neg_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var result = (byte)-destination;
                Registers.CarryFlag = destination != 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ result) & 0x10) != 0;
                //NEG is 0 - destination, so the operand is the source of the subtraction;
                //OF is set only for 0x80, the value whose negation doesn't fit
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, source: destination);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Neg_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var result = (ushort)-destination;
                Registers.CarryFlag = destination != 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ result) & 0x10) != 0;
                //NEG is 0 - destination, so the operand is the source of the subtraction;
                //OF is set only for 0x8000, the value whose negation doesn't fit
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, source: destination);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Neg_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var result = (uint)-(long)destination;
                Registers.CarryFlag = destination != 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ result) & 0x10) != 0;
                //NEG is 0 - destination, so the operand is the source of the subtraction;
                //OF is set only for 0x80000000, the value whose negation doesn't fit
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, source: destination);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Sbb()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Sbb_8(),
                2 => Op_Sbb_16(),
                4 => Op_Sbb_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Sbb_8()
        {
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            unchecked
            {
                var borrowIn = Registers.CarryFlag ? 1 : 0;
                var wideResult = destination - source - borrowIn;
                var result = (byte)wideResult;

                Registers.CarryFlag = wideResult < 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Sbb_16()
        {
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var borrowIn = Registers.CarryFlag ? 1 : 0;
                var wideResult = destination - source - borrowIn;
                var result = (ushort)wideResult;

                Registers.CarryFlag = wideResult < 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Sbb_32()
        {
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                //Widened to long because uint arithmetic would wrap before the borrow can be observed
                var borrowIn = Registers.CarryFlag ? 1L : 0L;
                var wideResult = (long)destination - source - borrowIn;
                var result = (uint)wideResult;

                Registers.CarryFlag = wideResult < 0;
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Clc() => Registers.CarryFlag = false;

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Stc() => Registers.CarryFlag = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Or()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Or_8(),
                2 => Op_Or_16(),
                4 => Op_Or_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            //Clear Flags
            Registers.CarryFlag = Registers.OverflowFlag = false;

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Or_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Or_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Or_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);
            destination |= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        /// <summary>
        /// Performs a right circular rotate (RCR) operation on the current operation size.
        /// </summary>
        /// <remarks>
        /// This method determines the appropriate RCR operation based on the current operation size.
        /// The RCR operation rotates the bits of the value to the right, carrying the least significant
        /// bit (LSB) into the most significant bit (MSB) and the carry flag into the least significant
        /// bit. The result is then written to the destination.
        /// </remarks>
        /// <exception cref="Exception">Thrown when the operation size is not supported.</exception>
        [MethodImpl(OpcodeCompilerOptimizations)]
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

        /// <summary>
        /// Performs an 8-bit right circular rotate (RCR) operation.
        /// </summary>
        /// <returns>The result of the RCR operation.</returns>
        /// <remarks>
        /// The RCR operation rotates the bits of the destination operand to the right by the number of times specified in the source operand.
        /// The carry flag (CF) is included in the rotation.
        /// Each rotation iteration performs the following steps:
        /// 1. Determines the carry flag value after the rotation plus carry.
        /// 2. Performs the rotation by shifting the bits of the destination operand to the right.
        /// 3. If the carry flag is set, the most significant bit of the destination operand is rotated into the least significant bit.
        /// 4. Sets the new carry flag value.
        /// </remarks>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Rcr_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //x86 masks the rotate count to 5 bits, then reduces it modulo (width+1) since
            //the Carry Flag participates in the rotation as an extra, 9th bit
            source &= 0x1F;
            source %= 9;

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(0);

                    //Perform Rotation
                    result >>= 1;

                    //If CF was set, rotate that value in
                    if (Registers.CarryFlag)
                        result |= 1 << 7;

                    //Set new CF Value
                    Registers.CarryFlag = newCFValue;
                }

                //Only on 1 Bit Rotations to we evaluate Overflow
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(7) ^ result.IsBitSet(6);

                return result;
            }
        }

        /// <summary>
        /// Performs an 16-bit right circular rotate (RCR) operation.
        /// </summary>
        /// <returns>The result of the RCR operation.</returns>
        /// <remarks>
        /// The RCR operation rotates the bits of the destination operand to the right by the number of times specified in the source operand.
        /// The carry flag (CF) is included in the rotation.
        /// Each rotation iteration performs the following steps:
        /// 1. Determines the carry flag value after the rotation plus carry.
        /// 2. Performs the rotation by shifting the bits of the destination operand to the right.
        /// 3. If the carry flag is set, the most significant bit of the destination operand is rotated into the least significant bit.
        /// 4. Sets the new carry flag value.
        /// </remarks>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Rcr_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //x86 masks the rotate count to 5 bits, then reduces it modulo (width+1) since
            //the Carry Flag participates in the rotation as an extra, 17th bit
            source &= 0x1F;
            source %= 17;

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    //Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(0);

                    //Perform Rotation
                    result >>= 1;

                    //If CF was set, rotate that value in
                    if (Registers.CarryFlag)
                        result |= 1 << 15;

                    //Set new CF Value
                    Registers.CarryFlag = newCFValue;
                }

                //Only on 1 Bit Rotations to we evaluate Overflow
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(15) ^ result.IsBitSet(14);

                return result;
            }
        }

        /// <summary>
        ///     Performs a left circular rotate (RCL) operation on the current operation size.
        /// </summary>
        /// <exception cref="Exception"></exception>
        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Rcl_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //x86 masks the rotate count to 5 bits, then reduces it modulo (width+1) since
            //the Carry Flag participates in the rotation as an extra, 9th bit
            source &= 0x1F;
            source %= 9;

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    // Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(7);

                    // Perform Rotation
                    result = (byte)(result << 1);

                    // If CF was set, rotate that value in to the LSB (0th bit)
                    if (Registers.CarryFlag)
                        result |= 1;

                    // Set new CF Value
                    Registers.CarryFlag = newCFValue;
                }

                //For 1 Bit Rotations, we evaluate Overflow
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(7) ^ Registers.CarryFlag;

                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Rcl_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //x86 masks the rotate count to 5 bits, then reduces it modulo (width+1) since
            //the Carry Flag participates in the rotation as an extra, 17th bit
            source &= 0x1F;
            source %= 17;

            unchecked
            {
                var result = destination;
                for (var i = 0; i < source; i++)
                {
                    // Determine the CF Value after rotation+carry
                    var newCFValue = result.IsBitSet(15);

                    // Perform Rotation
                    result = (ushort)(result << 1);

                    // If CF was set, rotate that value in to the LSB (0th bit)
                    if (Registers.CarryFlag)
                        result |= 1;

                    // Set new CF Value
                    Registers.CarryFlag = newCFValue;
                }

                //For 1 Bit Rotations, we evaluate Overflow
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(15) ^ Registers.CarryFlag;

                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Sar_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                // in order to inspect bits lost during shift when computing the carry flag, we
                // extend the size of the operation first by shifting left and then shifting right.
                // So result becomes 0xDDLL where DD is the value and LL are the lost bits.
                // The container is sign-extended so the right shift is arithmetic and fills
                // every vacated bit with the sign bit, as SAR requires for any count
                var sresult = ((int)(sbyte)destination << 8) >> source;
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftArithmeticRight, (byte)(sresult & 0x80));

                var result = (byte)((uint)sresult >> 8);

                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftArithmeticRight, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Sar_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                // in order to inspect bits lost during shift when computing the carry flag, we
                // extend the size of the operation first by shifting left and then shifting right.
                // So result becomes 0xDDDDLLLL where DDDD is the value and LLLL are the lost bits.
                // The container is sign-extended so the right shift is arithmetic and fills
                // every vacated bit with the sign bit, as SAR requires for any count
                var sresult = ((int)(short)destination << 16) >> source;
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftArithmeticRight, (ushort)(sresult & 0x8000));

                var result = (ushort)((uint)sresult >> 16);

                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftArithmeticRight, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Shr_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                // in order to inspect bits lost during shift when computing the carry flag, we
                // extend the size of the operation first by shifting left and then shifting right.
                // So result becomes 0xDDLL where DD is the value and LL are the lost bits
                var uresult = (ushort)((destination << 8) >> source);
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftRight, (byte)(uresult & 0x80));

                var result = (byte)(uresult >> 8);
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftRight, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Shr_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                // in order to inspect bits lost during shift when computing the carry flag, we
                // extend the size of the operation first by shifting left and then shifting right.
                // So result becomes 0xDDDDLLLL where DDDD is the value and LLLL are the lost bits
                var uresult = ((uint)destination << 16) >> source;
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftRight, (ushort)(uresult & 0x8000));

                var result = (ushort)(uresult >> 16);
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftRight, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Shl()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Shl_8(),
                2 => Op_Shl_16(),
                4 => Op_Shl_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Shl_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                var uresult = (ushort)(destination << (sbyte)source);
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftLeft, (ushort)(uresult & 0x100));

                var result = (byte)uresult;
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftLeft, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Shl_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                var uresult = (uint)(destination << source);
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftLeft, uresult & 0x1_0000);

                var result = (ushort)uresult;
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftLeft, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Shl_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            source &= 0x1F;

            unchecked
            {
                var uresult = (ulong)(((ulong)destination) << source);
                Flags_EvaluateCarry(EnumArithmeticOperation.ShiftLeft, (uresult & 0x1_0000_0000) != 0 ? 1u : 0u);

                var result = (uint)uresult;
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftLeft, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.ES = dataPointer.Segment;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Lds()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = Memory.GetPointer(segment, offset);

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.DS = dataPointer.Segment;
        }


        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Leave()
        {
            Registers.SP = Registers.BP;
            Registers.BP = Pop();
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Retf()
        {
            Registers.IP = Pop();
            Registers.CS = Pop();

            //Pop N bytes (N/2 words) that were Pushed before the CALL
            if (_currentInstruction.Op0Kind == OpKind.Immediate16)
                Registers.SP += GetOperandValueUInt16(OpKind.Immediate16, EnumOperandType.Destination);

            Registers.Halt |= Registers.CS == 0xFFFF;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Ret()
        {
            Registers.IP = Pop();

            //Pop N bytes (N/2 words) that were Pushed before the CALL
            if (_currentInstruction.Op0Kind == OpKind.Immediate16)
                Registers.SP += GetOperandValueUInt16(OpKind.Immediate16, EnumOperandType.Destination);

            Registers.Halt |= Registers.CS == 0xFFFF;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Stosw()
        {
            Repeat(() =>
            {
                Memory.SetWord(Registers.ES, Registers.DI, Registers.AX);

                if (Registers.DirectionFlag)
                {
                    Registers.DI -= 2;
                }
                else
                {
                    Registers.DI += 2;
                }
            });
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Inc()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Inc_8(),
                2 => Op_Inc_16(),
                4 => Op_Inc_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Inc_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (byte)(destination + 1);
                //INC leaves CF untouched; AF is set when the low nibble wraps from 0xF to 0x0
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Inc_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (ushort)(destination + 1);
                //INC leaves CF untouched; AF is set when the low nibble wraps from 0xF to 0x0
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Inc_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = destination + 1;
                //INC leaves CF untouched; AF is set when the low nibble wraps from 0xF to 0x0
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Dec()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Dec_8(),
                2 => Op_Dec_16(),
                4 => Op_Dec_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Dec_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (byte)(destination - 1);
                //DEC leaves CF untouched; AF is set when the low nibble borrows from 0x0 to 0xF
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0xF;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Dec_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = (ushort)(destination - 1);
                //DEC leaves CF untouched; AF is set when the low nibble borrows from 0x0 to 0xF
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0xF;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Dec_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            unchecked
            {
                var result = destination - 1;
                //DEC leaves CF untouched; AF is set when the low nibble borrows from 0x0 to 0xF
                Registers.AuxiliaryCarryFlag = (result & 0xF) == 0xF;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_And()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_And_8(),
                2 => Op_And_16(),
                4 => Op_And_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);

            //Clear Flags
            Registers.CarryFlag = Registers.OverflowFlag = false;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_And_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_And_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_And_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
            return destination;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Xor()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Xor_8(),
                2 => Op_Xor_16(),
                4 => Op_Xor_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);

            //Clear Flags
            Registers.CarryFlag = Registers.OverflowFlag = false;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Xor_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (byte)(destination ^ source);
            Flags_EvaluateSignZero(result);
            return result;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Xor_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = (ushort)(destination ^ source);
            Flags_EvaluateSignZero(result);
            return result;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Xor_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = destination ^ source;
            Flags_EvaluateSignZero(result);
            return result;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Ja()
        {
            if (!Registers.CarryFlag && !Registers.ZeroFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jae()
        {
            if (!Registers.CarryFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jbe()
        {
            if (Registers.ZeroFlag || Registers.CarryFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

                    var destinationPointer = new FarPtr(destinationPointerData);

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

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jle()
        {
            //ZF == 1 OR SF <> OF
            if (Registers.SignFlag != Registers.OverflowFlag
                || Registers.ZeroFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jge()
        {
            // SF == OF
            if (Registers.SignFlag == Registers.OverflowFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jne()
        {
            //ZF == 0
            if (!Registers.ZeroFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jg()
        {
            //ZF == 0 & SF == OF
            if (!Registers.ZeroFlag &&
                (Registers.SignFlag == Registers.OverflowFlag))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Je()
        {
            // ZF == 1
            if (Registers.ZeroFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jl()
        {
            //SF <> OF
            if (Registers.SignFlag != Registers.OverflowFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jb()
        {
            //CF == 1
            if (Registers.CarryFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jp()
        {
            if (Registers.ParityFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jnp()
        {
            if (!Registers.ParityFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Js()
        {
            //SF == 1
            if (Registers.SignFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Jns()
        {
            //SF == 0
            if (!Registers.SignFlag)
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.Length;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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
                case 4:
                    Op_Test_32();
                    break;
                default:
                    throw new Exception("Unsupported Operation Size");
            }

            //Clear Overflow & Carry
            Registers.CarryFlag = Registers.OverflowFlag = false;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Test_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Test_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Test_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            destination &= source;
            Flags_EvaluateSignZero(destination);
        }

        /// <summary>
        ///     Identical to SUB, just doesn't save result of subtraction
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Sub()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Sub_8(),
                2 => Op_Sub_16(),
                4 => Op_Sub_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Sub_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte)(destination - source);
                Flags_EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Sub_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort)(destination - source);
                Flags_EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Sub_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = destination - source;
                Flags_EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        /// <summary>
        ///     ADD and ADC are exactly the same, except that ADC also adds 1 if the carry flag is set
        ///     So we don't repeat the function, we just pass the ADC flag
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Add(bool addCarry = false)
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Add_8(addCarry),
                2 => Op_Add_16(addCarry),
                4 => Op_Add_32(addCarry),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Add_8(bool addCarry)
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var carryIn = addCarry && Registers.CarryFlag ? 1 : 0;
                var wideResult = destination + source + carryIn;
                var result = (byte)wideResult;

                Registers.CarryFlag = wideResult > byte.MaxValue;
                Registers.AuxiliaryCarryFlag = (((source & 0xF) + (destination & 0xF) + carryIn) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Add_16(bool addCarry)
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var carryIn = addCarry && Registers.CarryFlag ? 1 : 0;
                var wideResult = destination + source + carryIn;
                var result = (ushort)wideResult;

                Registers.CarryFlag = wideResult > ushort.MaxValue;
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Add_32(bool addCarry)
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var carryIn = addCarry && Registers.CarryFlag ? 1u : 0u;
                var wideResult = (ulong)destination + source + carryIn;
                var result = (uint)wideResult;

                Registers.CarryFlag = wideResult > uint.MaxValue;
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Addition, result, destination, source);
                Flags_EvaluateSignZero(result);
                return result;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_1operand()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Imul_1operand_8();
                    return;
                case 2:
                    Op_Imul_1operand_16();
                    return;
                case 4:
                    Op_Imul_1operand_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_1operand_8()
        {
            // Get the values and ensure they are sign-extended properly
            var operand2 = (sbyte)Registers.AL;
            var operand3 = GetOperandValueInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            // Perform the multiplication
            var result = (short)operand2 * operand3;

            // Set AH to the high 8 bits and AL to the low 8 bits of the result
            Registers.AH = (byte)(result >> 8);
            Registers.AL = (byte)(result & 0xFF);

            // Calculate the flags
            Registers.OverflowFlag = Registers.CarryFlag = (short)Registers.AL.ToUshortSignExtended() != result;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_1operand_16()
        {
            // Get the value from AX and the operand, ensuring they are sign-extended properly
            var operand2 = (short)Registers.AX;
            var operand3 = GetOperandValueInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            // Perform the multiplication
            var result = operand2 * operand3;

            // Set DX to the high 16 bits and AX to the low 16 bits of the result
            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);

            // Calculate the flags
            Registers.OverflowFlag = Registers.CarryFlag = Registers.AX.ToUintSignExtended() != (uint)result;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_1operand_32()
        {
            // Get the value from EAX and the operand, ensuring they are sign-extended properly
            var operand2 = (int)Registers.EAX;
            var operand3 = GetOperandValueInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            long result = (long)operand2 * operand3;


            //EDX is High 32-bits, EAX is low 32-bits
            Registers.EDX = (uint)(result >> 32);
            Registers.EAX = (uint)(result & 0xFFFFFFFF);

            //Set CarryFlag and OverflowFlag if the result is too large to fit in the destination
            Registers.OverflowFlag = Registers.CarryFlag = (long)(int)Registers.EAX != result;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_3operand()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Imul_3operand_8();
                    return;
                case 2:
                    Op_Imul_3operand_16();
                    return;
                case 4:
                    Op_Imul_3operand_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_3operand_8()
        {
            var operand2 = GetOperandValueInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operand3 = GetOperandValueInt8(_currentInstruction.Op2Kind, EnumOperandType.Source);
            var result = operand2 * operand3;

            //Set CarryFlag and OverflowFlag if the result is too large to fit in the destination
            Registers.OverflowFlag = Registers.CarryFlag = result is > sbyte.MaxValue or < sbyte.MinValue;

            WriteToDestination((byte)result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_3operand_16()
        {
            var operand2 = GetOperandValueInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operand3 = GetOperandValueInt16(_currentInstruction.Op2Kind, EnumOperandType.Source);
            var result = operand2 * operand3;

            //Set CarryFlag and OverflowFlag if the result is too large to fit in the destination
            Registers.OverflowFlag = Registers.CarryFlag = result is > short.MaxValue or < short.MinValue;

            WriteToDestination((ushort)result);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Imul_3operand_32()
        {
            var operand2 = GetOperandValueInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operand3 = GetOperandValueInt32(_currentInstruction.Op2Kind, EnumOperandType.Source);
            long result = operand2 * operand3;

            //Set CarryFlag and OverflowFlag if the result is too large to fit in the destination
            Registers.OverflowFlag = Registers.CarryFlag = result is > int.MaxValue or < int.MinValue;

            WriteToDestination((uint)result);
        }


        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Mul()
        {
            switch (_currentOperationSize)
            {
                case 1:
                    Op_Mul_8();
                    return;
                case 2:
                    Op_Mul_16();
                    return;
                case 4:
                    Op_Mul_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Mul_8()
        {
            var operand2 = Registers.AL;
            var operand3 = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = operand2 * operand3;

            Registers.AX = (ushort)result;

            Registers.OverflowFlag = Registers.CarryFlag = (result > byte.MaxValue);
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Mul_16()
        {
            var operand2 = Registers.AX;
            var operand3 = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = operand2 * operand3;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);

            Registers.OverflowFlag = Registers.CarryFlag = Registers.DX > 0;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Mul_32()
        {
            var operand2 = Registers.EAX;
            var operand3 = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = (ulong)operand2 * operand3;

            Registers.EDX = (uint)(result >> 32);
            Registers.EAX = (uint)(result & 0xFFFFFFFF);

            Registers.OverflowFlag = Registers.CarryFlag = Registers.EDX > 0;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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
                case 4:
                    Op_Idiv_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Idiv_8()
        {
            var divisor = (sbyte)GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            var quotient = Math.DivRem((short)Registers.AX, divisor, out var remainder);
            if (quotient > sbyte.MaxValue || quotient < sbyte.MinValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AL = (byte)quotient;
            Registers.AH = (byte)remainder;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Idiv_16()
        {
            var divisor = (short)GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            var quotient = Math.DivRem(Registers.GetLong(), divisor, out var remainder);
            if (quotient > short.MaxValue || quotient < short.MinValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.AX = (ushort)quotient;
            Registers.DX = (ushort)remainder;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Idiv_32()
        {
            var divisor = (int)GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = (long)((ulong)Registers.EDX << 32 | Registers.EAX);

            var quotient = Math.DivRem(dividend, divisor, out var remainder);
            if (quotient > int.MaxValue || quotient < int.MinValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            Registers.EAX = (uint)quotient;
            Registers.EDX = (uint)remainder;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Pop()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentOperationSize == 2:
                    Registers.SetValue(_currentInstruction.Op0Register, Pop());
                    break;
                case OpKind.Register when _currentOperationSize == 4:
                    Registers.SetValue(_currentInstruction.Op0Register, PopDWord());
                    break;
                case OpKind.Memory when _currentOperationSize == 2:
                    {
                        Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            Pop());
                        return;
                    }
                case OpKind.Memory when _currentOperationSize == 4:
                    {
                        Memory.SetDWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            PopDWord());
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown POP: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Push Op Code
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Push()
        {
            switch (_currentInstruction.StackPointerIncrement)
            {
                //8Bit
                case -2 when _currentOperationSize == 1:
                    Push(GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination));
                    return;

                //16bit, also default size (-1)
                case -2 when _currentOperationSize == -1:
                case -2 when _currentOperationSize == 2:
                    Push(GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination));
                    return;

                //32Bit
                case -4:
                    Push(GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination));
                    return;
                default:
                    throw new Exception(
                        $"Unsupported Stack Pointer Increment: {_currentInstruction.StackPointerIncrement}");
            }
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
                case OpKind.Register when _currentOperationSize == 4:
                    {
                        Registers.SetValue(_currentInstruction.Op0Register,
                            GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source));
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
                        Memory.SetDWord(Registers.GetValue(_currentInstruction.MemorySegment),
                            GetOperandOffset(_currentInstruction.Op0Kind),
                            GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source));
                        return;
                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown MOV: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Call()
        {
            _previousCallPointer.Segment = Registers.CS;
            _previousCallPointer.Offset = Registers.IP;

            switch (_currentInstruction.Op0Kind)
            {
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

#if DEBUG
                        if (_showDebug)
                            _logger.Info($"CALL {_currentInstruction.FarBranchSelector:X4}:{_currentInstruction.Immediate16:X4}");
#endif

                        _invokeExternalFunctionDelegate(_currentInstruction.FarBranchSelector,
                            _currentInstruction.Immediate16);

                        //Control Transfer occurred in the CALL, so we clean up the stack and return
                        if (ipBeforeCall != Registers.IP)
                            return;

                        //Simulate a LEAVE & retf
                        Registers.SP = Registers.BP;
                        Registers.SetValue(Register.BP, Pop());
                        Registers.SetValue(Register.EIP, Pop());
                        Registers.SetValue(Register.CS, Pop());
                        break;
                    }
                case OpKind.FarBranch16:
                    {
                        //Far call to another Segment
                        Push(Registers.CS);
                        Push((ushort)(Registers.IP + _currentInstruction.Length));

                        Registers.CS = _currentInstruction.FarBranchSelector;
                        Registers.IP = _currentInstruction.FarBranch16;

#if DEBUG
                        if (_showDebug)
                            _logger.Info($"CALL {Registers.CS:X4}:{Registers.IP:X4}");
#endif
                        break;
                    }
                case OpKind.NearBranch16:
                    {
                        //We push CS:IP to the stack
                        //Push the IP of the **NEXT** instruction to the stack
                        Push((ushort)(Registers.IP + _currentInstruction.Length));
                        Registers.IP = _currentInstruction.NearBranch16;

#if DEBUG
                        if (_showDebug)
                            _logger.Info($"CALL {Registers.CS:X4}:{Registers.IP:X4}");
#endif
                        break;
                    }
                case OpKind.Memory when _currentInstruction.IsCallFarIndirect:
                    {
                        Op_Call_CarFarIndirect_M16();
                        break;
                    }
                case OpKind.Memory:
                    {
                        //Pointer calling a SEG:OFF based on a pointer in memory
                        var offset = GetOperandOffset(OpKind.Memory);
                        if (_currentInstruction.IsCallNearIndirect)
                        {
                            Push((ushort)(Registers.IP + _currentInstruction.Length));
                            var destinationOffset =
                                Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offset);
                            Registers.IP = destinationOffset;

#if DEBUG
                            if (_showDebug)
                                _logger.Info($"CALL {Registers.CS:X4}:{Registers.IP:X4}");
#endif

                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException($"Unknown CALL Memory");
                        }

                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }

        /// <summary>
        ///     Indirect Call Far
        ///
        ///     Destination Pointer is loaded from Memory Address
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Call_CarFarIndirect_M16()
        {
            //Far call with target offset at memory location
            Push(Registers.CS);
            Push((ushort)(Registers.IP + _currentInstruction.Length));

            var offset = GetOperandOffset(OpKind.Memory);

            //Detect if the Destination Pointer is an Exported Module or a location within the Module
            var destinationPointer = Registers.GetValue(_currentInstruction.MemorySegment) < 0xFF00
                ? Memory.GetPointer(Registers.GetValue(_currentInstruction.MemorySegment), offset)
                : new FarPtr(Registers.GetValue(_currentInstruction.MemorySegment), offset);

            Registers.CS = destinationPointer.Segment;
            Registers.IP = destinationPointer.Offset;

#if DEBUG
            if (_showDebug)
                _logger.Info($"CALL {Registers.CS:X4}:{Registers.IP:X4}");
#endif

            //Loaded an Exported Function Delegate from Memory
            if (Registers.CS > 0xFF00)
            {
                Push(Registers.BP);
                Registers.BP = Registers.SP;

                var ipBeforeCall = Registers.IP;

                _invokeExternalFunctionDelegate(Registers.CS,
                    Registers.IP);

                //Control Transfer occurred in the CALL, so we clean up the stack and return
                if (ipBeforeCall != Registers.IP)
                    return;

                Registers.SP = Registers.BP;
                Registers.SetValue(Register.BP, Pop());
                Registers.SetValue(Register.EIP, Pop());
                Registers.SetValue(Register.CS, Pop());
            }
        }

        /// <summary>
        ///     Floating Point Load (x87)
        ///
        ///     FLD m32/m64 converts exactly: a signalling NaN becomes quiet (IE), a denormal raises DE. FLD m80 and
        ///     FLD ST(i) copy the register unchanged.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fld()
        {
            Float80 valueToLoad;
            if (_currentInstruction.Op0Kind == OpKind.Memory && _currentInstruction.MemorySize != MemorySize.Float80)
            {
                var bytes = Memory.GetArray(Registers.GetValue(_currentInstruction.MemorySegment),
                    GetOperandOffset(_currentInstruction.Op0Kind), (ushort)(_currentInstruction.MemorySize == MemorySize.Float32 ? 4 : 8));
                valueToLoad = _currentInstruction.MemorySize == MemorySize.Float32
                    ? Float80.FromSingle(BitConverter.ToUInt32(bytes), ref Registers.Fpu.StatusWord)
                    : Float80.FromDouble(BitConverter.ToUInt64(bytes), ref Registers.Fpu.StatusWord);
            }
            else
            {
                valueToLoad = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Destination, out _);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            }

            //Set Value as new ST(0)
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = valueToLoad;
        }

        /// <summary>
        ///     Integer Load Operation (x87): a 16-, 32- or 64-bit integer, always exact
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fild()
        {
            var valueToLoad = Float80.FromInt64(GetOperandValueInt64(_currentInstruction.Op0Kind));
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);

            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = valueToLoad;
        }

        /// <summary>
        ///     Floating Point Multiplication: destination = destination * source (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fmul()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Multiply(FpuStack[destination], source, Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Store & Pop (x87)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fstp()
        {
            Op_Fst();

            Registers.Fpu.PopStackTop(); //Pop the stack setting ST(1)->ST(0)
        }

        /// <summary>
        ///     Store Status Word to Memory or AX Register
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Sahf()
        {
            Registers.SignFlag = Registers.AH.IsFlagSet((byte)EnumFlags.SF);
            Registers.ParityFlag = Registers.AH.IsFlagSet((byte)EnumFlags.PF);
            Registers.ZeroFlag = Registers.AH.IsFlagSet((byte)EnumFlags.ZF);
            Registers.AuxiliaryCarryFlag = Registers.AH.IsFlagSet((byte)EnumFlags.AF);
            Registers.CarryFlag = Registers.AH.IsFlagSet((byte)EnumFlags.CF);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Not()
        {
            var destination = _currentOperationSize switch
            {
                1 => GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                2 => GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                4 => GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination),
                _ => throw new Exception("Unsupported Operation Size")
            };

            var result = ~destination;

            WriteToDestination(result);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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
                1 => GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source),
                2 => GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(source);
            WriteToSource(destination);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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
                case 4:
                    Op_Div_32();
                    return;
                default:
                    throw new Exception("Unsupported Operation Size");
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Div_8()
        {
            var divisor = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = Registers.AX;

            var quotient = dividend / divisor;
            if (quotient > byte.MaxValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            var remainder = dividend % divisor;

            Registers.AL = (byte)quotient;
            Registers.AH = (byte)remainder;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Div_16()
        {
            var divisor = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = (uint)Registers.GetLong();

            var quotient = dividend / divisor;
            if (quotient > ushort.MaxValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            var remainder = dividend % divisor;

            Registers.AX = (ushort)quotient;
            Registers.DX = (ushort)remainder;
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Div_32()
        {
            var divisor = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var dividend = (ulong)Registers.EDX << 32 | Registers.EAX;

            var quotient = dividend / divisor;
            if (quotient > uint.MaxValue)
                throw new OverflowException("Divide Error: Quotient Overflow");

            var remainder = dividend % divisor;

            Registers.EAX = (uint)quotient;
            Registers.EDX = (uint)remainder;
        }

        /// <summary>
        ///     Floating Point Subtraction: destination = destination - source (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsub()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Subtract(FpuStack[destination], source, Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Reverse Subtraction: destination = source - destination (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsubr()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Subtract(source, FpuStack[destination], Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Reverse Subtraction ST(i) = ST(0) - ST(i) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsubrp()
        {
            Op_Fsubr();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Load Zero
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldz()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.Zero;
        }

        /// <summary>
        ///     Load π onto stack, rounded by the rounding control as the 80387 does
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldpi()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.Pi(Registers.Fpu.ControlWord);
        }

        /// <summary>
        ///     Floating Point Load Control Word
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldcw()
        {
            var newControlWord = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.None);

            Registers.Fpu.ControlWord = newControlWord;
        }

        /// <summary>
        ///     Floating Point Store Control Word
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        ///     Floating Point Store Integer (x87): FIST m16/m32, and FISTP m16/m32/m64
        ///
        ///     ST(0) rounded by the rounding control (Borland's float-to-long conversion uses "chop" and a 64-bit
        ///     FISTP); a value that does not fit, an infinity or a NaN stores the integer indefinite (0x8000,
        ///     0x80000000, 0x8000000000000000) and raises IE
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fist()
        {
            var valueFromFpu = FpuStack[Registers.Fpu.GetStackTop()];
            var destinationSegment = Registers.GetValue(_currentInstruction.MemorySegment);
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            switch (_currentInstruction.MemorySize)
            {
                case MemorySize.Int16:
                    Memory.SetWord(destinationSegment, offset,
                        (ushort)valueFromFpu.ToInt16(Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord));
                    break;
                case MemorySize.Int32:
                    {
                        Span<byte> bytes = stackalloc byte[4];
                        BitConverter.TryWriteBytes(bytes, valueFromFpu.ToInt32(Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord));
                        Memory.SetArray(destinationSegment, offset, bytes);
                        break;
                    }
                case MemorySize.Int64:
                    {
                        Span<byte> bytes = stackalloc byte[8];
                        BitConverter.TryWriteBytes(bytes, valueFromFpu.ToInt64(Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord));
                        Memory.SetArray(destinationSegment, offset, bytes);
                        break;
                    }
                default:
                    throw new Exception($"Unsupported Memory Size: {_currentInstruction.MemorySize}");
            }
        }

        /// <summary>
        ///     Floating Point Store Integer and Pop (x87)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fistp()
        {
            Op_Fist();

            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Addition ST(i) = ST(i) + ST(0) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Faddp()
        {
            Op_Fadd();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Clear Interrupt Flag
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cli() => Registers.InterruptFlag = false;

        /// <summary>
        ///     Set Interrupt Flag
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Sti() => Registers.InterruptFlag = true;

        /// <summary>
        ///     Floating Point Store (x87)
        ///
        ///     ST(0) rounded to single or double precision by the rounding control (overflow, underflow and denormal
        ///     results as the coprocessor delivers them), or copied unchanged to an 80-bit operand or to ST(i)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fst()
        {
            var valueToSave = FpuStack[Registers.Fpu.GetStackTop()]; //Save off ST(0)

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float32:
                    {
                        Span<byte> bytes = stackalloc byte[4];
                        BitConverter.TryWriteBytes(bytes, valueToSave.ToSingle(Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord));
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), bytes);
                        break;
                    }
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float64:
                    {
                        Span<byte> bytes = stackalloc byte[8];
                        BitConverter.TryWriteBytes(bytes, valueToSave.ToDouble(Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord));
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), bytes);
                        break;
                    }
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.Float80:
                    {
                        Span<byte> bytes = stackalloc byte[10];
                        valueToSave.ToBytes(bytes);
                        Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), bytes);
                        Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                        break;
                    }
                case OpKind.Register:
                    {
                        FpuStack[Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register)] = valueToSave;
                        Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                        break;
                    }
                default:
                    throw new Exception($"Unsupported Destination: {_currentInstruction.Op0Kind}");
            }
        }

        /// <summary>
        ///     Covert Byte to Word
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cbw()
        {
            Registers.AH = Registers.AL.IsNegative() ? (byte)0xFF : (byte)0x0;
        }

        /// <summary>
        ///     Clears Direction Flag
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cld() => Registers.DirectionFlag = false;

        /// <summary>
        ///     Sets Direction Flag
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Std() => Registers.DirectionFlag = true;

        /// <summary>
        ///     Load byte at address DS:(E)SI into AL.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Lodsb()
        {
            Registers.AL = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            if (Registers.DirectionFlag)
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
        ///
        ///     For a forward (DF=0) REP STOSB that stays within its segment, fills the whole
        ///     run in one shot via Memory.FillArray instead of looping byte-by-byte.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Stosb()
        {
            if (IsRepInstruction() && !Registers.DirectionFlag && !_currentInstruction.HasRepnePrefix)
            {
                var count = Registers.CX;
                if ((uint)Registers.DI + count <= 0x10000)
                {
                    if (count > 0)
                    {
                        Memory.FillArray(Registers.ES, Registers.DI, count, Registers.AL);
                        Registers.DI += count;
                    }

                    Registers.CX = 0;
                    return;
                }
            }

            Repeat(() =>
            {
                Memory.SetByte(Registers.ES, Registers.DI, Registers.AL);

                if (Registers.DirectionFlag)
                {
                    Registers.DI--;
                }
                else
                {
                    Registers.DI++;
                }
            });
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Cmpsb()
        {
            Repeat(() =>
            {
                var destination = Memory.GetByte(Registers.DS, Registers.SI);
                var source = Memory.GetByte(Registers.ES, Registers.DI);
                var result = (byte)(destination - source);
                Flags_EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                Flags_EvaluateSignZero(result);

                if (Registers.DirectionFlag)
                {
                    Registers.DI--;
                    Registers.SI--;
                }
                else
                {
                    Registers.DI++;
                    Registers.SI++;
                }
            }, isRepeCompatible: true);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
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

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private void Op_Rep(Action action, bool isRepeCompatible = false)
        {
            while (Registers.CX != 0)
            {
                action.Invoke();
                Registers.CX--;
                if ((_currentInstruction.HasRepePrefix && isRepeCompatible && !Registers.ZeroFlag) || (_currentInstruction.HasRepnePrefix && Registers.ZeroFlag))
                    break;
            }
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Scasb()
        {
            Repeat(() =>
            {
                var destination = Registers.AL;
                var source = Memory.GetByte(Registers.ES, Registers.DI);

                unchecked
                {
                    var result = (byte)(destination - source);
                    Flags_EvaluateCarry(EnumArithmeticOperation.Subtraction, result, destination);
                    Registers.AuxiliaryCarryFlag = ((destination ^ source ^ result) & 0x10) != 0;
                    Flags_EvaluateOverflow(EnumArithmeticOperation.Subtraction, result, destination, source);
                    Flags_EvaluateSignZero(result);

                    if (Registers.DirectionFlag)
                    {
                        Registers.DI--;
                    }
                    else
                    {
                        Registers.DI++;
                    }
                }
            }, isRepeCompatible: true);
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Loope()
        {
            Registers.CX--;

            if (Registers.ZeroFlag && Registers.CX != 0)
            {
                Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);
                return;
            }

            //Continue with the next instruction
            Registers.IP += (ushort)_currentInstruction.Length;
        }

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Loopne()
        {
            Registers.CX--;

            if (!Registers.ZeroFlag && Registers.CX != 0)
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fld1()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.One;
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Exchanges the contents of registers ST(0) and ST(i). If no source operand is specified, the contents of ST(0) and ST(1) are exchanged.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fxch()
        {
            var destination = Registers.Fpu.GetStackPointer(_currentInstruction.Op0Register);
            var source = Registers.Fpu.GetStackPointer(_currentInstruction.Op1Register);

            //Exchange values
            (FpuStack[destination], FpuStack[source]) = (FpuStack[source], FpuStack[destination]);
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes the arctangent of the source operand in register ST(1) divided by the source operand in register ST(0), stores the result in ST(1), and pops the FPU register stack.
        ///     The FPATAN instruction returns the angle between the X axis and the line from the origin to the point (X,Y), where Y (the ordinate) is ST(1) and X (the abscissa) is ST(0).
        ///
        ///     Computed through double precision (Math.Atan2) on the operands rounded to double: the result may differ
        ///     from a 387's in the last bits. Both operands are first scaled by the same power of two (FSCALE: exact,
        ///     and a zero, an infinity or a NaN stays as it is), so that finite values outside double's range keep
        ///     their ratio. A positive ST(0) more than 2^40 times ST(1) gives the quotient ST(1) / ST(0), as a 387
        ///     does, rounded once in extended precision to the rounding control (a double may not hold it).
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fpatan()
        {
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];
            var ST1 = FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)];

            //atan(ST(1) / ST(0)) is the quotient itself once the exponents differ by more than 40; like the other
            //transcendentals it ignores the precision control
            if (!ST0.IsNegative && (ST0.SignExponent & 0x7FFF) < 0x7FFF &&
                (ST0.SignExponent & 0x7FFF) - (ST1.SignExponent & 0x7FFF) > 40)
            {
                FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = Float80.Divide(ST1, ST0,
                    (ushort)(Registers.Fpu.ControlWord | 0x0300), ref Registers.Fpu.StatusWord);
                Registers.Fpu.PopStackTop();
                return;
            }

            var largestExponent = Math.Max(ST0.IsInfinity || ST0.IsNaN ? 0 : ST0.SignExponent & 0x7FFF,
                ST1.IsInfinity || ST1.IsNaN ? 0 : ST1.SignExponent & 0x7FFF);
            var scale = Float80.FromInt64(0x3FFF - largestExponent);
            ushort scaleStatusWord = 0;
            var ST0x = (double)Float80.Scale(ST0, scale, Registers.Fpu.ControlWord, ref scaleStatusWord);
            var ST1y = (double)Float80.Scale(ST1, scale, Registers.Fpu.ControlWord, ref scaleStatusWord);
            var result = (ST0x, ST1y)
            switch
            {
                (double.NaN, double.NaN) => double.NaN,
                var (x, y) when double.IsNaN(x) && y >= double.NegativeInfinity && y <= double.PositiveInfinity => double.NaN,
                var (x, y) when double.IsNaN(x) && y >= -0 && y <= 0 => double.NaN,
                var (x, y) when double.IsPositiveInfinity(x) && double.IsNaN(y) => double.NaN,
                var (x, y) when double.IsNegativeInfinity(x) && double.IsNaN(y) => double.NaN,
                _ => Math.Atan2(ST1y, ST0x)
            };

            //TODO C1 Set to 0 if stack underflow occurred. Set if result was rounded up; cleared otherwise. C0, C2, C3 Undefined.

            //Store result at ST1
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = result;

            //Pop the Stack making ST(1)->ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Truncates the value in ST(1) toward 0 to an integral value and adds that value to the exponent of ST(0),
        ///     rounding by the rounding control where the result overflows or underflows.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fscale()
        {
            var top = Registers.Fpu.GetStackTop();
            FpuStack[top] = Float80.Scale(FpuStack[top], FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)],
                Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord);
        }

        /// <summary>
        ///     Floating Point Square Root Operation (x87)
        ///
        ///     Computes square root of ST(0) and stores the result in ST(0), rounded to the precision control and the
        ///     rounding control; -0 gives -0 and a negative operand is invalid.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsqrt()
        {
            var top = Registers.Fpu.GetStackTop();
            FpuStack[top] = Float80.SquareRoot(FpuStack[top], Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord);
        }

        /// <summary>
        ///     Floating Point Division: destination = destination / source (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fdiv()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Divide(FpuStack[destination], source, Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Multiplication ST(i) = ST(i) * ST(0) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fmulp()
        {
            Op_Fmul();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Compares the contents of register ST(0) and source value and sets condition code flags C0, C2, and C3 in the FPU status word
        ///     according to the results. The source operand can be a data register or a memory location. If no source operand is given,
        ///     the value in ST(0) is compared with the value in ST(1). The sign of zero is ignored, so that –0.0 is equal to +0.0.
        ///     A NaN gives "unordered" (C3, C2 and C0 set) and IE.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fcom()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            Float80.Compare(FpuStack[Registers.Fpu.GetStackTop()], source, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Same as FCOM, but Pops the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fcompp()
        {
            Float80.Compare(FpuStack[Registers.Fpu.GetStackTop()], FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)],
                ref Registers.Fpu.StatusWord);

            Registers.Fpu.PopStackTop();
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Push Flags Register to Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Pushf() => Push(Registers.F());

        /// <summary>
        ///     Push Flags Register to Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Pushfd() => Push(Registers.EF());

        /// <summary>
        ///     Floating Point Addition: destination = destination + source (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fadd()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Add(FpuStack[destination], source, Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Division ST(i) = ST(i) / ST(0) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fdivp()
        {
            Op_Fdiv();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Clears Exception Flags from the x87 FPU Status Register
        ///
        ///     FCLEX is similar to FNCLEX, except it is preceded by a WAIT which is ignored
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fclex()
        {
            Registers.Fpu.ClearExceptions();
        }

        /// <summary>
        ///     Rounds the value at ST(0) to an Integral Value by the rounding control and stores it in ST(0)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Frndint()
        {
            var top = Registers.Fpu.GetStackTop();
            FpuStack[top] = Float80.RoundToInteger(FpuStack[top], Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord);
        }

        /// <summary>
        ///     Rotate Left
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Rol()
        {
            var result = _currentOperationSize switch
            {
                1 => Op_Rol_8(),
                2 => Op_Rol_16(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     8-bit Rotate Left
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Rol_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            source &= 0x1F;
            if (source == 0)
                return destination;

            unchecked
            {
                //rotation is modulo the operand width; a multiple of the width
                //(count 8, 16, 24) is the identity but still updates CF below
                var rotate = source % 8;
                var result = (byte)((destination << rotate) | (destination >> (8 - rotate)));

                //CF Set if Least Significant Bit set to 1 (the bit rotated out of the top)
                Registers.CarryFlag = result.IsBitSet(0);

                //If the Most Significant Bit and CF are not the same, then we overflowed for 1 bit rotations
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(7) != Registers.CarryFlag;

                return result;
            }
        }

        /// <summary>
        ///     16-bit Rotate Left
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Rol_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            source &= 0x1F;
            if (source == 0)
                return destination;

            unchecked
            {
                //rotation is modulo the operand width; a multiple of the width
                //(count 16) is the identity but still updates CF below
                var rotate = source % 16;
                var result = (ushort)((destination << rotate) | (destination >> (16 - rotate)));

                //CF Set if Least Significant Bit set to 1 (the bit rotated out of the top)
                Registers.CarryFlag = result.IsBitSet(0);

                //If the Most Significant Bit and CF are not the same, then we overflowed for 1 bit rotations
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(15) != Registers.CarryFlag;

                return result;
            }
        }

        /// <summary>
        ///     Rotate Right
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
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
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private byte Op_Ror_8()
        {
            var destination = GetOperandValueUInt8(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            source &= 0x1F;
            if (source == 0)
                return destination;

            unchecked
            {
                //rotation is modulo the operand width; a multiple of the width
                //(count 8, 16, 24) is the identity but still updates CF below
                var rotate = source % 8;
                var result = (byte)((destination >> rotate) | (destination << (8 - rotate)));

                //CF Set if Most Significant Bit set to 1 (the bit rotated out of the bottom)
                Registers.CarryFlag = result.IsNegative();

                //If Bits 7 & 6 are not the same, then we overflowed for 1 bit rotations
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(7) != result.IsBitSet(6);

                return result;
            }
        }

        /// <summary>
        ///     16-bit Rotate Right
        /// </summary>
        /// <returns></returns>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Ror_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            source &= 0x1F;
            if (source == 0)
                return destination;

            unchecked
            {
                //rotation is modulo the operand width; a multiple of the width
                //(count 16) is the identity but still updates CF below
                var rotate = source % 16;
                var result = (ushort)((destination >> rotate) | (destination << (16 - rotate)));

                //CF Set if Most Significant Bit set to 1 (the bit rotated out of the bottom)
                Registers.CarryFlag = result.IsNegative();

                //If Bits 15 & 14 are not the same, then we overflowed for 1 bit rotations
                if (source == 1)
                    Registers.OverflowFlag = result.IsBitSet(15) != result.IsBitSet(14);

                return result;
            }
        }

        /// <summary>
        ///     Floating Point Compare ST(0) to 0.0 (x87)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Ftst()
        {
            Float80.Compare(FpuStack[Registers.Fpu.GetStackTop()], Float80.Zero, ref Registers.Fpu.StatusWord);
        }

        /// <summary>
        ///     Floating Point Reverse Division: destination = source / destination (x87)
        ///
        ///     A memory operand (32 or 64 bits) works on ST(0); the register forms work on their first operand.
        ///     Rounded once to the precision control and the rounding control of the control word.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fdivr()
        {
            var source = GetOperandValueFloat80(_currentInstruction.Op0Kind, EnumOperandType.Source, out var memoryOperandDenormal);
            var destination = GetFpuDestinationIndex();
            FpuStack[destination] = Float80.Divide(source, FpuStack[destination], Registers.Fpu.ControlWord, ref Registers.Fpu.StatusWord, memoryOperandDenormal);
        }

        /// <summary>
        ///     Floating Point Reverse Division ST(i) = ST(0) / ST(i) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fdivrp()
        {
            Op_Fdivr();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Sine (x87)
        ///
        ///     |ST(0)| >= 2^63 is outside the range the instruction reduces: C2 set, C1 cleared, ST(0) unchanged.
        ///     An infinite ST(0) is an invalid operand: ST(0) becomes the real indefinite (IE), C1 and C2 are cleared.
        ///
        ///     Computed through double precision (Math) on ST(0) rounded to double: the result may differ from a 387's
        ///     in the last bits.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsin()
        {
            var top = Registers.Fpu.GetStackTop();
            if ((FpuStack[top].SignExponent & 0x7FFF) is >= 0x3FFF + 63 and < 0x7FFF)
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                return;
            }

            var valueToSin = (double)FpuStack[top];

            switch (valueToSin)
            {
                case double.PositiveInfinity:
                case double.NegativeInfinity:
                    FpuStack[top] = Float80.Indefinite;
                    Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                    return;
                case double.NaN:
                    return;
            }

            FpuStack[top] = Math.Sin(valueToSin);
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
        }

        /// <summary>
        ///     Floating Point Cosine (x87)
        ///
        ///     |ST(0)| >= 2^63 is outside the range the instruction reduces: C2 set, C1 cleared, ST(0) unchanged.
        ///     An infinite ST(0) is an invalid operand: ST(0) becomes the real indefinite (IE), C1 and C2 are cleared.
        ///
        ///     Computed through double precision (Math) on ST(0) rounded to double: the result may differ from a 387's
        ///     in the last bits.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fcos()
        {
            var top = Registers.Fpu.GetStackTop();
            if ((FpuStack[top].SignExponent & 0x7FFF) is >= 0x3FFF + 63 and < 0x7FFF)
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                return;
            }

            var valueToCos = (double)FpuStack[top];

            switch (valueToCos)
            {
                case double.PositiveInfinity:
                case double.NegativeInfinity:
                    FpuStack[top] = Float80.Indefinite;
                    Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                    return;
                case double.NaN:
                    return;
            }

            FpuStack[top] = Math.Cos(valueToCos);
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
        }

        /// <summary>
        ///     Load String Operand
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Lodsw()
        {
            Registers.AX = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);

            if (Registers.DirectionFlag)
            {
                Registers.SI -= 2;
            }
            else
            {
                Registers.SI += 2;
            }
        }

        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private bool IsRepInstruction() => _currentInstruction.HasRepPrefix || _currentInstruction.HasRepnePrefix || _currentInstruction.HasRepePrefix;

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Repeat(Action action, bool isRepeCompatible = false)
        {
            if (IsRepInstruction())
                Op_Rep(action, isRepeCompatible);
            else
                action.Invoke();
        }

        /// <summary>
        ///     Move data from String to String
        ///
        ///     For a forward (DF=0) REP MOVSB that stays within its segments, copies the whole
        ///     run in one shot via Memory.GetArray/SetArray instead of looping byte-by-byte.
        ///     Falls back to the byte-at-a-time path for the backward-direction, unprefixed,
        ///     or segment-wraparound cases so behavior is unchanged there.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Movsb()
        {
            if (IsRepInstruction() && !Registers.DirectionFlag && !_currentInstruction.HasRepnePrefix)
            {
                var count = Registers.CX;
                if ((uint)Registers.SI + count <= 0x10000 && (uint)Registers.DI + count <= 0x10000)
                {
                    if (count > 0)
                    {
                        var source = Memory.GetArray(Registers.DS, Registers.SI, count);
                        Memory.SetArray(Registers.ES, Registers.DI, source);
                        Registers.SI += count;
                        Registers.DI += count;
                    }

                    Registers.CX = 0;
                    return;
                }
            }

            Repeat(() =>
            {
                Memory.SetByte(Registers.ES, Registers.DI, Memory.GetByte(Registers.DS, Registers.SI));

                if (Registers.DirectionFlag)
                {
                    Registers.DI -= 1;
                    Registers.SI -= 1;
                }
                else
                {
                    Registers.DI += 1;
                    Registers.SI += 1;
                }
            });
        }

        /// <summary>
        ///     Move data from String to String
        ///
        ///     Same forward-run bulk-copy fast path as <see cref="Op_Movsb"/>, sized in words.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Movsw()
        {
            if (IsRepInstruction() && !Registers.DirectionFlag && !_currentInstruction.HasRepnePrefix)
            {
                var count = Registers.CX;
                var byteCount = count * 2;
                if (byteCount <= ushort.MaxValue && (uint)Registers.SI + byteCount <= 0x10000 && (uint)Registers.DI + byteCount <= 0x10000)
                {
                    if (count > 0)
                    {
                        var source = Memory.GetArray(Registers.DS, Registers.SI, (ushort)byteCount);
                        Memory.SetArray(Registers.ES, Registers.DI, source);
                        Registers.SI += (ushort)byteCount;
                        Registers.DI += (ushort)byteCount;
                    }

                    Registers.CX = 0;
                    return;
                }
            }

            Repeat(() =>
            {
                Memory.SetWord(Registers.ES, Registers.DI, Memory.GetWord(Registers.DS, Registers.SI));

                if (Registers.DirectionFlag)
                {
                    Registers.DI -= 2;
                    Registers.SI -= 2;
                }
                else
                {
                    Registers.DI += 2;
                    Registers.SI += 2;
                }
            });
        }

        /// <summary>
        ///     Move with Sign-Extend
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Movsx()
        {

            var result = _currentOperationSize switch
            {
                2 => Op_Movsx_16(),
                4 => Op_Movsx_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     Move with Sign-Extend to 16bit destination
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Movsx_16()
        {
            var result = GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source).ToUshortSignExtended();
            return result;
        }

        /// <summary>
        ///     Move with Sign-Extend to 32bit destination
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Movsx_32()
        {
            var sourceSize = _currentInstruction.Op1Kind switch
            {
                OpKind.Register => GetSize(_currentInstruction.Op1Register),
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.Int8 => 1,
                    MemorySize.Int16 => 2,
                    _ => throw new NotImplementedException()
                },
                _ => throw new NotImplementedException()
            };

            var result = sourceSize switch
            {
                (1) => GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source).ToUintSignExtended(),
                (2) => GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source).ToUintSignExtended(),
                _ => throw new NotImplementedException(),
            };
            return result;
        }

        /// <summary>
        ///     Move with Zero-Extend
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Movzx()
        {

            var result = _currentOperationSize switch
            {
                2 => Op_Movzx_16(),
                4 => Op_Movzx_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     Move with Zero-Extend to 16bit destination
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Movzx_16()
        {
            var result = (ushort)GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source);
            return result;
        }

        /// <summary>
        ///     Move with Zero-Extend to 32bit destination
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Movzx_32()
        {
            var sourceSize = _currentInstruction.Op1Kind switch
            {
                OpKind.Register => GetSize(_currentInstruction.Op1Register),
                OpKind.Memory => _currentInstruction.MemorySize switch
                {
                    MemorySize.UInt8 => 1,
                    MemorySize.UInt16 => 2,
                    _ => throw new NotImplementedException()
                },
                _ => throw new NotImplementedException()
            };

            var result = sourceSize switch
            {
                (1) => (uint)GetOperandValueUInt8(_currentInstruction.Op1Kind, EnumOperandType.Source),
                (2) => (uint)GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source),
                _ => throw new NotImplementedException(),
            };
            return result;
        }

        /// <summary>
        ///     Pop from Stack into the Flags Register
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Popf() => Registers.SetF(Pop());

        /// <summary>
        ///     Pop from Stack into the Flags Register
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Popfd() => Registers.SetEF(PopDWord());

        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Iret()
        {
            Registers.IP = Pop();
            Registers.CS = Pop();
            Registers.SetF(Pop());
        }

        /// <summary>
        ///     Floating Point Subtraction ST(i) = ST(i) - ST(0) (x87), then Pop the FPU Stack
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsubp()
        {
            Op_Fsub();

            //ST(1) becomes ST(0)
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Change Sign of value in ST(0)
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fchs()
        {
            FpuStack[Registers.Fpu.GetStackTop()] = FpuStack[Registers.Fpu.GetStackTop()].Negate();
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes the absolute value of ST(0) and stores the result in ST(0).
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fabs()
        {
            FpuStack[Registers.Fpu.GetStackTop()] = FpuStack[Registers.Fpu.GetStackTop()].Abs();
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes both the sine and the cosine of ST(0), stores the sine in ST(0), and pushes the cosine onto the register stack.
        ///
        ///     |ST(0)| >= 2^63 is outside the range the instruction reduces: C2 set, C1 cleared, ST(0) unchanged.
        ///     An infinite ST(0) is an invalid operand: ST(0) and the pushed value become the real indefinite (IE),
        ///     and C1 and C2 are cleared.
        ///
        ///     Computed through double precision (Math) on ST(0) rounded to double: the result may differ from a 387's
        ///     in the last bits.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fsincos()
        {
            var top = Registers.Fpu.GetStackTop();
            if ((FpuStack[top].SignExponent & 0x7FFF) is >= 0x3FFF + 63 and < 0x7FFF)
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                return;
            }

            var value = (double)FpuStack[top];

            switch (value)
            {
                case double.PositiveInfinity:
                case double.NegativeInfinity:
                    FpuStack[top] = Float80.Indefinite;
                    Registers.Fpu.PushStackTop();
                    FpuStack[Registers.Fpu.GetStackTop()] = Float80.Indefinite;
                    Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                    return;
                case double.NaN:
                    return;
            }

            FpuStack[top] = Math.Sin(value);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Math.Cos(value);
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Separates ST(0) into its exponent and significand, stores the significand (in the range [1, 2)) in ST(0),
        ///     and pushes the unbiased exponent onto the register stack. Zero gives -infinity as the exponent (ZE).
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fxtract()
        {
            var significand = Float80.Extract(FpuStack[Registers.Fpu.GetStackTop()], out var exponent, ref Registers.Fpu.StatusWord);

            FpuStack[Registers.Fpu.GetStackTop()] = exponent;
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = significand;
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes the partial remainder obtained from dividing ST(0) by ST(1) (the quotient truncated toward zero)
        ///     and stores the result in ST(0). C0, C3, C1 receive bits 2, 1, 0 of the quotient; C2 is set when the
        ///     reduction is incomplete (exponents 64 or more apart) and the program must run FPREM again.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fprem()
        {
            var top = Registers.Fpu.GetStackTop();
            FpuStack[top] = Float80.PartialRemainder(FpuStack[top], FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)],
                ref Registers.Fpu.StatusWord);
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes 2^ST(0) - 1 and stores the result in ST(0). Valid for ST(0) in the range [-1, 1].
        ///
        ///     Computed through double precision (Math) on ST(0) rounded to double: the result may differ from a 387's
        ///     in the last bits.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_F2xm1()
        {
            var top = Registers.Fpu.GetStackTop();
            FpuStack[top] = Math.Pow(2, (double)FpuStack[top]) - 1;
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes (ST(1) * log2(ST(0))), stores the result in ST(1), and pops the FPU register stack.
        ///     ST(0) must be positive and neither operand may be NaN; per the x87 spec, a negative or NaN
        ///     ST(0)/ST(1), or an indeterminate 0 * Infinity combination, is an invalid operation and yields
        ///     a NaN result (the stack still pops).
        ///
        ///     log2(ST(0)) is its exponent plus log2 of its significand (FXTRACT; a zero raises ZE), the logarithm of
        ///     the significand computed through double precision (Math): the result may differ from a 387's in the
        ///     last bits. For ST(0) in [0.5, 2) it is log2(1 + d) with d = ST(0) - 1, which is exact, so that a value
        ///     next to 1 keeps the digits a double would round away. The product with ST(1) is rounded once in
        ///     extended precision to the rounding control (like a 387, the transcendentals ignore the precision
        ///     control), so neither operand is limited to double's range.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fyl2x()
        {
            var ST0x = FpuStack[Registers.Fpu.GetStackTop()];
            var ST1y = FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)];

            if (ST0x.IsNaN || ST1y.IsNaN || (ST0x.IsNegative && !ST0x.IsZero) ||
                (ST0x.IsZero && ST1y.IsZero) || (ST0x.IsInfinity && ST1y.IsZero))
            {
                FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = Float80.Indefinite;
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                Registers.Fpu.PopStackTop();
                return;
            }

            //Extended precision whatever the precision control, rounded to the rounding control
            var controlWord = (ushort)(Registers.Fpu.ControlWord | 0x0300);
            Float80 log2;
            if ((ST0x.SignExponent & 0x7FFF) is 0x3FFE or 0x3FFF)
            {
                //log2(1 + offset) as offset * log2(argument) / (argument - 1), argument = 1 + offset rounded to double;
                //the offset is exact, and rounding to nearest keeps log2(1) = +0 under every rounding control
                ushort offsetStatusWord = 0;
                var offset = Float80.Subtract(ST0x, Float80.One, 0x037F, ref offsetStatusWord);
                var argument = 1.0 + (double)offset;
                log2 = Float80.Multiply(offset,
                    argument == 1.0 ? Math.Log2(Math.E) : Math.Log2(argument) / (argument - 1.0), controlWord,
                    ref Registers.Fpu.StatusWord);
            }
            else
            {
                var significand = Float80.Extract(ST0x, out var exponent, ref Registers.Fpu.StatusWord);
                log2 = (double)exponent + Math.Log2((double)significand);
            }
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] =
                Float80.Multiply(ST1y, log2, controlWord, ref Registers.Fpu.StatusWord);

            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes (ST(1) * log2(ST(0) + 1.0)), stores the result in ST(1), and pops the FPU register stack.
        ///     Same invalid-operation guards as FYL2X, applied to (ST(0) + 1.0) since that is the value being
        ///     passed to log2.
        ///
        ///     log2(ST(0) + 1.0) is computed as ST(0) * log2(u) / (u - 1), with u = ST(0) + 1.0 rounded to double and
        ///     the logarithm through double precision (Math): u - 1 is exact, so the rounding of u cancels out and a
        ///     small ST(0) keeps its precision (u == 1: the factor is log2(e)); the result may differ from a 387's in
        ///     the last bits. The products with ST(0) and ST(1) are rounded in extended precision to the rounding
        ///     control (like a 387, the transcendentals ignore the precision control), so ST(1) is not limited to
        ///     double's range.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fyl2xp1()
        {
            var ST0 = FpuStack[Registers.Fpu.GetStackTop()];
            var ST0x = (double)ST0;
            var ST1y = FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)];
            var argument = ST0x + 1.0;

            if (double.IsNaN(ST0x) || ST1y.IsNaN || argument < 0 ||
                (argument == 0 && ST1y.IsZero) || (double.IsPositiveInfinity(argument) && ST1y.IsZero))
            {
                FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] = Float80.Indefinite;
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                Registers.Fpu.PopStackTop();
                return;
            }

            //Extended precision whatever the precision control, rounded to the rounding control
            var controlWord = (ushort)(Registers.Fpu.ControlWord | 0x0300);

            //An infinite u (ST(0) = +infinity, outside the instruction's range) gives log2(u) itself: +infinity
            var log2 = double.IsPositiveInfinity(argument)
                ? argument
                : Float80.Multiply(ST0, argument == 1.0 ? Math.Log2(Math.E) : Math.Log2(argument) / (argument - 1.0),
                    controlWord, ref Registers.Fpu.StatusWord);
            FpuStack[Registers.Fpu.GetStackPointer(Register.ST1)] =
                Float80.Multiply(ST1y, log2, controlWord, ref Registers.Fpu.StatusWord);

            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Floating Point Operation (x87)
        ///
        ///     Computes the tangent of ST(0), stores the result in ST(0), and pushes a 1.0 onto the register stack.
        ///
        ///     |ST(0)| >= 2^63 is outside the range the instruction reduces: C2 set, C1 cleared, ST(0) unchanged.
        ///     An infinite ST(0) is an invalid operand: ST(0) and the pushed value become the real indefinite (IE),
        ///     and C1 and C2 are cleared.
        ///
        ///     Computed through double precision (Math) on ST(0) rounded to double: the result may differ from a 387's
        ///     in the last bits.
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fptan()
        {
            var top = Registers.Fpu.GetStackTop();
            if ((FpuStack[top].SignExponent & 0x7FFF) is >= 0x3FFF + 63 and < 0x7FFF)
            {
                Registers.Fpu.SetFlag(EnumFpuStatusFlags.Code2);
                Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                return;
            }

            var value = (double)FpuStack[top];

            switch (value)
            {
                case double.PositiveInfinity:
                case double.NegativeInfinity:
                    FpuStack[top] = Float80.Indefinite;
                    Registers.Fpu.PushStackTop();
                    FpuStack[Registers.Fpu.GetStackTop()] = Float80.Indefinite;
                    Registers.Fpu.SetFlag(EnumFpuStatusFlags.InvalidOperationException);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
                    Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
                    return;
                case double.NaN:
                    return;
            }

            FpuStack[top] = Math.Tan(value);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.One;
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code2);
        }

        /// <summary>
        ///     Pushes log2(e) onto the FPU register stack, rounded by the rounding control
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldl2e()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.Log2E(Registers.Fpu.ControlWord);
        }

        /// <summary>
        ///     Pushes ln(2) onto the FPU register stack, rounded by the rounding control
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldln2()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.LnTwo(Registers.Fpu.ControlWord);
        }

        /// <summary>
        ///     Pushes log10(2) onto the FPU register stack, rounded by the rounding control
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldlg2()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.Log10Two(Registers.Fpu.ControlWord);
        }

        /// <summary>
        ///     Pushes log2(10) onto the FPU register stack, rounded by the rounding control
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fldl2t()
        {
            Registers.Fpu.ClearFlag(EnumFpuStatusFlags.Code1);
            Registers.Fpu.PushStackTop();
            FpuStack[Registers.Fpu.GetStackTop()] = Float80.Log2Ten(Registers.Fpu.ControlWord);
        }

        /// <summary>
        ///     Increments the FPU stack pointer without storing a value, equivalent to discarding ST(0)
        ///     and promoting ST(1) to ST(0). Our internal stack-top counter runs in the opposite direction
        ///     of the physical x87 TOP field, so this maps to PopStackTop() (see Fpatan/Fyl2x for the same pattern).
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fincstp()
        {
            Registers.Fpu.PopStackTop();
        }

        /// <summary>
        ///     Decrements the FPU stack pointer without storing a value, making room for a new ST(0)
        ///     and demoting the old ST(0) to ST(1). Maps to PushStackTop() for the same reason as Op_Fincstp().
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Fdecstp()
        {
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Extended Shift Left
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Shld()
        {
            var result = _currentOperationSize switch
            {
                2 => Op_Shld_16(),
                4 => Op_Shld_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     16-bit Extended Shift Left
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Shld_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var count = GetOperandValueUInt8(_currentInstruction.Op2Kind, EnumOperandType.Count);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            count &= 0x1F;
            if (count == 0)
                return destination;

            //Shifting the pair as one 32-bit value keeps the fill and the carry bit
            //in range for counts above the 16-bit operand width, where the result is
            //undefined on hardware but must still not throw
            var pair = ((uint)destination << 16) | source;

            //CF == the last bit shifted out of the destination
            Registers.CarryFlag = (pair & (1u << (32 - count))) != 0;

            var result = (ushort)((pair << count) >> 16);

            //Only evaluate Overflow on Shift of 1 Bit, otherwise it's clear
            if (count == 1)
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftLeft, result, destination, count);
            else
                Registers.OverflowFlag = false;

            Flags_EvaluateSignZero(result);

            return result;
        }

        /// <summary>
        ///     32-bit Extended Shift Left
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Shld_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var count = GetOperandValueUInt8(_currentInstruction.Op2Kind, EnumOperandType.Count);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            count &= 0x1F;
            if (count == 0)
                return destination;

            //The vacated low bits take the TOP count bits of the source, not its
            //bottom ones -- source >> count only happens to agree at counts 1, 16
            //and 32, which is every count the original tests covered
            var result = (destination << count) | (source >> ((sizeof(uint) * 8) - count));

            //CF == the last bit shifted out of the destination
            Registers.CarryFlag = destination.IsBitSet((sizeof(uint) * 8) - count);

            //Only evaluate Overflow on Shift of 1 Bit, otherwise it's clear
            if (count == 1)
                Flags_EvaluateOverflow(EnumArithmeticOperation.ShiftLeft, result, destination, count);
            else
                Registers.OverflowFlag = false;

            Flags_EvaluateSignZero(result);

            return result;
        }

        /// <summary>
        ///     Extended Shift Right
        /// </summary>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Op_Shrd()
        {
            var result = _currentOperationSize switch
            {
                2 => Op_Shrd_16(),
                4 => Op_Shrd_32(),
                _ => throw new Exception("Unsupported Operation Size")
            };

            WriteToDestination(result);
        }

        /// <summary>
        ///     16-bit Extended Shift Right
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private ushort Op_Shrd_16()
        {
            var destination = GetOperandValueUInt16(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt16(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var count = GetOperandValueUInt8(_currentInstruction.Op2Kind, EnumOperandType.Count);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            count &= 0x1F;
            if (count == 0)
                return destination;

            //Source feeds in from the top, so it occupies the high half of the pair
            var pair = ((uint)source << 16) | destination;

            //CF == the last bit shifted out of the destination
            Registers.CarryFlag = (pair & (1u << (count - 1))) != 0;

            var result = (ushort)(pair >> count);

            //Only evaluate Overflow on Shift of 1 Bit, otherwise it's clear.
            //SHRD shifts the source's bits into the top of the destination, so the
            //SHR convention (OF == the destination's old sign) does not hold here --
            //OF is set when the sign bit actually changed
            if (count == 1)
                Registers.OverflowFlag = result.IsNegative() != destination.IsNegative();
            else
                Registers.OverflowFlag = false;

            Flags_EvaluateSignZero(result);

            return result;
        }

        /// <summary>
        ///     32-bit Extended Shift Right
        /// </summary>
        [MethodImpl(OpcodeSubroutineCompilerOptimizations)]
        private uint Op_Shrd_32()
        {
            var destination = GetOperandValueUInt32(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValueUInt32(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var count = GetOperandValueUInt8(_currentInstruction.Op2Kind, EnumOperandType.Count);

            //286+ masks the count to 5 bits; a masked count of 0 leaves the
            //destination and all flags untouched
            count &= 0x1F;
            if (count == 0)
                return destination;

            //The vacated high bits take the BOTTOM count bits of the source
            var result = (destination >> count) | (source << ((sizeof(uint) * 8) - count));

            //CF == the last bit shifted out of the destination
            Registers.CarryFlag = destination.IsBitSet(count - 1);

            //Only evaluate Overflow on Shift of 1 Bit, otherwise it's clear.
            //SHRD shifts the source's bits into the top of the destination, so the
            //SHR convention (OF == the destination's old sign) does not hold here --
            //OF is set when the sign bit actually changed
            if (count == 1)
                Registers.OverflowFlag = result.IsNegative() != destination.IsNegative();
            else
                Registers.OverflowFlag = false;

            Flags_EvaluateSignZero(result);

            return result;
        }

        /// <summary>
        ///     Evaluates the given 8-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateCarry(EnumArithmeticOperation arithmeticOperation, byte result = 0,
            byte destination = 0, byte source = 0)
        {
            var setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => (source + destination) > byte.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftArithmeticLeft => result != 0,
                EnumArithmeticOperation.ShiftArithmeticRight => result != 0,
                EnumArithmeticOperation.ShiftLeft => result != 0,
                EnumArithmeticOperation.ShiftRight => result != 0,
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                    "Unsupported Carry Flag Operation for Evaluation"),
            };

            Registers.CarryFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates the given 16-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateCarry(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            var setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => (source + destination) > ushort.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftArithmeticLeft => result != 0,
                EnumArithmeticOperation.ShiftArithmeticRight => result != 0,
                EnumArithmeticOperation.ShiftLeft => result != 0,
                EnumArithmeticOperation.ShiftRight => result != 0,
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                    "Unsupported Carry Flag Operation for Evaluation")
            };

            Registers.CarryFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates the given 32-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateCarry(EnumArithmeticOperation arithmeticOperation, uint result = 0,
            uint destination = 0, uint source = 0)
        {
            var setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => ((ulong)source + destination) > uint.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftArithmeticLeft => result != 0,
                EnumArithmeticOperation.ShiftArithmeticRight => result != 0,
                EnumArithmeticOperation.ShiftLeft => result != 0,
                EnumArithmeticOperation.ShiftRight => result != 0,
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                    "Unsupported Carry Flag Operation for Evaluation")
            };

            Registers.CarryFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates the given 8-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, byte result = 0,
            byte destination = 0, byte source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticLeft:
                case EnumArithmeticOperation.ShiftLeft:
                    {
                        if (source != 1)
                            return;

                        setFlag = result.IsFlagSet(0x80) ^ Registers.CarryFlag;
                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = false;
                        break;
                    }
                case EnumArithmeticOperation.ShiftRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = destination.IsFlagSet(0x80);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
            }

            Registers.OverflowFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates the given 16-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticLeft:
                case EnumArithmeticOperation.ShiftLeft:
                    {
                        if (source != 1)
                            return;

                        setFlag = result.IsFlagSet(0x8000) ^ Registers.CarryFlag;
                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = false;
                        break;
                    }
                case EnumArithmeticOperation.ShiftRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = destination.IsFlagSet(0x8000);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
            }

            Registers.OverflowFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates the given 32-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public void Flags_EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, uint result = 0,
            uint destination = 0, uint source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticLeft:
                case EnumArithmeticOperation.ShiftLeft:
                    {
                        if (source != 1)
                            return;

                        setFlag = result.IsFlagSet(0x80000000) ^ Registers.CarryFlag;
                        break;
                    }
                case EnumArithmeticOperation.ShiftArithmeticRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = false;
                        break;
                    }
                case EnumArithmeticOperation.ShiftRight:
                    {
                        if (source != 1)
                            return;

                        setFlag = destination.IsFlagSet(0x80000000);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
            }

            Registers.OverflowFlag = setFlag;
        }

        /// <summary>
        ///     Evaluates and sets the value of both the Carry and Zero Flag based upon the specified result
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Flags_EvaluateSignZero(byte result)
        {
            Registers.ParityFlag = result.Parity();
            if (result == 0)
            {
                Registers.SignFlag = false;
                Registers.ZeroFlag = true;
            }
            else
            {
                Registers.ZeroFlag = false;
                Registers.SignFlag = result.IsNegative();
            }
        }

        /// <summary>
        ///     Evaluates and sets the value of both the Carry and Zero Flag based upon the specified result
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Flags_EvaluateSignZero(ushort result)
        {
            Registers.ParityFlag = result.Parity();
            if (result == 0)
            {
                Registers.SignFlag = false;
                Registers.ZeroFlag = true;
            }
            else
            {
                Registers.ZeroFlag = false;
                Registers.SignFlag = result.IsNegative();
            }
        }

        /// <summary>
        ///     Evaluates and sets the value of both the Carry and Zero Flag based upon the specified result
        /// </summary>
        /// <param name="result"></param>
        [MethodImpl(OpcodeCompilerOptimizations)]
        private void Flags_EvaluateSignZero(uint result)
        {
            Registers.ParityFlag = result.Parity();
            if (result == 0)
            {
                Registers.SignFlag = false;
                Registers.ZeroFlag = true;
            }
            else
            {
                Registers.ZeroFlag = false;
                Registers.SignFlag = result.IsNegative();
            }
        }

        /// <summary>
        ///     Returns the size of the given Register
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        [MethodImpl(OpcodeCompilerOptimizations)]
        public byte GetSize(Register register)
        {
            return register switch
            {
                Register.AL => sizeof(byte),
                Register.AH => sizeof(byte),
                Register.BL => sizeof(byte),
                Register.BH => sizeof(byte),
                Register.CL => sizeof(byte),
                Register.CH => sizeof(byte),
                Register.DL => sizeof(byte),
                Register.DH => sizeof(byte),
                Register.AX => sizeof(ushort),
                Register.BX => sizeof(ushort),
                Register.CX => sizeof(ushort),
                Register.DX => sizeof(ushort),
                Register.SP => sizeof(ushort),
                Register.BP => sizeof(ushort),
                Register.SI => sizeof(ushort),
                Register.DI => sizeof(ushort),
                Register.ES => sizeof(ushort),
                Register.CS => sizeof(ushort),
                Register.SS => sizeof(ushort),
                Register.DS => sizeof(ushort),
                Register.EIP => sizeof(uint),
                Register.EAX => sizeof(uint),
                Register.EBX => sizeof(uint),
                Register.ECX => sizeof(uint),
                Register.EDX => sizeof(uint),
                Register.ESP => sizeof(uint),
                Register.EBP => sizeof(uint),
                Register.ESI => sizeof(uint),
                Register.EDI => sizeof(uint),
                _ => throw new ArgumentOutOfRangeException(nameof(register), register, null)
            };
        }
    }
}

using Iced.Intel;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Runtime.CompilerServices;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        protected static readonly ILogger _logger;

        public delegate ushort InvokeExternalFunctionDelegate(ushort importedNameTableOrdinal, ushort functionOrdinal);

        private InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public CpuRegisters Registers;
        private IMemoryCore Memory;

        private Instruction _currentInstruction;

        private ushort STACK_SEGMENT;
        private ushort EXTRA_SEGMENT;
        private const ushort STACK_BASE = 0xFFFF;

        public bool IsRunning;

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
            //Setup Delegate Call   
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;

            //Setup Memory Space
            Memory = memoryCore;
            STACK_SEGMENT = 0;
            EXTRA_SEGMENT = ushort.MaxValue;

            //Setup Registers
            Registers = cpuRegisters;
            Registers.BP = STACK_BASE;
            Registers.SP = STACK_BASE;
            Registers.SS = STACK_SEGMENT;
            Registers.ES = EXTRA_SEGMENT;

            IsRunning = true;

            //These two values are the final values popped off on the routine's last RETF
            //Seeing a ushort.max for CS and IP tells the routine it's now done
            Push(ushort.MaxValue);
            Push(ushort.MaxValue);
        }

        public ushort Pop()
        {
            var value = Memory.GetWord(Registers.SS, (ushort) (Registers.SP + 1));
            Registers.SP += 2;
            return value;
        }

        public void Push(ushort value)
        {
            Memory.SetWord(Registers.SS, (ushort) (Registers.SP - 1), value);
            Registers.SP -= 2;
        }

        public void Tick()
        {

            //Check for segment end
            if ((Registers.CS == ushort.MaxValue || Registers.CS == 0) && Registers.IP == ushort.MaxValue) 
            {
                IsRunning = false;
                return;
            }

            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);
            Registers.IP = _currentInstruction.IP16;

#if DEBUG
            //_logger.InfoRegisters(this);
            //_logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction.ToString()}");

            //if(Registers.IP == 0x1661)
                //Debugger.Break();
#endif

            switch (_currentInstruction.Mnemonic)
            {
                //Instructions that will set the IP -- we just return
                case Mnemonic.Retf:
                    Op_retf();
                    return;
                case Mnemonic.Ret:
                    Op_ret();
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
                case Mnemonic.Jae:
                    Op_Jae();
                    return;
                case Mnemonic.Call:
                    Op_Call();
                    return;

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
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

            Registers.IP += (ushort)_currentInstruction.ByteLength;
#if DEBUG
            //_logger.InfoRegisters(this);
            //_logger.InfoStack(this);
            //_logger.Info("--------------------------------------------------------------");
#endif
        }

        /// <summary>
        ///     Returns the VALUE of Operand 0
        ///     Only use this for instructions where Operand 0 is a VALUE, not an address target
        /// </summary>
        /// <param name="opKind"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        private ushort GetOperandValue(OpKind opKind, EnumOperandType operandType)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    return Registers.GetValue(operandType == EnumOperandType.Destination
                        ? _currentInstruction.Op0Register
                        : _currentInstruction.Op1Register);

                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;

                case OpKind.Immediate16:
                {

                    //If it's FO SHO not a relocation record, just return the value
                    if (_currentInstruction.Immediate16 != ushort.MaxValue) return _currentInstruction.Immediate16;

                    //Check for Relocation Records
                    if (!Memory.GetSegment(Registers.CS).RelocationRecords
                        .TryGetValue((ushort) (Registers.IP + 1), out var relocationRecord))
                        return ushort.MaxValue;

                    return relocationRecord.TargetTypeValueTuple.Item1 switch
                    {
                        //External Property
                        EnumRecordsFlag.IMPORTORDINAL => _invokeExternalFunctionDelegate(
                            relocationRecord.TargetTypeValueTuple.Item2, relocationRecord.TargetTypeValueTuple.Item3),

                        //Internal Segment
                        EnumRecordsFlag.INTERNALREF => relocationRecord.TargetTypeValueTuple.Item2,
                        _ => throw new Exception("Unsupported Records Flag for Immediate16 Relocation Value")
                    };
                }

                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;

                case OpKind.Memory:
                {
                    var offset = GetOperandOffset(opKind);

                    return _currentInstruction.MemorySize == MemorySize.UInt16
                        ? Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offset)
                        : Memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), offset);
                }
                
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Op for: {opKind}");
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
            switch (opKind)
            {
                case OpKind.Memory:
                {
                    switch (_currentInstruction.MemoryBase)
                    {
                        case Register.DS when _currentInstruction.MemoryIndex == Register.None:
                        case Register.None when _currentInstruction.MemoryIndex == Register.None:
                            return (ushort) _currentInstruction.MemoryDisplacement;
                        case Register.BP when _currentInstruction.MemoryIndex == Register.None:
                        {
                            //This is a hack, I'm just assuming there wont be > 32k passed in values or local variables
                            //this might break if it's a crazy module. ¯\_(ツ)_/¯
                            if (_currentInstruction.MemoryDisplacement > short.MaxValue)
                            {
                                return (ushort) (Registers.BP -
                                                 (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                            }

                            return (ushort) (Registers.BP + _currentInstruction.MemoryDisplacement + 1);
                        }

                        case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                        {
                            if (_currentInstruction.MemoryDisplacement > short.MaxValue)
                            {
                                return (ushort) ((Registers.BP + Registers.SI) -
                                                 (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                            }

                            return (ushort) ((Registers.BP + Registers.SI) +
                                             _currentInstruction.MemoryDisplacement + 1);
                        }

                        case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                            return (ushort) (Registers.BX + _currentInstruction.MemoryDisplacement);
                        case Register.BX when _currentInstruction.MemoryIndex == Register.SI:
                            return (ushort) (Registers.BX + _currentInstruction.MemoryDisplacement + Registers.SI);
                        case Register.SI when _currentInstruction.MemoryIndex == Register.None:
                            return (ushort) (Registers.SI + _currentInstruction.MemoryDisplacement);
                        case Register.DI when _currentInstruction.MemoryIndex == Register.None:
                            return (ushort) (Registers.DI + _currentInstruction.MemoryDisplacement);
                        default:
                            throw new Exception("Unknown GetOperandOffset MemoryBase");
                    }
                }
                case OpKind.NearBranch16:
                    return _currentInstruction.NearBranch16;
                default:
                    throw new Exception($"Unknown OpKind for GetOperandOffset: {opKind}");
            }
        }

        /// <summary>
        ///     Returns if the Current Instruction is an 8bit or 16bit operation
        /// </summary>
        /// <returns></returns>
        private int GetCurrentOperationSize()
        {
            var operationSize = _currentInstruction.Op0Kind switch
            {
                OpKind.Register => _currentInstruction.Op0Register.GetSize(),
                OpKind.Memory => (_currentInstruction.MemorySize == MemorySize.UInt8 ? 1 : 2),
                _ => -1
            };
            return operationSize;
        }

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
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: {Registers.AH:X2}");
            }
        }

        private void Op_Cwd()
        {
            Registers.DX = Registers.AX.IsBitSet(15) ? (ushort) 0xFFFF : (ushort) 0x0000;
        }
        

        private void Op_Neg()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = (ushort)(0 - destination);

            var operationSize = 0;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                }
                case OpKind.Memory:
                {

                    if (_currentInstruction.MemorySize == MemorySize.UInt8)
                    {
                        operationSize = 1;
                        Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), (byte) result);
                    }
                    else
                    {
                        operationSize = 2;
                        Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), result);
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown NEG: {_currentInstruction.Op0Kind}");
            }

            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                    Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, 0);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    break;
                }
                default:
                    throw new Exception($"Unknown ADD operation size: {operationSize}");
            }
        }

        private void Op_Sbb()
        {
            var source = (ushort)(GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source) + (Registers.F.Flags.IsFlagSet((ushort)EnumFlags.CF) ? 1 : 0));
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = (ushort)(destination - source);
            int operationSize;

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown ADD: {_currentInstruction.Op0Kind}");
            }

            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                    Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, source);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                    Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    break;
                }
                default:
                    throw new Exception($"Unknown ADD operation size: {operationSize}");
            }
        }

        private void Op_Clc() => Registers.F.ClearFlag(EnumFlags.CF);

        private void Op_Or()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var result = (ushort) (destination | source);

            var operationSize = 0;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    break;
                default:
                    throw new Exception($"Unsupported OpKind for OR: {_currentInstruction.Op0Kind}");
            }

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);

            //Set Conditional Flags
            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    break;
                }
                default:
                    throw new Exception($"Unsupported OR operation size: {operationSize}");
            }
        }

        private void Op_Shl()
        {
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Shl_8(destination, source)
                : Op_Shl_16(destination, source);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    return;
                }
                case OpKind.Memory when operationSize == 1:
                {
                    Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination), (byte)result);
                    return;
                }
                case OpKind.Memory when operationSize == 2:
                {
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination), result);
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown Destination for SUB: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Shl_8(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (byte)(destination << (sbyte)source);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Shl_16(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (ushort)(destination << source);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            Registers.SetValue(_currentInstruction.Op0Register, offset);
            Registers.ES = segment;
        }

        private void Op_Lds()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            Registers.SetValue(_currentInstruction.Op0Register, offset);
            Registers.DS = segment;
        }


        private void Op_Lea()
        {
            switch (_currentInstruction.MemoryBase)
            {
                case Register.BP:
                {
                    var baseOffset = (ushort)(Registers.BP -
                                              (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                    Registers.SetValue(_currentInstruction.Op0Register,  baseOffset);
                    return;
                }
                default:
                    throw new Exception($"Unsupported MemoryBase for LEA: {_currentInstruction.MemoryBase}");
            }
        }

        private void Op_Enter()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Immediate16:
                    Push(Registers.BP);
                    Registers.BP = Registers.SP;
                    Registers.SP -= (ushort) (_currentInstruction.Immediate16 + 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown ENTER: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Leave()
        {
            Registers.SP = Registers.BP;
            Registers.BP = Pop();
        }

        public void Op_retf()
        {
            Registers.IP = Pop();
            Registers.CS = Pop();

        }

        public void Op_ret()
        {
            Registers.IP = Pop();
        }

        private void Op_Stosw()
        {
            while (Registers.CX > 0)
            {
                Memory.SetWord(Registers.ES, Registers.DI, Registers.AX);
                Registers.DI += 2;
                Registers.CX--;
            }
        }

        private void Op_Inc()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = _currentInstruction.MemorySize == MemorySize.UInt8 ? Op_Inc_8((byte)destination) : Op_Inc_16(destination);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt8:
                    Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), (byte) result);
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt16:
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), result);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Inc_8(byte destination)
        {
            unchecked
            {
                var result = (byte) (destination + 1);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Addition, result, destination, 1);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Inc_16(ushort destination)
        {
            unchecked
            {
                var result = (ushort)(destination + 1);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Addition, result, destination, 1);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        private void Op_Dec()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = _currentInstruction.MemorySize == MemorySize.UInt8 ? Op_Dec_8((byte)destination) : Op_Dec_16(destination);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt8:
                    Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), (byte)result);
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt16:
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), result);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Dec_8(byte destination)
        {
            unchecked
            {
                var result = (byte)(destination - 1);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Dec_16(ushort destination)
        {
            unchecked
            {
                var result = (ushort)(destination - 1);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, 1);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }


        private void Op_And()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var result = (ushort)(destination & source);

            var operationSize = 0;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    break;
                default:
                    throw new Exception($"Unsupported OpKind for OR: {_currentInstruction.Op0Kind}");
            }

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);

            //Set Conditional Flags
            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    break;
                }
                default:
                    throw new Exception($"Unsupported OR operation size: {operationSize}");
            }
        }

        private void Op_Xor()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var result = (ushort)(destination ^ source);

            var operationSize = 0;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    break;
                default:
                    throw new Exception($"Unsupported OpKind for OR: {_currentInstruction.Op0Kind}");
            }

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);

            //Set Conditional Flags
            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    break;
                }
                default:
                    throw new Exception($"Unsupported OR operation size: {operationSize}");
            }
        }

        private void Op_Jae()
        {
            if (!Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jbe()
        {
            if (Registers.F.IsFlagSet(EnumFlags.ZF) || Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jmp()
        {
            if (_currentInstruction.FlowControl == FlowControl.IndirectBranch)
            {
                //Get the destination offset
                var offsetToDestinationValue = GetOperandOffset(_currentInstruction.Op0Kind);
                var destinationOffset = Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offsetToDestinationValue);

                Registers.IP = destinationOffset;
                return;
            }

            //Near Jumps
            Registers.IP = GetOperandOffset(_currentInstruction.Op0Kind);

            if (_currentInstruction.IsJmpFar || _currentInstruction.IsJmpFarIndirect)
                Registers.CS = Registers.GetValue(_currentInstruction.MemorySegment);

        }

        public void Op_Jle()
        {
            //ZF == 1 OR SF <> OF
            if ((Registers.F.IsFlagSet(EnumFlags.SF) != Registers.F.IsFlagSet(EnumFlags.OF))
                || Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jge()
        {
            // SF == OF
            if (Registers.F.IsFlagSet(EnumFlags.SF) == Registers.F.IsFlagSet(EnumFlags.OF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jne()
        {
            //ZF == 0
            if (!Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

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
                Registers.IP += (ushort) _currentInstruction.ByteLength;
            }
        }

        private void Op_Je()
        {
            // ZF == 1
            if (Registers.F.IsFlagSet(EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jl()
        {
            //SF <> OF
            if (Registers.F.IsFlagSet(EnumFlags.SF) != Registers.F.IsFlagSet(EnumFlags.OF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Jb()
        {
            //CF == 1
            if (Registers.F.IsFlagSet(EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        /// <summary>
        ///     Identical to SUB, just doesn't save result of subtraction
        /// </summary>
        private void Op_Cmp()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            switch (GetCurrentOperationSize())
            {
                case 1:
                    Op_Sub_8((byte) destination, (byte) source);
                    return;
                case 2:
                    Op_Sub_16(destination, source);
                    return;
            }
        }

        private void Op_Sub()
        {
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Sub_8((byte) destination, (byte) source)
                : Op_Sub_16(destination, source);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    return;
                }
                case OpKind.Memory when operationSize == 1:
                {
                    Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination), (byte)result);
                    return;
                }
                case OpKind.Memory when operationSize == 2:
                {
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination), result);
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown Destination for SUB: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Sub_8(byte destination, byte source)
        {
            unchecked
            {
                var result = (byte) (destination - (sbyte) source);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sub_16(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (ushort) (destination - (short) source);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        /// <summary>
        ///     ADD and ADC are exactly the same, except that ADC also adds 1 if the carry flag is set
        ///     So we don't repeat the function, we just pass the ADC flag
        /// </summary>
        /// <param name="adc"></param>
        private void Op_Add(bool addCarry = false)
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var result = GetCurrentOperationSize() == 1
                ? Op_Add_8((byte) destination, (byte) source, addCarry)
                : Op_Add_16(destination, source, addCarry);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    return;
                }
                case OpKind.Memory when GetCurrentOperationSize() == 1:
                {
                    Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), (byte) result);
                    break;
                }
                case OpKind.Memory when GetCurrentOperationSize() == 2:
                {
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), result);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown Destination for ADD: {_currentInstruction.Op0Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Add_8(byte source, byte destination, bool addCarry)
        {
            unchecked
            {
                var result = (byte) (source + destination);
                if (addCarry && Registers.F.IsFlagSet(EnumFlags.CF))
                    result++;

                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Add_16(ushort source, ushort destination, bool addCarry)
        {
            unchecked
            {
                var result = (ushort) (source + destination);
                if (addCarry && Registers.F.IsFlagSet(EnumFlags.CF))
                    result++;
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        private void Op_Imul()
        {
            var destination = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op2Kind, EnumOperandType.Source);
            ushort result;
            unchecked
            {
                result = (ushort) (destination * source);
            }

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    return;
            }
        }

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
        private void Op_Push()
        {
            Push(GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination));
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        private void Op_Mov()
        {
            switch (_currentInstruction.Op0Kind)
            {
                //MOV r8*,imm8
                //MOV r16,imm16
                //MOV r16, r16
                //MOV AX,moffs16*
                case OpKind.Register:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source));
                    return;
                }
                case OpKind.Memory:
                {
                    switch (_currentInstruction.MemorySize)
                    {
                        //MOV r/m16,imm16
                        //MOV moffs16*,AX
                        case MemorySize.UInt16:
                            Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                                GetOperandOffset(_currentInstruction.Op0Kind),
                                GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source));
                            return;
                        //MOV moffs8*,AL
                        //MOV moffs16*,imm8
                        case MemorySize.UInt8:
                            Memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
                                GetOperandOffset(_currentInstruction.Op0Kind),
                                (byte) GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source));
                            return;
                        default:
                            throw new ArgumentOutOfRangeException(
                                $"Unsupported Memory type for MOV operation: {_currentInstruction.MemorySize}");
                    }
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown MOV: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Call()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.FarBranch16 when _currentInstruction.Immediate16 == ushort.MaxValue:
                {
                    //We push CS:IP to the stack
                    Push(Registers.IP);
                    Push(Registers.CS);

                    //Check for a possible relocation
                    int destinationValue;
                    if (_currentInstruction.Immediate16 == ushort.MaxValue)
                    {
                        if (!Memory.GetSegment(Registers.CS).RelocationRecords
                            .TryGetValue((ushort) (Registers.IP + 1), out var relocationRecord))
                        {
                            destinationValue = ushort.MaxValue;
                        }
                        else

                        {
                            //Relocation Record pointing to an internal Int16:Int16 address
                            if (relocationRecord.Flag == EnumRecordsFlag.INTERNALREF)
                            {
                                Registers.CS = relocationRecord.TargetTypeValueTuple.Item2;
                                Registers.IP = relocationRecord.TargetTypeValueTuple.Item3;
                                return;
                            }

                            //Simulate an ENTER
                            //Set BP to the current stack pointer
                            var priorToCallBp = Registers.BP;
                            Registers.BP = Registers.SP;

                            _invokeExternalFunctionDelegate(relocationRecord.TargetTypeValueTuple.Item2,
                                relocationRecord.TargetTypeValueTuple.Item3);

                            //Simulate a LEAVE & retf
                            Registers.BP = priorToCallBp;
                            Registers.SetValue(Register.CS, Pop());
                            Registers.SetValue(Register.EIP, Pop());
                            Registers.IP += (ushort) _currentInstruction.ByteLength;
                            return;
                        }
                    }
                    else
                    {
                        destinationValue = _currentInstruction.Immediate16;
                    }

                    //TODO -- Perform actual call
                    break;
                }

                case OpKind.NearBranch16:
                {
                    //We push CS:IP to the stack
                    //Push the IP of the **NEXT** instruction to the stack
                    Push((ushort) (Registers.IP + _currentInstruction.ByteLength));
                    Registers.IP = _currentInstruction.FarBranch16;
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }
    }
}
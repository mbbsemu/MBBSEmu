using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Linq;
using NLog.LayoutRenderers;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public delegate ushort InvokeExternalFunctionDelegate(ushort importedNameTableOrdinal, ushort functionOrdinal);

        public delegate ushort GetExternalMemoryValueDelegate(ushort segment, ushort offset);

        private readonly InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;
        private readonly GetExternalMemoryValueDelegate _getExternalMemoryValueDelegate;

        public readonly CpuRegisters Registers;
        public readonly IMemoryCore Memory;

        private Instruction _currentInstruction;


        private const ushort STACK_SEGMENT = 0xFF;
        private const ushort EXTRA_SEGMENT = 0xFE;
        private const ushort STACK_BASE = 0xFFFF;

        public CpuCore(InvokeExternalFunctionDelegate invokeExternalFunctionDelegate, GetExternalMemoryValueDelegate getExternalMemoryValueDelegate)
        {
            //Setup Delegate Call   
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;
            _getExternalMemoryValueDelegate = getExternalMemoryValueDelegate;

            //Setup Registers
            Registers = new CpuRegisters {BP = STACK_BASE, SP = STACK_BASE, SS = STACK_SEGMENT, ES = EXTRA_SEGMENT};

            //Setup Memory Space
            Memory = new MemoryCore();
            Memory.AddSegment(STACK_SEGMENT);
            Memory.AddSegment(EXTRA_SEGMENT);
        }

        public ushort Pop()
        {
            var value = Memory.GetWord(Registers.SS, Registers.SP);
            Registers.SP += 2;
            return value;
        }

        public void Push(ushort value)
        {
            Registers.SP -= 2;
            Memory.SetWord(Registers.SS, Registers.SP, value);
        }

        public void Tick()
        {
            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);

#if DEBUG
            //logger.InfoRegisters(this);
            _logger.Debug($"{_currentInstruction.ToString()}");
#endif

            switch (_currentInstruction.Mnemonic)
            {
                case Mnemonic.Add:
                    Op_Add();
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
                case Mnemonic.Call:
                    Op_Call();
                    return;
                case Mnemonic.Cmp:
                    Op_Cmp();
                    break;
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
                case Mnemonic.Jl:
                    Op_Jl();
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
                case Mnemonic.Xor:
                    Op_Xor();
                    break;
                case Mnemonic.Inc:
                    Op_Inc();
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
                case Mnemonic.Retf:
                case Mnemonic.Ret:
                    Op_retf();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

            Registers.IP += (ushort)_currentInstruction.ByteLength;
#if DEBUG
            _logger.InfoRegisters(this);
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
                    var relocationRecord = Memory.GetSegment(Registers.CS).RelocationRecords
                        .FirstOrDefault(x => x.Offset == Registers.IP + 1);

                    //Actual ushort.MaxValue? Weird ¯\_(ツ)_/¯
                    if (relocationRecord == null)
                        return ushort.MaxValue;

                    switch (relocationRecord?.TargetTypeValueTuple.Item1)
                    {
                        //External Property
                        case EnumRecordsFlag.IMPORTORDINAL:
                            return _invokeExternalFunctionDelegate(
                                relocationRecord.TargetTypeValueTuple.Item2,
                                relocationRecord.TargetTypeValueTuple.Item3);
                        //Internal Segment
                        case EnumRecordsFlag.INTERNALREF:
                            return relocationRecord.TargetTypeValueTuple.Item2;
                        default:
                            throw new Exception("Unsupported Records Flag for Immediate16 Relocation Value");
                    }
                }

                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;

                case OpKind.Memory:
                {
                    var offset = GetOperandOffset(opKind);

                    //Anything in the ES segment is MOST LIKELY an external segment, so check that
                    if (_currentInstruction.MemorySegment == Register.ES && Registers.GetValue(Register.ES) > 0xFF)
                        return _getExternalMemoryValueDelegate(Registers.ES, offset);

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
                            return (ushort) (Registers.BP -
                                             (ushort.MaxValue - _currentInstruction.MemoryDisplacement) + 1);
                        case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                            return (ushort) (Registers.BP -
                                             (ushort.MaxValue - _currentInstruction.MemoryDisplacement) + 1 +
                                             Registers.SI);
                        case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                            return Registers.BX;
                        case Register.SI when _currentInstruction.MemoryIndex == Register.None:
                            return Registers.SI;
                            case Register.DI when _currentInstruction.MemoryIndex == Register.None:
                                return (ushort) (Registers.DI + _currentInstruction.MemoryDisplacement);
                        default:
                            throw new Exception("Unknown GetOperandOffset MemoryBase");
                    }
                }
                default:
                    throw new Exception($"Unknown OpKind for GetOperandOffset: {opKind}");
            }
        }

        private void Op_Or()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort) (destination | source));
                    return;
                default:
                    throw new Exception($"Unsupported OpKind for OR: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Shl()
        {
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort)(Registers.GetValue(_currentInstruction.Op0Register) << source));
                    return;
                default:
                    throw new Exception($"Unsupported OpKind for SHL: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Lea()
        {
            switch (_currentInstruction.MemoryBase)
            {
                case Register.BP:
                {
                    var baseOffset = (ushort)(Registers.BP -
                                              (ushort.MaxValue - _currentInstruction.MemoryDisplacement) + 1);
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
                    Registers.SP -= _currentInstruction.Immediate16;
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
            Registers.CS = Pop();
            Registers.IP = Pop();
        }

        private void Op_Stosw()
        {
            while (Registers.CX > 0)
            {
                Memory.SetWord(Registers.ES, Registers.DI, Registers.AX);
                Registers.DI += 2;
                Registers.CX -= 2;
            }
        }

        private void Op_Inc()
        {
            ushort newValue;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    newValue = (ushort) (Registers.GetValue(_currentInstruction.Op0Register) + 1);
                    Registers.SetValue(_currentInstruction.Op0Register, newValue);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown INC: {_currentInstruction.Op0Kind}");
            }

            Registers.F = newValue == 0 ? Registers.F.SetFlag((ushort) EnumFlags.ZF) : Registers.F.ClearFlag((ushort)EnumFlags.ZF);
        }

        private void Op_Xor()
        {
            var value1 = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var value2 = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort) (value1 ^ value2));
                    return;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown XOR: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Jbe()
        {
            if (Registers.F.IsFlagSet((ushort) EnumFlags.ZF) || Registers.F.IsFlagSet((ushort) EnumFlags.ZF))
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
            Registers.IP = _currentInstruction.Immediate16;
        }

        public void Op_Jle()
        {
            if ((!Registers.F.IsFlagSet((ushort)EnumFlags.ZF) && Registers.F.IsFlagSet((ushort)EnumFlags.CF))
                || (Registers.F.IsFlagSet((ushort)EnumFlags.ZF) && !Registers.F.IsFlagSet((ushort)EnumFlags.CF)))
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
            if ((!Registers.F.IsFlagSet((ushort)EnumFlags.ZF) && !Registers.F.IsFlagSet((ushort)EnumFlags.CF))
                 || (Registers.F.IsFlagSet((ushort)EnumFlags.ZF) && !Registers.F.IsFlagSet((ushort)EnumFlags.CF)))
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
            if (!Registers.F.IsFlagSet((ushort) EnumFlags.ZF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Je()
        {
            if (Registers.F.IsFlagSet((ushort) EnumFlags.ZF))
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
            if (!Registers.F.IsFlagSet((ushort) EnumFlags.ZF) && Registers.F.IsFlagSet((ushort) EnumFlags.CF))
            {
                Registers.IP = _currentInstruction.Immediate16;
            }
            else
            {
                Registers.IP += (ushort)_currentInstruction.ByteLength;
            }
        }

        private void Op_Cmp()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //Set Appropriate Flags
            if (destination == source)
            {
                Registers.F = Registers.F.SetFlag((ushort) EnumFlags.ZF);
                Registers.F = Registers.F.ClearFlag((ushort)EnumFlags.CF);
            }
            else if (destination < source)
            {
                Registers.F = Registers.F.ClearFlag((ushort)EnumFlags.ZF);
                Registers.F = Registers.F.SetFlag((ushort)EnumFlags.CF);
            }
            else if (destination > source)
            {
                Registers.F = Registers.F.ClearFlag((ushort)EnumFlags.ZF);
                Registers.F = Registers.F.ClearFlag((ushort)EnumFlags.CF);
            }
        }

        private void Op_Add()
        {
            ushort oldValue;
            ushort newValue;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Register:
                {
                    unchecked
                    {
                        oldValue = Registers.GetValue(_currentInstruction.Op0Register);
                        newValue = (ushort) (oldValue +
                                             Registers.GetValue(_currentInstruction.Op1Register));
                    }

                    Registers.SetValue(_currentInstruction.Op0Register, newValue);
                    break;
                }

                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8:
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8to16:
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate16:
                {
                    unchecked
                    {
                        oldValue = Registers.GetValue(_currentInstruction.Op0Register);
                        newValue = (ushort) (oldValue +
                                             _currentInstruction.Immediate16);
                    }

                    Registers.SetValue(_currentInstruction.Op0Register, newValue);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown ADD: {_currentInstruction.Op0Kind}");
            }

            Registers.F = newValue == 0 ? Registers.F.SetFlag((ushort)EnumFlags.ZF) : Registers.F.ClearFlag((ushort)EnumFlags.ZF);
            Registers.F = oldValue >> 15 != newValue >> 15 ? Registers.F.SetFlag((ushort) EnumFlags.CF) : Registers.F.ClearFlag((ushort)EnumFlags.CF);
        }

        private void Op_Imul()
        {
            var destination = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op2Kind, EnumOperandType.Source);
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort) (destination * source));
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
                        var relocationRecord = Memory.GetSegment(Registers.CS).RelocationRecords
                            .FirstOrDefault(x => x.Offset == Registers.IP + 1);

                        if (relocationRecord == null)
                        {
                            destinationValue = ushort.MaxValue;
                        }
                        else
                        {
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
                    //Push the Current IP to the stack
                    Push(Registers.IP);
                    Registers.IP = _currentInstruction.FarBranch16;
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }
    }
}
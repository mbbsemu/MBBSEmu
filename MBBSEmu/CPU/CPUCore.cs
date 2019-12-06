using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Linq;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        public delegate int InvokeExternalFunctionDelegate(int importedNameTableOrdinal, int functionOrdinal);

        private readonly InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public readonly CpuRegisters Registers;
        public readonly IMemoryCore Memory;

        private Instruction _currentInstruction;


        private const ushort STACK_SEGMENT = 0xFF;
        private const ushort EXTRA_SEGMENT = 0xFE;
        private const ushort STACK_BASE = 0xFFFF;

        public CpuCore(InvokeExternalFunctionDelegate invokeExternalFunctionDelegate)
        {
            //Setup Delegate Call   
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;

            //Setup Registers
            Registers = new CpuRegisters {SP = STACK_BASE, SS = STACK_SEGMENT, ES = EXTRA_SEGMENT};

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
    _logger.Debug($"{_currentInstruction.ToString()}");
    _logger.InfoRegisters(this);
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
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

            Registers.IP += (ushort)_currentInstruction.ByteLength;
        }

        private ushort GetOpValue(OpKind opKind)
        {
            switch (opKind)
            {
                case OpKind.Register:
                    return Registers.GetValue(_currentInstruction.Op0Register);
                case OpKind.Immediate8:
                    return _currentInstruction.Immediate8;
                case OpKind.Memory when _currentInstruction.MemoryBase == Register.None:
                    return Memory.GetByte(Registers.DS, (ushort) _currentInstruction.MemoryDisplacement);
                case OpKind.Memory when _currentInstruction.MemoryBase == Register.BP:
                {
                    var baseOffset = ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1;
                    return Memory.GetByte(Registers.DS, (ushort) (Registers.BP - (ushort) baseOffset));
                }
                case OpKind.Immediate8to16:
                    return (ushort) _currentInstruction.Immediate8to16;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Op for: {opKind}");
            }
        }

        private void Op_Or()
        {
            var value1 = GetOpValue(_currentInstruction.Op0Kind);
            var value2 = GetOpValue(_currentInstruction.Op1Kind);

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                        Registers.SetValue(_currentInstruction.Op0Register, (ushort) (value1 | value2));
                        break;
                }
            }
        }

        private void Op_Shl()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort)(Registers.GetValue(_currentInstruction.Op0Register) << 1));
                    return;
            }
        }

        private void Op_Lea()
        {
            switch (_currentInstruction.MemoryBase)
            {
                case Register.BP:
                {
                    var baseOffset = ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1;
                    Registers.SetValue(_currentInstruction.Op0Register,  (ushort) (Registers.BP - (ushort) baseOffset));
                    break;
                }
            }
        }

        private void Op_Enter()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Immediate16:
                    Registers.BP += _currentInstruction.Immediate16;
                    Registers.SP = Registers.BP;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown ENTER: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Leave()
        {
            Registers.SP = Registers.BP;
            Registers.SetValue(Register.CS, Pop());
            Registers.SetValue(Register.EIP, Pop());
        }

        private void Op_Stosw()
        {
            while (Registers.CX != 0)
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
            var value1 = GetOpValue(_currentInstruction.Op0Kind);
            var value2 = GetOpValue(_currentInstruction.Op1Kind);

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
            var destination = GetOpValue(_currentInstruction.Op0Kind);
            var source = GetOpValue(_currentInstruction.Op1Kind);

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
            ushort oldValue = 0;
            ushort newValue = 0;
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
            var value1 = GetOpValue(_currentInstruction.Op1Kind);
            var value2 = GetOpValue(_currentInstruction.Op2Kind);
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort) (value1*value2));
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
            ushort pushValue;
            switch (_currentInstruction.Op0Kind)
            {
                //PUSH r16
                case OpKind.Register:
                    pushValue = Registers.GetValue(_currentInstruction.Op0Register);
                    break;
                //PUSH imm8 - PUSH imm16
                case OpKind.Immediate8:
                case OpKind.Immediate8to16:
                case OpKind.Immediate16:
                    pushValue = _currentInstruction.Immediate16;
                    break;

                //PUSH r/m16
                case OpKind.Memory when _currentInstruction.MemorySegment == Register.DS:
                    pushValue = BitConverter.ToUInt16(Memory.GetArray(Registers.GetValue(Register.DS),
                        (ushort) _currentInstruction.MemoryDisplacement, 2));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown PUSH: {_currentInstruction.Op0Kind}");
            }
            Push(pushValue);
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        private void Op_Mov()
        {
            switch (_currentInstruction.Op0Kind)
            {
                //MOV r8*,imm8
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, _currentInstruction.Immediate8);
                    return;
                }

                //MOV r16,imm16
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate16:
                {
                    //Check for a possible relocation
                    ushort destinationValue = 0;

                    if (_currentInstruction.Immediate16 == ushort.MaxValue)
                    {
                        var relocationRecord = Memory.GetSegment(Registers.CS).RelocationRecords
                            .FirstOrDefault(x => x.Offset == Registers.IP + 1);
                        switch (relocationRecord?.TargetTypeValueTuple.Item1)
                        {
                            //External Property
                            case EnumRecordsFlag.IMPORTORDINAL:
                            {
                                break;
                            }
                            //Internal Segment
                            case EnumRecordsFlag.INTERNALREF:
                                destinationValue = relocationRecord?.TargetTypeValueTuple.Item2 ?? 0;
                                break;
                            default:
                                destinationValue = ushort.MaxValue;
                                break;

                        }

                    }
                    else
                    {
                        destinationValue = _currentInstruction.Immediate16;
                    }

                    Registers.SetValue(_currentInstruction.Op0Register, destinationValue);
                    return;
                }

                //MOV r16, r16
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Register:
                {
                    Registers.SetValue(_currentInstruction.Op0Register,
                        Registers.GetValue(_currentInstruction.Op1Register));
                    return;
                }

                //MOV AX,moffs16*
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Memory:
                {
                    Registers.SetValue(_currentInstruction.Op0Register, Memory.GetWord(Registers.DS, (ushort) _currentInstruction.MemoryDisplacement));
                    return;
                }

                //MOV r/m16,imm16
                /*
                 * The instruction in the question only uses a single constant offset so no effective address with registers.
                 * As such, it's DS unless overridden by a prefix.
                 */
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Immediate16:
                {
                    Memory.SetWord(Registers.DS, (ushort) _currentInstruction.MemoryDisplacement, _currentInstruction.Immediate16);
                    break;
                } 
                
                
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Register:
                {
                    switch (_currentInstruction.MemorySize)
                    {
                        //MOV moffs16*,AX
                        case MemorySize.UInt16:
                            Memory.SetWord(Registers.DS, (ushort) _currentInstruction.MemoryDisplacement,
                                (ushort) Registers.GetValue(_currentInstruction.Op1Register));
                            break;
                        //MOV moffs8*,AL
                        case MemorySize.UInt8:
                            Memory.SetByte(Registers.DS, (ushort) _currentInstruction.MemoryDisplacement,
                                (byte) Registers.GetValue(_currentInstruction.Op1Register));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unsupported Memory type for MOV operation: {_currentInstruction.MemorySize}");
                    }

                    break;
                }

                //MOV moffs16*,imm8
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Immediate8:
                {
                    Memory.SetByte(Registers.DS, (ushort)_currentInstruction.MemoryDisplacement, _currentInstruction.Immediate8);
                    return;
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

                    //We Handle this like a standard CALL function
                    //where, we set the BP to the current SP then 

                    //Set BP to the current stack pointer


                    //We push CS:IP to the stack
                    //Push the Current IP to the stack
                    Push(Registers.IP);
                    Push(Registers.CS);

                    Registers.BP = Registers.SP;

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
                            _invokeExternalFunctionDelegate(relocationRecord.TargetTypeValueTuple.Item2,
                                relocationRecord.TargetTypeValueTuple.Item3);

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
                    Push(Registers.CS);
                    Registers.BP = Registers.SP;

                    Registers.IP = _currentInstruction.FarBranch16;
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }
    }
}
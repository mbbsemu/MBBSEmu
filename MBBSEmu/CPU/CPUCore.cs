using Iced.Intel;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        public delegate int InvokeExternalFunctionDelegate(int importedNameTableOrdinal, int functionOrdinal);

        private readonly InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public readonly CpuRegisters Registers;
        public readonly CpuMemory Memory;

        private Instruction _currentInstruction;

        private Queue<ushort> _basePointerBeforeOffset;

        public CpuCore(InvokeExternalFunctionDelegate invokeExternalFunctionDelegate)
        {
            Registers = new CpuRegisters();
            Memory = new CpuMemory();
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;
            Registers.SP = CpuMemory.STACK_BASE;
            Registers.SS = 0xFF;
            _basePointerBeforeOffset = new Queue<ushort>();
        }

        

        public void Tick()
        {
            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);

#if DEBUG
    //_logger.Debug($"{_currentInstruction.ToString()}");
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
                case OpKind.Memory when (_currentInstruction.Op1Kind == OpKind.Immediate8 || _currentInstruction.Op1Kind == OpKind.Immediate8to16):
                    return Memory.GetByte(Registers.DS, (int)_currentInstruction.MemoryDisplacement);
                case OpKind.Immediate8to16:
                    return (ushort)_currentInstruction.Immediate8to16;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Op for: {opKind}");
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
            Registers.SetValue(Register.CS, Memory.Pop(Registers.SP));
            Registers.SP += 2;
            Registers.SetValue(Register.EIP, Memory.Pop(Registers.SP));
            Registers.SP += 2;
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
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register,
                        (ushort) (Registers.GetValue(_currentInstruction.Op0Register) + 1));
                    return;
                default:
                    throw new ArgumentOutOfRangeException($"Uknown INC: {_currentInstruction.Op0Kind}");
            }
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
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register,
                        Registers.GetValue(_currentInstruction.Op1Register));
                    break;

                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8:
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate8to16:
                case OpKind.Register when _currentInstruction.Op1Kind == OpKind.Immediate16:
                    Registers.SetValue(_currentInstruction.Op0Register, (ushort)(Registers.GetValue(_currentInstruction.Op0Register) + _currentInstruction.Immediate16));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown ADD: {_currentInstruction.Op0Kind}");
            }
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
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, Memory.Pop(Registers.SP));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown POP: {_currentInstruction.Op0Kind}");
            }

            Registers.SP += 2;
        }

        /// <summary>
        ///     Push Op Code
        /// </summary>
        private void Op_Push()
        {
            Registers.SP -= 2;
            switch (_currentInstruction.Op0Kind)
            {
                //PUSH r16
                case OpKind.Register:
                    Memory.Push(Registers.SP,
                        BitConverter.GetBytes((ushort) Registers.GetValue(_currentInstruction.Op0Register)));
                    break;
                //PUSH imm8 - PUSH imm16
                case OpKind.Immediate8:
                case OpKind.Immediate8to16:
                case OpKind.Immediate16:
                    Memory.Push(Registers.SP, BitConverter.GetBytes(_currentInstruction.Immediate16));
                    break;

                //PUSH r/m16
                case OpKind.Memory when _currentInstruction.MemorySegment == Register.DS:
                    Memory.Push(Registers.SP,
                        Memory.GetArray(Registers.GetValue(Register.DS),
                            (ushort) _currentInstruction.MemoryDisplacement, 2));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown PUSH: {_currentInstruction.Op0Kind}");
            }
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
                    ushort destinationValue;

                    if (_currentInstruction.Immediate16 == ushort.MaxValue)
                    {
                        var relocationRecord = Memory._segments[Registers.CS].RelocationRecords
                            .FirstOrDefault(x => x.Offset == Registers.IP + 1);

                        //If we found a relocation record, set the new value, otherwise, it must have been a literal max
                        destinationValue = relocationRecord?.TargetTypeValueTuple.Item2 ?? ushort.MaxValue;
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
                    Registers.SetValue(_currentInstruction.Op0Register, Memory.GetWord(Registers.DS, (int) _currentInstruction.MemoryDisplacement));
                    return;
                }

                //MOV r/m16,imm16
                /*
                 * The instruction in the question only uses a single constant offset so no effective address with registers.
                 * As such, it's DS unless overridden by a prefix.
                 */
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Immediate16:
                {
                    Memory.SetWord(Registers.DS, (int) _currentInstruction.MemoryDisplacement, _currentInstruction.Immediate16);
                    break;
                } 
                
                
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Register:
                {
                    switch (_currentInstruction.MemorySize)
                    {
                        //MOV moffs16*,AX
                        case MemorySize.UInt16:
                            Memory.SetWord(Registers.DS, (int) _currentInstruction.MemoryDisplacement,
                                (ushort) Registers.GetValue(_currentInstruction.Op1Register));
                            break;
                        //MOV moffs8*,AL
                        case MemorySize.UInt8:
                            Memory.SetByte(Registers.DS, (int) _currentInstruction.MemoryDisplacement,
                                (byte) Registers.GetValue(_currentInstruction.Op1Register));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("Unsupported Memory type for MOV operation");
                    }

                    break;
                }

                //MOV moffs16*,imm8
                case OpKind.Memory when _currentInstruction.Op1Kind == OpKind.Immediate8:
                {
                    Memory.SetByte(Registers.DS, (int)_currentInstruction.MemoryDisplacement, _currentInstruction.Immediate8);
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
                    Registers.SP -= 2;
                    Memory.Push(Registers.SP, BitConverter.GetBytes((ushort)Registers.IP));
                    Registers.SP -= 2;
                    Memory.Push(Registers.SP, BitConverter.GetBytes((ushort)Registers.CS));
                    Registers.BP = Registers.SP;


                        //Check for a possible relocation
                        int destinationValue;

                    if (_currentInstruction.Immediate16 == ushort.MaxValue)
                    {
                        var relocationRecord = Memory._segments[Registers.CS].RelocationRecords
                            .FirstOrDefault(x => x.Offset == Registers.IP + 1);

                        if (relocationRecord == null)
                        {
                            destinationValue = ushort.MaxValue;
                        }
                        else
                        {
                            _invokeExternalFunctionDelegate(relocationRecord.TargetTypeValueTuple.Item2,
                                relocationRecord.TargetTypeValueTuple.Item3);

                            Registers.SetValue(Register.CS, Memory.Pop(Registers.SP));
                            Registers.SP += 2;
                            Registers.SetValue(Register.EIP, Memory.Pop(Registers.SP));
                            Registers.SP += 2;
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
                    Registers.SP -= 2;
                    Memory.Push(Registers.SP, BitConverter.GetBytes((ushort) Registers.IP));
                    Registers.SP -= 2;
                    Memory.Push(Registers.SP, BitConverter.GetBytes((ushort) Registers.CS));
                    Registers.BP = Registers.SP;

                    Registers.IP = (ushort) _currentInstruction.FarBranch16;
                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown CALL: {_currentInstruction.Op0Kind}");
            }

        }
    }
}
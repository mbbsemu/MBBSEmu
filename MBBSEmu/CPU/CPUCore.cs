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

        private readonly InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public readonly CpuRegisters Registers;
        private readonly IMemoryCore _memory;

        private Instruction _currentInstruction;


        private readonly ushort STACK_SEGMENT;
        private readonly ushort EXTRA_SEGMENT;
        private const ushort STACK_BASE = 0xFFFF;

        public bool IsRunning;

        public CpuCore(IMemoryCore memoryCore, CpuRegisters cpuRegisters,
            InvokeExternalFunctionDelegate invokeExternalFunctionDelegate)
        {
            //Setup Delegate Call   
            _invokeExternalFunctionDelegate = invokeExternalFunctionDelegate;

            //Setup Memory Space
            _memory = memoryCore;
            STACK_SEGMENT = _memory.AllocateRoutineMemorySegment();
            EXTRA_SEGMENT = _memory.AllocateRoutineMemorySegment();

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
            var value = _memory.GetWord(Registers.SS, (ushort) (Registers.SP + 1));
            Registers.SP += 2;
            return value;
        }

        public void Push(ushort value)
        {
            _memory.SetWord(Registers.SS, (ushort) (Registers.SP - 1), value);
            Registers.SP -= 2;
        }

        public void Tick()
        {

            //Check for segment end
            if (Registers.CS == ushort.MaxValue && Registers.IP == ushort.MaxValue) 
            {
                IsRunning = false;
                return;
            }

            _currentInstruction = _memory.GetInstruction(Registers.CS, Registers.IP);

#if DEBUG
            //_logger.InfoRegisters(this);
            _logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction.ToString()}");
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
                case Mnemonic.Jge:
                    Op_Jge();
                    return;
                case Mnemonic.Jle:
                    Op_Jle();
                    return;
                case Mnemonic.Jbe:
                    Op_Jbe();
                    return;
                case Mnemonic.Call:
                    Op_Call();
                    return;

                //Instructions that do not set IP -- we'll just increment
                case Mnemonic.Add:
                    Op_Add();
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
                case Mnemonic.Les:
                    Op_Les();
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
                    var relocationRecord = _memory.GetSegment(Registers.CS).RelocationRecords
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

                    return _currentInstruction.MemorySize == MemorySize.UInt16
                        ? _memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offset)
                        : _memory.GetByte(Registers.GetValue(_currentInstruction.MemorySegment), offset);
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
                                             (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                        case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                            return (ushort) (Registers.BP -
                                             (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1) +
                                             Registers.SI);
                        case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                            return (ushort) (Registers.BX + _currentInstruction.MemoryDisplacement);
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
            var destination = Registers.GetValue(_currentInstruction.Op0Register);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var result = (ushort) (destination << source);

            switch (_currentInstruction.Op0Register.GetSize())
            {
                case 1: //8-Bit

                    //Only Set CF if the count (destination) if less than the source
                    if (destination < 8)
                    {
                        if(((byte)source).IsBitSet(destination - 1))
                            Registers.F.SetFlag(EnumFlags.CF);
                    }

                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    break;
                case 2: //16-bit

                    //Only Set CF if the count (destination) if less than the source
                    if (destination < 16)
                    {
                        if (source.IsBitSet(destination - 1))
                            Registers.F.SetFlag(EnumFlags.CF);
                    }

                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    break;
                default:
                    throw new Exception($"Unsupported Op0Register Size for SHL: {_currentInstruction.Op0Register.GetSize()}");
            }

            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                default:
                    throw new Exception($"Unsupported OpKind for SHL: {_currentInstruction.Op0Kind}");
            }
        }

        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            Registers.SetValue(_currentInstruction.Op0Register, offset);
            Registers.ES = segment;
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
                _memory.SetWord(Registers.ES, Registers.DI, Registers.AX);
                Registers.DI += 2;
                Registers.CX -= 2;
            }
        }

        private void Op_Inc()
        {
            ushort result;
            ushort destination;
            var operationSize = 0;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                {
                    operationSize = _currentInstruction.Op0Register.GetSize();
                    destination = Registers.GetValue(_currentInstruction.Op0Register);
                    result = (ushort) (destination + 1);
                    Registers.SetValue(_currentInstruction.Op0Register, result);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Uknown INC: {_currentInstruction.Op0Kind}");
            }

            switch (operationSize)
            {
                case 1:
                {
                    Registers.F.Evaluate<byte>(EnumFlags.OF, result, destination);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.Evaluate<ushort>(EnumFlags.OF, result, destination);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    break;
                }
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

        private void Op_Jbe()
        {
            if (Registers.F.IsFlagSet(EnumFlags.ZF) || Registers.F.IsFlagSet(EnumFlags.ZF))
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

        private void Op_Cmp()
        {

            //Get Comparison Size
            int comparisonSize;
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register:
                    comparisonSize = _currentInstruction.Op0Register.GetSize();
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt8:
                    comparisonSize = 1;
                    break;
                case OpKind.Memory when _currentInstruction.MemorySize == MemorySize.UInt16:
                    comparisonSize = 2;
                    break;
                default:
                    throw new Exception("Unknown Size for CMP operation");
            }

            switch (comparisonSize)
            {
                case 1:
                    Op_Cmp_8();
                    return;
                case 2:
                    Op_Cmp_16();
                    return;
            }
        }

        private void Op_Cmp_8()
        {
            var destination = (byte) GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = (byte) GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (byte) (destination - (sbyte) source);

                //_logger.Info($"CMP: {destination}-{(sbyte) source} == {result}");
                
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.CF, destination: destination, source: source);
                Registers.F.Evaluate<byte>(EnumFlags.OF, result, destination);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
            }
        }

        private void Op_Cmp_16()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            unchecked
            {
                var result = (ushort) (destination - (short) source);
                //_logger.Info($"CMP: {destination}-{(short) source} == {result}");

                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.CF, destination: destination, source: source);
                Registers.F.Evaluate<ushort>(EnumFlags.OF, result, destination);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
            }
        }

        private void Op_Add()
        {
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var result = (ushort)(destination + source);
            var operationSize = 0;

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
                    Registers.F.Evaluate<byte>(EnumFlags.OF, result, destination);
                    Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                    break;
                }
                case 2:
                {
                    Registers.F.Evaluate<ushort>(EnumFlags.OF, result, destination);
                    Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                    Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                    break;
                }
                default:
                    throw new Exception($"Unknown ADD operation size: {operationSize}");
            }
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
                            _memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                                GetOperandOffset(_currentInstruction.Op0Kind),
                                GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source));
                            return;
                        //MOV moffs8*,AL
                        //MOV moffs16*,imm8
                        case MemorySize.UInt8:
                            _memory.SetByte(Registers.GetValue(_currentInstruction.MemorySegment),
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
                        var relocationRecord = _memory.GetSegment(Registers.CS).RelocationRecords
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
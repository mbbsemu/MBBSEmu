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

namespace MBBSEmu.CPU
{
    public class CpuCore : ICpuCore
    {
        protected static readonly ILogger _logger;

        public delegate ReadOnlySpan<byte> InvokeExternalFunctionDelegate(ushort importedNameTableOrdinal, ushort functionOrdinal);

        private InvokeExternalFunctionDelegate _invokeExternalFunctionDelegate;

        public CpuRegisters Registers;
        private IMemoryCore Memory;

        private Instruction _currentInstruction;

        private ushort STACK_SEGMENT;
        private ushort EXTRA_SEGMENT;
        private const ushort STACK_BASE = 0xFFFF;

        private readonly List<byte[]> _fpuStack = new List<byte[]>(8) { new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4] };

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
            Registers.Halt = false;

            //These two values are the final values popped off on the routine's last RETF
            //Seeing a ushort.max for CS and IP tells the routine it's now done
            Push(ushort.MaxValue);
            Push(ushort.MaxValue);
        }

        /// <summary>
        ///     Only Resets Register Values
        /// </summary>
        public void Reset()
        {
            STACK_SEGMENT = 0;
            EXTRA_SEGMENT = ushort.MaxValue;

            //Setup Registers
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Pop()
        {
            var value = Memory.GetWord(Registers.SS, (ushort) (Registers.SP + 1));
#if DEBUG
            //_logger.Info($"Popped {value:X4} from {Registers.SP+1:X4}");
#endif
            Registers.SP += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ushort value)
        {
            Memory.SetWord(Registers.SS, (ushort) (Registers.SP - 1), value);

#if DEBUG
           // _logger.Info($"Pushed {value:X4} to {Registers.SP - 1:X4}");
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

            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);
            Registers.IP = _currentInstruction.IP16;

#if DEBUG
            //_logger.InfoRegisters(this);
            //_logger.Debug($"{Registers.CS:X4}:{_currentInstruction.IP16:X4} {_currentInstruction.ToString()}");

            //if(Registers.IP == 0xB3B)
              //Debugger.Break();
#endif

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
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported OpCode: {_currentInstruction.Mnemonic}");
            }

            Registers.IP += (ushort)_currentInstruction.Length;
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
                    return (ushort) _currentInstruction.Immediate8to16;
                case OpKind.Memory:
                {
                    //Relocation Record is applied to the offset of the value
                    //Because of this, we return out
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
            ushort result = 0;
            switch (opKind)
            {
                case OpKind.Memory:
                {
                    switch (_currentInstruction.MemoryBase)
                    {
                        case Register.DS when _currentInstruction.MemoryIndex == Register.None:
                        case Register.None when _currentInstruction.MemoryIndex == Register.None:
                            result = (ushort) _currentInstruction.MemoryDisplacement;
                            break;
                        case Register.BP when _currentInstruction.MemoryIndex == Register.None:
                        {
                            //This is a hack, I'm just assuming there wont be > 32k passed in values or local variables
                            //this might break if it's a crazy module. ¯\_(ツ)_/¯
                            if (_currentInstruction.MemoryDisplacement > short.MaxValue)
                            {
                                result = (ushort) (Registers.BP -
                                                   (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                                break;
                            }

                            result = (ushort) (Registers.BP + _currentInstruction.MemoryDisplacement + 1);
                            break;
                        }

                        case Register.BP when _currentInstruction.MemoryIndex == Register.SI:
                        {
                            if (_currentInstruction.MemoryDisplacement > short.MaxValue)
                            {
                                result = (ushort) ((Registers.BP + Registers.SI) -
                                                   (ushort.MaxValue - _currentInstruction.MemoryDisplacement + 1));
                                break;
                            }

                            result = (ushort) ((Registers.BP + Registers.SI) +
                                               _currentInstruction.MemoryDisplacement + 1);
                            break;
                        }

                        case Register.BX when _currentInstruction.MemoryIndex == Register.None:
                            result = (ushort) (Registers.BX + _currentInstruction.MemoryDisplacement);
                            break;
                        case Register.BX when _currentInstruction.MemoryIndex == Register.SI:
                            result = (ushort) (Registers.BX + _currentInstruction.MemoryDisplacement + Registers.SI);
                            break;
                        case Register.SI when _currentInstruction.MemoryIndex == Register.None:
                            result = (ushort) (Registers.SI + _currentInstruction.MemoryDisplacement);
                            break;
                        case Register.DI when _currentInstruction.MemoryIndex == Register.None:
                            result = (ushort) (Registers.DI + _currentInstruction.MemoryDisplacement);
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
                OpKind.Memory => (_currentInstruction.MemorySize == MemorySize.UInt8 ? 1 : 2),
                _ => -1
            };
        }

        /// <summary>
        ///     Saves the specified result of the current instruction into the current instructions destination
        /// </summary>
        /// <param name="result"></param>
        /// <param name="operationSize"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SaveResult(ushort result, int operationSize)
        {

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
                        GetOperandOffset(_currentInstruction.Op0Kind), (byte) result);
                    return;
                }
                case OpKind.Memory when operationSize == 2:
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
        private void Op_Loop()
        {
            if (Registers.CX == 0)
            {
                //Continue with the next instruction
                Registers.IP += (ushort)_currentInstruction.Length;
                return;
            }

            Registers.CX--;
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
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: {Registers.AH:X2}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Cwd()
        {
            Registers.DX = Registers.AX.IsBitSet(15) ? (ushort) 0xFFFF : (ushort) 0x0000;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Neg()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1 ? Op_Neg_8((byte)destination) : Op_Neg_16(destination);
            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Neg_8(byte destination)
        {
            unchecked
            {
                var result = (byte)(0 - destination);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Neg_16(ushort destination)
        {
            unchecked
            {
                var result = (ushort)(0 - destination);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sbb()
        {
            var source = (ushort) (GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source) +
                                   (Registers.F.Flags.IsFlagSet((ushort) EnumFlags.CF) ? 1 : 0));
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var operationSize = GetCurrentOperationSize();

            var result = operationSize == 1
                ? Op_Sbb_8((byte) source, (byte) destination)
                : Op_Sbb_16(source, destination);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Sbb_8(byte source, byte destination)
        {
            unchecked
            {
                var result = (byte)(destination - source);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sbb_16(ushort source, ushort destination)
        {
            unchecked
            {
                var result = (ushort)(destination - source);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Clc() => Registers.F.ClearFlag(EnumFlags.CF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Or()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1 ? Op_Or_8((byte) destination, (byte) source) : Op_Or_16(destination, source);

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Or_8(byte source, byte destination)
        {
            destination |= source;
            Registers.F.Evaluate<byte>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.PF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Or_16(ushort source, ushort destination)
        {
            destination |= source;
            Registers.F.Evaluate<ushort>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.PF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Rcr()
        {
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Rcr_8(destination, source)
                : Op_Rcr_16(destination, source);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Rcr_8(ushort destination, ushort source)
        {
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
        private ushort Op_Rcr_16(ushort destination, ushort source)
        {
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
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Rcl_8(destination, source)
                : Op_Rcl_16(destination, source);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Rcl_8(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (byte)destination;
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
        private ushort Op_Rcl_16(ushort destination, ushort source)
        {
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
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Sar_8(destination, source)
                : Op_Sar_16(destination, source);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sar_8(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (byte)(destination >> (sbyte)source);

                //Maintain Sign Bit
                if (((byte)destination).IsNegative())
                    result.SetFlag(1 << 7);

                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Sar_16(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (ushort)(destination >> source);
                
                //Maintain Sign Bit
                if (destination.IsNegative())
                    result.SetFlag(1 << 15);

                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Shr()
        {
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Shr_8(destination, source)
                : Op_Shr_16(destination, source);

            SaveResult(result, operationSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Shr_8(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (byte)(destination >> (sbyte)source);
                Registers.F.EvaluateCarry<byte>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<byte>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
                Registers.F.Evaluate<byte>(EnumFlags.SF, result);
                Registers.F.Evaluate<byte>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Shr_16(ushort destination, ushort source)
        {
            unchecked
            {
                var result = (ushort)(destination >> source);
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Subtraction, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Subtraction, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Shl()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Shl_8(destination, source)
                : Op_Shl_16(destination, source);

            SaveResult(result, operationSize);
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
                Registers.F.EvaluateCarry<ushort>(EnumArithmeticOperation.Addition, result, destination);
                Registers.F.EvaluateOverflow<ushort>(EnumArithmeticOperation.Addition, result, destination, source);
                Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
                Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Les()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            var dataPointer = new IntPtr16(Memory.GetArray(segment, offset, 4));

#if DEBUG
            //_logger.Info($"LES Loaded {dataPointer.Segment:X4}:{dataPointer.Offset:X4}");
#endif

            Registers.SetValue(_currentInstruction.Op0Register, dataPointer.Offset);
            Registers.ES = dataPointer.Segment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Lds()
        {
            var offset = GetOperandOffset(_currentInstruction.Op1Kind);
            var segment = Registers.GetValue(_currentInstruction.MemorySegment);

            Registers.SetValue(_currentInstruction.Op0Register, offset);
            Registers.DS = segment;
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
                    Registers.SP -= (ushort) (_currentInstruction.Immediate16 + 1);
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
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1 ? Op_Inc_8((byte)destination) : Op_Inc_16(destination);

            SaveResult(result, operationSize);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Dec()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Dec_8((byte) destination)
                : Op_Dec_16(destination);

            SaveResult(result, operationSize);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_And()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1 ? Op_And_8((byte) destination, (byte) source) : Op_And_16(destination, source);

            SaveResult(result, operationSize);

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_And_8(byte destination, byte source)
        {
            destination &= source;
            Registers.F.Evaluate<byte>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.PF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_And_16(ushort destination, ushort source)
        {
            destination &= source;
            Registers.F.Evaluate<ushort>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.PF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.SF, destination);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Xor()
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Xor_8(destination, source)
                : Op_Xor_16(destination, source);

            SaveResult(result, operationSize);

            //Clear Flags
            Registers.F.ClearFlag(EnumFlags.CF);
            Registers.F.ClearFlag(EnumFlags.OF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte Op_Xor_8(ushort destination, ushort source)
        {
            var result = (byte)(destination ^ source);
            Registers.F.Evaluate<byte>(EnumFlags.ZF, result);
            Registers.F.Evaluate<byte>(EnumFlags.PF, result);
            Registers.F.Evaluate<byte>(EnumFlags.SF, result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Xor_16(ushort destination, ushort source)
        {
            var result = (ushort)(destination ^ source);
            Registers.F.Evaluate<ushort>(EnumFlags.ZF, result);
            Registers.F.Evaluate<ushort>(EnumFlags.PF, result);
            Registers.F.Evaluate<ushort>(EnumFlags.SF, result);
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
                var destinationOffset = Memory.GetWord(Registers.GetValue(_currentInstruction.MemorySegment), offsetToDestinationValue);

                Registers.IP = destinationOffset;
                return;
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
                Registers.IP += (ushort) _currentInstruction.Length;
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
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);

            //Clear Overflow & Carry
            Registers.F.Flags.ClearFlag((ushort)EnumFlags.OF);
            Registers.F.Flags.ClearFlag((ushort)EnumFlags.CF);

            switch (GetCurrentOperationSize())
            {
                case 1:
                    Op_Test_8((byte) destination, (byte) source);
                    return;
                case 2:
                    Op_Test_16(destination, source);
                    return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Test_8(byte destination, byte source)
        {
            destination &= source;
            Registers.F.Evaluate<byte>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.SF, destination);
            Registers.F.Evaluate<byte>(EnumFlags.PF, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Test_16(ushort destination, ushort source)
        {
            destination &= source;
            Registers.F.Evaluate<ushort>(EnumFlags.ZF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.SF, destination);
            Registers.F.Evaluate<ushort>(EnumFlags.PF, destination);
        }

        /// <summary>
        ///     Identical to SUB, just doesn't save result of subtraction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sub()
        {
            //Get Comparison Operation Size
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Sub_8((byte) destination, (byte) source)
                : Op_Sub_16(destination, source);

            SaveResult(result, operationSize);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Add(bool addCarry = false)
        {
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var source = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Source);
            var operationSize = GetCurrentOperationSize();
            var result = operationSize == 1
                ? Op_Add_8((byte) destination, (byte) source, addCarry)
                : Op_Add_16(destination, source, addCarry);

            SaveResult(result, operationSize);
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
            var operand3 = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
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
            var operand2 = GetOperandValue(_currentInstruction.Op1Kind, EnumOperandType.Destination);
            var operand3 = GetOperandValue(_currentInstruction.Op2Kind, EnumOperandType.Source);
            ushort result;
            unchecked
            {
                result = (ushort) (operand2 * operand3);
            }

            Registers.SetValue(_currentInstruction.Op0Register, result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Idiv()
        {
            var source = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);

            switch (GetCurrentOperationSize())
            {
                case 1:
                    
                    Op_Idiv_8(Registers.AX, (byte) source);
                    return;
                case 2:
                    Op_Idiv_16(Registers.GetLong(Register.DX, Register.AX), source);
                    return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Idiv_8(ushort source, ushort destination)
        {
            unchecked
            {
                var quotient = Math.DivRem(source, destination, out var remainder);
                Registers.AL = (byte) quotient;
                Registers.AH = (byte) remainder;
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort Op_Idiv_16(int source, ushort destination)
        {
            unchecked
            {
                var quotient = Math.DivRem(source, destination, out var remainder);
                Registers.AX = (ushort)quotient;
                Registers.DX = (ushort)remainder;
                return 0;
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
            Push(GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination));
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Call()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.FarBranch16 when _currentInstruction.FarBranchSelector <= 0xFF:
                {
                    //Far call to another Segment
                    Push(Registers.CS);
                    Push((ushort) (Registers.IP + _currentInstruction.Length));

                    Registers.CS = _currentInstruction.FarBranchSelector;
                    Registers.IP = _currentInstruction.Immediate16;
                    return;
                }
                case OpKind.FarBranch16 when _currentInstruction.FarBranchSelector > 0xFF00:
                {
                    //We push CS:IP to the stack
                    Push(Registers.CS);
                    Push((ushort) (Registers.IP + _currentInstruction.Length));

                    //Simulate an ENTER
                    //Set BP to the current stack pointer
                    Push(Registers.BP);
                    Registers.BP = Registers.SP;

                    _invokeExternalFunctionDelegate(_currentInstruction.FarBranchSelector,
                        _currentInstruction.Immediate16);

                    //Simulate a LEAVE & retf
                    Registers.SP = Registers.BP;
                    Registers.SetValue(Register.BP, Pop());
                    Registers.SetValue(Register.EIP, Pop());
                    Registers.SetValue(Register.CS, Pop());
                    return;
                }
                case OpKind.NearBranch16:
                {
                    //We push CS:IP to the stack
                    //Push the IP of the **NEXT** instruction to the stack
                    Push((ushort) (Registers.IP + _currentInstruction.Length));
                    Registers.IP = _currentInstruction.FarBranch16;
                    return;
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
            _fpuStack[Registers.Fpu.GetStackTop()] = floatToLoad.ToArray();
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

            _fpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(convertedInt);
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
            var float1 = BitConverter.ToSingle(_fpuStack[Registers.Fpu.GetStackTop()]);
            var float2 = BitConverter.ToSingle(floatToMultiply);
            
            var result = float1 * float2;
            _fpuStack[Registers.Fpu.GetStackTop()] = BitConverter.GetBytes(result);
            Registers.Fpu.PushStackTop();
        }

        /// <summary>
        ///     Floating Pointe Store & Pop (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fstp()
        {
            var offset = GetOperandOffset(_currentInstruction.Op0Kind);

            Registers.Fpu.PopStackTop();
            var valueToSave = _fpuStack[Registers.Fpu.GetStackTop()];
            Memory.SetArray(Registers.DS, offset, valueToSave);
        }

        /// <summary>
        ///     Floating Point Compare (x87)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Fcompp()
        {
            Registers.Fpu.PopStackTop();
            var float1 = BitConverter.ToSingle(_fpuStack[Registers.Fpu.GetStackTop()]);
            Registers.Fpu.PopStackTop();
            var float2 = BitConverter.ToSingle(_fpuStack[Registers.Fpu.GetStackTop()]);

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
                    Registers.SetValue(Register.AX, Registers.Fpu.RegisterWord);
                    break;
                case OpKind.Memory:
                    Memory.SetWord(Registers.GetValue(_currentInstruction.MemorySegment),
                        GetOperandOffset(_currentInstruction.Op0Kind), Registers.Fpu.RegisterWord);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Op_Sahf()
        {

            if (Registers.AH.IsFlagSet((byte) EnumFlags.SF))
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
            var destination = GetOperandValue(_currentInstruction.Op0Kind, EnumOperandType.Destination);
            var operationSize = GetCurrentOperationSize();
            var result = (ushort)~destination;

            SaveResult(result, operationSize);
        }
    }
}
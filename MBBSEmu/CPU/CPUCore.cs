using System;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        public readonly CpuRegisters Registers;
        public readonly Stack<int> StackMemory;
        public readonly CpuMemory Memory;

        private Instruction _currentInstruction;


        public CpuCore()
        {
            Registers = new CpuRegisters();
            StackMemory = new Stack<int>();
            Memory = new CpuMemory();
        }

        public void Tick()
        {
            _currentInstruction = Memory.GetInstruction(Registers.CS, Registers.IP);

            switch (_currentInstruction.Mnemonic)
            {
                case Mnemonic.Push:
                    Op_Push();
                    break;
                case Mnemonic.Mov:
                    Op_Mov();
                    break;
            }

            Registers.IP += _currentInstruction.ByteLength;
        }


        /// <summary>
        ///     Push Op Code
        /// </summary>
        private void Op_Push()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentInstruction.Op0Register == Register.SI:
                    Memory.PushWord(Registers.SP, (ushort)Registers.SI);
                    Registers.SP += 2;
                    break;
                case OpKind.Register when _currentInstruction.Op0Register == Register.DI:
                    Memory.PushWord(Registers.SP, (ushort)Registers.DI);
                    Registers.SP += 2;
                    break;
                case OpKind.Register when _currentInstruction.Op0Register == Register.DS:
                    Memory.PushWord(Registers.SP, (ushort)Registers.DS);
                    Registers.SP += 2;
                    break;
            }
        }

        /// <summary>
        ///     MOV Op Code
        /// </summary>
        private void Op_Mov()
        {
            switch (_currentInstruction.Op0Kind)
            {
                case OpKind.Register when _currentInstruction.Op2Kind == OpKind.Immediate16:
                {
                    //Check for a possible relocation
                    int destinationValue;

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

                    switch (_currentInstruction.Op1Register)
                    {
                            case Register.AX:
                                Registers.AX = destinationValue;
                                break;
                            case Register.BX:
                                Registers.BX = destinationValue;
                                break;
                            case Register.CX:
                                Registers.CX = destinationValue;
                                break;
                            case Register.DX:
                                Registers.DX = destinationValue;
                                break;
                            default:
                                throw new InvalidOperationException("Unknown Destination Register");
                    }
                    break;
                }
            }
        }
    }
}
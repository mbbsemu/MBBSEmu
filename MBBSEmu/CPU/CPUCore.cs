using System;
using System.Collections.Generic;
using Iced.Intel;

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

        public void ExecuteOp(Instruction instruction)
        {
            _currentInstruction = instruction;
            switch (_currentInstruction.Mnemonic)
            {
                case Mnemonic.Mov:
                    Op_Mov();
                    break;
                case Mnemonic.Push:
                    Op_Push();
                    break;
            }

            return;
        }

        private void Op_Mov()
        {

        }

        private void Op_Push()
        {

        }
    }
}
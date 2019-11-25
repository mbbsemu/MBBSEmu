using System;
using System.Collections.Generic;

namespace MBBSEmu.CPU
{
    public class CPUCore
    {
        public readonly CPURegisters Registers;
        public readonly Stack<int> StackMemory;
        public readonly CPUMemory Memory;

        public CPUCore()
        {
            Registers = new CPURegisters();
            StackMemory = new Stack<int>();
            Memory = new CPUMemory();
        }
    }
}
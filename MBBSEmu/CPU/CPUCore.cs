using System;
using System.Collections.Generic;

namespace MBBSEmu.CPU
{
    public class CpuCore
    {
        public readonly CpuRegisters Registers;
        public readonly Stack<int> StackMemory;
        public readonly CpuMemory Memory;

        public CpuCore()
        {
            Registers = new CpuRegisters();
            StackMemory = new Stack<int>();
            Memory = new CpuMemory();
        }
    }
}
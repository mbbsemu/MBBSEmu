using System;
using System.Collections.Generic;
using System.Reflection;

namespace MBBSEmu.CPU
{
    public class CPUMemory
    {
        public List<byte[]> PointerMemory;

        public CPUMemory()
        {
            PointerMemory = new List<byte[]>();
            Console.WriteLine("X86_16 Memory Space Initialized!");
        }

        /// <summary>
        ///     Adds data to "memory" at he specified pointer
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int AddPointer(byte[] value)
        {
            PointerMemory.Add(value);
            return PointerMemory.Count - 1;
        }

        public void ClearPointer(int pointer)
        {
            PointerMemory.RemoveAt(pointer);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.CPU;

namespace MBBSEmu.Host
{
    /// <summary>
    ///     Class acts as the MBBS/WG Host Process which contains all the
    ///     imported functions.
    ///
    ///     We'll perform the imported functions here
    /// </summary>
    public class MBBSHost
    {
        private readonly CpuCore _cpu;

        public MBBSHost(CpuCore cpu)
        {
            _cpu = cpu;
        }

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        /// 
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Pointer for TIME_t assigned to AX, Value is 32-Bit TIME_T
        /// </summary>
        [MBBSHostFunction("TIME", 599)]
        public void Func_Time()
        {
            //For now, ignore the input pointer for time_t
            _cpu.StackMemory.Pop();
            _cpu.StackMemory.Pop();

            var b = new byte[] { 10, 12, 12, 12 };
            var now = DateTime.Now;
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var tsEpoch = now - epoch;
            var passedSeconds = (int)tsEpoch.TotalSeconds;
            var copyBytes = BitConverter.GetBytes(passedSeconds);
            Array.Copy(copyBytes, 0, b, 0, 4);


            _cpu.Registers.AX = BitConverter.ToUInt16(b, 2);
            _cpu.Registers.DX = BitConverter.ToUInt16(b, 0);
        }

        /// <summary>
        ///     Initializes the Pseudo-Random Number Generator with the given seen
        ///
        ///     Since we'll handle this internally, we'll just ignore this
        ///
        ///     Signature: void srand (unsigned int seed);
        /// </summary>
        [MBBSHostFunction("SRAND", 599)]
        public void Func_Srand()
        {
            //Pop the input int, since we're ignoring this
            _cpu.StackMemory.Pop();
        }

        /// <summary>
        ///     Allocate a new memory block and zeros it out
        /// 
        ///     Signature: char *alczer(unsigned nbytes);
        ///     Return: AX = Pointer to memory
        ///             DX = Size of memory
        /// </summary>
        public void Func_Alczer()
        {
            var size = _cpu.StackMemory.Pop();
            //Get the current pointer
            var pointer = _cpu.Memory.GetHostPointer();

            //Increment the pointer to 'allocate' the memory
            _cpu.Memory.IncrementHostPointer(size);

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = size;
        }
    }
}

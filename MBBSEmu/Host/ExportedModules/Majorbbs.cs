using MBBSEmu.CPU;
using MBBSEmu.Logging;
using NLog;
using System;

namespace MBBSEmu.Host
{
    /// <summary>
    ///     Class which defines functions that are part of the MajorBBS/WG SDK and included in
    ///     MAJORBBS.H.
    ///
    ///     While a majority of these functions are specific to MajorBBS/WG, some are just proxies for
    ///     Borland C++ macros and are noted as such.
    /// </summary>
    public class Majorbbs
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly MbbsHostMemory _mbbsHostMemory;
        private readonly CpuCore _cpu;

        public Majorbbs(CpuCore cpuCore)
        {
            _mbbsHostMemory = new MbbsHostMemory();
            _cpu = cpuCore;
        }

        /// <summary>
        ///     Initializes the Pseudo-Random Number Generator with the given seen
        ///
        ///     Since we'll handle this internally, we'll just ignore this
        ///
        ///     Signature: void srand (unsigned int seed);
        /// </summary>
        [ExportedModuleFunction(Name = "SRAND", Ordinal = 561)]
        public void Func_Srand()
        {
            //Pop the input int, since we're ignoring this
            _cpu.Memory.PopWord(_cpu.Registers.SP);
            _cpu.Registers.SP -= 2;
        }

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        /// 
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Value is 32-Bit TIME_T (AX:DX)
        /// </summary>
        [ExportedModuleFunction(Name = "TIME", Ordinal = 599)]
        public void Func_Time()
        {
            //For now, ignore the input pointer for time_t
            _cpu.Memory.PopWord(_cpu.Registers.SP);
            _cpu.Registers.SP += 2;
            _cpu.Memory.PopWord(_cpu.Registers.SP);
            _cpu.Registers.SP += 2;

            var outputArray = new byte[4];
            var passedSeconds = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Array.Copy(BitConverter.GetBytes(passedSeconds), 0, outputArray, 0, 4);

            _cpu.Registers.AX = BitConverter.ToUInt16(outputArray, 2);
            _cpu.Registers.DX = BitConverter.ToUInt16(outputArray, 0);

#if DEBUG
            _logger.Debug($"time() passed seconds: {passedSeconds} (AX:{_cpu.Registers.AX:X4}, DX:{_cpu.Registers.DX:X4})");
#endif
        }



        /// <summary>
        ///     Allocate a new memory block and zeros it out
        /// 
        ///     Signature: char *alczer(unsigned nbytes);
        ///     Return: AX = Pointer to memory
        ///             DX = Size of memory
        /// </summary>
        [ExportedModuleFunction(Name = "ALCZER", Ordinal = 68)]
        public void Func_Alczer()
        {
            var size = _cpu.Memory.PopByte(_cpu.Registers.SP);
            _cpu.Registers.SP -= 2;

            //Get the current pointer
            var pointer = _mbbsHostMemory.AllocateHostMemory(size);

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = size;

#if DEBUG
            _logger.Debug($"alczer() allocated {size} bytes starting at {size:X4}");
#endif
        }
    }
}

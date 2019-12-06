using System.Collections.Generic;
using System.IO;
using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using NLog;

namespace MBBSEmu.Host.ExportedModules
{
    /// <summary>
    ///     Base Class for Exported MajorBBS Routines
    /// </summary>
    public abstract class ExportedModuleBase
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        protected readonly IMemoryCore Memory;
        protected readonly CpuCore _cpu;
        protected readonly MbbsModule _module;

        private ushort _hostMemoryOffset = 0x0;

        protected ExportedModuleBase(CpuCore cpuCore, MbbsModule module)
        {
            //Setup Host Memory
            Memory = new MemoryCore();
            Memory.AddSegment((ushort)EnumHostSegments.MemoryRegion);

            _cpu = cpuCore;
            _module = module;
        }

        /// <summary>
        ///     Tracks the pointer of the allocated memory in the Host Memory Pool
        ///
        ///     This will increment with each call by size
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        protected ushort AllocateHostMemory(ushort size)
        {
            var offset = _hostMemoryOffset;
            _hostMemoryOffset += size;

#if DEBUG
            _logger.Debug($"Allocated {size} bytes of memory in Host Memory Segment");
#endif
            return offset;
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        protected ushort GetParameter(ushort parameterOrdinal)
        {
            return _cpu.Memory.GetWord(_cpu.Registers.SS, (ushort)(_cpu.Registers.BP + 4 + (2 * parameterOrdinal)));
        }

        /// <summary>
        ///     Gets the required parameters for the specified "printf" formatted string
        /// </summary>
        /// <param name="stringToFormat"></param>
        /// <param name="startingParameterOrdinal"></param>
        /// <returns></returns>
        protected List<object> GetPrintfParameters(string stringToFormat, ushort startingParameterOrdinal)
        {
            var formatParameters = new List<object>();
            for (var i = 0; i < stringToFormat.CountPrintf(); i++)
            {
                //Gets the control character for the ordinal provided
                switch (stringToFormat.GetPrintf(i))
                {
                    case 'c':
                        {
                            var charParameter = GetParameter((ushort) (startingParameterOrdinal + i));
                            formatParameters.Add((char)charParameter);
                            break;
                        }
                    case 's':
                        {
                            var parameterOffset = GetParameter((ushort)(startingParameterOrdinal + i));
                            var parameterSegment = GetParameter((ushort)(startingParameterOrdinal + i++));

                            var parameter = parameterSegment == 0xFFFF
                                ? Memory.GetString(0, parameterOffset)
                                : _cpu.Memory.GetString(parameterSegment, parameterOffset);

                            formatParameters.Add(Encoding.ASCII.GetString(parameter));
                            break;
                        }
                    case 'd':
                        {
                            var lowWord = GetParameter((ushort)(startingParameterOrdinal + i));
                            var highWord = GetParameter((ushort)(startingParameterOrdinal + i++));

                            var parameter = highWord << 16 | lowWord;

                            formatParameters.Add(parameter);
                            break;
                        }
                    default:
                        throw new InvalidDataException($"Unhandled Printf Control Character: {stringToFormat.GetPrintf(i)}");
                }
            }
            return formatParameters;
        }
    }
}

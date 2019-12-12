using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Attributes;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Base Class for Exported MajorBBS Routines
    /// </summary>
    public abstract class ExportedModuleBase : IDisposable
    {
        public delegate ushort ExportedFunctionDelegate();

        public Dictionary<ushort, ExportedFunctionDelegate> ExportedFunctions;

        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        protected readonly IMemoryCore Memory;
        protected readonly CpuRegisters Registers;
        protected readonly MbbsModule Module;

        private ushort _routineMemoryOffset = 0x0;
        protected ushort RoutineMemorySegment;

        protected ExportedModuleBase(IMemoryCore memoryCore, CpuRegisters cpuRegisters, MbbsModule module)
        {
            //Setup Host Memory
            Memory = memoryCore;
            RoutineMemorySegment = Memory.AllocateRoutineMemorySegment();

            Registers = cpuRegisters;

            Module = module;

            //Setup Exported Functions
            ExportedFunctions = new Dictionary<ushort, ExportedFunctionDelegate>();
            SetupExportedFunctionDelegates();
        }

        private void SetupExportedFunctionDelegates()
        {
            _logger.Info($"Setting up {this.GetType().Name.ToUpper()} exported functions...");
            var functionBindings = this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(ExportedFunctionAttribute), false).Length > 0).Select(y => new
                {
                    binding = (ExportedFunctionDelegate) Delegate.CreateDelegate(typeof(ExportedFunctionDelegate),
                        this,
                        y.Name),
                    definitions = y.GetCustomAttributes(typeof(ExportedFunctionAttribute))
                });

            foreach (var f in functionBindings)
            {
                var ordinal = ((ExportedFunctionAttribute) f.definitions.First()).Ordinal;
                ExportedFunctions[ordinal] = f.binding;
            }

            _logger.Info($"Setup {ExportedFunctions.Count} functions from {this.GetType().Name.ToUpper()}");
        }

        /// <summary>
        ///     Tracks the pointer of the allocated memory in the Host Memory Pool
        ///
        ///     This will increment with each call by size
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        protected ushort AllocateRoutineMemory(ushort size)
        {
            var offset = _routineMemoryOffset;
#if DEBUG
            _logger.Debug($"Allocated {size} bytes of memory in Routine Memory Segment at {RoutineMemorySegment:X4}:{_routineMemoryOffset}");
#endif
            _routineMemoryOffset += size;
            return offset;
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        protected ushort GetParameter(ushort parameterOrdinal)
        {
            var parameterOffset = (ushort) (Registers.BP + 5 + (2 * parameterOrdinal));
            return Memory.GetWord(Registers.SS, parameterOffset);
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
            var currentParameter = startingParameterOrdinal;
            for (var i = 0; i < stringToFormat.CountPrintf(); i++)
            {
                //Gets the control character for the ordinal provided
                switch (stringToFormat.GetPrintf(i))
                {
                    case 'c':
                        {
                            var charParameter = GetParameter(currentParameter++);
                            formatParameters.Add((char)charParameter);
                            break;
                        }
                    case 's':
                        {

                            var parameterOffset = GetParameter(currentParameter++);
                            var parameterSegment = GetParameter(currentParameter++);
                            var parameter = Memory.GetString(parameterSegment, parameterOffset);
                            formatParameters.Add(Encoding.Default.GetString(parameter));
                            break;
                        }
                    case 'd':
                        {
                            var lowWord = GetParameter(currentParameter++);
                            var highWord = GetParameter(currentParameter++);
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

        protected virtual void Dispose(bool managedAndNative)
        {
#if DEBUG
            _logger.Info($"Freeing Routine Memory: {RoutineMemorySegment:X4} ({_routineMemoryOffset} bytes freed)");
#endif

            Memory.FreeRoutineMemorySegment(RoutineMemorySegment);
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

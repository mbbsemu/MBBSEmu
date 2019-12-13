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
    public abstract class ExportedModuleBase
    {
        public delegate ushort ExportedFunctionDelegate();

        /// <summary>
        ///     Dictionary of Exported Functions
        ///
        ///     Key Represents the Function Ordinal in the associated .H file
        /// </summary>
        public Dictionary<ushort, ExportedFunctionDelegate> ExportedFunctions;

        /// <summary>
        ///     Internal Variables are stored inside the system (module name, etc.)
        ///
        ///     We want to write these only once to the HostMemorySegment, so we use this
        ///     dictionary to track the variable by a given name (key) and the pointer to the segment
        ///     it lives in.
        /// </summary>
        private protected Dictionary<string, IntPtr16> HostMemoryVariables;

        private protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public IMemoryCore Memory;
        public CpuRegisters Registers;
        public MbbsModule Module;

        /// <summary>
        ///     Convenience Variable, prevents having to repeatedly cast the Enum to ushort
        /// </summary>
        private protected readonly ushort HostMemorySegment = (ushort) EnumHostSegments.HostMemorySegment;

        private protected ExportedModuleBase(CpuRegisters cpuRegisters, MbbsModule module)
        {
            Memory = module.Memory;
            Module = module;
            Registers = cpuRegisters;

            //Setup Exported Functions
            ExportedFunctions = new Dictionary<ushort, ExportedFunctionDelegate>();
            SetupExportedFunctionDelegates();

            HostMemoryVariables = new Dictionary<string, IntPtr16>();
        }

        private void SetupExportedFunctionDelegates()
        {
            _logger.Info($"Setting up {this.GetType().Name.ToUpper()} exported functions...");
            var functionBindings = GetType()
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

            _logger.Info($"Setup {ExportedFunctions.Count} functions from {GetType().Name.ToUpper()}");
        }


        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        private protected ushort GetParameter(ushort parameterOrdinal)
        {
            var parameterOffset = (ushort) (Registers.BP + 5 + (2 * parameterOrdinal));
            return Memory.GetWord(Registers.SS, parameterOffset);
        }

        private protected static readonly char[] _controlCharacters = {'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%'};

        private protected bool IsControlCharacter(ReadOnlySpan<byte> c)
        {
            for (var i = 0; i < _controlCharacters.Length; i++)
            {
                if (_controlCharacters[i] == c[0])
                    return true;
            }
            return false;
        }

        /// <summary>
        ///     Printf Parsing and Encoding
        /// </summary>
        /// <param name="s"></param>
        /// <param name="stringToParse"></param>
        /// <param name="startingParameterOrdinal"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatPrintf(ReadOnlySpan<byte> stringToParse, ushort startingParameterOrdinal)
        {
            using var msOutput = new MemoryStream(stringToParse.Length);
            var currentParameter = startingParameterOrdinal;
            for (var i = 0; i < stringToParse.Length; i++)
            {
                //Found a Control Character
                if (stringToParse[i] == '%' && IsControlCharacter(stringToParse.Slice(i + 1,1)))
                {
                    switch ((char)stringToParse[i + 1])
                    {
                        case 'c':
                        {
                            var charParameter = GetParameter(currentParameter++);
                            msOutput.WriteByte((byte) charParameter);
                            break;
                        }
                        case 's':
                        {

                            var parameterOffset = GetParameter(currentParameter++);
                            var parameterSegment = GetParameter(currentParameter++);
                            var parameter = Memory.GetString(parameterSegment, parameterOffset);
                            msOutput.Write(parameter);
                            break;
                        }
                        case 'd':
                        {
                            var lowWord = GetParameter(currentParameter++);
                            var highWord = GetParameter(currentParameter++);
                            var parameter = highWord << 16 | lowWord;
                            msOutput.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
                            break;
                        }
                        default:
                            throw new InvalidDataException(
                                $"Unhandled Printf Control Character: {(char)stringToParse[i + 1]}");
                    }
                    i++;
                    continue;
                }
                msOutput.WriteByte(stringToParse[i]);
            }
            return msOutput.ToArray();
        }
    }
}
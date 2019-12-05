using System.Collections.Generic;
using System.IO;
using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using NLog;

namespace MBBSEmu.Host.ExportedModules
{
    public abstract class ExportedModuleBase
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        protected readonly MbbsHostMemory _mbbsHostMemory;
        protected readonly CpuCore _cpu;
        protected readonly MbbsModule _module;

        protected ExportedModuleBase(CpuCore cpuCore, MbbsModule module)
        {
            _mbbsHostMemory = new MbbsHostMemory();
            _cpu = cpuCore;
            _module = module;
        }

        protected List<object> GetPrintfVariables(string stringToFormat, ushort stackStartingOffset)
        {
            var formatParameters = new List<object>();
            var parameterOffsetAdjustment = 0;
            for (var i = 0; i < stringToFormat.CountPrintf(); i++)
            {
                //Gets the control character for the ordinal provided
                switch (stringToFormat.GetPrintf(i))
                {
                    case 'c':
                        {
                            var charParameter = _cpu.Memory.Pop(stackStartingOffset + parameterOffsetAdjustment);
                            formatParameters.Add((char)charParameter);
                            parameterOffsetAdjustment += 2;
                            break;
                        }
                    case 's':
                        {
                            var parameterOffset = _cpu.Memory.Pop(stackStartingOffset + parameterOffsetAdjustment);
                            var parameterSegment = _cpu.Memory.Pop(stackStartingOffset + 2 + parameterOffsetAdjustment);

                            var parameter = parameterSegment == 0xFFFF
                                ? _mbbsHostMemory.GetString(0, parameterOffset)
                                : _cpu.Memory.GetString(parameterSegment, parameterOffset);

                            formatParameters.Add(Encoding.ASCII.GetString(parameter));
                            parameterOffsetAdjustment += 4;
                            break;
                        }
                    case 'd':
                        {
                            var lowWord = _cpu.Memory.Pop(stackStartingOffset + parameterOffsetAdjustment);
                            var highWord = _cpu.Memory.Pop(stackStartingOffset + 2 + parameterOffsetAdjustment);

                            var parameter = highWord << 16 | lowWord;

                            formatParameters.Add(parameter);
                            parameterOffsetAdjustment += 4;
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

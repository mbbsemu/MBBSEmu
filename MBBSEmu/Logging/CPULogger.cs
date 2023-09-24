using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Logging.Targets;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     Logger used for logging CPU Instructions, Registers, and Messages
    ///
    ///     Messages are in turn written to the specified Logging Targets
    /// </summary>
    public class CpuLogger
    {
        private static readonly List<ILoggingTarget> LOGGING_TARGETS = new();

        public CpuLogger() { }

        public CpuLogger(ILoggingTarget target)
        {
            AddTarget(target);
        }

        public void AddTarget(ILoggingTarget target)
        {
            LOGGING_TARGETS.Add(target);
        }

        public void Log(string message = "", Instruction instruction = default, CpuRegisters registers = null)
        {
            foreach (var target in LOGGING_TARGETS)
            {
                target.Write(message, instruction, registers);
            }
        }

    }
}

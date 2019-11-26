using MBBSEmu.CPU;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MBBSEmu.Host
{
    /// <summary>
    ///     Class acts as the MBBS/WG Host Process which contains all the
    ///     imported functions.
    ///
    ///     We'll perform the imported functions here
    /// </summary>
    public class MbbsHost
    {
        private delegate void ExportedFunctionDelegate();
        private readonly Dictionary<string,Dictionary<int, ExportedFunctionDelegate>> _exportedFunctionDelegates;
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        private readonly MbbsModule _module;
        private readonly Majorbbs _hostFunctions;
        private readonly CpuCore _cpu;
        private readonly Thread _hostThread;
        private bool _isRunning;

        public MbbsHost(MbbsModule module)
        {
            _module = module;
            _hostThread = new Thread(Run);
            _logger.Info("Constructing MbbsEmu Host...");
            _logger.Info("Initalizing x86_16 CPU Emulator...");
            _cpu = new CpuCore(InvokeHostedFunction);
            _hostFunctions = new Majorbbs(_cpu);

            //Setup Function Delegates
            _exportedFunctionDelegates = new Dictionary<string, Dictionary<int, ExportedFunctionDelegate>>();

            var functionBindings = _hostFunctions.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(ExportedModuleFunctionAttribute), false).Length > 0).Select(y => new
                {
                    binding = (ExportedFunctionDelegate) Delegate.CreateDelegate(typeof(ExportedFunctionDelegate), _hostFunctions,
                        y.Name),
                    definitions = y.GetCustomAttributes(typeof(ExportedModuleFunctionAttribute))
                });

            foreach (var f in functionBindings)
            {
                var ordinal = ((ExportedModuleFunctionAttribute) f.definitions.First()).Ordinal;
                _exportedFunctionDelegates["MAJORBBS"][ordinal] = f.binding;
            }

            foreach (var seg in _module.File.SegmentTable)
            {
                var segmentOffset = _cpu.Memory.AddSegment(seg);
                Console.WriteLine($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded at {segmentOffset}!");
            }

            _logger.Info("Constructed MbbsEmu Host!");
        }

        /// <summary>
        ///     Starts a new instance of the MbbsHost running the specified MbbsModule
        /// </summary>
        public void Start()
        {
            _logger.Info("Preparing to Start MbbsEmu Host...");
            _logger.Info("Locating _INIT_ function...");
            var initResidentName = _module.File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));
            if (initResidentName == null)
                throw new Exception("Unable to locate _INIT_ entry in Resident Name Table");

            _logger.Info($"Located Resident Name Table record for _INIT_: {initResidentName.Name}");
            _logger.Info($"Ordinal in Entry Table: {initResidentName.IndexIntoEntryTable}");
            var initEntryPoint = _module.File.EntryTable
                .First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
            _logger.Info(
                $"Located Entry Table Record, _INIT_ function located in Segment {initEntryPoint.SegmentNumber}");

            _cpu.Registers.CS = initEntryPoint.SegmentNumber;
            _cpu.Registers.IP = 0;
            _logger.Info($"");
            _logger.Info($"Starting MbbsEmu Host at {initResidentName.Name} (Seg {initEntryPoint.SegmentNumber}:{initEntryPoint.Offset:X4}h)...");
            _hostThread.Start();
        }

        private void Run()
        {
            _isRunning = true;
            while(_isRunning)
                _cpu.Tick();
        }

        public void Stop()
        {
            _isRunning = false;
        }
        
        /// <summary>
        ///     Invoked from the Executing x86 Code when an imported function from the MajorBBS/Worldgroup
        ///     host process is to be called.
        /// </summary>
        private void InvokeHostedFunction(string importedModuleName, int functionOrdinal)
        {
            if(!_exportedFunctionDelegates.TryGetValue(importedModuleName, out var exportedModule))
                throw new Exception($"Exported Module Not Found: {importedModuleName}");

            if(!exportedModule.TryGetValue(functionOrdinal, out var function))
                throw new Exception($"Exported Module Ordinal Not Found in {importedModuleName}: {functionOrdinal}");

            //Execute it
            function();
        }

        

        
    }
}

using MBBSEmu.CPU;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Host.ExportedModules;

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
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        /// <summary>
        ///     Delegate used to invoke functions Exported from MAJORBBS or GALGSBL
        /// </summary>
        /// <returns></returns>
        private delegate ushort ExportedFunctionDelegate();
        
        /// <summary>
        ///     Delegate used to access memory values EXPORTED (mostly arrays & structs)
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private delegate byte ExportedMemoryValueDelegate(ushort segment, ushort offset);

        private readonly Dictionary<string,Dictionary<int, ExportedFunctionDelegate>> _exportedFunctionDelegates;
        private readonly Dictionary<string, ExportedMemoryValueDelegate> _exportedMemoryValueDelegates;
        
        
        private readonly MbbsModule _module;

        private readonly Majorbbs _majorbbsHostFunctions;
        private readonly Galsbl _galsblHostFunctions;

        private readonly CpuCore _cpu;
        private readonly Thread _hostThread;
        private bool _isRunning;

        public MbbsHost(MbbsModule module)
        {
            _module = module;
            _hostThread = new Thread(Run);
            _logger.Info("Constructing MbbsEmu Host...");
            _logger.Info("Initalizing x86_16 CPU Emulator...");
            _cpu = new CpuCore(InvokeHostedFunction, GetHostMemoryValue);

            _majorbbsHostFunctions = new Majorbbs(_cpu, _module);
            _galsblHostFunctions = new Galsbl(_cpu, _module);
            //Setup Function Delegates
            _exportedFunctionDelegates = new Dictionary<string, Dictionary<int, ExportedFunctionDelegate>>();

            //Get Exported Functions for MAJORBBS.H
            _logger.Info("Setting up MAJORBBS.H exported functions...");
            var mbbsfunctionBindings = _majorbbsHostFunctions.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(ExportedModuleAttribute), false).Length > 0).Select(y => new
                {
                    binding = (ExportedFunctionDelegate) Delegate.CreateDelegate(typeof(ExportedFunctionDelegate), _majorbbsHostFunctions,
                        y.Name),
                    definitions = y.GetCustomAttributes(typeof(ExportedModuleAttribute))
                });
            _exportedFunctionDelegates["MAJORBBS"] = new Dictionary<int, ExportedFunctionDelegate>();
            foreach (var f in mbbsfunctionBindings)
            {
                var ordinal = ((ExportedModuleAttribute) f.definitions.First()).Ordinal;
                _exportedFunctionDelegates["MAJORBBS"][ordinal] = f.binding;
            }
            _logger.Info($"{_exportedFunctionDelegates["MAJORBBS"].Count} exports setup");

            //Get Exported for GALGSBL.H
            _logger.Info("Setting up GALGSBL.H exported functions...");
            var galgsblFunctionBindings = _galsblHostFunctions.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(ExportedModuleAttribute), false).Length > 0).Select(y => new
                {
                    binding = (ExportedFunctionDelegate)Delegate.CreateDelegate(typeof(ExportedFunctionDelegate), _galsblHostFunctions,
                        y.Name),
                    definitions = y.GetCustomAttributes(typeof(ExportedModuleAttribute))
                });
            _exportedFunctionDelegates["GALGSBL"] = new Dictionary<int, ExportedFunctionDelegate>();
            foreach (var f in galgsblFunctionBindings)
            {
                var ordinal = ((ExportedModuleAttribute)f.definitions.First()).Ordinal;
                _exportedFunctionDelegates["GALGSBL"][ordinal] = f.binding;
            }
            _logger.Info($"{_exportedFunctionDelegates["GALGSBL"].Count} exports setup");

            foreach (var seg in _module.File.SegmentTable)
            {
                _cpu.Memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Verify that the imported functions are all supported by MbbsEmu

            //if (!VerifyImportedFunctions())
            //    throw new Exception("Module is currently unsupported by MbbEmu! :(");


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

        public bool VerifyImportedFunctions()
        {
            _logger.Info("Scanning CODE segments to ensure all Imported Functions are supported by MbbsEmu...");
            var scanResult = true;
            var ordinalMajorBbs = _module.File.ImportedNameTable.First(x => x.Name == "MAJORBBS");
            foreach (var seg in _module.File.SegmentTable.Where(x=> x.Flags.Contains(EnumSegmentFlags.Code)))
            {
                foreach (var relo in seg.RelocationRecords.Where(x =>
                    x.Flag == EnumRecordsFlag.IMPORTORDINAL &&
                    x.TargetTypeValueTuple.Item2 == ordinalMajorBbs.Ordinal))
                {

                    if (!_exportedFunctionDelegates["MAJORBBS"].ContainsKey(relo.TargetTypeValueTuple.Item3))
                    {
                        _logger.Error($"Module Relies on MAJORBBS Function {relo.TargetTypeValueTuple.Item3}, which is not implemented");
                        scanResult = false;
                    }
                }
            }
            return scanResult;
        }
        
        /// <summary>
        ///     Invoked from the Executing x86 Code when an imported function from the MajorBBS/Worldgroup
        ///     host process is to be called.
        /// </summary>
        private ushort InvokeHostedFunction(ushort importedNameTableOrdinal, ushort functionOrdinal)
        {
            var importedModuleName = _module.File.ImportedNameTable.First(x => x.Ordinal == importedNameTableOrdinal).Name;

            if(!_exportedFunctionDelegates.TryGetValue(importedModuleName, out var exportedModule))
                throw new Exception($"Exported Module Not Found: {importedModuleName}");

            if(!exportedModule.TryGetValue(functionOrdinal, out var function))
                throw new Exception($"Exported Module Ordinal Not Found in {importedModuleName}: {functionOrdinal}");

            //Execute it
            return function();
        }

        private ushort GetHostMemoryValue(ushort segment, ushort offset)
        {
            //MAJORBBS Segment
            if ((segment | 0xF000) == segment)
                return _majorbbsHostFunctions.Memory.GetByte(segment, offset);

            //GALGSBL Segment
            if ((segment | 0xE000) == segment)
                return _galsblHostFunctions.Memory.GetByte(segment, offset);


            throw new Exception($"Unknown Exported Segment: {segment:X4}");
        }
    }
}

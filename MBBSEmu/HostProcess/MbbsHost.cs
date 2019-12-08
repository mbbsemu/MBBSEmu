using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using NLog;
using System;
using System.Linq;

namespace MBBSEmu.HostProcess
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

        private readonly MbbsModule _module;
        private readonly IMemoryCore _memory;

        public MbbsHost(MbbsModule module)
        {
            _module = module;
            _logger.Info("Constructing MbbsEmu Host...");
            _logger.Info("Initalizing x86_16 CPU Emulator...");

            //Setup new memory core and init with host memory segment
            _memory = new MemoryCore();
            _memory.AddSegment((ushort)EnumHostSegments.HostMemorySegment);


            foreach (var seg in _module.File.SegmentTable)
            {
                _memory.AddSegment(seg);
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
        public void Init()
        {
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

            _module.EntryPoints["_INIT_"] = new EntryPoint(initEntryPoint.SegmentNumber, initEntryPoint.Offset);

            _logger.Info(
                $"Starting MbbsEmu Host at {initResidentName.Name} (Seg {initEntryPoint.SegmentNumber}:{initEntryPoint.Offset:X4}h)...");

            var _cpuRegisters = new CpuRegisters()
                {CS = _module.EntryPoints["_INIT_"].Segment, IP = _module.EntryPoints["_INIT_"].Offset};

            using var majorbbsHostFunctions = new Majorbbs(_memory, _cpuRegisters, _module);
            using var galsblHostFunctions = new Galsbl(_memory, _cpuRegisters, _module);

            var _cpu = new CpuCore(_memory, _cpuRegisters, (delegate(ushort ordinal, ushort functionOrdinal)
            {
                var importedModuleName =
                    _module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

                switch (importedModuleName)
                {
                    case "MAJORBBS":
                        return majorbbsHostFunctions.ExportedFunctions[functionOrdinal]();
                    case "GALGSBL":
                        return galsblHostFunctions.ExportedFunctions[functionOrdinal]();
                    default:
                        throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}");
                }
            }));


        while (_cpu.IsRunning)
                _cpu.Tick();
        }

        /*
        public bool VerifyImportedFunctions()
        {
            _logger.Info("Scanning CODE segments to ensure all Imported Functions are supported by MbbsEmu...");
            var scanResult = true;
            var ordinalMajorBbs = _module.File.ImportedNameTable.First(x => x.Name == "MAJORBBS");
            foreach (var seg in _module.File.SegmentTable.Where(x => x.Flags.Contains(EnumSegmentFlags.Code)))
            {
                foreach (var relo in seg.RelocationRecords.Where(x =>
                    x.Flag == EnumRecordsFlag.IMPORTORDINAL &&
                    x.TargetTypeValueTuple.Item2 == ordinalMajorBbs.Ordinal))
                {

                    if (!_exportedFunctionDelegates["MAJORBBS"].ContainsKey(relo.TargetTypeValueTuple.Item3))
                    {
                        _logger.Error(
                            $"Module Relies on MAJORBBS Function {relo.TargetTypeValueTuple.Item3}, which is not implemented");
                        scanResult = false;
                    }
                }
            }

            return scanResult;
        }
        */
    }
}

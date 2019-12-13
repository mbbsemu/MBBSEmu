using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
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
        public delegate void SentToUserDelegate(ReadOnlySpan<byte> data);

        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly List<UserSession> _sessions;
        private readonly Dictionary<string, MbbsModule> _modules;
        private readonly Dictionary<string, object> _exportedFunctions;

        public MbbsHost()
        {
            _logger.Info("Constructing MbbsEmu Host...");
            _logger.Info("Initalizing x86_16 CPU Emulator...");

            _sessions = new List<UserSession>();
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, object>();

            _logger.Info("Constructed MbbsEmu Host!");
        }

        /// <summary>
        ///     Adds a Module to the MbbsHost Process and sets it up for use
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(MbbsModule module)
        {
            //Verify that the imported functions are all supported by MbbsEmu

            //if (!VerifyImportedFunctions())
            //    throw new Exception("Module is currently unsupported by MbbEmu! :(");

            //Setup new memory core and init with host memory segment
            module.Memory.AddSegment(EnumHostSegments.HostMemorySegment);
            module.Memory.AddSegment(EnumHostSegments.Status);
            module.Memory.AddSegment(EnumHostSegments.User);
            module.Memory.AddSegment(EnumHostSegments.UserNum);

            //Add CODE/DATA Segments from the actual DLL
            foreach (var seg in module.File.SegmentTable)
            {
                module.Memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Add it to the Module Dictionary
            _modules[module.ModuleIdentifier] = module;
        }

        public void AddSession(UserSession session)
        {
            _sessions.Add(session);
        }

        /// <summary>
        ///     Starts a new instance of the MbbsHost running the specified MbbsModule
        /// </summary>
        public void Init(string moduleName)
        {
            var module = _modules[moduleName];

            var initResidentName = module.File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));

            if (initResidentName == null)
                throw new Exception("Unable to locate _INIT_ entry in Resident Name Table");

            var initEntryPoint = module.File.EntryTable
                .First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);

            module.EntryPoints["_INIT_"] = new EntryPoint(initEntryPoint.SegmentNumber, initEntryPoint.Offset);

            _logger.Info(
                $"Starting MbbsEmu Host at {initResidentName.Name} (Seg {initEntryPoint.SegmentNumber}:{initEntryPoint.Offset:X4}h)...");

            var _cpuRegisters = new CpuRegisters()
                {CS = module.EntryPoints["_INIT_"].Segment, IP = module.EntryPoints["_INIT_"].Offset};

            //Setup Host Functions for this Module
            var majorbbsHostFunctions = new Majorbbs(_cpuRegisters, module, null);
            _exportedFunctions[$"{module.ModuleIdentifier}-MAJORBBS"] = majorbbsHostFunctions;
            var galsblHostFunctions = new Galsbl(_cpuRegisters, module);
            _exportedFunctions[$"{module.ModuleIdentifier}-GALGSBL"] = galsblHostFunctions;

            var _cpu = new CpuCore(module.Memory, _cpuRegisters, (delegate(ushort ordinal, ushort functionOrdinal)
            {
                try
                {
                    var importedModuleName =
                        module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

                    switch (importedModuleName)
                    {
                        case "MAJORBBS":
                            return majorbbsHostFunctions.ExportedFunctions[functionOrdinal]();
                        case "GALGSBL":
                            return galsblHostFunctions.ExportedFunctions[functionOrdinal]();
                        default:
                            throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Fatal(e);
                    throw;
                }
            }));


            while (_cpu.IsRunning)
                _cpu.Tick();
        }

        /// <summary>
        ///     Runs the specified routine in the specified module
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="routineName"></param>
        /// <param name="sendToUserDelegate"></param>
        public void Run(string moduleName, string routineName, UserSession userSession = null)
        {
            var module = _modules[moduleName];

            if(!module.EntryPoints.ContainsKey(routineName))
                throw new Exception($"Attempted to execute unknown Routine name: {routineName}");

            //Setup Memory for User Objects in the Module Memory if Required
            if (userSession != null)
            {
                module.Memory.SetArray((ushort)EnumHostSegments.User, 0, userSession.UsrPrt.ToSpan());
                module.Memory.SetArray((ushort)EnumHostSegments.UserNum, 0, BitConverter.GetBytes(userSession.Channel));
            }

            _logger.Info($"Running {routineName}...");

            var _cpuRegisters = new CpuRegisters()
                { CS = module.EntryPoints[routineName].Segment, IP = module.EntryPoints[routineName].Offset, DS = ushort.MaxValue, SI = ushort.MaxValue };

            var majorbbsHostFunctions = (Majorbbs)_exportedFunctions[$"{module.ModuleIdentifier}-MAJORBBS"];
            var galsblHostFunctions = (Galsbl)_exportedFunctions[$"{module.ModuleIdentifier}-GALGSBL"];

            var _cpu = new CpuCore(module.Memory, _cpuRegisters, delegate (ushort ordinal, ushort functionOrdinal)
            {
                var importedModuleName =
                    module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

                switch (importedModuleName)
                {
                    case "MAJORBBS":
                        return majorbbsHostFunctions.ExportedFunctions[functionOrdinal]();
                    case "GALGSBL":
                        return galsblHostFunctions.ExportedFunctions[functionOrdinal]();
                    default:
                        throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}");
                }
            });
            while (_cpu.IsRunning)
                _cpu.Tick();
        }

        private void SendToChannel(ushort channel, ReadOnlySpan<byte> dataToSend)
        {
            var userToSend = _sessions.First(x => x.Channel == channel);
            userToSend.SendToClient(dataToSend);
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

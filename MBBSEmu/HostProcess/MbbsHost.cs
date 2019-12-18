using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess
{
    /// <summary>
    ///     Class acts as the MBBS/WG Host Process which contains all the
    ///     imported functions.
    ///
    ///     We'll perform the imported functions here
    /// </summary>
    public class MbbsHost : IMbbsHost
    {
        private readonly ILogger _logger;

        private readonly PointerDictionary<UserSession> _channelDictionary;
        private readonly Dictionary<string, MbbsModule> _modules;
        private readonly Dictionary<string, object> _exportedFunctions;
        private readonly CpuCore _cpu;
        private bool _isRunning;

        public MbbsHost(ILogger logger)
        {
            _logger = logger;
            _logger.Info("Constructing MbbsEmu Host...");
            _channelDictionary = new PointerDictionary<UserSession>();
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, object>();

            _cpu = new CpuCore();
            
            _logger.Info("Constructed MbbsEmu Host!");
        }

        /// <summary>
        ///     Starts the MbbsHost Worker Thread
        /// </summary>
        public void Start()
        {
            _isRunning = true;
            var workerThread = new Thread(WorkerThread);
            workerThread.Start();
        }

        /// <summary>
        ///     Stops the MbbsHost worker thread
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>
        ///     The main MajorBBS/WG worker loop that processes events in serial order
        /// </summary>
        private void WorkerThread()
        {
            while (_isRunning)
            {
                //Process Input
                //TODO -- Converting this to a FOR probably wont juice things too much,
                //TODO -- but probably want to look into it just to be sure
                foreach (var s in _channelDictionary.Values)
                {
                    //Channel is entering module, run both
                    if (s.SessionState == EnumSessionState.EnteringModule)
                    {
                        s.StatusChange = false;
                        Run(s.ModuleIdentifier, "sttrou", s.Channel);
                        Run(s.ModuleIdentifier, "stsrou", s.Channel);
                        s.SessionState = EnumSessionState.InModule;
                        continue;
                    }

                    //Following Events are only processed if they're IN a module
                    if (s.SessionState != EnumSessionState.InModule)
                        continue;

                    //Did the text change cause a status update
                    if (s.StatusChange)
                    {
                        s.StatusChange = false;
                        Run(s.ModuleIdentifier, "stsrou", s.Channel);
                    }

                    //Is there Text to send to the module
                    if (s.DataFromClient.Count > 0)
                    {
                        Run(s.ModuleIdentifier, "sttrou", s.Channel);
                    }
                }

                //Cleanup Logged Off
                for (ushort i = 0; i < _channelDictionary.Count; i++)
                {
                    if (_channelDictionary[i].SessionState == EnumSessionState.LoggedOff)
                        _channelDictionary.Remove(i);
                }

                Thread.Sleep(50);
            }
        }

        /// <summary>
        ///     Adds a Module to the MbbsHost Process and sets it up for use
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(MbbsModule module)
        {
            _logger.Info($"Adding Module {module.ModuleIdentifier}...");
            //Verify that the imported functions are all supported by MbbsEmu

            //if (!VerifyImportedFunctions())
            //    throw new Exception("Module is currently unsupported by MbbEmu! :(");

            //Setup new memory core and init with host memory segment
            module.Memory.AddSegment(EnumHostSegments.HostMemorySegment);
            module.Memory.AddSegment(EnumHostSegments.Status);
            module.Memory.AddSegment(EnumHostSegments.UserPtr);
            module.Memory.AddSegment(EnumHostSegments.UserNum);

            //Add CODE/DATA Segments from the actual DLL
            foreach (var seg in module.File.SegmentTable)
            {
                module.Memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Add it to the Module Dictionary
            _modules[module.ModuleIdentifier] = module;

            //Run INIT
            Run(module.ModuleIdentifier, "_INIT_", ushort.MaxValue);

            _logger.Info($"Module {module.ModuleIdentifier} added!");
        }

        /// <summary>
        ///     Assigns a Channel # to a session and adds it to the Channel Dictionary
        /// </summary>
        /// <param name="session"></param>
        public void AddSession(UserSession session)
        {
            session.Channel = (ushort)_channelDictionary.Allocate(session);
            _logger.Info($"Added Session {session.SessionId} to Channel {session.Channel}");
        }

        /// <summary>
        ///     Removes a Channel # from the Channel Dictionary
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public bool RemoveSession(ushort channel) => _channelDictionary.Remove(channel);

        /// <summary>
        ///     Runs the specified routine in the specified module
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="routineName"></param>
        /// <param name="channelNumber"></param>
        private void Run(string moduleName, string routineName, ushort channelNumber)
        {
            var module = _modules[moduleName];

            if (!module.EntryPoints.ContainsKey(routineName))
                throw new Exception($"Attempted to execute unknown Routine name: {routineName}");

            //Setup Memory for User Objects in the Module Memory if Required
            if (channelNumber != ushort.MaxValue)
            {
                module.Memory.SetArray((ushort) EnumHostSegments.UserPtr, 0, _channelDictionary[channelNumber].UsrPrt.ToSpan());
                module.Memory.SetArray((ushort) EnumHostSegments.UserNum, 0,
                    BitConverter.GetBytes(channelNumber));
                module.Memory.SetWord((ushort)EnumHostSegments.Status, 0, _channelDictionary[channelNumber].Status);

               //_logger.Info($"Channel {channelNumber}: Running {routineName}");

                _channelDictionary[channelNumber].StatusChange = false;
            }

            var cpuRegisters = new CpuRegisters
            {
                CS = module.EntryPoints[routineName].Segment, 
                IP = module.EntryPoints[routineName].Offset
            };

            var majorbbsHostFunctions = GetFunctions<Majorbbs>(module);
            majorbbsHostFunctions.Registers = cpuRegisters;
            majorbbsHostFunctions.SetCurrentChannel(channelNumber);

            var galsblHostFunctions = GetFunctions<Galsbl>(module);
            galsblHostFunctions.Registers = cpuRegisters;
            galsblHostFunctions.SetCurrentChannel(channelNumber);

            _cpu.Reset(module.Memory, cpuRegisters, delegate(ushort ordinal, ushort functionOrdinal)
            {
                var importedModuleName =
                    module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

#if DEBUG
                //_logger.Info($"Calling {importedModuleName}:{functionOrdinal}");
#endif

                 return importedModuleName switch
                {
                    "MAJORBBS" => majorbbsHostFunctions.ExportedFunctions[functionOrdinal](),
                    "GALGSBL" => galsblHostFunctions.ExportedFunctions[functionOrdinal](),
                    _ => throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}")
                };
            });

            //Run as long as the CPU still has code to execute and the host is still running
            while (_cpu.IsRunning && _isRunning)
                _cpu.Tick();

            //Extract the User Information as it might have updated
            if (channelNumber != ushort.MaxValue)
            {
                _channelDictionary[channelNumber].UsrPrt
                    .FromSpan(module.Memory.GetSpan((ushort) EnumHostSegments.UserPtr, 0, 41));

                //If the status wasn't set internally, then set it to the default 5
                _channelDictionary[channelNumber].Status = !_channelDictionary[channelNumber].StatusChange
                    ? (ushort) 5
                    : module.Memory.GetWord((ushort) EnumHostSegments.Status, 0);
            }
            
        }
        private T GetFunctions<T>(MbbsModule module)
        {
            var requestedType = typeof(T);
            var key = $"{module.ModuleIdentifier}-{(requestedType == typeof(Majorbbs) ? "MAJORBBS" : "GALGSBL")}";

            if (!_exportedFunctions.TryGetValue(key, out var _functions))
            {
                if (requestedType == typeof(Majorbbs))
                    _exportedFunctions[key] = new Majorbbs(module, _channelDictionary);

                if(requestedType == typeof(Galsbl))
                    _exportedFunctions[key] = new Galsbl(module, _channelDictionary);

                _functions = _exportedFunctions[key];
            }

            return (T) _functions;
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

using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

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
        private readonly Dictionary<string, IExportedModule> _exportedFunctions;
        private readonly CpuCore _cpu;
        private bool _isRunning;
        private readonly Stopwatch realTimeStopwatch;
        private bool _isAddingModule;
        private bool _isAddingSession;
        private readonly IMbbsRoutines _mbbsRoutines;
        public MbbsHost(ILogger logger, IMbbsRoutines mbbsRoutines)
        {
            _logger = logger;
            _mbbsRoutines = mbbsRoutines;
            _logger.Info("Constructing MbbsEmu Host...");
            _channelDictionary = new PointerDictionary<UserSession>(minimumValue: 1);
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, IExportedModule>();

            _cpu = new CpuCore();
            realTimeStopwatch = Stopwatch.StartNew();
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
                if (!_isAddingSession && !_isAddingModule)
                {
                    //Process Channels
                    foreach (var s in _channelDictionary.Values)
                    {
                        switch (s.SessionState)
                        {
                            case EnumSessionState.Unauthenticated:
                                _mbbsRoutines.ShowLogin(s);
                                continue;
                            case EnumSessionState.DisplayingLoginUsername:
                                _mbbsRoutines.DisplayLoginUsername(s);
                                continue;
                            case EnumSessionState.AuthenticatingUsername:
                                _mbbsRoutines.AuthenticatingUsername(s);
                                continue;
                            case EnumSessionState.DisplayingLoginPassword:
                                _mbbsRoutines.DisplayLoginPassword(s);
                                continue;
                            case EnumSessionState.AuthenticatingPassword:
                                _mbbsRoutines.AuthenticatingPassword(s);
                                continue;
                            case EnumSessionState.EnteringModule:
                            {
                                s.StatusChange = false;
                                Run(s.ModuleIdentifier, "sttrou", s.Channel);
                                Run(s.ModuleIdentifier, "stsrou", s.Channel);
                                s.SessionState = EnumSessionState.InModule;
                                continue;
                            }
                            case EnumSessionState.InModule:
                            {
                                //Did the text change cause a status update
                                if (s.StatusChange || s.Status == 240)
                                {
                                    s.StatusChange = false;
                                    Run(s.ModuleIdentifier, "stsrou", s.Channel);
                                    continue;
                                }

                                //Is there Text to send to the module
                                if (s.DataFromClient.Count > 0)
                                {
                                    Run(s.ModuleIdentifier, "sttrou", s.Channel);
                                    continue;
                                }

                                break;
                            }
                            default:
                                continue;
                        }
                    }

                    //We don't want to run these while a module is still being kicked off
                    foreach (var m in _modules.Values)
                    {
                        if (m.RtkickRoutines.Count == 0) continue;

                        foreach (var r in m.RtkickRoutines)
                        {
                            if (r.Value.Elapsed.ElapsedMilliseconds > (r.Value.Delay * 1000))
                            {
                                Run(m.ModuleIdentifier, $"RTKICK-{r.Key}", ushort.MaxValue);
                            }
                        }
                    }

                    //Run rtihdlr routines
                    if (realTimeStopwatch.ElapsedMilliseconds > 55)
                    {
                        foreach (var m in _modules.Values)
                        {
                            if (m.RtihdlrRoutines.Count == 0) continue;

                            foreach (var r in m.RtihdlrRoutines)
                            {
                                Run(m.ModuleIdentifier, $"RTIHDLR-{r.Key}", ushort.MaxValue);
                            }
                        }

                        realTimeStopwatch.Restart();
                    }
                }

                //Cleanup Logged Off
                for (ushort i = 1; i < _channelDictionary.Count; i++)
                {
                    if (_channelDictionary.ContainsKey(i))
                    {
                        if (_channelDictionary[i].SessionState == EnumSessionState.LoggedOff)
                            _channelDictionary.Remove(i);
                    }
                }
            }
        }

        /// <summary>
        ///     Adds a Module to the MbbsHost Process and sets it up for use
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(MbbsModule module)
        {
            _isAddingModule = true;
            _logger.Info($"Adding Module {module.ModuleIdentifier}...");
            //Verify that the imported functions are all supported by MbbsEmu

            //if (!VerifyImportedFunctions())
            //    throw new Exception("Module is currently unsupported by MbbEmu! :(");

            //Setup new memory core and init with host memory segment
            module.Memory.AddSegment(EnumHostSegments.HostMemorySegment);
            module.Memory.AddSegment(EnumHostSegments.Status);
            module.Memory.AddSegment(EnumHostSegments.UserPtr);
            module.Memory.AddSegment(EnumHostSegments.UserNum);
            module.Memory.AddSegment(EnumHostSegments.UsrAcc);
            module.Memory.AddSegment(EnumHostSegments.StackSegment);

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
            _isAddingModule = false;
        }

        /// <summary>
        ///     Assigns a Channel # to a session and adds it to the Channel Dictionary
        /// </summary>
        /// <param name="session"></param>
        public void AddSession(UserSession session)
        {
            _isAddingSession = true;
            session.Channel = (ushort)_channelDictionary.Allocate(session);
            _logger.Info($"Added Session {session.SessionId} to Channel {session.Channel}");
            _isAddingSession = false;
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

            var majorbbsHostFunctions = GetFunctions(module, "MAJORBBS");
            majorbbsHostFunctions.SetState(cpuRegisters, channelNumber);

            var galsblHostFunctions = GetFunctions(module, "GALGSBL");
            galsblHostFunctions.SetState(cpuRegisters, channelNumber);

            _cpu.Reset(module.Memory, cpuRegisters, delegate(ushort ordinal, ushort functionOrdinal)
            {
#if DEBUG
                //_logger.Info($"Calling {importedModuleName}:{functionOrdinal}");
#endif
                return module.File.ImportedNameTable[ordinal].Name switch
                {
                    "MAJORBBS" => majorbbsHostFunctions.Invoke(functionOrdinal),
                    "GALGSBL" => galsblHostFunctions.Invoke(functionOrdinal),
                    _ => throw new Exception($"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[ordinal].Name}")
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
        private IExportedModule GetFunctions(MbbsModule module, string exportedModule)
        {
            var key = $"{module.ModuleIdentifier}-{exportedModule}";

            if (!_exportedFunctions.TryGetValue(key, out var functions))
            {
                _exportedFunctions[key] = exportedModule switch
                {
                    "MAJORBBS" => new Majorbbs(module, _channelDictionary),
                    "GALGSBL" => new Galsbl(module, _channelDictionary),
                    _ => _exportedFunctions[key]
                };

                functions = _exportedFunctions[key];
            }

            return functions;
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

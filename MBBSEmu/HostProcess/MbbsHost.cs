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
        private readonly Stopwatch _realTimeStopwatch;
        private bool _isAddingModule;
        private readonly IMbbsRoutines _mbbsRoutines;

        private readonly Queue<UserSession> _incomingSessions;

        public MbbsHost(ILogger logger, IMbbsRoutines mbbsRoutines)
        {
            _logger = logger;
            _mbbsRoutines = mbbsRoutines;
            _logger.Info("Constructing MbbsEmu Host...");
            _channelDictionary = new PointerDictionary<UserSession>();
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, IExportedModule>();
            _cpu = new CpuCore();
            _realTimeStopwatch = Stopwatch.StartNew();
            _incomingSessions = new Queue<UserSession>();
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
                //Add any incoming new sessions
                while (_incomingSessions.TryDequeue(out var incomingSession))
                {
                    incomingSession.Channel = (ushort)_channelDictionary.Allocate(incomingSession);
                    _logger.Info($"Added Session {incomingSession.SessionId} to channel {incomingSession.Channel}");
                }

                //Any Disconnects
                for (ushort i = 0; i < _channelDictionary.Count; i++)
                {
                    if (_channelDictionary.ContainsKey(i))
                    {
                        if (_channelDictionary[i].SessionState == EnumSessionState.LoggedOff)
                        {
                            _logger.Info($"Removing LoggedOff Channel: {i}");
                            _channelDictionary.Remove(i);
                        }
                    }
                }

                if (!_isAddingModule)
                {
                    //Process Channels
                    foreach (var s in _channelDictionary.Values)
                    {
                        switch (s.SessionState)
                        {


                            case EnumSessionState.EnteringModule:
                            {
                                s.StatusChange = false;
                                s.SessionState = EnumSessionState.InModule;
                                Run(s.CurrentModule.ModuleIdentifier, s.CurrentModule.EntryPoints["sttrou"], s.Channel);
                                continue;
                            }

                            case EnumSessionState.InModule:
                            {
                                //Process Character Interceptor in GSBL
                                if (s.DataToProcess)
                                {
                                    s.DataToProcess = false;
                                    if (s.CharacterInterceptor != null)
                                    {
                                        //Create Parameters for BTUCHI Routine
                                        var initialStackValues = new Queue<ushort>(2);
                                        initialStackValues.Enqueue(s.LastCharacterReceived);
                                        initialStackValues.Enqueue(s.Channel);

                                        var result = Run(s.CurrentModule.ModuleIdentifier, s.CharacterInterceptor,
                                            s.Channel,
                                            initialStackValues);

                                        //Result replaces the character in the buffer
                                        s.InputBuffer.SetLength(s.InputBuffer.Length - 1);
                                        s.InputBuffer.WriteByte((byte) result);
                                        s.LastCharacterReceived = (byte) result;

                                        //If the new character is a carriage return, 
                                        if (result == 0xD)
                                            s.Status = 3;
                                    }

                                    s.DataToClient.WriteByte(s.LastCharacterReceived);
                                }

                                //Did the text change cause a status update
                                if (s.StatusChange || s.Status == 240)
                                {
                                    s.StatusChange = false;
                                    Run(s.CurrentModule.ModuleIdentifier, s.CurrentModule.EntryPoints["stsrou"],
                                        s.Channel);
                                    continue;
                                }

                                //Is there Text to send to the module
                                if (s.Status == 3)
                                {
                                    var result = Run(s.CurrentModule.ModuleIdentifier,
                                        s.CurrentModule.EntryPoints["sttrou"], s.Channel);

                                    //stt returned an exit code
                                    if (result == 0)
                                    {
                                        s.SessionState = EnumSessionState.MainMenuDisplay;
                                        s.CurrentModule = null;

                                        //Clear the Input Buffer
                                        s.InputBuffer.SetLength(0);

                                        //Clear any data waiting to be processed from the client
                                        s.InputBuffer.SetLength(0);
                                    }

                                    continue;
                                }

                                break;
                            }

                            //Check for any other session states, we handle these here as they are
                            //lower priority than handling "in-module" states
                            default:
                                _mbbsRoutines.ProcessSessionState(s, _modules);
                                break;
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
                                Run(m.ModuleIdentifier, m.EntryPoints[$"RTKICK-{r.Key}"], ushort.MaxValue);
                                r.Value.Elapsed.Restart();
                            }
                        }
                    }

                    //Run rtihdlr routines
                    if (_realTimeStopwatch.ElapsedMilliseconds > 55)
                    {
                        foreach (var m in _modules.Values)
                        {
                            if (m.RtihdlrRoutines.Count == 0) continue;

                            foreach (var r in m.RtihdlrRoutines)
                            {
                                Run(m.ModuleIdentifier, m.EntryPoints[$"RTIHDLR-{r.Key}"], ushort.MaxValue);
                            }
                        }

                        _realTimeStopwatch.Restart();
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

            //Add CODE/DATA Segments from the actual DLL
            foreach (var seg in module.File.SegmentTable)
            {
                module.Memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Add it to the Module Dictionary
            _modules[module.ModuleIdentifier] = module;

            //Run INIT
            Run(module.ModuleIdentifier, module.EntryPoints["_INIT_"], ushort.MaxValue);

            _logger.Info($"Module {module.ModuleIdentifier} added!");
            _isAddingModule = false;
        }

        /// <summary>
        ///     Assigns a Channel # to a session and adds it to the Channel Dictionary
        /// </summary>
        /// <param name="session"></param>
        public void AddSession(UserSession session)
        {
            _incomingSessions.Enqueue(session);
            _logger.Info($"Session {session.SessionId} added to incoming queue");
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
        /// <param name="routine"></param>
        /// <param name="channelNumber"></param>
        /// <param name="initialStackValues"></param>
        private ushort Run(string moduleName, IntPtr16 routine, ushort channelNumber, Queue<ushort> initialStackValues = null)
        {
            var module = _modules[moduleName];

            //Setup Memory for User Objects in the Module Memory if Required
            if (channelNumber != ushort.MaxValue)
                _channelDictionary[channelNumber].StatusChange = false;

            var cpuRegisters = new CpuRegisters
            {
                CS = routine.Segment, 
                IP = routine.Offset
            };

            //Set Host Process Imported Methods
            var majorbbsHostFunctions = GetFunctions(module, "MAJORBBS");
            majorbbsHostFunctions.SetState(cpuRegisters, channelNumber);

            var galsblHostFunctions = GetFunctions(module, "GALGSBL");
            galsblHostFunctions.SetState(cpuRegisters, channelNumber);

            //Set CPU to Startup State
            _cpu.Reset(module.Memory, cpuRegisters, delegate(ushort ordinal, ushort functionOrdinal)
            {
                return module.File.ImportedNameTable[ordinal].Name switch
                {
                    "MAJORBBS" => majorbbsHostFunctions.Invoke(functionOrdinal),
                    "GALGSBL" => galsblHostFunctions.Invoke(functionOrdinal),
                    _ => throw new Exception($"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[ordinal].Name}")
                };
            });

            //Check for Parameters for BTUCHI
            if (initialStackValues != null)
            {
                //Push Parameters
                while(initialStackValues.TryDequeue(out var valueToPush))
                    _cpu.Push(valueToPush);

                //Set stack to simulate CALL FAR
                cpuRegisters.BP = cpuRegisters.SP;
                _cpu.Push(ushort.MaxValue); //CS
                _cpu.Push(ushort.MaxValue); //IP
                //_cpu.Push(ushort.MaxValue); //BP
            }

            //Run as long as the CPU still has code to execute and the host is still running
            while (_cpu.IsRunning && _isRunning)
                _cpu.Tick();

            //Update the session with any information from memory
            if (channelNumber != ushort.MaxValue && initialStackValues == null)
                majorbbsHostFunctions.UpdateSession(channelNumber);

            return cpuRegisters.AX;
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

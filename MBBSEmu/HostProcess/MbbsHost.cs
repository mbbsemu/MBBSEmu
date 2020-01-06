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
                                Run(s.ModuleIdentifier, "sttrou", s.Channel);
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
                                if (s.Status == 3)
                                {
                                    Run(s.ModuleIdentifier, "sttrou", s.Channel);
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
                                Run(m.ModuleIdentifier, $"RTKICK-{r.Key}", ushort.MaxValue);
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
                                Run(m.ModuleIdentifier, $"RTIHDLR-{r.Key}", ushort.MaxValue);
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

            //Setup new memory core and init with host memory segment
            module.Memory.AddSegment(EnumHostSegments.Status);
            module.Memory.AddSegment(EnumHostSegments.User);
            module.Memory.AddSegment(EnumHostSegments.UserPtr);
            module.Memory.AddSegment(EnumHostSegments.UserNum);
            module.Memory.AddSegment(EnumHostSegments.UsrAcc);
            module.Memory.AddSegment(EnumHostSegments.StackSegment);
            module.Memory.AddSegment(EnumHostSegments.ChannelArray);
            module.Memory.AddSegment((ushort)EnumHostSegments.Prfbuf);
            module.Memory.AddSegment(EnumHostSegments.Nterms);
            module.Memory.SetByte((ushort)EnumHostSegments.Nterms, 0, 0x7F);

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
                //Update the User Record and also the poitner to the current user
                module.Memory.SetArray((ushort) EnumHostSegments.User, (ushort) (41 * channelNumber), _channelDictionary[channelNumber].UsrPtr.ToSpan());
                module.Memory.SetArray((ushort) EnumHostSegments.UserPtr, 0,
                    new IntPtr16((ushort) EnumHostSegments.User, (ushort) (41 * channelNumber)).ToSpan());

                //Update the Current User #
                module.Memory.SetArray((ushort) EnumHostSegments.UserNum, 0,
                    BitConverter.GetBytes(channelNumber));
                module.Memory.SetWord((ushort)EnumHostSegments.Status, 0, _channelDictionary[channelNumber].Status);
                module.Memory.SetArray((ushort)EnumHostSegments.ChannelArray, 0, GetChannelArray());

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
                _channelDictionary[channelNumber].UsrPtr
                    .FromSpan(module.Memory.GetSpan((ushort) EnumHostSegments.User, 0, 41));

                //If the status wasn't set internally, then set it to the default 5
                _channelDictionary[channelNumber].Status = !_channelDictionary[channelNumber].StatusChange
                    ? (ushort) 5
                    : module.Memory.GetWord((ushort) EnumHostSegments.Status, 0);

                if (routineName == "sttrou" && cpuRegisters.AX == 0)
                {
                    _channelDictionary[channelNumber].SessionState = EnumSessionState.MainMenuDisplay;
                    _channelDictionary[channelNumber].ModuleIdentifier = string.Empty;

                    //Clear the Input Buffer
                    _channelDictionary[channelNumber].InputBuffer.SetLength(0);

                    //Clear any data waiting to be processed from the client
                    _channelDictionary[channelNumber].DataFromClient.Clear();
                }
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

        /// <summary>
        ///     Builds the int channel[] array that links user numbers to channels.
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> GetChannelArray()
        {
            var arrayOutput = new byte[1024];

            ushort channelsWritten = 0;
            foreach (var k in _channelDictionary.Keys)
            {
                Array.Copy(BitConverter.GetBytes((ushort)k), 0, arrayOutput, k + (2 * channelsWritten++), 2);
            }

            return arrayOutput;
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

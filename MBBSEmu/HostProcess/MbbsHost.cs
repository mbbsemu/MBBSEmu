using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.HostRoutines;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Rlogin;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.GlobalRoutines;
using MBBSEmu.Reports;
using MBBSEmu.Session.Attributes;
using MBBSEmu.Session.Enums;

namespace MBBSEmu.HostProcess
{
    /// <summary>
    ///     MbbsHost acts as the Host Process would in MajorBBS or Worldgroup.
    ///
    ///     This is the focal point of all this MBBSEmu, as it handles:
    ///     - Loading Modules
    ///     - Applying Relocation Records for Exported Functions
    ///     - Adding incoming connections to a Channel
    ///     - Processing the main MajorBBS/Worldgroup event loop
    /// </summary>
    public class MbbsHost : IMbbsHost
    {
        public ILogger Logger { get; set; }

        // 3 in the morning
        private readonly TimeSpan DEFAULT_CLEANUP_TIME = new TimeSpan(hours: 3, minutes: 0, seconds: 0);

        /// <summary>
        ///     Dictionary containing all active Channels
        /// </summary>
        private readonly PointerDictionary<SessionBase> _channelDictionary;

        /// <summary>
        ///     Dictionary containing all added Modules
        /// </summary>
        private readonly Dictionary<string, MbbsModule> _modules;

        /// <summary>
        ///     Dictionary containing all Exported Functions
        /// </summary>
        private readonly Dictionary<string, IExportedModule> _exportedFunctions;

        /// <summary>
        ///     Boolean denoting if the Host is Running or Not
        /// </summary>
        private bool _isRunning;

        /// <summary>
        ///     Stopwatch tracking wall time for Real Time Events
        /// </summary>
        private readonly Stopwatch _realTimeStopwatch;

        /// <summary>
        ///     Host Routines for Non-Module Status (Menus, Signup, etc.)
        /// </summary>
        private readonly IEnumerable<IHostRoutine> _mbbsRoutines;

        /// <summary>
        ///     Routines for Global Commands to be intercepted while logged in
        /// </summary>
        private readonly IEnumerable<IGlobalRoutine> _globalRoutines;

        /// <summary>
        ///     Queue of incoming sessions not added to a Channel yet
        /// </summary>
        private readonly Queue<SessionBase> _incomingSessions;

        /// <summary>
        ///     Configuration Class giving access to the appsettings.json file
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        ///     Time of day that the cleanup routine will trigger
        /// </summary>
        private readonly TimeSpan _cleanupTime;

        /// <summary>
        ///     Timer that triggers when nightly cleanup should occur
        /// </summary>
        private readonly Timer _timer;

        /// <summary>
        ///     Flag that controls whether the main loop will perform a nightly cleanup
        /// </summary>
        private bool _performCleanup = false;
        private EventWaitHandle _cleanupRestartEvent = null;

        private readonly IGlobalCache _globalCache;
        private readonly IFileUtility _fileUtility;
        private List<ModuleConfiguration> _moduleConfigurations;

        private Thread _workerThread;

        public MbbsHost(ILogger logger, IGlobalCache globalCache, IFileUtility fileUtility, IEnumerable<IHostRoutine> mbbsRoutines, IConfiguration configuration, IEnumerable<IGlobalRoutine> globalRoutines)
        {
            Logger = logger;
            _globalCache = globalCache;
            _fileUtility = fileUtility;
            _mbbsRoutines = mbbsRoutines;
            _configuration = configuration;
            _globalRoutines = globalRoutines;

            Logger.Info("Constructing MBBSEmu Host...");

            _channelDictionary = new PointerDictionary<SessionBase>();
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, IExportedModule>();
            _realTimeStopwatch = Stopwatch.StartNew();
            _incomingSessions = new Queue<SessionBase>();
            _cleanupTime = ParseCleanupTime();
            _timer = new Timer(unused => _performCleanup = true, this, NowUntil(_cleanupTime), TimeSpan.FromDays(1));
            Logger.Info("Constructed MBBSEmu Host!");
        }

        /// <summary>
        ///     Starts the MbbsHost Worker Thread
        /// </summary>
        public void Start(List<ModuleConfiguration> moduleConfigurations)
        {
            //Load Modules
            foreach (var m in moduleConfigurations)
                AddModule(new MbbsModule(_fileUtility, Logger, m.ModuleIdentifier, m.ModulePath) {MenuOptionKey = m.MenuOptionKey});

            //Remove any modules that did not properly initialize
            foreach (var (_, value) in _modules.Where(m => m.Value.EntryPoints.Count == 1))
            {
                Logger.Error($"{value.ModuleIdentifier} not properly initialized, Removing");
                moduleConfigurations.RemoveAll(x => x.ModuleIdentifier == value.ModuleIdentifier);
                _modules.Remove(value.ModuleIdentifier);
                foreach (var e in _exportedFunctions.Keys.Where(x => x.StartsWith(value.ModuleIdentifier)))
                    _exportedFunctions.Remove(e);
            }

            //Save module configurations for cleanup
            _moduleConfigurations = moduleConfigurations;

            _isRunning = true;

            if (_workerThread == null)
            {
                _workerThread = new Thread(WorkerThread);
                _workerThread.Start();
            }
        }

        /// <summary>
        ///     Stops the MbbsHost worker thread
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _timer.Dispose();
        }

        public void ScheduleNightlyShutdown(EventWaitHandle eventWaitHandle)
        {
            _cleanupRestartEvent = eventWaitHandle;
            _performCleanup = true;
        }

        public void WaitForShutdown()
        {
            _workerThread.Join();
        }

        /// <summary>
        ///     This is the main MajorBBS/Worldgroup loop similar to how it actually functions with the software itself.
        ///
        ///     Because it was a DOS based software, it did not support threads/tasking, so all the events had to happen in
        ///     serial order as quickly as possible. For compatibility, we've replicated the process here as well.
        /// </summary>
        private void WorkerThread()
        {
            while (_isRunning)
            {
                ProcessNightlyCleanup();

                //Handle Channels
                ProcessIncomingSessions();
                ProcessDisconnects();

                //Process Channel Events
                foreach (var session in _channelDictionary.Values)
                {
                    //Process a single incoming byte from the client session
                    session.ProcessDataFromClient();

                    //Global Command Handler
                    if (session.Status == 3 && DoGlobalsAttribute.Get(session.SessionState))
                    {
                        //Transfer Input Buffer to Command Buffer, but don't clear it
                        session.InputBuffer.WriteByte(0x0);
                        session.InputCommand = session.InputBuffer.ToArray();

                        //Check for Internal System Globals
                        if (_globalRoutines.Any(g =>
                            g.ProcessCommand(session.InputCommand, session.Channel, _channelDictionary, _modules)))
                        {
                            session.Status = 1;
                            session.InputBuffer.SetLength(0);
                            continue;
                        }

                        //Check for Module Globals
                        foreach (var m in _modules.Values.Where(x => x.GlobalCommandHandlers.Any()))
                        {
                            var result = Run(m.ModuleIdentifier,
                                m.GlobalCommandHandlers.First(), session.Channel);

                            //Command Not Recognized
                            if (result == 0)
                            {
                                //Because Status on Exit Sets to the Exit code of the module, we reset it back to 3
                                session.Status = 3;

                                continue;
                            }

                            break;
                        }
                    }

                    switch (session.SessionState)
                    {
                        case EnumSessionState.RloginEnteringModule:
                            {
                                ProcessLONROU_FromRlogin(session);
                                continue;
                            }
                        //Initial call to STTROU when a User is Entering a Module
                        case EnumSessionState.EnteringModule:
                            {
                                ProcessSTTROU_EnteringModule(session);
                                continue;
                            }

                        //Post-Login Display Routine
                        case EnumSessionState.LoginRoutines:
                            {
                                ProcessLONROU(session);
                                continue;
                            }

                        //User is in the module, process all the in-module type of events
                        case EnumSessionState.InModule:
                            {
                                //Invoke routine registered with BTUCHE if it has been registered and the criteria is met
                                if (session.EchoEmptyInvoke && session.CharacterInterceptor != null)
                                {
                                    ProcessEchoEmptyInvoke(session);
                                }

                                //Invoke BTUCHI registered routine if one exists
                                else if (session.DataToProcess)
                                {
                                    session.DataToProcess = false;

                                    if (session.CharacterInterceptor != null)
                                    {
                                        if (ProcessBTUCHI(session) == 0)
                                            continue;
                                    }

                                    //If the client is in transparent mode, don't echo
                                    if (!session.TransparentMode && (session.Status == 1 || session.Status == 192))
                                        session.SendToClient(new[] { session.LastCharacterReceived });
                                }

                                //Did BTUCHI or a previous command cause a status change?
                                if (session.StatusChange && (session.Status == 240 || session.Status == 5))
                                {
                                    ProcessSTSROU(session);
                                    continue;
                                }

                                //User Input Available? Invoke *STTROU
                                if (session.Status == 3)
                                {
                                    ProcessSTTROU(session);
                                }

                                //If the channel has been registered with BEGIN_POLLING
                                if (session.PollingRoutine != null)
                                {
                                    ProcessPollingRoutine(session);

                                    //Keep the user in Polling Status if the polling routine is still there
                                    if (session.PollingRoutine != IntPtr16.Empty)
                                        session.Status = 192;
                                }

                                break;
                            }

                        //Check for any other session states, we handle these here as they are
                        //lower priority than handling "in-module" states
                        default:
                            {
                                foreach (var r in _mbbsRoutines)
                                    if (r.ProcessSessionState(session, _modules))
                                        break;

                            }
                            break;
                    }
                }

                //Process Timed/Real-Time Events
                ProcessRTKICK();
                ProcessRTIHDLR();
                ProcessTasks();
            }

            Shutdown();
        }

        private void Shutdown()
        {
            Logger.Info("SHUTTING DOWN");

            // kill all active sessions
            foreach (var session in _channelDictionary.Values) {
                session.Stop();
            }

            // let modules clean themselves up
            CallModuleRoutine("finrou", module => Logger.Info($"Calling shutdown routine on module {module.ModuleIdentifier}"));

            //clean up modules
            foreach (var m in _modules.Keys)
            {
                _modules.Remove(m);

                foreach (var e in _exportedFunctions.Keys.Where(x => x.StartsWith(m)))
                    _exportedFunctions.Remove(e);
            }
        }

        private void CallModuleRoutine(string routine, Action<MbbsModule> preRunCallback, ushort channel = ushort.MaxValue) {
            foreach (var m in _modules.Values)
            {
                if (!m.EntryPoints.TryGetValue(routine, out var routineEntryPoint)) continue;

                if (routineEntryPoint.Segment != 0 &&
                    routineEntryPoint.Offset != 0)
                {
#if DEBUG
                    Logger.Info($"Calling {routine} on module {m.ModuleIdentifier} for channel {channel}");
#endif

                    preRunCallback?.Invoke(m);

                    Run(m.ModuleIdentifier, routineEntryPoint, channel);
                }
            }
        }

        /// <summary>
        ///     Adds any incoming sessions to an available Channel
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessIncomingSessions()
        {
            while (_incomingSessions.TryDequeue(out var incomingSession))
            {
                incomingSession.Channel = (ushort)_channelDictionary.Allocate(incomingSession);
                incomingSession.SessionTimer.Start();
                Logger.Info($"Added Session {incomingSession.SessionId} to channel {incomingSession.Channel}");
            }
        }

        /// <summary>
        ///     Removes any channels that have disconnected
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessDisconnects()
        {
            // We only remove channels that are logged off
            RemoveSessions(session =>
                session.SessionState == EnumSessionState.LoggedOff);
        }

        private void RemoveSessions(Predicate<SessionBase> match)
        {
            // for removing channels sequentially, starting with the highest index to not break
            // _channelDictionary
            Stack<ushort> channelsToRemove = new Stack<ushort>();

            for (ushort i = 0; i < _channelDictionary.Count; i++)
            {
                //Because users might not be on sequential channels (0,1,3), we verify if the channel number
                //is even in use first.
                if (!_channelDictionary.ContainsKey(i) || !match(_channelDictionary[i])) continue;

                channelsToRemove.Push(i);
            }

            while (channelsToRemove.Count > 0)
            {
                RemoveSession(channelsToRemove.Pop());
            }
        }

        /// <summary>
        ///     When first entering a module, we need to set a couple first time variables before
        ///     invoking STTROU
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSTTROU_EnteringModule(SessionBase session)
        {
            session.StatusChange = false;
            session.Status = 3;
            session.SessionState = EnumSessionState.InModule;
            session.UsrPtr.State = session.CurrentModule.StateCode;
            ProcessSTTROU(session);
        }

        /// <summary>
        ///     Invokes routine registered as STTROU during MAJORBBS->REGISTER_MODULE() call
        ///
        ///     Method is invoked any time user enters input and hits ENTER/RETURN
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSTTROU(SessionBase session)
        {
            //Transfer Input Buffer to Command Buffer
            //Null terminated already by Global Command Handler
            session.InputCommand = session.InputBuffer.ToArray();
            session.InputBuffer.SetLength(0);

            var result = Run(session.CurrentModule.ModuleIdentifier,
                session.CurrentModule.EntryPoints["sttrou"], session.Channel);

            //Finally, display prompt character if one is set
            if (session.PromptCharacter > 0)
                session.SendToClient(new[] { session.PromptCharacter });

            //stt returned an exit code -- we're done
            if (result == 0)
                ExitModule(session);
        }

        /// <summary>
        ///     Invoked when a users STTROU returns 0, which means they're exiting
        ///
        ///     This method handles session, input buffer, and status cleanup returning the
        ///     user to the main menu with a clean session
        /// </summary>
        /// <param name="session"></param>
        private void ExitModule(SessionBase session)
        {
            session.SessionState = EnumSessionState.MainMenuDisplay;
            session.CurrentModule = null;
            session.CharacterInterceptor = null;
            session.PromptCharacter = 0;

            //Reset States
            session.Status = 0;
            session.UsrPtr.Substt = 0;

            //Clear the Input Buffer
            session.InputBuffer.SetLength(0);

            //Clear any data waiting to be processed from the client
            session.InputBuffer.SetLength(0);

            //Is this an Rlogin session and its specific to a module, log them off
            if (session.SessionType == EnumSessionType.Rlogin &&
                !string.IsNullOrEmpty(((RloginSession)session).ModuleIdentifier))
            {
                session.SessionState = EnumSessionState.ConfirmLogoffDisplay;
            }
        }

        /// <summary>
        ///     Invokes routine registered as LONROU during MAJORBBS->REGISTER_MODULE() call on only
        ///     the selected Rlogin module, specified by ModuleIdentifier.
        ///
        ///     Executes on the given channel once after a user successfully logs in.
        /// </summary>
        private void ProcessLONROU_FromRlogin(SessionBase session)
        {
            session.OutputEnabled = false; // always disabled for RLogin

            Run(session.CurrentModule.ModuleIdentifier,
                session.CurrentModule.EntryPoints["lonrou"], session.Channel);

            session.SessionState = EnumSessionState.EnteringModule;
            session.OutputEnabled = true;
        }

        /// <summary>
        ///     Invokes routine registered as LONROU during MAJORBBS->REGISTER_MODULE() call
        ///
        ///     Executes on the given channel once after a user successfully logs in.
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLONROU(SessionBase session)
        {
            bool.TryParse(_configuration["Module.DoLoginRoutine"], out var doLoginRoutine);

            session.OutputEnabled = doLoginRoutine;

            CallModuleRoutine("lonrou", preRunCallback: null, session.Channel);

            session.SessionState = EnumSessionState.MainMenuDisplay;
            session.OutputEnabled = true;
        }

        /// <summary>
        ///     When BTUCHE is enabled on a given channel, the registered BTUCHI routine is invoked
        ///     whenever the Echo Buffer to a given channel is empty (after everything that needed to
        ///     be sent, has been sent)
        ///
        ///     The registered BTUCHI routine is invoked with a keycode of -1, denoting the invocation
        ///     came from BTUCHE
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessEchoEmptyInvoke(SessionBase session)
        {
            session.EchoEmptyInvoke = false;

            //Call BTUCHI when Echo Buffer Is Empty
            var initialStackValues = new Queue<ushort>(2);
            initialStackValues.Enqueue(0xFFFF); //-1 is the keycode passed in
            initialStackValues.Enqueue(session.Channel);

            var result = Run(session.CurrentModule.ModuleIdentifier,
                session.CharacterInterceptor,
                session.Channel, true,
                initialStackValues);
        }

        /// <summary>
        ///     Invokes routine registered using GSBL->BTUCHI()
        ///
        ///     BTUCHI is a character interceptor method which intercepts every incoming character on a channel
        ///     and processes it before it's sent to the buffer. In the case of MBBSEmu, since the byte is ALREADY in
        ///     the buffer, we overwrite the last byte received with the result of the registered BTUCHI routine
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ProcessBTUCHI(SessionBase session)
        {
            //Create Parameters for BTUCHI Routine
            var initialStackValues = new Queue<ushort>(2);
            initialStackValues.Enqueue(session.LastCharacterReceived);
            initialStackValues.Enqueue(session.Channel);

            var result = Run(session.CurrentModule.ModuleIdentifier,
                session.CharacterInterceptor,
                session.Channel, true,
                initialStackValues);

            //Only Take the low bytes, as it's the return character and not every routine/compiler
            //would clear out AH before assigning AL
            result &= 0xFF;

            //Result replaces the character in the buffer
            if (session.InputBuffer.Length > 0 && result != 0xD)
                session.InputBuffer.SetLength(session.InputBuffer.Length - 1);

            //Ignore
            if (result == 0)
                return result;

            //If the new character is a carriage return, null terminate it and set status
            if (result == 0xD)
            {
                session.Status = 3;
                session.InputBuffer.WriteByte(0x0);
            }
            else
            {
                session.InputBuffer.WriteByte((byte)result);
                session.LastCharacterReceived = (byte)result;
            }

            return result;
        }

        /// <summary>
        ///     Invokes routine registered as STSROU during MAJORBBS->REGISTER_MODULE() call
        ///
        ///     The registered STSROU is invoked any time there is a status change or deferred processing
        ///     using a channel Status code of 240 or 5. Most modules that utilize this use it as a method to
        ///     rapidly call the module to perform actions at a calculated interval
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSTSROU(SessionBase session)
        {
            session.StatusChange = false;
            Run(session.CurrentModule.ModuleIdentifier, session.CurrentModule.EntryPoints["stsrou"],
                session.Channel);
        }

        /// <summary>
        ///     Invokes routine registered during MAJORBBS->BEGIN_POLLING()
        ///
        ///     Method registered using BEGIN_POLLING() is called as often as possible to poll for a specific event
        ///     and will not stop polling until STOP_POLLING() is invoked
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessPollingRoutine(SessionBase session)
        {
            Run(session.CurrentModule.ModuleIdentifier, session.PollingRoutine, session.Channel,
                true);
        }
        /// <summary>
        ///     Invokes routine registered during MAJORBBS->RTKICK()
        ///
        ///     Methods registered using RTKICK are automatically invoked after a specified delay (in seconds). Methods
        ///     only execute once and are then discarded.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRTKICK()
        {
            //Check for any rtkick routines
            foreach (var module in _modules.Values)
            {
                if (module.RtkickRoutines.Count == 0) continue;

                foreach (var (key, value) in module.RtkickRoutines.ToList())
                {
                    if (!value.Executed && value.Elapsed.ElapsedMilliseconds > (value.Delay * 1000))
                    {
#if DEBUG
                        Logger.Info($"Running RTKICK-{key}: {module.EntryPoints[$"RTKICK-{key}"]}");
#endif
                        Run(module.ModuleIdentifier, module.EntryPoints[$"RTKICK-{key}"], ushort.MaxValue);
                        value.Elapsed.Stop();
                        value.Executed = true;
                        module.EntryPoints.Remove($"RTKICK-{key}");
                        module.RtkickRoutines.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        ///     Invokes routine registered during MAJORBBS->RTIHDLR()
        ///
        ///     Methods registered with RTIHDLR execute at 18hz (every ~55ms) and on the original DOS MajorBBS/Worldgroup
        ///     were executed at the DOS interrupt level. This is why on later Windows versions of worldgroup, any Module
        ///     that relied on RTIHDLR wouldn't work.
        ///
        ///     Unlike RTKICK, methods registered with RTIHDLR() only need to be registered once and will run until the BBS
        ///     is shut down.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRTIHDLR()
        {
            //Too soon? Bail.
            if (_realTimeStopwatch.ElapsedMilliseconds <= 55) return;

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

        /// <summary>
        ///     Invokes routine registered during MAJORBBS->INITASK()
        ///
        ///     Similar to BEGIN_POLLING() or RTIHDLR(), methods registered with INITASK() are invoked very quickly as often
        ///     as possible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessTasks()
        {
            //Run task routines
            foreach (var m in _modules.Values)
            {
                if (m.TaskRoutines.Count == 0) continue;

                foreach (var r in m.TaskRoutines)
                {
                    //Create Parameters for BTUCHI Routine
                    var initialStackValues = new Queue<ushort>(1);
                    initialStackValues.Enqueue((ushort)r.Key);
                    Run(m.ModuleIdentifier, m.EntryPoints[$"TASK-{r.Key}"], ushort.MaxValue, false, initialStackValues);
                }
            }
        }

        /// <summary>
        ///     Adds the specified module to the MBBS Host
        ///
        ///     This includes:
        ///     - Patching Relocation Information for Exported Modules
        ///     - Setting up Module Memory and loading Disassembly
        ///     - Executing Module "_INIT_" routine
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(MbbsModule module)
        {
            Logger.Info($"Adding Module {module.ModuleIdentifier}...");

            //Patch Relocation Information to Bytecode
            PatchRelocation(module);

            //Run Segments through AOT Decompiler & add them to Memory
            foreach (var seg in module.File.SegmentTable)
            {
                module.Memory.AddSegment(seg);
                Logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Setup Exported Modules
            module.ExportedModuleDictionary.Add(Majorbbs.Segment, GetFunctions(module, "MAJORBBS"));
            module.ExportedModuleDictionary.Add(Galgsbl.Segment, GetFunctions(module, "GALGSBL"));
            module.ExportedModuleDictionary.Add(Phapi.Segment, GetFunctions(module, "PHAPI"));
            module.ExportedModuleDictionary.Add(Galme.Segment, GetFunctions(module, "GALME"));
            module.ExportedModuleDictionary.Add(Doscalls.Segment, GetFunctions(module, "DOSCALLS"));

            //Add it to the Module Dictionary
            module.StateCode = (short)(_modules.Count + 1);
            _modules[module.ModuleIdentifier] = module;

            //Run INIT
            Run(module.ModuleIdentifier, module.EntryPoints["_INIT_"], ushort.MaxValue);

            Logger.Info($"Module {module.ModuleIdentifier} added!");
        }

        /// <summary>
        ///     Gets the Registered instance of the specified Module within the MBBS Host Process
        /// </summary>
        /// <param name="uniqueIdentifier"></param>
        /// <returns></returns>
        public MbbsModule GetModule(string uniqueIdentifier) => _modules[uniqueIdentifier];

        /// <summary>
        ///     Assigns a Channel # to a session and adds it to the Channel Dictionary
        /// </summary>
        /// <param name="session"></param>
        public void AddSession(SessionBase session)
        {
            Logger.Info($"Session {session.SessionId} added to incoming queue");
            _incomingSessions.Enqueue(session);
        }

        /// <summary>
        ///     Removes a Channel # from the Channel Dictionary
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public bool RemoveSession(ushort channel) {
            if (!_channelDictionary.ContainsKey(channel))
            {
                return false;
            }

            Logger.Info($"Removing Channel: {channel}");

            CallModuleRoutine("huprou", preRunCallback: null, channel);

            var session = _channelDictionary[channel];
            session.Stop();
            session.SessionState = EnumSessionState.Disconnected;

            _channelDictionary.Remove(channel);

            return true;
        }

        /// <summary>
        ///     Runs the specified routine in the specified module
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="routine"></param>
        /// <param name="channelNumber"></param>
        /// <param name="simulateCallFar"></param>
        /// <param name="initialStackValues"></param>
        private ushort Run(string moduleName, IntPtr16 routine, ushort channelNumber, bool simulateCallFar = false, Queue<ushort> initialStackValues = null)
        {
            var resultRegisters = _modules[moduleName].Execute(routine, channelNumber, simulateCallFar, false, initialStackValues);
            return resultRegisters.AX;
        }

        /// <summary>
        ///     Returns the specified Exported Module for the specified MajorBBS Module
        ///
        ///     Each Module gets its own copy of the Exported Modules, this module keeps track
        ///     of them using a Dictionary and will create them as needed.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="exportedModule"></param>
        /// <returns></returns>
        private IExportedModule GetFunctions(MbbsModule module, string exportedModule)
        {
            var key = $"{module.ModuleIdentifier}-{exportedModule}";

            if (!_exportedFunctions.TryGetValue(key, out var functions))
            {
                _exportedFunctions[key] = exportedModule switch
                {
                    "MAJORBBS" => new Majorbbs(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    "GALGSBL" => new Galgsbl(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    "DOSCALLS" => new Doscalls(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    "GALME" => new Galme(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    "PHAPI" => new Phapi(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    "GALMSG" => new Galmsg(Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary),
                    _ => throw new Exception($"Unknown Exported Library: {exportedModule}")
                };

                functions = _exportedFunctions[key];
            }

            return functions;
        }

        public IList<SessionBase> GetUserSessions() => _channelDictionary.Values.ToList();

        /// <summary>
        ///     Patches Relocation information from each Code Segment Relocation Records into the Segment Byte Code
        ///
        ///     Because the compiler doesn't know the location in memory of the hosts Exported Modules (Imported when
        ///     viewed from the standpoint of the DLL), it saves the information to the Relocation Records for the
        ///     given Code Segment.
        ///
        ///     The x86 Emulator knows that any CALL FAR to a Segment >= 0xFF00 is an emulated Exported Module and properly
        ///     handles calling the correct Module using the Segment of the target, and the Ordinal of the call using the Offset.
        ///     A relocation record for a call to MAJORBBS->ATOL() would be patched as:
        ///
        ///     CALL FAR 0xFFFF:0x004D
        ///
        ///     Segment & Offset meaning:
        ///     0xFFFF == MAJORBBS
        ///     0x004D == 77, Ordinal for ATOL()
        /// </summary>
        /// <param name="module"></param>
        private void PatchRelocation(MbbsModule module)
        {
            //Declare Host Functions
            var majorbbsHostFunctions = GetFunctions(module, "MAJORBBS");
            var galsblHostFunctions = GetFunctions(module, "GALGSBL");
            var doscallsHostFunctions = GetFunctions(module, "DOSCALLS");
            var galmeFunctions = GetFunctions(module, "GALME");
            var phapiFunctions = GetFunctions(module, "PHAPI");
            var galmsgFunctions = GetFunctions(module, "GALMSG");

            foreach (var s in module.File.SegmentTable)
            {
                if (s.RelocationRecords == null || s.RelocationRecords.Count == 0)
                    continue;

                foreach (var relocationRecord in s.RelocationRecords.Values)
                {
                    //Ignored Relocation Record
                    if (relocationRecord.TargetTypeValueTuple == null)
                        continue;

                    switch (relocationRecord.TargetTypeValueTuple.Item1)
                    {
                        case EnumRecordsFlag.ImportOrdinalAdditive:
                        case EnumRecordsFlag.ImportOrdinal:
                            {
                                var nametableOrdinal = relocationRecord.TargetTypeValueTuple.Item2;
                                var functionOrdinal = relocationRecord.TargetTypeValueTuple.Item3;

                                var relocationResult = module.File.ImportedNameTable[nametableOrdinal].Name switch
                                {
                                    "MAJORBBS" => majorbbsHostFunctions.Invoke(functionOrdinal, true),
                                    "GALGSBL" => galsblHostFunctions.Invoke(functionOrdinal, true),
                                    "DOSCALLS" => doscallsHostFunctions.Invoke(functionOrdinal, true),
                                    "GALME" => galmeFunctions.Invoke(functionOrdinal, true),
                                    "PHAPI" => phapiFunctions.Invoke(functionOrdinal, true),
                                    "GALMSG" => galmsgFunctions.Invoke(functionOrdinal, true),
                                    _ => throw new Exception(
                                        $"Unknown or Unimplemented Imported Library: {module.File.ImportedNameTable[nametableOrdinal].Name}")
                                };

                                var relocationPointer = new IntPtr16(relocationResult);

                                //32-Bit Pointer
                                if (relocationRecord.SourceType == 3)
                                {
                                    Array.Copy(relocationPointer.ToArray(), 0, s.Data, relocationRecord.Offset, 4);
                                    continue;
                                }

                                //16-Bit Values
                                var result = relocationRecord.SourceType switch
                                {
                                    //Offset
                                    2 => relocationPointer.Segment,
                                    5 => relocationPointer.Offset,
                                    _ => throw new ArgumentOutOfRangeException(
                                        $"Unhandled Relocation Source Type: {relocationRecord.SourceType}")
                                };

                                if (relocationRecord.Flag.HasFlag(EnumRecordsFlag.ImportOrdinalAdditive))
                                    result += BitConverter.ToUInt16(s.Data, relocationRecord.Offset);

                                Array.Copy(BitConverter.GetBytes(result), 0, s.Data, relocationRecord.Offset, 2);
                                break;
                            }
                        case EnumRecordsFlag.InternalRef:
                            {

                                //32-Bit Pointer
                                if (relocationRecord.SourceType == 3)
                                {
                                    var relocationPointer = new IntPtr16(relocationRecord.TargetTypeValueTuple.Item2,
                                        relocationRecord.TargetTypeValueTuple.Item4);

                                    Array.Copy(relocationPointer.ToArray(), 0, s.Data, relocationRecord.Offset, 4);
                                    break;
                                }
                                Array.Copy(BitConverter.GetBytes(relocationRecord.TargetTypeValueTuple.Item2), 0, s.Data, relocationRecord.Offset, 2);
                                break;
                            }

                        case EnumRecordsFlag.ImportNameAdditive:
                        case EnumRecordsFlag.ImportName:
                            {
                                var nametableOrdinal = relocationRecord.TargetTypeValueTuple.Item2;
                                var functionOrdinal = relocationRecord.TargetTypeValueTuple.Item3;

                                var newSegment = module.File.ImportedNameTable[nametableOrdinal].Name switch
                                {
                                    "MAJORBBS" => Majorbbs.Segment,
                                    "GALGSBL" => Galgsbl.Segment,
                                    "PHAPI" => Phapi.Segment,
                                    "GALME" => Galme.Segment,
                                    "DOSCALLS" => Doscalls.Segment,
                                    _ => throw new Exception(
                                        $"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[nametableOrdinal].Name}")

                                };

                                var relocationPointer = new IntPtr16(newSegment, functionOrdinal);

                                //32-Bit Pointer
                                if (relocationRecord.SourceType == 3)
                                {
                                    Array.Copy(relocationPointer.ToArray(), 0, s.Data, relocationRecord.Offset, 4);
                                    continue;
                                }

                                //16-Bit Values
                                var result = relocationRecord.SourceType switch
                                {
                                    //Offset
                                    2 => relocationPointer.Segment,
                                    5 => relocationPointer.Offset,
                                    _ => throw new ArgumentOutOfRangeException(
                                        $"Unhandled Relocation Source Type: {relocationRecord.SourceType}")
                                };

                                if (relocationRecord.Flag.HasFlag(EnumRecordsFlag.ImportNameAdditive))
                                    result += BitConverter.ToUInt16(s.Data, relocationRecord.Offset);

                                Array.Copy(BitConverter.GetBytes(result), 0, s.Data, relocationRecord.Offset, 2);
                                break;

                            }
                        default:
                            throw new Exception("Unsupported Records Flag for Relocation Value");
                    }
                }
            }
        }

        private void ProcessNightlyCleanup()
        {
            if (_performCleanup)
            {
                _performCleanup = false;
                DoNightlyCleanup();

                // signal that cleanup has completed
                _cleanupRestartEvent?.Set();
                _cleanupRestartEvent = null;
            }
        }

        private void DoNightlyCleanup()
        {
            Logger.Info("PERFORMING NIGHTLY CLEANUP");

            // Notify Users of Nightly Cleanup
            foreach (var c in _channelDictionary)
                _channelDictionary[c.Value.Channel].SendToClient($"|RESET|\r\n|B||RED|Nightly Cleanup Running -- Please log back on shortly|RESET|\r\n".EncodeToANSIArray());

            // removes all sessions and stops worker thread
            RemoveSessions(session => true);

            CallModuleRoutine("mcurou", module => Logger.Info($"Calling nightly cleanup routine on module {module.ModuleIdentifier}"));
            CallModuleRoutine("finrou", module => Logger.Info($"Calling finish-up (sys-shutdown) routine on module {module.ModuleIdentifier}"));

            foreach (var m in _modules.ToList())
            {
                _modules.Remove(m.Value.ModuleIdentifier);
               foreach (var e in _exportedFunctions.Keys.Where(x => x.StartsWith(m.Value.ModuleIdentifier)))
                    _exportedFunctions.Remove(e);
            }

            Logger.Info("NIGHTLY CLEANUP COMPLETE -- RESTARTING HOST");

            Start(_moduleConfigurations);
        }

        private TimeSpan NowUntil(TimeSpan timeOfDay)
        {
            var waitTime = _cleanupTime - DateTime.Now.TimeOfDay;
            if (waitTime < TimeSpan.Zero)
            {
                waitTime += TimeSpan.FromDays(1);
            }

            Logger.Info($"Waiting {waitTime} until {timeOfDay} to perform nightly cleanup");
            return waitTime;
        }

        private TimeSpan ParseCleanupTime()
        {
            if (!TimeSpan.TryParse(_configuration["Cleanup.Time"], out var cleanupTime))
            {
                cleanupTime = DEFAULT_CLEANUP_TIME;
            }

            return cleanupTime;
        }

        public void GenerateAPIReport()
        {
            foreach (var apiReport in _modules.Select(m => new ApiReport(Logger, m.Value)))
            {
                apiReport.GenerateReport();
            }
        }
    }
}

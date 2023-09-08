using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Date;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.DOS;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.GlobalRoutines;
using MBBSEmu.HostProcess.HostRoutines;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Reports;
using MBBSEmu.Session;
using MBBSEmu.Session.Attributes;
using MBBSEmu.Session.Enums;
using MBBSEmu.Session.Rlogin;
using MBBSEmu.TextVariables;
using MBBSEmu.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

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
    public class MbbsHost : IMbbsHost, IDisposable
    {
        public ILogger Logger { get; init; }
        public IClock Clock { get; init; }

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
        private readonly AppSettings _configuration;

        /// <summary>
        ///     Time of day that the cleanup routine will trigger
        /// </summary>
        private readonly TimeSpan _cleanupTime;

        /// <summary>
        ///     Timer that triggers when nightly cleanup should occur
        /// </summary>
        private readonly Timer _cleanupTimer;

        /// <summary>
        ///     Timer that triggers when nightly cleanup warning messages should start
        /// </summary>
        private Timer _cleanupWarningTimer;

        /// <summary>
        ///     Amount of time to start warning before cleanup grace period ends.
        /// </summary>
        private const int CleanupWarningInitialMinutes = 5;

        /// <summary>
        ///     Track minutes left until the nightly cleanup occurs.
        /// </summary>
        private int _cleanupWarningMinutesRemaining = CleanupWarningInitialMinutes;

        /// <summary>
        ///     Amount of time to give as a grace period to logoff after nightly cleanup.
        /// </summary>
        private const int _cleanupGracePeriodMinutes = 10;
        private readonly TimeSpan _cleanupGracePeriod = TimeSpan.FromMinutes(_cleanupGracePeriodMinutes);


        /// <summary>
        ///     Timer that sets _timerEvent on a set interval, controlled by Timer.Hertz
        /// </summary>
        private readonly Timer _tickTimer;
        private readonly EventWaitHandle _timerEvent;

        /// <summary>
        ///     Flag that controls whether the main loop will perform a nightly cleanup
        /// </summary>
        private bool _performCleanup = false;
        private EventWaitHandle _cleanupRestartEvent = null;

        private readonly IGlobalCache _globalCache;
        private readonly IFileUtility _fileUtility;

        private Thread _workerThread;

        private readonly IAccountKeyRepository _accountKeyRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ITextVariableService _textVariableService;

        public MbbsHost(IClock clock, ILogger logger, IGlobalCache globalCache, IFileUtility fileUtility, IEnumerable<IHostRoutine> mbbsRoutines, AppSettings configuration, IEnumerable<IGlobalRoutine> globalRoutines, IAccountKeyRepository accountKeyRepository, IAccountRepository accountRepository, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService, IMessagingCenter messagingCenter)
        {
            Logger = logger;
            Clock = clock;
            _globalCache = globalCache;
            _fileUtility = fileUtility;
            _mbbsRoutines = mbbsRoutines;
            _configuration = configuration;
            _globalRoutines = globalRoutines;
            _channelDictionary = channelDictionary;
            _accountKeyRepository = accountKeyRepository;
            _accountRepository = accountRepository;
            _textVariableService = textVariableService;
            var _messagingCenter = messagingCenter;

            Logger.Info("Constructing MBBSEmu Host...");

            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, IExportedModule>();
            _realTimeStopwatch = Stopwatch.StartNew();
            _incomingSessions = new Queue<SessionBase>();

            //Setup Cleanup Restart Event
            _cleanupTime = _configuration.CleanupTime;
            _cleanupTimer = new Timer(_ => _performCleanup = true, null, NowUntil(_cleanupTime + _cleanupGracePeriod), TimeSpan.FromDays(1));
            _cleanupWarningTimer = SetupCleanupWarningTimer();

            if (_configuration.TimerHertz > 0)
            {
                _timerEvent = new AutoResetEvent(true);
                _tickTimer = new Timer(_ => _timerEvent.Set(), this, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / configuration.TimerHertz));
            }

            //Setup Text Variables
            _textVariableService.SetVariable("SYSTEM_NAME", () => _configuration.BBSTitle);
            _textVariableService.SetVariable("SYSTEM_COMPANY", () => _configuration.BBSCompanyName);
            _textVariableService.SetVariable("SYSTEM_ADDRESS1", () => _configuration.BBSAddress1);
            _textVariableService.SetVariable("SYSTEM_ADDRESS2", () => _configuration.BBSAddress2);
            _textVariableService.SetVariable("SYSTEM_PHONE", () => _configuration.BBSDataPhone);
            _textVariableService.SetVariable("NUMBER_OF_LINES", () => _configuration.BBSChannels.ToString());
            _textVariableService.SetVariable("DATE", () => Clock.Now.ToString("M/d/yy"));
            _textVariableService.SetVariable("TIME", () => Clock.Now.ToString("t"));
            _textVariableService.SetVariable("TOTAL_ACCOUNTS", () => _accountRepository.GetAccounts().Count().ToString());
            _textVariableService.SetVariable("OTHERS_ONLINE", () => (GetUserSessions().Count - 1).ToString());
            _textVariableService.SetVariable("REG_NUMBER", () => _configuration.GSBLBTURNO);

            //Setup Message Subscribers
            _messagingCenter.Subscribe<SysopGlobal, string>(this, EnumMessageEvent.EnableModule, (sender, moduleId) => { EnableModule(moduleId); });
            _messagingCenter.Subscribe<SysopGlobal, string>(this, EnumMessageEvent.DisableModule, (sender, moduleId) => { DisableModule(moduleId); });
            _messagingCenter.Subscribe<SysopGlobal>(this, EnumMessageEvent.Cleanup, (sender) => { _performCleanup = true; });

            Logger.Info("Constructed MBBSEmu Host!");
        }

        public void Dispose()
        {
            foreach (var module in _modules)
                module.Value.Dispose();

            _modules.Clear();
        }

        /// <summary>
        ///     Starts the MbbsHost Worker Thread
        /// </summary>
        public void Start(List<ModuleConfiguration> moduleConfigurations)
        {
            //Load Modules
            foreach (var m in moduleConfigurations)
                AddModule(new MbbsModule(_fileUtility, Clock, Logger, m));

            //Remove any modules that did not properly initialize
            foreach (var (_, value) in _modules.Where(m => m.Value.MainModuleDll.EntryPoints.Count == 1 && (bool)m.Value.ModuleConfig.ModuleEnabled))
            {
                Logger.Error($"{value.ModuleIdentifier} not properly initialized, Removing");
                moduleConfigurations.RemoveAll(x => x.ModuleIdentifier == value.ModuleIdentifier);
                _modules.Remove(value.ModuleIdentifier);
                foreach (var e in _exportedFunctions.Keys.Where(x => x.StartsWith(value.ModuleIdentifier)))
                    _exportedFunctions.Remove(e);
            }

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
            _cleanupTimer?.Dispose();
            _cleanupWarningTimer?.Dispose();
            _tickTimer?.Dispose();
            // this set must come after _isRunning is set to false, to trigger the exit of the
            // worker thread.
            _timerEvent?.Set();
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

        private void WaitForNextTick()
        {
            if (_timerEvent == null ||
                _channelDictionary.Values.Any(session => session.DataFromClient.Count > 0 || session.DataToClient.Count > 0 || session.DataToProcess))
                return;

            _timerEvent.WaitOne();
        }

        public void TriggerProcessing() => _timerEvent?.Set();

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
                WaitForNextTick();

                ProcessNightlyCleanup();

                //Handle Channels
                ProcessIncomingSessions();
                ProcessDisconnects();

                //Process Channel Events
                foreach (var session in _channelDictionary.Values)
                {
                    //Process a single incoming byte from the client session
                    session.ProcessDataFromClient();

                    //Handle GSBL Chain of Events
                    if (!ProcessGSBLInputEvents(session))
                    {
                        session.DataToProcess = false;
                        continue;
                    }

                    //Handle Character based Events
                    if (session.DataToProcess)
                        ProcessIncomingCharacter(session);

                    //Global Command Handler
                    if (session.GetStatus() == EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE && DoGlobalsAttribute.Get(session.SessionState))
                    {
                        //Transfer Input Buffer to Command Buffer, but don't clear it
                        session.InputBuffer.WriteByte(0x0);
                        session.InputCommand = session.InputBuffer.ToArray();

                        //Check for Internal System Globals
                        if (_globalRoutines.Any(g =>
                            g.ProcessCommand(session.InputCommand, session.Channel, _channelDictionary, _modules)))
                        {
                            session.Status.Enqueue(EnumUserStatus.RINGING);
                            session.InputBuffer.SetLength(0);

                            //Redisplay Main Menu prompt after global if session is at Main Menu
                            if (session.SessionState == EnumSessionState.MainMenuInput)
                            {
                                session.SessionState = EnumSessionState.MainMenuInputDisplay;
                            }

                            session.Status.Dequeue();
                            continue;
                        }

                        //Check for Module Globals
                        foreach (var m in _modules.Values.Where(x => x.GlobalCommandHandlers.Any()))
                        {
                            var result = Run(m.ModuleIdentifier,
                                m.GlobalCommandHandlers.First(), session.Channel);

                            //Command Not Processed
                            if (result == 0) continue;

                            //Otherwise dequeue the input status to denote we've handled it
                            session.Status.Dequeue();
                            break;

                        }
                    }

                    switch (session.SessionState)
                    {
                        case EnumSessionState.RloginEnteringModule:
                            {
                                ProcessLONROU_FromRlogin(session);
                                break;
                            }
                        //Initial call to STTROU when a User is Entering a Module
                        case EnumSessionState.EnteringModule:
                            {
                                ProcessSTTROU_EnteringModule(session);
                                break;
                            }

                        //Post-Login Display Routine
                        case EnumSessionState.LoginRoutines:
                            {
                                ProcessLONROU(session);
                                break;
                            }

                        //User is in the module, process all the in-module type of events
                        case EnumSessionState.InModule:
                            {
                                //Did BTUCHI or a previous command cause a status change?
                                if (session.GetStatus() == EnumUserStatus.CYCLE || session.GetStatus() == EnumUserStatus.OUTPUT_BUFFER_EMPTY)
                                {
                                    ProcessSTSROU(session);
                                    break;
                                }

                                //User Input Available? Invoke *STTROU
                                if (session.GetStatus() == EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)
                                {
                                    ProcessSTTROU(session);
                                }

                                //If the channel has been registered with BEGIN_POLLING
                                if (session.PollingRoutine != null)
                                {
                                    session.Status.Enqueue(EnumUserStatus.POLLING_STATUS);
                                    ProcessPollingRoutine(session);
                                    session.Status.Dequeue();
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

                    //Mark Data Processing for this Channel as Complete
                    session.DataToProcess = false;

                }

                //Process Timed/Real-Time Events
                ProcessRTKICK();
                ProcessRTIHDLR();
                ProcessSYSCYC();
                ProcessTasks();

                foreach (var c in _channelDictionary.Where(x => x.Value.Status.Count > 0))
                    c.Value.Status.Dequeue();
            }

            Shutdown();
        }

        private void Shutdown()
        {
            Logger.Info("SHUTTING DOWN");

            // kill all active sessions
            foreach (var session in _channelDictionary.Values)
            {
                session.Stop();
            }

            // let modules clean themselves up
            CallModuleRoutine("finrou", module => Logger.Info($"Calling shutdown routine on module {module.ModuleIdentifier}"));

            //clean up modules
            foreach (var m in _modules)
            {
                m.Value.Dispose();
                _modules.Remove(m.Key);

                var exportedFunctionsToRemove = _exportedFunctions.Keys.Where(x => x.StartsWith(m.Key)).ToList();

                foreach (var e in exportedFunctionsToRemove)
                {
                    _exportedFunctions[e].Dispose();
                    _exportedFunctions.Remove(e);
                }
            }
        }

        private void CallModuleRoutine(string routine, Action<MbbsModule> preRunCallback, ushort channel = ushort.MaxValue)
        {
            foreach (var m in _modules.Values.Where(x => (bool)x.ModuleConfig.ModuleEnabled))
            {
                if (!m.MainModuleDll.EntryPoints.TryGetValue(routine, out var routineEntryPoint)) continue;

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
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)
            {
                session.Status.Clear();
                session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);
            }

            session.SessionState = EnumSessionState.InModule;
            session.UsrPtr.State = session.CurrentModule.MainModuleDll.StateCode;
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
                session.CurrentModule.MainModuleDll.EntryPoints["sttrou"], session.Channel);

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
            //Clear VDA
            Array.Clear(session.VDA, 0, Majorbbs.VOLATILE_DATA_SIZE);

            session.SessionState = EnumSessionState.MainMenuDisplay;
            session.CurrentModule = null;
            session.CharacterInterceptor = null;
            session.PromptCharacter = 0;

            //Reset States
            session.Status.Clear();
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
        ///     Invokes routine registered as LONROU during MAJORBBS->REGISTER_MODULE() call for
        ///     all modules, and then enters module specified by ModuleIdentifier.
        ///
        ///     Executes on the given channel once after a user successfully logs in.
        /// </summary>
        private void ProcessLONROU_FromRlogin(SessionBase session)
        {
            session.OutputEnabled = false; // always disabled for RLogin

            CallModuleRoutine("lonrou", preRunCallback: null, session.Channel);

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
            session.OutputEnabled = _configuration.ModuleDoLoginRoutine;

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

            var _ = Run(session.CurrentModule.ModuleIdentifier,
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
            initialStackValues.Enqueue(session.CharacterReceived);
            initialStackValues.Enqueue(session.Channel);

            var result = Run(session.CurrentModule.ModuleIdentifier,
                session.CharacterInterceptor,
                session.Channel, true,
                initialStackValues);

            //Only Take the low bytes, as it's the return character and not every routine/compiler
            //would clear out AH before assigning AL
            result &= 0xFF;

            if (result == 0)
                return result;

            session.CharacterProcessed = (byte)result;

            if (session.CharacterProcessed == 0xD)
            {
                session.Status.Clear();
                session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);
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
            Run(session.CurrentModule.ModuleIdentifier, session.CurrentModule.MainModuleDll.EntryPoints["stsrou"],
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
            foreach (var module in _modules.Values.Where(m => (bool)m.ModuleConfig.ModuleEnabled))
            {
                if (module.RtkickRoutines.Count == 0) continue;

                foreach (var (key, value) in module.RtkickRoutines.ToList())
                {
                    if (!value.Executed && value.Elapsed.ElapsedMilliseconds > value.Delay * 1000)
                    {
#if DEBUG
                        Logger.Info($"Running RTKICK-{key}: {value}");
#endif
                        Run(module.ModuleIdentifier, value, ushort.MaxValue);

                        value.Elapsed.Stop();
                        value.Executed = true;
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

            foreach (var m in _modules.Values.Where(m => (bool)m.ModuleConfig.ModuleEnabled))
            {
                if (m.RtihdlrRoutines.Count == 0) continue;

                foreach (var r in m.RtihdlrRoutines)
                {
                    Run(m.ModuleIdentifier, r.Value, ushort.MaxValue);
                }
            }

            _realTimeStopwatch.Restart();
        }

        /// <summary>
        ///     Processes the routine set to a modules specified SYSCYC Pointer
        /// </summary>
        private void ProcessSYSCYC()
        {
            foreach (var m in _modules.Values.Where(m => (bool)m.ModuleConfig.ModuleEnabled))
            {
                var syscycPointer = m.Memory.GetPointer(m.Memory.GetVariablePointer("SYSCYC"));
                if (syscycPointer == FarPtr.Empty) continue;

                Run(m.ModuleIdentifier, syscycPointer, ushort.MaxValue);
            }
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
            foreach (var m in _modules.Values.Where(m => (bool)m.ModuleConfig.ModuleEnabled))
            {
                if (m.TaskRoutines.Count == 0) continue;

                foreach (var r in m.TaskRoutines)
                {
                    var initialStackValues = new Queue<ushort>(1);
                    initialStackValues.Enqueue((ushort)r.Key);
                    Run(m.ModuleIdentifier, r.Value, ushort.MaxValue, false, initialStackValues);
                }
            }
        }

        /// <summary>
        ///     Processes the incoming character from a channel through the GSBL series of events.
        ///
        ///     In an actual MBBS System, these events are all triggered before MBBS even "gets" the character
        ///     from the serial channel.
        /// </summary>
        /// <param name="session"></param>
        /// <returns>
        ///     TRUE == Data Processed, Continue
        ///     FALSE == Data Processed, Halt Processing on Channel
        /// </returns>
        private bool ProcessGSBLInputEvents(SessionBase session)
        {
            //Quick Exit
            if (session.CharacterInterceptor == null)
                return true;

            //Invoke BTUCHI registered routine if one exists
            if (session.DataToProcess)
            {
                return ProcessBTUCHI(session) != 0;
            }

            //Invoke routine registered with BTUCHE if it has been registered and the criteria is met
            if (session.EchoEmptyInvokeEnabled && session.EchoEmptyInvoke)
            {
                ProcessEchoEmptyInvoke(session);
                return true;
            }

            return true;
        }

        /// <summary>
        ///     Processes the incoming character from a given channel and takes specific action depending on the
        ///     character and the current state of the channel.
        ///
        ///     Echoing of the character back to the channel is also handled within this method
        /// </summary>
        /// <param name="session"></param>
        private void ProcessIncomingCharacter(SessionBase session)
        {
            //Handling Incoming Characters
            switch (session.CharacterReceived)
            {
                //Backspace
                case 127 when session.SessionState == EnumSessionState.InModule:
                case 0x8:
                    {
                        if (session.InputBuffer.Length > 0)
                        {
                            session.SendToClient(new byte[] { 0x08, 0x20, 0x08 });
                            session.InputBuffer.SetLength(session.InputBuffer.Length - 1);
                        }
                        break;
                    }

                //Enter or Return
                case 0xD when (session.SessionState != EnumSessionState.InFullScreenDisplay && session.SessionState != EnumSessionState.InFullScreenEditor):
                    {
                        //If we're in transparent mode or BTUCHI has changed the character to null, don't echo
                        if (!session.TransparentMode && session.CharacterProcessed > 0)
                            session.SendToClient(new byte[] { 0xD, 0xA });

                        //If BTUCHI Injected a deferred Execution Status, respect that vs. processing the input
                        if (session.GetStatus() == EnumUserStatus.CYCLE)
                        {
                            //If we're cycling, ignore \r for triggering STTROU and add it to the input buffer for STSROU to handle
                            if (session.TransparentMode)
                            {
                                session.InputBuffer.WriteByte(session.CharacterProcessed);
                                break;
                            }

                            //Set Status == 3, which means there is a Command Ready
                            session.Status.Clear(); //Clear the 240
                            session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE); //Enqueue Status of 3
                            session.EchoSecureEnabled = false;
                            break;
                        }

                        //Always Enqueue Input Ready
                        session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);

                        break;
                    }

                case 0xA: //Ignore Linefeed
                case 0x0: //Ignore Null
                    break;
                default:
                    {
                        //If Secure Echo is on, enforce maximum length
                        if (session.EchoSecureEnabled && session.InputBuffer.Length >= session.ExtUsrAcc.wid)
                            break;

                        if (session.CharacterProcessed > 0)
                        {
                            session.InputBuffer.WriteByte(session.CharacterProcessed);

                            //If the client is in transparent mode, don't echo
                            if (session.TransparentMode)
                                break;

                            if (session.Status.Count == 0 || session.GetStatus() == EnumUserStatus.UNUSED || session.GetStatus() == EnumUserStatus.RINGING ||
                               session.GetStatus() == EnumUserStatus.POLLING_STATUS)
                            {

                                {
                                    //Check for Secure Echo being Enabled
                                    session.SendToClient(session.EchoSecureEnabled
                                        ? new[] { session.ExtUsrAcc.ech }
                                        : new[] { session.CharacterReceived });
                                }
                            }

                        }

                        break;
                    }
            }
        }

        private void RunProgram(string modulePath, string cmdline)
        {
            var exe = cmdline.Split(' ')[0];
            var args = cmdline.Split(' ').Skip(1).ToArray();
            exe = Path.ChangeExtension(exe, ".EXE");

            exe = _fileUtility.FindFile(modulePath, exe);

            Logger.Info($"Running auxiliary program: {cmdline}");

            var runtime = new ExeRuntime(
                            new MBBSEmu.Disassembler.MZFile(Path.Combine(modulePath, exe)),
                            Clock,
                            Logger,
                            _fileUtility,
                            modulePath,
                            sessionBase: null,
                            new TextReaderStream(Console.In),
                            new TextWriterStream(Console.Out),
                            new TextWriterStream(Console.Error));
            runtime.Load(args);
            runtime.Run();

            runtime.Dispose();
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
            Logger.Info($"({module.ModuleIdentifier}) Adding Module...");
            Logger.Info($"({module.ModuleIdentifier}) CRC32: {module.MainModuleDll.File.CRC32}");

            //Setup Exported Modules
            module.ExportedModuleDictionary.Add(Majorbbs.Segment, GetFunctions(module, "MAJORBBS"));
            module.ExportedModuleDictionary.Add(Galgsbl.Segment, GetFunctions(module, "GALGSBL"));
            module.ExportedModuleDictionary.Add(Phapi.Segment, GetFunctions(module, "PHAPI"));
            module.ExportedModuleDictionary.Add(Galme.Segment, GetFunctions(module, "GALME"));
            module.ExportedModuleDictionary.Add(Doscalls.Segment, GetFunctions(module, "DOSCALLS"));
            module.ExportedModuleDictionary.Add(Galmsg.Segment, GetFunctions(module, "GALMSG"));

            //Patch Relocation Information to Bytecode
            PatchRelocation(module);

            //Run Segments through AOT Decompiler & add them to Memory
            for (var i = 0; i < module.ModuleDlls.Count; i++)
            {
                var dll = module.ModuleDlls[i];

                //Only add the main module to the Modules List
                if (i == 0)
                    _modules[module.ModuleIdentifier] = module;

                foreach (var seg in dll.File.SegmentTable)
                {
                    var originalOrdinal = seg.Ordinal;
                    seg.Ordinal += dll.SegmentOffset;
                    module.ProtectedMemory.AddSegment(seg);
                    Logger.Debug($"({module.ModuleIdentifier}:{dll.File.FileName}:{originalOrdinal}) Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
                }

                dll.StateCode = (short)(_modules.Count * 10 + i);
            }

            //Run INIT
            foreach (var dll in module.ModuleDlls.OrderBy(m => m.File.FileName))
            {
                if (!dll.EntryPoints.TryGetValue("_INIT_", out var entryPointer))
                    continue;

                foreach (var bbsup in module.Mdf.BBSUp)
                    RunProgram(module.ModulePath, bbsup);

                Run(module.ModuleIdentifier, entryPointer, ushort.MaxValue);
            }

            Logger.Info($"({module.ModuleIdentifier}) Module Added!");
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
        public bool RemoveSession(ushort channel)
        {
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
        private ushort Run(string moduleName, FarPtr routine, ushort channelNumber, bool simulateCallFar = false, Queue<ushort> initialStackValues = null)
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
                    "MAJORBBS" => new Majorbbs(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _accountKeyRepository, _accountRepository, _textVariableService),
                    "GALGSBL" => new Galgsbl(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _textVariableService),
                    "DOSCALLS" => new Doscalls(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _textVariableService),
                    "GALME" => new Galme(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _textVariableService),
                    "PHAPI" => new Phapi(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _textVariableService),
                    "GALMSG" => new Galmsg(Clock, Logger, _configuration, _fileUtility, _globalCache, module, _channelDictionary, _textVariableService),
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

            foreach (var dll in module.ModuleDlls)
            {
                //Patch Segment 0 (PHAPI) to just RETF (0xCB)
                dll.File.SegmentTable[0].Data[0] = 0xCB;

                foreach (var s in dll.File.SegmentTable)
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

                                    var relocationPointer = FarPtr.Empty;
                                    switch (dll.File.ImportedNameTable[nametableOrdinal].Name)
                                    {
                                        case "MAJORBBS":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Majorbbs.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case "GALGSBL":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Galgsbl.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case "DOSCALLS":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Doscalls.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case "GALME":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Galme.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case "PHAPI":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Phapi.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case "GALMSG":
                                            relocationPointer = new FarPtr(module.ExportedModuleDictionary[Galmsg.Segment].Invoke(functionOrdinal, true));
                                            break;
                                        case var importedName
                                            when module.ModuleDlls.Any(m =>
                                                m.File.FileName.Split('.')[0].ToUpper() == importedName):
                                            {
                                                //Find the Imported DLL in the List of Required
                                                var importedDll = module.ModuleDlls.First(m =>
                                                    m.File.FileName.Split('.')[0].ToUpper() == importedName);

                                                //Get The Entry Point based on the Ordinal
                                                var initEntryPoint =
                                                    importedDll.File.EntryTable.First(x => x.Ordinal == functionOrdinal);
                                                relocationPointer = new FarPtr((ushort)(initEntryPoint.SegmentNumber + importedDll.SegmentOffset),
                                                    initEntryPoint.Offset);
                                                break;
                                            }
                                        default:
                                            Logger.Error($"({module.ModuleIdentifier}) Unknown or Unimplemented Imported Library: {dll.File.ImportedNameTable[nametableOrdinal].Name}");
                                            continue;
                                            //throw new Exception(
                                            //    $"Unknown or Unimplemented Imported Library: {dll.File.ImportedNameTable[nametableOrdinal].Name}");
                                    }

                                    //32-Bit Pointer
                                    if (relocationRecord.SourceType == 3)
                                    {
                                        Array.Copy(relocationPointer.Data, 0, s.Data, relocationRecord.Offset, 4);
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
                            case EnumRecordsFlag.InternalRef when relocationRecord.SourceType == 3:
                                {
                                    var relocationPointer = new FarPtr(
                                        (ushort)(relocationRecord.TargetTypeValueTuple.Item2 + dll.SegmentOffset),
                                        relocationRecord.TargetTypeValueTuple.Item4);

                                    Array.Copy(relocationPointer.Data, 0, s.Data, relocationRecord.Offset, 4);
                                    break;
                                }
                            case EnumRecordsFlag.InternalRef:
                                {
                                    Array.Copy(
                                        BitConverter.GetBytes(relocationRecord.TargetTypeValueTuple.Item2 +
                                                              dll.SegmentOffset), 0,
                                        s.Data, relocationRecord.Offset, 2);
                                    break;
                                }
                            case EnumRecordsFlag.ImportNameAdditive:
                            case EnumRecordsFlag.ImportName:
                                {
                                    var nametableOrdinal = relocationRecord.TargetTypeValueTuple.Item2;
                                    var functionOrdinal = relocationRecord.TargetTypeValueTuple.Item3;

                                    var newSegment = dll.File.ImportedNameTable[nametableOrdinal].Name switch
                                    {
                                        "MAJORBBS" => Majorbbs.Segment,
                                        "GALGSBL" => Galgsbl.Segment,
                                        "PHAPI" => Phapi.Segment,
                                        "GALME" => Galme.Segment,
                                        "DOSCALLS" => Doscalls.Segment,
                                        _ => throw new Exception(
                                            $"Unknown or Unimplemented Imported Module: {dll.File.ImportedNameTable[nametableOrdinal].Name}")

                                    };

                                    var relocationPointer = new FarPtr(newSegment, functionOrdinal);

                                    //32-Bit Pointer
                                    if (relocationRecord.SourceType == 3)
                                    {
                                        Array.Copy(relocationPointer.Data, 0, s.Data, relocationRecord.Offset, 4);
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
        }

        private void EnableModule(string moduleId)
        {
            _modules[moduleId].ModuleConfig.ModuleEnabled = true;
        }

        private void DisableModule(string moduleId)
        {
            if (_channelDictionary.Values.Where(session => session.SessionState == EnumSessionState.InModule).Any(channel => channel.CurrentModule.ModuleIdentifier == moduleId))
            {
                Logger.Warn($"(Sysop Command) Tried to disable {moduleId} while in use -- Wait until all users have exited module");
                return;
            }

            _modules[moduleId].ModuleConfig.ModuleEnabled = false;
        }

        private Timer SetupCleanupWarningTimer()
        {
            _cleanupWarningMinutesRemaining = CleanupWarningInitialMinutes;

            var initialWarningTime = _cleanupTime + _cleanupGracePeriod - TimeSpan.FromMinutes(CleanupWarningInitialMinutes);
            var dueTime = NowUntil(initialWarningTime);
            var period = TimeSpan.FromMinutes(1);

            Logger.Debug($"_cleanupTime Hours: {_cleanupTime.Hours}");
            Logger.Debug($"_cleanupTime Minutes: {_cleanupTime.Minutes}");
            Logger.Debug($"initialWarningTime Hours: {initialWarningTime.Hours}");
            Logger.Debug($"initialWarningTime Minutes: {initialWarningTime.Minutes}");
            Logger.Debug($"dueTime: {NowUntil(initialWarningTime).Minutes}");
            Logger.Debug($"period: {period.Minutes}");

            return new Timer(SendCleanupWarning, null, (int) dueTime.TotalMilliseconds, (int) period.TotalMilliseconds);
        }

        private void SendCleanupWarning(object _)
        {
            if (_cleanupWarningMinutesRemaining > 0)
            {
                Logger.Debug($"in SendCleanupWarning, _cleanupWarningMinutesRemaining = {_cleanupWarningMinutesRemaining}");

                var minuteText = (_cleanupWarningMinutesRemaining > 1) ? "minutes" : "minute";

                foreach (var c in _channelDictionary)
                {
                    var channel = c.Value.Channel;

                    Logger.Error($"Sending cleanup warning to channel {channel}: {_cleanupWarningMinutesRemaining} {minuteText} until shutdown.");
                    _channelDictionary[channel].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry to interrupt here, but the server will be shutting down in {_cleanupWarningMinutesRemaining} {minuteText} for the nightly \"auto-cleanup\" process. Please finish up and log off... thank you!|RESET|\r\n".EncodeToANSIArray());
                }
                _cleanupWarningMinutesRemaining--;
                Logger.Debug($"finished SendCleanupWarning");
            }
            else
            {
                _cleanupWarningTimer.Dispose();
                _cleanupWarningTimer = null;
            }
        }

        private void ProcessNightlyCleanup()
        {
            if (_performCleanup)
            {
                Logger.Info($"Beginning nightly cleanup process.");
                _performCleanup = false;
                DoNightlyCleanup();

                // signal that cleanup has completed
                _cleanupRestartEvent?.Set();
                _cleanupRestartEvent = null;

                _cleanupWarningTimer = SetupCleanupWarningTimer();
                Logger.Info($"Nightly cleanup complete.");
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

            //Save current module config
            var moduleConfigurations = (from m in _modules select m.Value.ModuleConfig).ToList();

            foreach (var m in _modules)
            {
                var modulePath = m.Value.ModulePath;

                m.Value.Dispose();
                _modules.Remove(m.Value.ModuleIdentifier);
                var exportedFunctionsToRemove = _exportedFunctions.Keys.Where(x => x.StartsWith(m.Value.ModuleIdentifier)).ToList();

                foreach (var e in exportedFunctionsToRemove)
                {
                    _exportedFunctions[e].Dispose();
                    _exportedFunctions.Remove(e);
                }

                foreach (var cleanup in m.Value.Mdf.Cleanup)
                    RunProgram(modulePath, cleanup);
            }

            Logger.Info("NIGHTLY CLEANUP COMPLETE -- RESTARTING HOST");

            Start(moduleConfigurations);
        }

        /// <summary>
        ///     Returns a TimeSpan representing the time between now and the specified time.
        /// </summary>
        /// <param name="timeOfDay">24-hour timespan representing a time of day, and determining the time between now and the time specified</param>
        /// <returns></returns>
        private TimeSpan NowUntil(TimeSpan timeOfDay)
        {
            var waitTime = timeOfDay - Clock.Now.TimeOfDay;
            if (waitTime < TimeSpan.Zero)
            {
                waitTime += TimeSpan.FromDays(1);
            }

            return waitTime;
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

using MBBSEmu.CPU;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.HostRoutines;
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
        private readonly ILogger _logger;

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
        private readonly IEnumerable<IHostRoutines> _mbbsRoutines;

        /// <summary>
        ///     Queue of incoming sessions not added to a Channel yet
        /// </summary>
        private readonly Queue<SessionBase> _incomingSessions;

        /// <summary>
        ///     Configuration Class giving access to the appsettings.json file
        /// </summary>
        private readonly IConfiguration _configuration;

        public MbbsHost(ILogger logger, IEnumerable<IHostRoutines> mbbsRoutines, IConfiguration configuration)
        {
            _logger = logger;
            _mbbsRoutines = mbbsRoutines;
            _configuration = configuration;

            _logger.Info("Constructing MBBSEmu Host...");

            _channelDictionary = new PointerDictionary<SessionBase>();
            _modules = new Dictionary<string, MbbsModule>();
            _exportedFunctions = new Dictionary<string, IExportedModule>();
            _realTimeStopwatch = Stopwatch.StartNew();
            _incomingSessions = new Queue<SessionBase>();

            _logger.Info("Constructed MBBSEmu Host!");
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
        ///     This is the main MajorBBS/Worldgroup loop similar to how it actually functions with the software itself.
        ///
        ///     Because it was a DOS based software, it did not support threads/tasking, so all the events had to happen in
        ///     serial order as quickly as possible. For compatibility, we've replicated the process here as well.
        /// </summary>
        private void WorkerThread()
        {
            while (_isRunning)
            {
                //Handle Channels
                ProcessIncomingSessions();
                ProcessDisconnects();

                //Process Channel Events
                foreach (var session in _channelDictionary.Values)
                {
                    //Process a single incoming byte from the client session
                    session.ProcessDataFromClient();

                    switch (session.SessionState)
                    {
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
                                    if (!session.TransparentMode && session.Status == 1)
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

        }

        /// <summary>
        ///     Adds any incoming sessions to an available Channel
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessIncomingSessions()
        {
            //No new connections? Bail.
            if (_incomingSessions.Count <= 0) return;

            while (_incomingSessions.TryDequeue(out var incomingSession))
            {
                incomingSession.Channel = (ushort)_channelDictionary.Allocate(incomingSession);
                incomingSession.SessionTimer.Start();
                _logger.Info($"Added Session {incomingSession.SessionId} to channel {incomingSession.Channel}");
            }
        }

        /// <summary>
        ///     Removes any channels that have disconnected
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessDisconnects()
        {
            //Any Disconnects
            for (ushort i = 0; i < _channelDictionary.Count; i++)
            {
                //Because users might not be on sequential channels (0,1,3), we verify if the channel number
                //is even in use first.
                if (!_channelDictionary.ContainsKey(i)) continue;

                //We only process channels that are logged off
                if (_channelDictionary[i].SessionState != EnumSessionState.LoggedOff) continue;

                _logger.Info($"Removing LoggedOff Channel: {i}");
                _channelDictionary.Remove(i);
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
            session.InputBuffer.WriteByte(0x0);
            session.InputCommand = session.InputBuffer.ToArray();
            session.InputBuffer.SetLength(0);

            var result = Run(session.CurrentModule.ModuleIdentifier,
                session.CurrentModule.EntryPoints["sttrou"], session.Channel);

            //stt returned an exit code -- we're done
            if (result != 0) return;

            session.SessionState = EnumSessionState.MainMenuDisplay;
            session.CurrentModule = null;
            session.CharacterInterceptor = null;

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
        ///     Invokes routine registered as LONROU during MAJORBBS->REGISTER_MODULE() call
        ///
        ///     Executes on the given channel once after a user successfully logs in.
        /// </summary>
        /// <param name="session"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLONROU(SessionBase session)
        {
            bool.TryParse(_configuration["Module.DoLoginRoutine"], out var doLoginRoutine);

            if (doLoginRoutine)
            {
                foreach (var m in _modules.Values)
                {
                    //No Login Routine for the Module? NEXT!
                    if (!m.EntryPoints.TryGetValue("lonrou", out var logonRoutineEntryPoint)) continue;

                    if (logonRoutineEntryPoint.Segment != 0 &&
                        logonRoutineEntryPoint.Offset != 0)
                    {
                        Run(m.ModuleIdentifier, logonRoutineEntryPoint, session.Channel);
                    }
                }
            }

            session.SessionState = EnumSessionState.MainMenuDisplay;
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
                        _logger.Info($"Running RTKICK-{key}: {module.EntryPoints[$"RTKICK-{key}"]}");
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
            if (_isRunning)
                throw new Exception("Unable to Add Module after host is running");

            _logger.Info($"Adding Module {module.ModuleIdentifier}...");

            //Patch Relocation Information to Bytecode
            PatchRelocation(module);

            //Run Segments through AOT Decompiler & add them to Memory
            foreach (var seg in module.File.SegmentTable)
            {
                module.Memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Setup Exported Modules
            module.ExportedModuleDictionary.Add(Majorbbs.Segment, GetFunctions(module, "MAJORBBS"));
            module.ExportedModuleDictionary.Add(Galgsbl.Segment, GetFunctions(module, "GALGSBL"));
            module.ExportedModuleDictionary.Add(Phapi.Segment, GetFunctions(module, "PHAPI"));
            module.ExportedModuleDictionary.Add(Galme.Segment, GetFunctions(module, "GALME"));
            module.ExportedModuleDictionary.Add(Doscalls.Segment, GetFunctions(module, "DOSCALLS"));

            //Add it to the Module Dictionary
            _modules[module.ModuleIdentifier] = module;
            module.StateCode = (short)(_modules.Count + 1);

            //Run INIT
            Run(module.ModuleIdentifier, module.EntryPoints["_INIT_"], ushort.MaxValue);

            _logger.Info($"Module {module.ModuleIdentifier} added!");
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
                    "MAJORBBS" => new Majorbbs(module, _channelDictionary),
                    "GALGSBL" => new Galgsbl(module, _channelDictionary),
                    "DOSCALLS" => new Doscalls(module, _channelDictionary),
                    "GALME" => new Galme(module, _channelDictionary),
                    "PHAPI" => new Phapi(module, _channelDictionary),
                    "GALMSG" => new Galmsg(module, _channelDictionary),
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
    }
}
using MBBSEmu.CPU;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly IConfigurationRoot _configuration;
        private readonly Queue<UserSession> _incomingSessions;
        private readonly bool _doLoginRoutine;

        public MbbsHost(ILogger logger, IMbbsRoutines mbbsRoutines, IConfigurationRoot configuration)
        {
            _logger = logger;
            _mbbsRoutines = mbbsRoutines;
            _configuration = configuration;

            _logger.Info("Constructing MbbsEmu Host...");

            bool.TryParse(_configuration["Module.DoLoginRoutine"], out _doLoginRoutine);

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
            _cpu.Registers.Halt = true;
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
                    incomingSession.SessionTimer.Start();
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

                //Don't fire these events while the system is adding a module, prevents a race condition
                if (!_isAddingModule)
                {
                    //Process channel events
                    foreach (var s in _channelDictionary.Values)
                    {
                        switch (s.SessionState)
                        {
                            //Initial call to STTROU when a User is Entering a Module
                            case EnumSessionState.EnteringModule:
                                {
                                    s.StatusChange = false;
                                    s.SessionState = EnumSessionState.InModule;
                                    s.UsrPtr.State = s.CurrentModule.StateCode; //Default ModuleID, need to make this dynamic
                                                                                //Transfer Input Buffer to Command Buffer
                                    s.InputBuffer.WriteByte(0x0);
                                    s.InputCommand = s.InputBuffer.ToArray();

                                    s.InputBuffer.SetLength(0);
                                    Run(s.CurrentModule.ModuleIdentifier, s.CurrentModule.EntryPoints["sttrou"], s.Channel);
                                    continue;
                                }

                            //Post-Login Display Routine
                            case EnumSessionState.LoginRoutines:
                                {
                                    if (_doLoginRoutine)
                                    {
                                        foreach (var m in _modules.Values)
                                        {
                                            if (m.EntryPoints.TryGetValue("lonrou", out var logonRoutineEntryPoint))
                                            {
                                                if (logonRoutineEntryPoint.Segment != 0 &&
                                                    logonRoutineEntryPoint.Offset != 0)
                                                {
                                                    Run(m.ModuleIdentifier, logonRoutineEntryPoint, s.Channel);
                                                }
                                            }
                                        }
                                    }
                                    s.SessionState = EnumSessionState.MainMenuDisplay;
                                    continue;
                                }

                            //User is in the module, process all the in-module type of events
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
                                                s.Channel, true,
                                                initialStackValues);

                                            //Result replaces the character in the buffer
                                            if (s.InputBuffer.Length > 0 && result != 0xD)
                                                s.InputBuffer.SetLength(s.InputBuffer.Length - 1);

                                            //Ignore
                                            if (result == 0)
                                                continue;

                                            s.InputBuffer.WriteByte((byte)result);
                                            s.LastCharacterReceived = (byte)result;

                                            //If the new character is a carriage return, null terminate it and set status
                                            if (result == 0xD)
                                            {
                                                s.Status = 3;
                                                s.InputBuffer.WriteByte(0x0);
                                            }
                                        }

                                        //If the client is in transparent mode, don't echo
                                        if (!s.TransparentMode)
                                            s.DataToClient.Enqueue(new[] { s.LastCharacterReceived });
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
                                        //Transfer Input Buffer to Command Buffer
                                        s.InputBuffer.WriteByte(0x0);
                                        s.InputCommand = s.InputBuffer.ToArray();
                                        s.InputBuffer.SetLength(0);

                                        var result = Run(s.CurrentModule.ModuleIdentifier,
                                            s.CurrentModule.EntryPoints["sttrou"], s.Channel);

                                        //stt returned an exit code
                                        if (result == 0)
                                        {
                                            s.SessionState = EnumSessionState.MainMenuDisplay;
                                            s.CurrentModule = null;
                                            s.CharacterInterceptor = null;

                                            //Reset States
                                            s.Status = 0;
                                            s.UsrPtr.Substt = 0;

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

                    //Check for any rtkick routines
                    foreach (var m in _modules.Values)
                    {
                        if (m.RtkickRoutines.Count == 0) continue;

                        foreach (var r in m.RtkickRoutines.ToList())
                        {
                            if (!r.Value.Executed && r.Value.Elapsed.ElapsedMilliseconds > (r.Value.Delay * 1000))
                            {
                                //Run(m.ModuleIdentifier, m.EntryPoints[$"RTKICK-{r.Key}"], ushort.MaxValue);
                                r.Value.Elapsed.Stop();
                                r.Value.Executed = true;
                            }

                            if (r.Value.Executed)
                            {
                                m.EntryPoints.Remove($"RTKICK-{r.Key}");
                                m.RtkickRoutines.Remove(r.Key);
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
        private ushort Run(string moduleName, IntPtr16 routine, ushort channelNumber, bool simulateCallFar = false, Queue<ushort> initialStackValues = null)
        {
            var resultRegisters = _modules[moduleName].Execute(routine, channelNumber, simulateCallFar, false, initialStackValues);
            return resultRegisters.AX;
        }

        /// <summary>
        /// 
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
                    _ => throw new Exception($"Unknown Exported Function: {exportedModule}")
                };

                functions = _exportedFunctions[key];
            }

            return functions;
        }

        /// <summary>
        ///     Patches all relocation information into the byte code
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
                                    _ => throw new Exception(
                                        $"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[nametableOrdinal].Name}")
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
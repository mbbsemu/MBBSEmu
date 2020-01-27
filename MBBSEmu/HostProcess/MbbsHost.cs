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

                if (!_isAddingModule)
                {
                    //Process Channels
                    foreach (var s in _channelDictionary.Values)
                    {
                        switch (s.SessionState)
                        {
                            //Initial call to STTROU when a User is Entering a Module
                            case EnumSessionState.EnteringModule:
                            {
                                s.StatusChange = false;
                                s.SessionState = EnumSessionState.InModule;
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
                                            s.Channel,
                                            initialStackValues);

                                        //Result replaces the character in the buffer
                                        if(s.InputBuffer.Length > 0)
                                            s.InputBuffer.SetLength(s.InputBuffer.Length - 1);

                                        s.InputBuffer.WriteByte((byte) result);
                                        s.LastCharacterReceived = (byte) result;

                                        //If the new character is a carriage return, null terminate it and set status
                                        if (result == 0xD)
                                        {
                                            s.Status = 3;
                                            s.InputBuffer.WriteByte(0x0);
                                        }
                                    }

                                    //If the client is in transparent mode, don't echo
                                    if (!s.TransparentMode)
                                        s.DataToClient.Enqueue(new[] {s.LastCharacterReceived});
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

            //Patch Relocation Information to Bytecode
            PatchRelocation(module);

            //Run Segments through AOT Decompiler & add them to Memory
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
                return ordinal switch
                {
                    0xFFFF => majorbbsHostFunctions.Invoke(functionOrdinal),
                    0xFFFE => galsblHostFunctions.Invoke(functionOrdinal),
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
            }

            //Run as long as the CPU still has code to execute and the host is still running
            while (!_cpu.Registers.Halt)
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
                    "DOSCALLS" => new Doscalls(module, _channelDictionary),
                    "GALME" => new Galme(module, _channelDictionary),
                    _ => _exportedFunctions[key]
                };

                functions = _exportedFunctions[key];
            }

            return functions;
        }

        /// <summary>
        ///     Patches all relocation information into the 
        /// </summary>
        /// <param name="moduleIdentifier"></param>
        private void PatchRelocation(MbbsModule module)
        {
            //Declare Host Functions
            var majorbbsHostFunctions = GetFunctions(module, "MAJORBBS");
            var galsblHostFunctions = GetFunctions(module, "GALGSBL");
            var doscallsHostFunctions = GetFunctions(module, "DOSCALLS");
            var galmeFunctions = GetFunctions(module, "GALME");

            foreach (var s in module.File.SegmentTable)
            {
                if (s.RelocationRecords == null || s.RelocationRecords.Count == 0)
                    continue;

                foreach(var relocationRecord in s.RelocationRecords.Values)
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
                                _ => throw new Exception(
                                    $"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[nametableOrdinal].Name}")
                            };

                            var relocationPointer = new IntPtr16(relocationResult);

                            //32-Bit Pointer
                            if (relocationRecord.SourceType == 3)
                            {
#if DEBUG
                                //_logger.Info($"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Imported Pointer {relocationPointer.Segment:X4}:{relocationPointer.Offset:X4}");
#endif
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

#if DEBUG
                            //_logger.Info($"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Imported value {result:X4}");
#endif
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

#if DEBUG
                                //_logger.Info(
                               //     $"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Internal Ref Pointer value {relocationPointer.Segment:X4}:{relocationPointer.Offset:X4}");
#endif
                                break;
                            }

#if DEBUG
                            //_logger.Info($"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Internal Ref value {relocationRecord.TargetTypeValueTuple.Item2:X4}");
#endif

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
                                "MAJORBBS" => (ushort) 0xFFFF,
                                "GALGSBL" => (ushort) 0xFFFE,
                                "PHAPI" => (ushort) 0xFFFD,
                                "GALME" => (ushort) 0xFFFC,
                                _ => throw new Exception(
                                    $"Unknown or Unimplemented Imported Module: {module.File.ImportedNameTable[nametableOrdinal].Name}")

                            };

                            var relocationPointer = new IntPtr16(newSegment, functionOrdinal);

                            //32-Bit Pointer
                            if (relocationRecord.SourceType == 3)
                            {
#if DEBUG
                                //_logger.Info($"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Imported Pointer {relocationPointer.Segment:X4}:{relocationPointer.Offset:X4}");
#endif
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
#if DEBUG
                            //_logger.Info($"Patching {s.Ordinal:X4}:{relocationRecord.Offset:X4} with Imported Name value {relocationPointer.Segment:X4}:{relocationPointer.Offset:X4}");
#endif
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
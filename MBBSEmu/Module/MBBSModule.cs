using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS.Structs;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     The MajorBBS Module Class
    ///
    ///     This contains all information related to the Module that is being loaded into MBBSEmu,
    ///     including:
    ///
    ///     - MDF, MSG, MCF File information
    ///     - DLL File Structure ((N)ew (E)xecutable Format)
    ///     - Full 16-Bit Memory Space for the Module (hence no relocation required)
    ///     - Execution Units for the Module (Multiple CPUs accessing shared memory space, used for subroutines)
    /// </summary>
    public class MbbsModule : IDisposable
    {
        protected readonly IMessageLogger _logger;
        protected readonly IFileUtility _fileUtility;
        private protected readonly IClock _clock;

        /// <summary>
        ///     Module Memory Manager
        /// </summary>
        public IMemoryCore Memory;

        public ProtectedModeMemoryCore ProtectedMemory;

        /// <summary>
        ///     The unique name of the module (same as the DLL name)
        /// </summary>
        public readonly string ModuleIdentifier;

        /// <summary>
        ///     Directory Path of Module
        /// </summary>
        public readonly string ModulePath;

        /// <summary>
        ///     Module Configuration
        /// </summary>
        public ModuleConfiguration ModuleConfig { get; set; }

        /// <summary>
        ///     Module MSG File
        /// </summary>
        public readonly List<MsgFile> Msgs;

        /// <summary>
        ///     Module MDF File
        /// </summary>
        public readonly MdfFile Mdf;

        /// <summary>
        ///     Routine definitions for functions registered via RTKICK
        /// </summary>
        public PointerDictionary<RealTimeRoutine> RtkickRoutines { get; set; }

        /// <summary>
        ///     Routine definitions for functions registered via RTIHDLR
        /// </summary>
        public PointerDictionary<RealTimeRoutine> RtihdlrRoutines { get; set; }

        /// <summary>
        ///     Routine definitions for functions registered via INITASK
        /// </summary>
        public PointerDictionary<RealTimeRoutine> TaskRoutines { get; set; }

        /// <summary>
        ///     Global Command Handler Definitions for modules that register global commands
        /// </summary>
        public List<FarPtr> GlobalCommandHandlers { get; set; }

        /// <summary>
        ///     Description of the Module as Defined by REGISTER_MODULE
        /// </summary>
        public string ModuleDescription { get; set; }

        /// <summary>
        ///     Required DLL's are DLL Files that are referenced by the Module
        /// </summary>
        public List<MbbsDll> ModuleDlls { get; set; }

        /// <summary>
        ///     Helper to always return the main Module DLL (loaded in 0)
        /// </summary>
        public MbbsDll MainModuleDll => ModuleDlls?[0];

        /// <summary>
        ///     Executions Units (EU's) for the Module
        ///
        ///     Execution Units are how subroutines get called without having to mess around with saving/resetting CPU state.
        ///     This way, we can have an execution chain of:
        ///     EU0 -> MAJORBBS.H -> EU1 -> Text Variable Function
        ///
        ///     At no point is the state of EU0 modified (registers), even though they share a common memory core, allowing the stack
        ///     to unwind gracefully and execution to continue without much fuss.
        ///
        ///     Most modules will ever need 1-2 EU's
        /// </summary>
        public Queue<ExecutionUnit> ExecutionUnits { get; set; }

        /// <summary>
        ///     Exported Modules used by the given MajorBBS Module
        ///
        ///     Exported Modules are the libraries exposed by the host process (MAJORBBS) that contain
        ///     the statically linked methods that are imported into the DLL and invoked via EXTERN calls.
        ///
        ///     Each module gets its own set of these Exported Modules since each module as it's own 16-bit address space,
        ///     thus keeping things nice and clean.
        /// </summary>
        public Dictionary<ushort, IExportedModule> ExportedModuleDictionary { get; set; }

        /// <summary>
        ///     Message Center used to send notifications and events to other parts of the system
        /// </summary>
        private readonly IMessagingCenter _messagingCenter;

        /// <summary>
        ///     Constructor for MbbsModule
        /// 
        ///     Pass in an empty/blank moduleIdentifier for a Unit Test/Fake Module
        /// </summary>
        /// <param name="fileUtility"></param>
        /// <param name="clock"></param>
        /// <param name="logger"></param>
        /// <param name="moduleConfig"></param>
        /// <param name="memoryCore">(Optional) Memory Core to be loaded into the Module memory space</param>
        /// <param name="messagingCenter">(Optional) Messaging Center used to notify other threads of events</param>
        public MbbsModule(IFileUtility fileUtility, IClock clock, IMessageLogger logger, ModuleConfiguration moduleConfig, ProtectedModeMemoryCore memoryCore = null, IMessagingCenter messagingCenter = null)
        {

            _fileUtility = fileUtility;
            _logger = logger;
            _clock = clock;

            ModuleConfig = moduleConfig;
            _messagingCenter = messagingCenter;

            ModuleIdentifier = moduleConfig.ModuleIdentifier;

            ModuleDlls = new List<MbbsDll>();

            //Sanitize and setup Path
            if (string.IsNullOrEmpty(moduleConfig.ModulePath))
                moduleConfig.ModulePath = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(moduleConfig.ModulePath))
                moduleConfig.ModulePath += Path.DirectorySeparatorChar;

            ModulePath = moduleConfig.ModulePath;

            // will be null in tests
            if (string.IsNullOrEmpty(ModuleIdentifier))
            {
                Mdf = MdfFile.createForTest();
                ModuleDlls.Add(new MbbsDll(fileUtility, logger) { File = NEFile.createForTest() });
            }
            else
            {
                //Verify Module Path Exists
                if (!Directory.Exists(ModulePath))
                {
                    _logger.Error($"Unable to find the specified directory for the module {ModuleIdentifier.ToUpper()}: {ModulePath}");
                    _logger.Error("Please verify your Command Line Argument or the path specified in your Module JSON File and try again.");
                    throw new DirectoryNotFoundException($"Unable to locate {ModulePath}");
                }

                //Verify MDF File Exists
                var mdfFile = fileUtility.FindFile(ModulePath, $"{ModuleIdentifier}.MDF");
                var fullMdfFilePath = Path.Combine(ModulePath, mdfFile);
                if (!File.Exists(fullMdfFilePath))
                {
                    _logger.Error($"Unable to locate {fullMdfFilePath}");
                    _logger.Error($"Please verify your Command Line Argument or the Module JSON File to ensure {ModuleIdentifier} is the correct Module Identifier and that the Module is installed properly.");
                    throw new FileNotFoundException($"Unable to locate Module: {fullMdfFilePath}");
                }
                Mdf = new MdfFile(fullMdfFilePath);

                LoadModuleDll(Mdf.DLLFiles[0].Trim());

                if (Mdf.MSGFiles.Count > 0)
                {
                    Msgs = new List<MsgFile>(Mdf.MSGFiles.Count);
                    foreach (var m in Mdf.MSGFiles)
                    {
                        Msgs.Add(new MsgFile(_fileUtility, ModulePath, m));
                    }
                }
            }

            //Set Initial Values
            RtkickRoutines = new PointerDictionary<RealTimeRoutine>();
            RtihdlrRoutines = new PointerDictionary<RealTimeRoutine>();
            TaskRoutines = new PointerDictionary<RealTimeRoutine>();
            GlobalCommandHandlers = new List<FarPtr>();
            ExportedModuleDictionary = new Dictionary<ushort, IExportedModule>(6);
            ExecutionUnits = new Queue<ExecutionUnit>(2);

            Memory = memoryCore ?? new ProtectedModeMemoryCore(logger);
            ProtectedMemory = (ProtectedModeMemoryCore)Memory;

            //Declare PSP Segment
            var psp = new PSPStruct { NextSegOffset = 0x9FFF, EnvSeg = 0xFFFF };
            ProtectedMemory.AddSegment(0x4000);
            Memory.SetArray(0x4000, 0, psp.Data);

            Memory.AllocateVariable("Int21h-PSP", sizeof(ushort));
            Memory.SetWord("Int21h-PSP", 0x4000);

            //Find _INIT_ values if any
            foreach (var dll in ModuleDlls)
            {
                //If it's a Test, setup a fake _INIT_
                if (string.IsNullOrEmpty(ModuleIdentifier))
                {
                    dll.EntryPoints["_INIT_"] = null;
                    return;
                }

                //Setup _INIT_ Entrypoint
                FarPtr initEntryPointPointer;
                var initResidentName = dll.File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT__"));
                if (initResidentName == null)
                {
                    //This only happens with MajorMUD -- I have no idea why it's a special little snowflake ¯\_(ツ)_/¯
                    _logger.Warn($"({moduleConfig.ModuleIdentifier}) Unable to locate _INIT_ in Resident Name Table, checking Non-Resident Name Table...");

                    var initNonResidentName = dll.File.NonResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT__"));

                    if (initNonResidentName == null)
                    {
                        _logger.Error($"Unable to locate _INIT__ entry in Resident Name Table for {dll.File.FileName}");
                        continue;
                    }

                    var initEntryPoint = dll.File.EntryTable.First(x => x.Ordinal == initNonResidentName.IndexIntoEntryTable);

                    initEntryPointPointer = new FarPtr((ushort)(initEntryPoint.SegmentNumber + dll.SegmentOffset), initEntryPoint.Offset);

                }
                else
                {
                    var initEntryPoint = dll.File.EntryTable.First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
                    initEntryPointPointer = new FarPtr((ushort)(initEntryPoint.SegmentNumber + dll.SegmentOffset), initEntryPoint.Offset);
                }


                _logger.Debug($"({ModuleIdentifier}) Located _INIT__: {initEntryPointPointer}");
                dll.EntryPoints["_INIT_"] = initEntryPointPointer;
            }
        }

        public void Dispose()
        {
            foreach (var unit in ExecutionUnits)
            {
                unit.Dispose();
            }
        }

        /// <summary>
        ///     Tells the module to begin x86 execution at the specified Entry Point
        /// </summary>
        /// <param name="entryPoint">Entry Point where execution will begin</param>
        /// <param name="channelNumber">Channel Number calling for execution</param>
        /// <param name="simulateCallFar">Are we simulating a CALL FAR? This is usually when we're calling a local function pointer</param>
        /// <param name="bypassSetState">Should we bypass setting a startup state? This is true for execution of things like Text Variable processing</param>
        /// <param name="initialStackValues">Initial Stack Values to be pushed to the stack prior to executing (simulating parameters on a method call)</param>
        /// <param name="initialStackPointer">
        ///     Initial Stack Pointer Value. Because EU's share the same memory space, including stack space, we need to sometimes need to manually decrement the
        ///     stack pointer enough to where the stack on the nested call won't overlap with the stack on the parent caller.
        /// </param>
        /// <returns></returns>
        public ICpuRegisters Execute(FarPtr entryPoint, ushort channelNumber, bool simulateCallFar = false, bool bypassSetState = false,
            Queue<ushort> initialStackValues = null, ushort initialStackPointer = CpuCore.STACK_BASE)
        {
            ICpuRegisters resultRegisters = null;
            ExecutionUnit executionUnit = null;
            try
            {
                //Set the proper DLL making the call based on the Segment
                for (ushort i = 0; i < ModuleDlls.Count; i++)
                {
                    var dll = ModuleDlls[i];
                    if (entryPoint.Segment >= dll.SegmentOffset &&
                        entryPoint.Segment <= (dll.SegmentOffset + dll.File.SegmentTable.Count))

                        foreach (var (_, value) in ExportedModuleDictionary)
                            ((ExportedModuleBase)value).ModuleDll = i;
                }

                //Try to dequeue an execution unit, if one doesn't exist, create a new one
                if (!ExecutionUnits.TryDequeue(out executionUnit))
                {
                    _logger.Debug($"({ModuleIdentifier}) Exhausted execution Units, creating additional");
                    executionUnit = new ExecutionUnit(Memory, _clock, _fileUtility, ExportedModuleDictionary, _logger, ModulePath);
                }

                resultRegisters = executionUnit.Execute(entryPoint, channelNumber, simulateCallFar, bypassSetState, initialStackValues, initialStackPointer);
                ExecutionUnits.Enqueue(executionUnit);
                return resultRegisters;
            }
            catch (Exception e)
            {
                //Set Module Status to Disabled
                ModuleConfig.ModuleEnabled = false;

                //Grab the Registers from the Execution Unit
                var registers = executionUnit?.ModuleCpuRegisters;

                //Call Crash Logger
                var crashReport = new CrashReport(this, registers, e);
                crashReport.Save();

                //Notify the Host Process that the Module is Disabled
                //Host process knows that messages to disable module coming from here is a crash event
                _messagingCenter?.Send(this, EnumMessageEvent.DisableModule, ModuleIdentifier);

                //Log the crash
                _logger.Error($"({ModuleIdentifier}) CRASH: {e.Message}");

                //Set AX == 1, we don't return an exit code so it can be gracefully handled by the host process
                return new CpuRegisters { AX = 1, Halt = true};
            }
        }

        /// <summary>
        ///     Loads the specified DLL, and then inspects that DLLs Module Reference Table to import any additional
        ///     references it might require recursively.
        /// </summary>
        /// <param name="dllToLoad"></param>
        private void LoadModuleDll(string dllToLoad)
        {
            var requiredDll = new MbbsDll(_fileUtility, _logger);
            if (!requiredDll.Load(dllToLoad, ModulePath, ModuleConfig.Patches))
            {
                _logger.Error($"Unable to load {dllToLoad}");
                return;
            }

            requiredDll.SegmentOffset = (ushort)ModuleDlls.Sum(x => x.File.SegmentTable.Count);
            ModuleDlls.Add(requiredDll);

            _logger.Info($"Loaded {dllToLoad}");

            //Pull records from ModuleReferenceTable that aren't of References handled internally by the emulator
            foreach (var import in ModuleDlls[0].File.ModuleReferenceTable
                .Where(x => GetAllExportedModules().All(e => e != x.Name)).Select(x => x.Name))
            {
                //Only load a dependency if it's not already loaded by a previous library
                if (ModuleDlls.All(x => x.File.FileName.ToUpper().Split('.')[0] != import.ToUpper()))
                    LoadModuleDll(import);
            }
        }

        /// <summary>
        ///     Returns a list of each Class which Implements IExportedModule
        ///
        ///     We use this to not try and import DLL references that are emulated internally
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetAllExportedModules()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => typeof(IExportedModule).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(x => x.Name.ToUpper()).ToList();
        }
    }
}

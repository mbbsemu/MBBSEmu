using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS.Structs;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public class MbbsModule
    {
        protected readonly ILogger _logger;
        protected readonly IFileUtility _fileUtility;
        private protected readonly IClock _clock;

        /// <summary>
        ///     Module Memory Manager
        /// </summary>
        public IMemoryCore Memory;

        /// <summary>
        ///     The unique name of the module (same as the DLL name)
        /// </summary>
        public readonly string ModuleIdentifier;

        /// <summary>
        ///     Directory Path of Module
        /// </summary>
        public readonly string ModulePath;

        /// <summary>
        ///     Menu Option Key of Module
        /// </summary>
        public string MenuOptionKey { get; set; }

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
        ///     Constructor for MbbsModule
        ///
        ///     Pass in an empty/blank moduleIdentifier for a Unit Test/Fake Module
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="moduleIdentifier">Will be null in a test</param>
        /// <param name="path"></param>
        /// <param name="memoryCore"></param>
        /// <param name="fileUtility"></param>
        public MbbsModule(IFileUtility fileUtility, IClock clock, ILogger logger, string moduleIdentifier, string path = "", MemoryCore memoryCore = null)
        {
            _fileUtility = fileUtility;
            _logger = logger;
            _clock = clock;

            ModuleIdentifier = moduleIdentifier;
            ModuleDlls = new List<MbbsDll>();


            //Sanitize and setup Path
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!Path.EndsInDirectorySeparator(path))
                path += Path.DirectorySeparatorChar;

            ModulePath = path;

            // will be null in tests
            if (string.IsNullOrEmpty(ModuleIdentifier))
            {
                Mdf = MdfFile.createForTest();
                ModuleDlls.Add(new MbbsDll(fileUtility, logger) { File = NEFile.createForTest() });
            }
            else
            {
                //Verify MDF File Exists
                var mdfFile = fileUtility.FindFile(ModulePath, $"{ModuleIdentifier}.MDF");
                var fullMdfFilePath = Path.Combine(ModulePath, mdfFile);
                if (!File.Exists(fullMdfFilePath))
                {
                    throw new FileNotFoundException($"Unable to locate Module: {fullMdfFilePath}");
                }

                Mdf = new MdfFile(fullMdfFilePath);
                var moduleDll = new MbbsDll(fileUtility, logger);
                moduleDll.Load(Mdf.DLLFiles[0].Trim(), ModulePath);
                ModuleDlls.Add(moduleDll);

                //We load any DLL's required by the module and load it into the same memory space
                //by looking at the NE Module Reference Tableto see what is being referenced.
                //We filter out entries that are handled internally
                foreach (var imports in ModuleDlls[0].File.ModuleReferenceTable
                    .Where(x => x.Name != "MAJORBBS" && x.Name != "GALGSBL" && x.Name != "PHAPI" && x.Name != "DOSCALLS").Select(x=> x.Name))
                {
                    var requiredDll = new MbbsDll(fileUtility, logger);
                    if (!requiredDll.Load(imports, ModulePath)) continue;

                    requiredDll.SegmentOffset = (ushort)(ModuleDlls.Sum(x => x.File.SegmentTable.Count) + 1);
                    ModuleDlls.Add(requiredDll);
                }
                
                if (Mdf.MSGFiles.Count > 0)
                {
                    Msgs = new List<MsgFile>(Mdf.MSGFiles.Count);
                    foreach (var m in Mdf.MSGFiles)
                    {
                        Msgs.Add(new MsgFile(ModulePath, m));
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

            Memory = memoryCore ?? new MemoryCore(logger);

            //Declare PSP Segment
            var psp = new PSPStruct { NextSegOffset = 0x9FFF, EnvSeg = 0xFFFF };
            Memory.AddSegment(0x4000);
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
                    _logger.Warn($"({moduleIdentifier}) Unable to locate _INIT_ in Resident Name Table, checking Non-Resident Name Table...");

                    var initNonResidentName = dll.File.NonResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT__"));

                    if (initNonResidentName == null)
                    {
                        _logger.Error($"Unable to locate _INIT__ entry in Resident Name Table for {dll.File.FileName}");
                        continue;
                    }

                    var initEntryPoint = dll.File.EntryTable.First(x => x.Ordinal == initNonResidentName.IndexIntoEntryTable);

                    initEntryPointPointer = new FarPtr((ushort) (initEntryPoint.SegmentNumber + dll.SegmentOffset), initEntryPoint.Offset);

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
        public CpuRegisters Execute(FarPtr entryPoint, ushort channelNumber, bool simulateCallFar = false, bool bypassSetState = false,
            Queue<ushort> initialStackValues = null, ushort initialStackPointer = CpuCore.STACK_BASE)
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
            if (!ExecutionUnits.TryDequeue(out var executionUnit))
            {
                _logger.Debug($"({ModuleIdentifier}) Exhausted execution Units, creating additional");
                executionUnit = new ExecutionUnit(Memory, _clock, ExportedModuleDictionary, _logger, ModulePath);
            }

            var resultRegisters = executionUnit.Execute(entryPoint, channelNumber, simulateCallFar, bypassSetState, initialStackValues, initialStackPointer);
            ExecutionUnits.Enqueue(executionUnit);
            return resultRegisters;
        }
    }
}

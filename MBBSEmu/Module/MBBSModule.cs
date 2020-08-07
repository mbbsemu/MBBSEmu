using MBBSEmu.CPU;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
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
        private protected readonly ILogger _logger;

        /// <summary>
        ///     State returned by REGISTER_MODULE
        ///
        ///     Used to identify module within The MajorBBS/Worldgroup
        /// </summary>
        public short StateCode { get; set; }

        /// <summary>
        ///     The unique name of the module (same as the DLL name)
        /// </summary>
        public readonly string ModuleIdentifier;

        /// <summary>
        ///     Directory Path of Module
        /// </summary>
        public readonly string ModulePath;

        /// <summary>
        ///     Module DLL
        /// </summary>
        public readonly NEFile File;

        /// <summary>
        ///     Module MSG File
        /// </summary>
        public readonly List<MsgFile> Msgs;

        /// <summary>
        ///     Module MDF File
        /// </summary>
        public readonly MdfFile Mdf;

        /// <summary>
        ///     Module Memory Manager
        /// </summary>
        public readonly IMemoryCore Memory;

        /// <summary>
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, IntPtr16> EntryPoints { get; set; }
        
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
        ///     Text Variable definitions for variables registered via REGISTER_VARIABLE
        /// </summary>
        public Dictionary<string, IntPtr16> TextVariables { get; set; }

        /// <summary>
        ///     Global Command Handler Definitions for modules that register global commands
        /// </summary>
        public List<IntPtr16> GlobalCommandHandlers { get; set; }

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
        ///     Description of the Module as Defined by REGISTER_MODULE
        /// </summary>
        public string ModuleDescription { get; set; }

        public MbbsModule(string module, string path = "", MemoryCore memoryCore = null)
        {
            ModuleIdentifier = module == null ? "test" : module;

            _logger = ServiceResolver.GetService<ILogger>();

            //Sanitize Path
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            ModulePath = path;

            if (module != null)
            {
                if (!System.IO.File.Exists($"{ModulePath}{ModuleIdentifier}.MDF"))
                {
                    throw new FileNotFoundException($"Unable to locate Module: {ModulePath}{ModuleIdentifier}.MDF");
                }
            }


            Mdf = module != null ? new MdfFile($"{ModulePath}{ModuleIdentifier}.MDF") : MdfFile.createForTest();
            File = module != null ? new NEFile($"{ModulePath}{Mdf.DLLFiles[0].Trim()}.DLL") : NEFile.createForTest();
            
            if (Mdf.MSGFiles.Count > 0)
            {
                Msgs = new List<MsgFile>(Mdf.MSGFiles.Count);
                foreach (var m in Mdf.MSGFiles)
                {
                    Msgs.Add(new MsgFile(ModulePath, m));
                }
            }

            EntryPoints = new Dictionary<string, IntPtr16>();
            RtkickRoutines = new PointerDictionary<RealTimeRoutine>();
            RtihdlrRoutines = new PointerDictionary<RealTimeRoutine>();
            TaskRoutines = new PointerDictionary<RealTimeRoutine>();
            TextVariables = new Dictionary<string, IntPtr16>();
            ExecutionUnits = new Queue<ExecutionUnit>(2);
            ExportedModuleDictionary = new Dictionary<ushort, IExportedModule>(4);
            GlobalCommandHandlers = new List<IntPtr16>();
            Memory = memoryCore != null ? memoryCore : new MemoryCore();

            if (module == null)
            {
                EntryPoints["_INIT_"] = null;
                return;
            }

            //Setup _INIT_ Entrypoint
            IntPtr16 initEntryPointPointer;
            var initResidentName = File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT__"));
            if (initResidentName == null)
            {
                //This only happens with MajorMUD -- I have no idea why it's a special little snowflake ¯\_(ツ)_/¯
                _logger.Warn("Unable to locate _INIT_ in Resident Name Table, checking Non-Resident Name Table...");

                var initNonResidentName = File.NonResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT__"));

                if(initNonResidentName == null)
                    throw new Exception("Unable to locate _INIT__ entry in Resident Name Table");

                var initEntryPoint = File.EntryTable.First(x => x.Ordinal == initNonResidentName.IndexIntoEntryTable);
                initEntryPointPointer = new IntPtr16(initEntryPoint.SegmentNumber, initEntryPoint.Offset);
            }
            else
            {
                var initEntryPoint = File.EntryTable.First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
                initEntryPointPointer = new IntPtr16(initEntryPoint.SegmentNumber, initEntryPoint.Offset);
            }

            
            _logger.Info($"Located _INIT__: {initEntryPointPointer}");
            EntryPoints["_INIT_"] = initEntryPointPointer;
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
        public CpuRegisters Execute(IntPtr16 entryPoint, ushort channelNumber, bool simulateCallFar = false, bool bypassSetState = false,
            Queue<ushort> initialStackValues = null, ushort initialStackPointer = CpuCore.STACK_BASE)
        {
            //Try to dequeue an execution unit, if one doesn't exist, create a new one
            if (!ExecutionUnits.TryDequeue(out var executionUnit))
            {
                _logger.Warn($"{ModuleIdentifier} exhausted execution Units, creating additional");
                executionUnit = new ExecutionUnit(Memory, ExportedModuleDictionary);
            }

            var resultRegisters = executionUnit.Execute(entryPoint, channelNumber, simulateCallFar, bypassSetState, initialStackValues, initialStackPointer);
            ExecutionUnits.Enqueue(executionUnit);
            return resultRegisters;
        }
    }
}

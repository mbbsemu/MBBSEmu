using System;
using MBBSEmu.Disassembler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MBBSEmu.CPU;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using NLog;

namespace MBBSEmu.Module
{
    public class MbbsModule
    {
        private protected readonly ILogger _logger;

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

        public short StateCode { get; set; }

        /// <summary>
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, IntPtr16> EntryPoints;

        public PointerDictionary<RealTimeRoutine> RtkickRoutines;

        public PointerDictionary<RealTimeRoutine> RtihdlrRoutines;

        public Dictionary<string, IntPtr16> TextVariables;
        public List<IntPtr16> GlobalCommandHandlers;

        public readonly IMemoryCore Memory;

        public Queue<ExecutionUnit> ExecutionUnits;

        public Dictionary<ushort, IExportedModule> ExportedModuleDictionary;

        /// <summary>
        ///     Description of the Module as Defined by REGISTER_MODULE
        /// </summary>
        public string ModuleDescription;

        public MbbsModule(string module, string path = "")
        {
            ModuleIdentifier = module;

            _logger = ServiceResolver.GetService<ILogger>();

            //Sanitize Path
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            ModulePath = path;

            if (!System.IO.File.Exists($"{ModulePath}{ModuleIdentifier}.MDF"))
                throw new FileNotFoundException($"Unable to locate Module: {ModulePath}{ModuleIdentifier}.MDF");

            Mdf = new MdfFile($"{ModulePath}{ModuleIdentifier}.MDF");
            File = new NEFile($"{ModulePath}{Mdf.DLLFiles[0].Trim()}.DLL");

            
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
            TextVariables = new Dictionary<string, IntPtr16>();
            ExecutionUnits = new Queue<ExecutionUnit>(2);
            ExportedModuleDictionary = new Dictionary<ushort, IExportedModule>(4);
            GlobalCommandHandlers = new List<IntPtr16>();
            Memory = new MemoryCore();

            //Setup _INIT_ Entrypoint
            var initResidentName = File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));
            if (initResidentName == null)
                throw new Exception("Unable to locate _INIT_ entry in Resident Name Table");

            var initEntryPoint = File.EntryTable.First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
            EntryPoints["_INIT_"] = new IntPtr16(initEntryPoint.SegmentNumber, initEntryPoint.Offset);
        }

        public CpuRegisters Execute(IntPtr16 entryPoint, ushort channelNumber, bool simulateCallFar = false,
            Queue<ushort> initialStackValues = null)
        {
            //Try to dequeue an execution unit, if one doesn't exist, create a new one
            if (!ExecutionUnits.TryDequeue(out var executionUnit))
            {
                _logger.Warn($"{ModuleIdentifier} Exhausted Execution Units, creating additional");
                executionUnit = new ExecutionUnit(Memory, ExportedModuleDictionary);
            }

            var resultRegisters = executionUnit.Execute(entryPoint, channelNumber, simulateCallFar, initialStackValues);
            ExecutionUnits.Enqueue(executionUnit);
            return resultRegisters;
        }
    }
}

using MBBSEmu.Disassembler;
using MBBSEmu.HostProcess.ExecutionUnits;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using NLog;
using System.Collections.Generic;
using System.IO;
using MBBSEmu.IO;

namespace MBBSEmu.Module
{
    public class MbbsDll
    {
        protected readonly ILogger _logger;

        protected readonly IFileUtility _fileUtility;

        /// <summary>
        ///     Module DLL
        /// </summary>
        public NEFile File;

        /// <summary>
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, IntPtr16> EntryPoints { get; set; }

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
        ///     The Segment Offset in the Memory Core that the DLL will be loaded in
        ///
        ///     Segment 1 of the DLL will become SegmentOffset, Segment 2 will be SegmentOffset + 1, etc.
        ///
        ///     This value will be used during relocation patching so CALL FAR calls will be within the same Memory Space
        /// </summary>
        public ushort SegmentOffset { get; set; }

        public MbbsDll(IFileUtility fileUtility, ILogger logger)
        {
            _fileUtility = fileUtility;
            _logger = logger;
        }
        
        public void Load(string file, string path)
        { 
            var neFile = _fileUtility.FindFile(path, $"{file}.DLL");
            var fullNeFilePath = Path.Combine(path, neFile);
            File = new NEFile(_logger, fullNeFilePath);
        }
    }
}

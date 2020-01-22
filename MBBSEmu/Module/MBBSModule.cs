using System;
using MBBSEmu.Disassembler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MBBSEmu.Memory;

namespace MBBSEmu.Module
{
    public class MbbsModule
    {
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
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, IntPtr16> EntryPoints;

        public PointerDictionary<RealTimeRoutine> RtkickRoutines;

        public PointerDictionary<RealTimeRoutine> RtihdlrRoutines;

        public readonly IMemoryCore Memory;

        /// <summary>
        ///     Description of the Module as Defined by REGISTER_MODULE
        /// </summary>
        public string ModuleDescription;

        public MbbsModule(string module, string path = "")
        {
            ModuleIdentifier = module;

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

            Memory = new MemoryCore();

            //Setup _INIT_ Entrypoint
            var initResidentName = File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));
            if (initResidentName == null)
                throw new Exception("Unable to locate _INIT_ entry in Resident Name Table");

            var initEntryPoint = File.EntryTable.First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
            EntryPoints["_INIT_"] = new IntPtr16(initEntryPoint.SegmentNumber, initEntryPoint.Offset);
        }
    }
}

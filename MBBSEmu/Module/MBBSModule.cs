using MBBSEmu.Disassembler;
using System.Collections.Generic;
using System.IO;

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
        public readonly MsgFile Msg;

        /// <summary>
        ///     Module MDF File
        /// </summary>
        public readonly MdfFile Mdf;

        /// <summary>
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, EntryPoint> EntryPoints;

        public MbbsModule(string module, string path = "")
        {
            ModuleIdentifier = module;
            ModulePath = path;

            //Sanitize Path
            if (string.IsNullOrEmpty(ModulePath))
                path = Directory.GetCurrentDirectory();
            if (!path.EndsWith(@"\"))
                path += @"\";

            if (!System.IO.File.Exists($"{ModulePath}{ModuleIdentifier}.MDF"))
                throw new FileNotFoundException($"Unable to locate Module: {ModulePath}{ModuleIdentifier}.MDF");

            Mdf = new MdfFile($"{ModulePath}{ModuleIdentifier}.MDF");
            File = new NEFile($"{ModulePath}{Mdf.DLLFiles[0].Trim()}.DLL");
            Msg = new MsgFile(ModulePath, ModuleIdentifier);
            EntryPoints = new Dictionary<string, EntryPoint>(9);
        }
    }
}

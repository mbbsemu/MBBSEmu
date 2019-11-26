using MBBSEmu.Disassembler;
using System.IO;

namespace MBBSEmu.Module
{
    public class MbbsModule
    {
        public readonly string ModuleIdentifier;
        public readonly string ModulePath;
        public readonly NEFile File;
        public readonly MsgFile Msg;
        public readonly MdfFile Mdf;

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
        }
    }
}

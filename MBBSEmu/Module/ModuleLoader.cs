using System.IO;

namespace MBBSEmu.Module
{
    public class ModuleLoader
    {
        public MBBSModule Module;

        private byte[] _moduleFileData;

        public ModuleLoader(byte[] moduleFileData)
        {
            
        }

        public ModuleLoader(string moduleFilePath)
        {
            _moduleFileData = File.ReadAllBytes(moduleFilePath);
        }

        /// <summary>
        ///     Parses the NE File Header and identifies code/data segments
        /// </summary>
        private void ParseModuleHeader()
        {


        }
    }
}

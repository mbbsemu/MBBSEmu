using MBBSEmu.Disassembler;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Module
{
    public class MbbsDll
    {
        private readonly ILogger _logger;

        private readonly IFileUtility _fileUtility;

        /// <summary>
        ///     Module DLL
        /// </summary>
        public NEFile File;

        /// <summary>
        ///     Entry Points for the Module, as defined by register_module()
        /// </summary>
        public Dictionary<string, FarPtr> EntryPoints { get; set; }

        /// <summary>
        ///     The Segment Offset in the Memory Core that the DLL will be loaded in
        ///
        ///     Segment 1 of the DLL will become SegmentOffset, Segment 2 will be SegmentOffset + 1, etc.
        ///
        ///     This value will be used during relocation patching so CALL FAR calls will be within the same Memory Space
        /// </summary>
        public ushort SegmentOffset { get; set; }

        /// <summary>
        ///     State returned by REGISTER_MODULE
        ///
        ///     Used to identify module within The MajorBBS/Worldgroup
        ///
        ///     Because sub-DLL's also have an _INIT_ routine and can call REGISTER_MODULE, this is associated at the DLL level
        /// </summary>
        public short StateCode { get; set; }

        public MbbsDll(IFileUtility fileUtility, ILogger logger)
        {
            _fileUtility = fileUtility;
            _logger = logger;
            
            EntryPoints = new Dictionary<string, FarPtr>();
        }
        
        public bool Load(string file, string path, IEnumerable<ModulePatch> modulePatches)
        {
            var neFile = _fileUtility.FindFile(path, $"{file}.DLL");
            var fullNeFilePath = Path.Combine(path, neFile);
            if (!System.IO.File.Exists(fullNeFilePath))
            {
                _logger.Warn($"Unable to Load {neFile}");
                return false;
            }

            var fileData = System.IO.File.ReadAllBytes(fullNeFilePath);

            if (modulePatches != null)
            {
                foreach (var p in modulePatches)
                {
                    _logger.Info($"Applying Patch: {p.Name} to Absolute Offet {p.AbsoluteOffset}");
                    var bytesToPatch = p.GetBytes();
                    Array.Copy(bytesToPatch.ToArray(), 0, fileData, p.AbsoluteOffset,
                        bytesToPatch.Length);
                }
            }

            File = new NEFile(_logger, fullNeFilePath, fileData);
            return true;
        }
    }
}

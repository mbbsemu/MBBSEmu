using MBBSEmu.Disassembler;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            var fileCRC32 = BitConverter.ToString(new Crc32().ComputeHash(fileData)).Replace("-", string.Empty);

            //Absolute Offset Patching
            //We perform Absolute Patching here as this is the last stop before the data is loaded into the NE file and split into Segments
            if (modulePatches != null)
            {
                foreach (var p in modulePatches.Where(x => x?.AbsoluteOffset > 0))
                {
                    if (string.Compare(p.CRC32, fileCRC32, StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        _logger.Error($"Unable to apply patch {p.Name}: Module CRC32 Mismatch (Expected: {p.CRC32}, Actual: {fileCRC32})");
                        continue;
                    }

                    _logger.Info($"Applying Patch: {p.Name} to Absolute Offet {p.AbsoluteOffset}");
                    var bytesToPatch = p.GetBytes();
                    Array.Copy(bytesToPatch.ToArray(), 0, fileData, p.AbsoluteOffset,
                        bytesToPatch.Length);
                }
            }

            File = new NEFile(_logger, fullNeFilePath, fileData);

            //Address Patching
            if (modulePatches != null)
            {
                foreach (var p in modulePatches.Where(x => x.Addresses.Count > 0 || x.Address != null))
                {
                    if (string.Compare(p.CRC32, fileCRC32, StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        _logger.Error($"Unable to apply patch {p.Name}: Module CRC32 Mismatch (Expected: {p.CRC32}, Actual: {fileCRC32})");
                        continue;
                    }

                    if (p.Address != null && p.Addresses == null)
                        p.Addresses = new List<FarPtr>() { p.Address };

                    foreach (var a in p.Addresses)
                    {
                        var bytesToPatch = p.GetBytes();
                        _logger.Info($"Applying Patch: {p.Name} to {a}");
                        Array.Copy(bytesToPatch.ToArray(), 0, File.SegmentTable.First(x => x.Ordinal == a.Segment).Data,
                            a.Offset,
                            bytesToPatch.Length);
                    }
                }
            }

            return true;
        }
    }
}

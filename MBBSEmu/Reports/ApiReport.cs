using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Module;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MBBSEmu.Reports
{
    public class ApiReport
    {
        private readonly ILogger _logger;

        internal class ApiReportRecord
        {
            public string UniqueIdentifier { get; set; }
            public string File { get; set; }
            public Dictionary<string, List<int>> Imports { get; set; }
        }

        private readonly MbbsModule _module;
        private ApiReportRecord _record;

        public ApiReport(MbbsModule module)
        {
            _module = module;
            _logger = ServiceResolver.GetService<ILogger>();
        }

        public void GenerateReport()
        {
            _record = new ApiReportRecord
            {
                UniqueIdentifier = _module.ModuleIdentifier,
                File = _module.File.FileName,
                Imports = new Dictionary<string, List<int>>()
            };

            //Create Imports Records
            foreach (var nt in _module.File.ImportedNameTable.Values)
            {
                _record.Imports.Add(nt.Name, new List<int>());
            }

            //Loop through each segment
            foreach (var s in _module.File.SegmentTable.Where(seg => seg.RelocationRecords != null && seg.RelocationRecords.Count > 0))
            {
                foreach (var r in s.RelocationRecords.Values.Where(relo => relo.Flag == EnumRecordsFlag.ImportName || relo.Flag == EnumRecordsFlag.ImportOrdinal))
                {
                    var key = _module.File.ImportedNameTable[r.TargetTypeValueTuple.Item2].Name;
                    var ordinal = r.TargetTypeValueTuple.Item3;
                    if(!_record.Imports[key].Contains(ordinal))
                        _record.Imports[key].Add(ordinal);
                }
            }

            File.WriteAllText($"{_module.ModulePath}{_module.ModuleIdentifier}_api.json", System.Text.Json.JsonSerializer.Serialize(_record));
            _logger.Info($"Generated API Report: {_module.ModuleIdentifier}_api.json");
        }
    }
}

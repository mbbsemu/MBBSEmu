using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess
{
    /// <summary>
    ///     Class acts as the MBBS/WG Host Process which contains all the
    ///     imported functions.
    ///
    ///     We'll perform the imported functions here
    /// </summary>
    public class MbbsHost
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly MbbsModule _module;
        private readonly IMemoryCore _memory;

        public MbbsHost(MbbsModule module)
        {
            _module = module;
            _logger.Info("Constructing MbbsEmu Host...");
            _logger.Info("Initalizing x86_16 CPU Emulator...");

            //Setup new memory core and init with host memory segment
            _memory = new MemoryCore();
            _memory.AddSegment((ushort)EnumHostSegments.HostMemorySegment);
            _memory.AddSegment((ushort)EnumHostSegments.Status);
            AllocateUser(_memory);
            AllocateUserNum(_memory);

            foreach (var seg in _module.File.SegmentTable)
            {
                _memory.AddSegment(seg);
                _logger.Info($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded!");
            }

            //Verify that the imported functions are all supported by MbbsEmu

            //if (!VerifyImportedFunctions())
            //    throw new Exception("Module is currently unsupported by MbbEmu! :(");


            _logger.Info("Constructed MbbsEmu Host!");
        }

        /// <summary>
        ///     Starts a new instance of the MbbsHost running the specified MbbsModule
        /// </summary>
        public void Init()
        {
            _logger.Info("Locating _INIT_ function...");
            var initResidentName = _module.File.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));
            if (initResidentName == null)
                throw new Exception("Unable to locate _INIT_ entry in Resident Name Table");

            _logger.Info($"Located Resident Name Table record for _INIT_: {initResidentName.Name}");
            _logger.Info($"Ordinal in Entry Table: {initResidentName.IndexIntoEntryTable}");
            var initEntryPoint = _module.File.EntryTable
                .First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
            _logger.Info(
                $"Located Entry Table Record, _INIT_ function located in Segment {initEntryPoint.SegmentNumber}");

            _module.EntryPoints["_INIT_"] = new EntryPoint(initEntryPoint.SegmentNumber, initEntryPoint.Offset);

            _logger.Info(
                $"Starting MbbsEmu Host at {initResidentName.Name} (Seg {initEntryPoint.SegmentNumber}:{initEntryPoint.Offset:X4}h)...");

            var _cpuRegisters = new CpuRegisters()
                {CS = _module.EntryPoints["_INIT_"].Segment, IP = _module.EntryPoints["_INIT_"].Offset};

            using var majorbbsHostFunctions = new Majorbbs(_memory, _cpuRegisters, _module);
            using var galsblHostFunctions = new Galsbl(_memory, _cpuRegisters, _module);

            var _cpu = new CpuCore(_memory, _cpuRegisters, (delegate(ushort ordinal, ushort functionOrdinal)
            {
                try
                {
                    var importedModuleName =
                        _module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

                    switch (importedModuleName)
                    {
                        case "MAJORBBS":
                            return majorbbsHostFunctions.ExportedFunctions[functionOrdinal]();
                        case "GALGSBL":
                            return galsblHostFunctions.ExportedFunctions[functionOrdinal]();
                        default:
                            throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Fatal(e);
                    throw;
                }
            }));


        while (_cpu.IsRunning)
                _cpu.Tick();
        }

        public void Run(string routineName)
        {
            if(!_module.EntryPoints.ContainsKey(routineName))
                throw new Exception($"Attempted to execute unknown Routine name: {routineName}");

            _logger.Info($"Running {routineName}...");

            var _cpuRegisters = new CpuRegisters()
                { CS = _module.EntryPoints[routineName].Segment, IP = _module.EntryPoints[routineName].Offset };

            using var majorbbsHostFunctions = new Majorbbs(_memory, _cpuRegisters, _module);
            using var galsblHostFunctions = new Galsbl(_memory, _cpuRegisters, _module);

            var _cpu = new CpuCore(_memory, _cpuRegisters, delegate (ushort ordinal, ushort functionOrdinal)
            {
                var importedModuleName =
                    _module.File.ImportedNameTable.First(x => x.Ordinal == ordinal).Name;

                switch (importedModuleName)
                {
                    case "MAJORBBS":
                        return majorbbsHostFunctions.ExportedFunctions[functionOrdinal]();
                    case "GALGSBL":
                        return galsblHostFunctions.ExportedFunctions[functionOrdinal]();
                    default:
                        throw new Exception($"Unknown or Unimplemented Imported Module: {importedModuleName}");
                }
            });
            while (_cpu.IsRunning)
                _cpu.Tick();
        }

        /// <summary>
        ///     Allocates the user struct for the current user and stores it
        ///     so it can be referenced.
        /// </summary>
        private void AllocateUser(IMemoryCore memory)
        {
            /* From MAJORBBS.H:
             *   struct user {                 // volatile per-user info maintained        
                     int class;               //  [0,1]  class (offline, or flavor of online)  
                     int *keys;               //  [2,3,4,5]  dynamically alloc'd array of key bits 
                     int state;               //  [6,7]  state (module number in effect)       
                     int substt;              //  [8,9]  substate (for convenience of module)  
                     int lofstt;              //  state which has final lofrou() routine
                     int usetmr;              //  usage timer (for nonlive timeouts etc)
                     int minut4;              //  total minutes of use, times 4         
                     int countr;              //  general purpose counter               
                     int pfnacc;              //  profanity accumulator                 
                     unsigned long flags;     //  runtime flags                         
                     unsigned baud;           //  baud rate currently in effect         
                     int crdrat;              //  credit-consumption rate               
                     int nazapc;              //  no-activity auto-logoff counter       
                     int linlim;              //  "logged in" module loop limit         
                     struct clstab *cltptr;   //  ??  pointer to guys current class in table
                     void (*polrou)();        //  ??  pointer to current poll routine       
                     char lcstat;             //  ??  LAN chan state (IPX.H) 0=nonlan/nonhdw
                 };        
             */
            var output = new MemoryStream();
            output.Write(BitConverter.GetBytes((short)6)); //class (ACTUSR)
            output.Write(BitConverter.GetBytes((short)0)); //keys:segment
            output.Write(BitConverter.GetBytes((short)0)); //keys:offset
            output.Write(BitConverter.GetBytes((short)1)); //state (register_module always returns 1)
            output.Write(BitConverter.GetBytes((short)0)); //substt (always starts at 0)
            output.Write(BitConverter.GetBytes((short)0)); //lofstt
            output.Write(BitConverter.GetBytes((short)0)); //usetmr
            output.Write(BitConverter.GetBytes((short)0)); //minut4
            output.Write(BitConverter.GetBytes((short)0)); //countr
            output.Write(BitConverter.GetBytes((short)0)); //pfnacc
            output.Write(BitConverter.GetBytes((int)0)); //flags
            output.Write(BitConverter.GetBytes((ushort)0)); //baud
            output.Write(BitConverter.GetBytes((short)0)); //crdrat
            output.Write(BitConverter.GetBytes((short)0)); //nazapc
            output.Write(BitConverter.GetBytes((short)0)); //linlim
            output.Write(BitConverter.GetBytes((short)0)); //clsptr:segment
            output.Write(BitConverter.GetBytes((short)0)); //clsptr:offset
            output.Write(BitConverter.GetBytes((short)0)); //polrou:segment
            output.Write(BitConverter.GetBytes((short)0)); //polrou:offset
            output.Write(BitConverter.GetBytes('0')); //lcstat

            memory.AddSegment((ushort)EnumHostSegments.User);
            memory.SetArray((ushort)EnumHostSegments.User, 0, output.ToArray());
        }

        /// <summary>
        ///     Creates and Allocates the usernum global variable
        /// </summary>
        /// <param name="memory"></param>
        private void AllocateUserNum(IMemoryCore memory)
        {
            memory.AddSegment((ushort)EnumHostSegments.UserNum);
            memory.SetArray((ushort)EnumHostSegments.UserNum, 0, BitConverter.GetBytes((ushort)1));
        }

        /*
        public bool VerifyImportedFunctions()
        {
            _logger.Info("Scanning CODE segments to ensure all Imported Functions are supported by MbbsEmu...");
            var scanResult = true;
            var ordinalMajorBbs = _module.File.ImportedNameTable.First(x => x.Name == "MAJORBBS");
            foreach (var seg in _module.File.SegmentTable.Where(x => x.Flags.Contains(EnumSegmentFlags.Code)))
            {
                foreach (var relo in seg.RelocationRecords.Where(x =>
                    x.Flag == EnumRecordsFlag.IMPORTORDINAL &&
                    x.TargetTypeValueTuple.Item2 == ordinalMajorBbs.Ordinal))
                {

                    if (!_exportedFunctionDelegates["MAJORBBS"].ContainsKey(relo.TargetTypeValueTuple.Item3))
                    {
                        _logger.Error(
                            $"Module Relies on MAJORBBS Function {relo.TargetTypeValueTuple.Item3}, which is not implemented");
                        scanResult = false;
                    }
                }
            }

            return scanResult;
        }
        */
    }
}

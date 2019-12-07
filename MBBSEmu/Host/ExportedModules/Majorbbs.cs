using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.Host.ExportedModules;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.Host
{
    /// <summary>
    ///     Class which defines functions that are part of the MajorBBS/WG SDK and included in
    ///     MAJORBBS.H.
    ///
    ///     While a majority of these functions are specific to MajorBBS/WG, some are just proxies for
    ///     Borland C++ macros and are noted as such.
    /// </summary>
    public class Majorbbs : ExportedModuleBase
    {
        private readonly PointerDictionary<McvFile> _mcvFiles;
        private McvFile _currentMcvFile;
        private McvFile _previousMcvFile;

        private readonly PointerDictionary<BtrieveFile> _btrieveFiles;
        private BtrieveFile _currentBtrieveFile;
        private BtrieveFile _previousBtrieveFile;

        private int userStructOffset;

        private readonly MemoryStream outputBuffer;


        /// <summary>
        ///     Imported Functions from the REGISTER_MODULE method are saved here.
        ///
        ///     Tuple is INT16:INT16 (SEGMENT:OFFSET) of method entry
        /// </summary>
        public readonly Dictionary<string, Tuple<int, int>> ModuleRoutines;


        public Majorbbs(CpuCore cpuCore,  MbbsModule module) : base(cpuCore, module)
        {
            ModuleRoutines = new Dictionary<string, Tuple<int, int>>();
            _mcvFiles = new PointerDictionary<McvFile>();
            outputBuffer = new MemoryStream();
            _btrieveFiles = new PointerDictionary<BtrieveFile>();

            //Setup the user struct for *usrptr which holds the current user
            AllocateUser();
        }

        /// <summary>
        ///     Allocates the user struct for the current user and stores it
        ///     so it can be referenced.
        ///
        ///     TODO -- This is static for the time being, probably need to update this
        /// </summary>
        private void AllocateUser()
        {
            /* From MAJORBBS.H:
             *   struct user {                 // volatile per-user info maintained        
                     int class;               //    class (offline, or flavor of online)  
                     int *keys;               //    dynamically alloc'd array of key bits 
                     int state;               //    state (module number in effect)       
                     int substt;              //    substate (for convenience of module)  
                     int lofstt;              //    state which has final lofrou() routine
                     int usetmr;              //    usage timer (for nonlive timeouts etc)
                     int minut4;              //    total minutes of use, times 4         
                     int countr;              //    general purpose counter               
                     int pfnacc;              //    profanity accumulator                 
                     unsigned long flags;     //    runtime flags                         
                     unsigned baud;           //    baud rate currently in effect         
                     int crdrat;              //    credit-consumption rate               
                     int nazapc;              //    no-activity auto-logoff counter       
                     int linlim;              //    "logged in" module loop limit         
                     struct clstab *cltptr;   //    pointer to guys current class in table
                     void (*polrou)();        //    pointer to current poll routine       
                     char lcstat;             //    LAN chan state (IPX.H) 0=nonlan/nonhdw
                 };        
             */
            var output = new MemoryStream();
            output.Write(BitConverter.GetBytes((short)0)); //class
            output.Write(BitConverter.GetBytes((short)0)); //keys:segment
            output.Write(BitConverter.GetBytes((short)0)); //keys:offset
            output.Write(BitConverter.GetBytes((short)0)); //state
            output.Write(BitConverter.GetBytes((short)0)); //substt
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

            Memory.AddSegment((ushort)EnumHostSegments.User);
            Memory.SetArray((ushort)EnumHostSegments.User, 0, output.ToArray());
        }

        /// <summary>
        ///     Initializes the Pseudo-Random Number Generator with the given seen
        ///
        ///     Since we'll handle this internally, we'll just ignore this
        ///
        ///     Signature: void srand (unsigned int seed);
        /// </summary>
        [ExportedModule(Name = "SRAND", Ordinal = 561, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort srand() => 0;

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        /// 
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Value is 32-Bit TIME_T (AX:DX)
        /// </summary>
        [ExportedModule(Name = "TIME", Ordinal = 599, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort time()
        {
            //For now, ignore the input pointer for time_t

            var outputArray = new byte[4];
            var passedSeconds = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Array.Copy(BitConverter.GetBytes(passedSeconds), 0, outputArray, 0, 4);

            _cpu.Registers.AX = BitConverter.ToUInt16(outputArray, 2);
            _cpu.Registers.DX = BitConverter.ToUInt16(outputArray, 0);

#if DEBUG
            _logger.Info($"Passed seconds: {passedSeconds} (AX:{_cpu.Registers.AX:X4}, DX:{_cpu.Registers.DX:X4})");
#endif

            return 0;
        }

        /// <summary>
        ///     Allocate a new memory block and zeros it out
        /// 
        ///     Signature: char *alczer(unsigned nbytes);
        ///     Return: AX = Offset in Segment (host)
        ///             DX = Data Segment
        /// </summary>
        [ExportedModule(Name = "ALCZER", Ordinal = 68, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort alczer()
        {
            var size = GetParameter(0);

            //Get the current pointer
            var pointer = AllocateHostMemory(size);

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = (ushort)EnumHostSegments.MemoryRegion;

#if DEBUG
            _logger.Info($"Allocated {size} bytes starting at {pointer:X4}");
#endif

            return 0;
        }

        /// <summary>
        ///     Get's a module's name from the specified .MDF file
        /// 
        ///     Signature: char *gmdnam(char *mdfnam);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModule(Name = "GMDNAM", Ordinal = 331, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort gmdnam()
        {
            var datSegmentOffset = GetParameter(0);
            var dataSegment = GetParameter(1);
            var size = GetParameter(2);

            //Get the current pointer
            var pointer = AllocateHostMemory(size);

            //Get the Module Name from the Mdf
            var moduleName = _module.Mdf.ModuleName;

            //Sanity Check -- 
            if (moduleName.Length > size)
            {
                _logger.Warn($"Module Name \"{moduleName}\" greater than specified size {size}, truncating");
                moduleName = moduleName.Substring(0, size);
            }

            Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, pointer, Encoding.ASCII.GetBytes(moduleName));

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = (ushort)EnumHostSegments.MemoryRegion;

#if DEBUG
            _logger.Info($"Retrieved module name \"{moduleName}\" and saved it at host memory offset {(ushort)EnumHostSegments.MemoryRegion:X4}:{pointer:X4}");
#endif

            return 0;
        }

        /// <summary>
        ///     Copies the C string pointed by source into the array pointed by destination, including the terminating null character
        ///
        ///     Signature: char* strcpy(char* destination, const char* source );
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModule(Name = "STRCPY", Ordinal = 574, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort strcpy()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);
            var srcOffset = GetParameter(2);
            var srcSegment = GetParameter(3);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(srcSegment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetString(srcSegment, srcOffset)
                : _cpu.Memory.GetString(srcSegment, srcOffset));

            if (destinationSegment == (ushort)EnumHostSegments.MemoryRegion)
            {
                Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, destinationOffset, inputBuffer.ToArray());
            }
            else
            {
                _cpu.Memory.SetArray(destinationSegment, destinationOffset, inputBuffer.ToArray());
            }

#if DEBUG
            _logger.Info($"Copied {inputBuffer.Length} bytes from {srcSegment:X4}:{srcOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif

            _cpu.Registers.AX = destinationOffset;
            _cpu.Registers.DX = destinationSegment;

            return 0;
        }

        /// <summary>
        ///     Copies a string with a fixed length
        ///
        ///     Signature: stzcpy(char *dest, char *source, int nbytes);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModule(Name = "STZCPY", Ordinal = 589, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort stzcpy()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);
            var srcOffset = GetParameter(2);
            var srcSegment = GetParameter(3);
            var limit = GetParameter(4);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(srcSegment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetArray((ushort) EnumHostSegments.MemoryRegion, srcOffset, limit)
                : _cpu.Memory.GetArray(srcSegment, srcOffset, limit));

            //If the value read is less than the limit, it'll be padded with null characters
            //per the MajorBBS Development Guide
            for (var i = inputBuffer.Length; i < limit; i++)
                inputBuffer.WriteByte(0x0);


            if (destinationSegment == (ushort)EnumHostSegments.MemoryRegion)
            {
                Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, destinationOffset, inputBuffer.ToArray());
            }
            else
            {
                _cpu.Memory.SetArray(destinationSegment, destinationOffset, inputBuffer.ToArray());
            }

#if DEBUG
            _logger.Info($"Copied {inputBuffer.Length} bytes from {srcSegment:X4}:{srcOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif

            _cpu.Registers.AX = destinationOffset;
            _cpu.Registers.DX = destinationSegment;

            return 0;
        }

        /// <summary>
        ///     Registers the Module with the MajorBBS system
        ///
        ///     Signature: int register_module(struct module *mod)
        ///     Return: AX = Value of usrptr->state whenever user is 'in' this module
        /// </summary>
        [ExportedModule(Name = "REGISTER_MODULE", Ordinal = 492, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort register_module()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);

            var moduleStruct = _cpu.Memory.GetArray(destinationSegment, destinationOffset, 61);

            var relocationRecords =
                _module.File.SegmentTable.First(x => x.Ordinal == destinationSegment).RelocationRecords;

            //Description for Main Menu
            var moduleDescription = Encoding.ASCII.GetString(moduleStruct, 0, 25).Trim();
#if DEBUG
            _logger.Info($"Module Description set to {moduleDescription}");
#endif

            var moduleRoutines = new[]
                {"lonrou", "sttrou", "stsrou", "injrou", "lofrou", "huprou", "mcurou", "dlarou", "finrou"};

            for (var i = 0; i < 9; i++)
            {
                var currentOffset = 25 + (i * 4);
                var routineEntryPoint = new byte[4];
                Array.Copy(moduleStruct, currentOffset, routineEntryPoint, 0, 4);

                //If there's a Relocation record for this routine, apply it
                if (relocationRecords.Any(y => y.Offset == currentOffset))
                {
                    var routineRelocationRecord = relocationRecords.First(x => x.Offset == currentOffset);
                    Array.Copy(BitConverter.GetBytes(routineRelocationRecord.TargetTypeValueTuple.Item4), 0, routineEntryPoint, 0, 2);
                    Array.Copy(BitConverter.GetBytes(routineRelocationRecord.TargetTypeValueTuple.Item2), 0, routineEntryPoint, 2, 2);
                }
#if DEBUG
                _logger.Info($"Routine {moduleRoutines[i]} set to {BitConverter.ToUInt16(routineEntryPoint, 2):X4}:{BitConverter.ToUInt16(routineEntryPoint, 0):X4}");
#endif
            }

            //usrptr->state is the Module Number in use, as assigned by the host process
            //Because we only support 1 module running at a time right now, we just set this to one
            _cpu.Registers.AX = 1;

            return 0;
        }

        /// <summary>
        ///     Opens the specified CNF file (.MCV in runtime form)
        ///
        ///     Signature: FILE *mbkprt=opnmsg(char *fileName)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment
        /// </summary>
        [ExportedModule(Name = "OPNMSG", Ordinal = 456, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort opnmsg()
        {
            var sourceOffset = GetParameter(0);
            var sourceSegment = GetParameter(1);

            var msgFileName = sourceSegment < 0xFF
                ? Encoding.ASCII.GetString(_cpu.Memory.GetString(sourceSegment, sourceOffset))
                : Encoding.ASCII.GetString(Memory.GetString(sourceSegment, sourceOffset));

            msgFileName = msgFileName.TrimEnd('\0');

            _currentMcvFile = new McvFile(msgFileName, _module.ModulePath);

            if (_mcvFiles.Count == 0 || _mcvFiles.Values.All(x => x.FileName != msgFileName))
                _mcvFiles.Add(_mcvFiles.Count, _currentMcvFile);

#if DEBUG
            _logger.Info(
                $"Opened MSG file: {msgFileName}, assigned to {(int) EnumHostSegments.Msg:X4}:1");
#endif

            _cpu.Registers.AX = (ushort) (_mcvFiles.Count - 1);
            _cpu.Registers.DX = (ushort)EnumHostSegments.Msg;

            return 0;
        }

        /// <summary>
        ///     Retrieves a numeric option from MCV file
        ///
        ///     Signature: int numopt(int msgnum,int floor,int ceiling)
        ///     Return: AX = Value retrieved
        /// </summary>
        [ExportedModule(Name = "NUMOPT", Ordinal = 441, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort numopt()
        {
            if(_mcvFiles.Count == 0)
                throw new Exception("Attempted to read configuration value from MSG file prior to calling opnmsg()");

            var msgnum = GetParameter(0);
            var floor = GetParameter(1);
            var ceiling = GetParameter(2);

            var outputValue = _currentMcvFile.GetNumeric(msgnum);

            //Validate
            if(outputValue < floor || outputValue >  ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"Retrieved option {msgnum}  value: {outputValue}");
#endif
            _cpu.Registers.AX = (ushort) outputValue;

            return 0;
        }

        /// <summary>
        ///     Retrieves a yes/no option from an MCV file
        ///
        ///     Signature: int ynopt(int msgnum)
        ///     Return: AX = 1/Yes, 0/No
        /// </summary>
        [ExportedModule(Name = "YNOPT", Ordinal = 650, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort ynopt()
        {
            var msgnum = GetParameter(0);

            var outputValue = _currentMcvFile.GetBool(msgnum);

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            _cpu.Registers.AX = (ushort) (outputValue ? 1 : 0);

            return 0;
        }

        /// <summary>
        ///     Gets a long (32-bit) numeric option from the MCV File
        ///
        ///     Signature: long lngopt(int msgnum,long floor,long ceiling)
        ///     Return: AX = Most Significant 16-Bits
        ///             DX = Least Significant 16-Bits
        /// </summary>
        [ExportedModule(Name = "LNGOPT", Ordinal = 389, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort lngopt()
        {
            var msgnum = GetParameter(0);

            var floorLow = GetParameter(1);
            var floorHigh = GetParameter(2);

            var ceilingLow = GetParameter(3);
            var ceilingHigh = GetParameter(4);

            var floor = floorHigh << 16 | floorLow;
            var ceiling = ceilingHigh << 16 | ceilingLow;

            var outputValue = _currentMcvFile.GetLong(msgnum);

            //Validate
            if (outputValue < floor || outputValue > ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            _cpu.Registers.AX = (ushort) (outputValue & 0xFFFF0000);
            _cpu.Registers.DX = (ushort) (outputValue & 0xFFFF);

            return 0;
        }

        /// <summary>
        ///     Gets a string from an MCV file
        ///
        ///     Signature: char *string=stgopt(int msgnum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment     
        /// </summary>
        [ExportedModule(Name = "STGOPT", Ordinal = 566, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort stgopt()
        {
            var msgnum = GetParameter(0);

            var outputValue = _currentMcvFile.GetString(msgnum);

            var outputValueOffset = AllocateHostMemory((ushort) outputValue.Length);
            Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, outputValueOffset, Encoding.ASCII.GetBytes(outputValue));

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue} saved to {(int)EnumHostSegments.MemoryRegion:X4}:{outputValueOffset:X4}");
#endif

            _cpu.Registers.AX = outputValueOffset;
            _cpu.Registers.DX = (ushort)EnumHostSegments.MemoryRegion;

            return 0;
        }

        /// <summary>
        ///     Read value of CNF option (text blocks with ASCII compatible line terminators)
        ///
        ///     Functionally, as far as this helper method is concerned, there's no difference between this method and stgopt()
        /// 
        ///     Signature: char *bufadr=getasc(int msgnum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment 
        /// </summary>
        [ExportedModule(Name = "GETASC", Ordinal = 316, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort getasc()
        {
#if DEBUG
            _logger.Info($"Called, redirecting to stgopt()");
#endif
            stgopt();

            return 0;
        }

        /// <summary>
        ///     Converts a long to an ASCII string
        ///
        ///     Signature: char *l2as(long longin)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment 
        /// </summary>
        [ExportedModule(Name = "L2AS", Ordinal = 377, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort l2as()
        {
            var lowByte = GetParameter(0);
            var highByte = GetParameter(1);

            var outputValue = (highByte << 16 | lowByte) + "\0";

            var outputValueOffset = AllocateHostMemory((ushort) outputValue.Length);
            Memory.SetArray((ushort) EnumHostSegments.MemoryRegion, outputValueOffset,
                Encoding.ASCII.GetBytes(outputValue));

#if DEBUG
            _logger.Info(
                $"Received value: {outputValue}, string saved to {(ushort) EnumHostSegments.MemoryRegion:X4}:{outputValueOffset:X4}");
#endif

            _cpu.Registers.AX = outputValueOffset;
            _cpu.Registers.DX = (ushort) EnumHostSegments.MemoryRegion;

            return 0;
        }

        /// <summary>
        ///     Converts string to a long integer
        ///
        ///     Signature: long int atol(const char *str)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        [ExportedModule(Name = "ATOL", Ordinal = 77, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort atol()
        {
            var sourceOffset = GetParameter(0);
            var sourceSegment = GetParameter(1);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(sourceSegment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort)EnumHostSegments.MemoryRegion, sourceOffset)
                : _cpu.Memory.GetString(sourceSegment, sourceOffset));

            var inputValue = Encoding.ASCII.GetString(inputBuffer.ToArray());

            if (!int.TryParse(inputValue, out var outputValue))
                throw new InvalidCastException($"atol(): Unable to cast string value located at {sourceSegment:X4}:{sourceOffset:X4} to long");

#if DEBUG
            _logger.Info($"Cast {inputValue} ({sourceSegment:X4}:{sourceOffset:X4}) to long");
#endif

            _cpu.Registers.AX = (ushort) (outputValue & 0xFFFF0000);
            _cpu.Registers.DX = (ushort) (outputValue & 0xFFFF);

            return 0;
        }

        /// <summary>
        ///     Find out today's date coded as YYYYYYYMMMMDDDDD
        ///
        ///     Signature: int date=today()
        ///     Return: AX = Packed Date
        /// </summary>
        [ExportedModule(Name = "TODAY", Ordinal = 601, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort today()
        {
            //From DOSFACE.H:
            //#define dddate(mon,day,year) (((mon)<<5)+(day)+(((year)-1980)<<9))
            var packedDate = (DateTime.Now.Month << 5) + DateTime.Now.Day + (DateTime.Now.Year << 9);

#if DEBUG
            _logger.Info($"Returned packed date: {packedDate}");
#endif
            _cpu.Registers.AX = (ushort)packedDate;

            return 0;
        }

        /// <summary>
        ///     Copies Struct into another Struct (Borland C++ Implicit Function)
        ///     CX contains the number of bytes to be copied
        ///
        ///     Signature: None -- Compiler Generated
        ///     Return: None
        /// </summary>
        [ExportedModule(Name = "F_SCOPY", Ordinal = 665, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort f_scopy()
        {
            var srcOffset = GetParameter(0);
            var srcSegment = GetParameter(1);
            var destinationOffset = GetParameter(2);
            var destinationSegment = GetParameter(3);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(srcSegment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetArray((ushort)EnumHostSegments.MemoryRegion,srcOffset, _cpu.Registers.CX)
                : _cpu.Memory.GetArray(srcSegment, srcOffset, _cpu.Registers.CX));

            if (destinationSegment == (ushort)EnumHostSegments.MemoryRegion)
            {
                Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, destinationOffset, inputBuffer.ToArray());
            }
            else
            {
                _cpu.Memory.SetArray(destinationSegment, destinationOffset, inputBuffer.ToArray());
            }

#if DEBUG
            _logger.Info($"Copied {inputBuffer.Length} bytes from {srcSegment:X4}:{srcOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif
            return 0;
        }

        /// <summary>
        ///     Case ignoring string match
        ///
        ///     Signature: int match=sameas(char *stgl, char* stg2)
        ///     Returns: AX = 1 if match
        /// </summary>
        [ExportedModule(Name = "SAMEAS", Ordinal = 520, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort sameas()
        {
            var string1Offset = GetParameter(0);
            var string1Segment = GetParameter(1);
            var string2Offset = GetParameter(2);
            var string2Segment = GetParameter(3);

            var string1InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string1Segment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort) EnumHostSegments.MemoryRegion, string1Offset)
                : _cpu.Memory.GetString(string1Segment, string1Offset));

            var string1InputValue = Encoding.ASCII.GetString(string1InputBuffer.ToArray()).ToUpper();

            var string2InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string2Segment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort) EnumHostSegments.MemoryRegion, string2Offset)
                : _cpu.Memory.GetString(string2Segment, string2Offset));

            var string2InputValue = Encoding.ASCII.GetString(string2InputBuffer.ToArray()).ToUpper();

            var result = string1InputValue == string2InputValue;
#if DEBUG
            _logger.Info($"Returned {result} comparing {string1InputValue} to {string2InputValue}");
#endif
            _cpu.Registers.AX = (ushort) (result ? 1 : 0);

            return 0;
        }

        /// <summary>
        ///     Property with the User Number (Channel) of the user currently being serviced
        ///
        ///     Signature: int usrnum
        ///     Retrurns: int == User Number (Channel), always 1
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "USERNUM", Ordinal = 628, ExportedModuleType = EnumExportedModuleType.Value)]
        public ushort usernum() => 0;

        /// <summary>
        ///     Gets the online user account info
        /// 
        ///     Signature: struct usracc *uaptr=uacoff(unum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "UACOFF", Ordinal = 713, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort uacoff()
        {
            var userNumber = GetParameter(0);

            if (userNumber != 1)
                throw new Exception($"Should only ever receive a User Number of 1, value passed in: {userNumber}");


            _cpu.Registers.AX = (ushort) userStructOffset;
            _cpu.Registers.DX = (ushort) EnumHostSegments.MemoryRegion;
            return 0;
        }

        /// <summary>
        ///     Like printf(), except the converted text goes into a buffer
        ///
        ///     Signature: void prf(string)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "PRF", Ordinal = 474, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort prf()
        {
            var sourceOffset = GetParameter(0);
            var sourceSegment = GetParameter(1);

            var output = sourceSegment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort) EnumHostSegments.MemoryRegion, sourceOffset)
                : _cpu.Memory.GetString(sourceSegment, sourceOffset);

            var outputString = Encoding.ASCII.GetString(output);

            //If the supplied string has any control characters for formatting, process them
            if (outputString.CountPrintf() > 0)
            {
                var formatParameters = GetPrintfParameters(outputString, 2);
                outputString = string.Format(outputString.FormatPrintf(), formatParameters.ToArray());
            }

            outputBuffer.Write(Encoding.ASCII.GetBytes(outputString));

#if DEBUG
            _logger.Info($"Added {output.Length} bytes to the buffer");
#endif

            return 0;
        }

        /// <summary>
        ///     Send prfbuf to a channel & clear
        ///
        ///     Signature: void outprf (unum)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "OUTPRF", Ordinal = 463, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort outprf()
        {
            //TODO -- this will need to write to a destination output delegate
            Console.WriteLine(Encoding.ASCII.GetString(outputBuffer.ToArray()));
            outputBuffer.Flush();

            return 0;
        }

        /// <summary>
        ///     Deduct real credits from online acct
        ///
        ///     Signature: int enuf=dedcrd(long amount, int asmuch)
        ///     Returns: AX = 1 == Had enough, 0 == Not enough
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "DEDCRD", Ordinal = 160, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort dedcrd()
        {
            var sourceOffset = GetParameter(0);
            var lowByte = GetParameter(1);
            var highByte = GetParameter(2);

            var creditsToDeduct = (highByte << 16 | lowByte);

#if DEBUG
            _logger.Info($"Deducted {creditsToDeduct} from the current users account (unlimited)");
#endif
            return 0;
        }

        /// <summary>
        ///     Points to that channels 'user' struct
        ///     This is held in its own data segment on the host
        ///
        ///     Signature: struct user *usrptr;
        ///     Returns: int = Segment on host for User Pointer
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "USRPTR", Ordinal = 629, ExportedModuleType = EnumExportedModuleType.Reference)]
        public ushort usrptr() => (int) EnumHostSegments.User;

        /// <summary>
        ///     Like prf(), but the control string comes from an .MCV file
        ///
        ///     Signature: void prfmsg(msgnum,p1,p2, ..• ,pn);
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "PRFMSG", Ordinal = 476, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort prfmsg()
        {
            var messageNumber = GetParameter(0);

            if(!_currentMcvFile.Messages.TryGetValue(messageNumber, out var outputValue))
                throw new Exception($"prfmsg() unable to locate message number {messageNumber} in current MCV file {_currentMcvFile.FileName}");

            outputBuffer.Write(Encoding.ASCII.GetBytes(outputValue));

#if DEBUG
            _logger.Info($"Added {outputValue.Length} bytes to the buffer from message number {messageNumber}");
#endif
            return 0;
        }

        /// <summary>
        ///     Displays a message in the Audit Trail
        ///
        ///     Signature: void shocst(char *summary, char *detail, p1, p1,...,pn);
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "SHOCST", Ordinal = 550, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort shocst()
        {
            var string1Offset = GetParameter(0);
            var string1Segment = GetParameter(1);
            var string2Offset = GetParameter(2);
            var string2Segment = GetParameter(3);

            var string1InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string1Segment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort) EnumHostSegments.MemoryRegion, string1Offset)
                : _cpu.Memory.GetString(string1Segment, string1Offset));

            var string1InputValue = Encoding.ASCII.GetString(string1InputBuffer.ToArray());

            var string2InputBuffer = new MemoryStream();
            string2InputBuffer.Write(string2Segment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort) EnumHostSegments.MemoryRegion, string2Offset)
                : _cpu.Memory.GetString(string2Segment, string2Offset));

            var string2InputValue = Encoding.ASCII.GetString(string2InputBuffer.ToArray());

            //If the supplied string has any control characters for formatting, process them
            if (string2InputValue.CountPrintf() > 0)
            {
                var formatParameters = GetPrintfParameters(string2InputValue, 4);
                string2InputValue = string.Format(string2InputValue.FormatPrintf(), formatParameters.ToArray());
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.WriteLine($"AUDIT SUMMARY: {string1InputValue}");
            Console.WriteLine($"AUDIT DETAIL: {string2InputValue}");
            Console.ResetColor();

            return 0;
        }

        /// <summary>
        ///     Array of chanel codes (as displayed)
        ///
        ///     Signature: int *channel
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "CHANNEL", Ordinal = 97, ExportedModuleType = EnumExportedModuleType.Reference)]
        public ushort channel() => (int) EnumHostSegments.ChannelArray;

        /// <summary>
        ///     Post credits to the specified Users Account
        ///
        ///     signature: int addcrd(char *keyuid,char *tckstg,int real)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "ADDCRD", Ordinal = 59, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort addcrd()
        {
            var real = GetParameter(0);
            var string1Offset = GetParameter(1);
            var string1Segment = GetParameter(2);
            var string2Offset = GetParameter(3);
            var string2Segment = GetParameter(4);

            var string1InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string1Segment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort)EnumHostSegments.MemoryRegion, string1Offset)
                : _cpu.Memory.GetString(string1Segment, string1Offset));

            var string1InputValue = Encoding.ASCII.GetString(string1InputBuffer.ToArray());

            var string2InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string2Segment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort)EnumHostSegments.MemoryRegion, string2Offset)
                : _cpu.Memory.GetString(string2Segment, string2Offset));

            var string2InputValue = Encoding.ASCII.GetString(string2InputBuffer.ToArray());

#if DEBUG
            _logger.Info($"Added {string1InputValue} credits to user account {string2InputValue} (unlimited -- this function is ignored)");
#endif

            return 0;
        }

        /// <summary>
        ///     Converts an integer value to a null-terminated string using the specified base and stores the result in the array given by str
        ///
        ///     Signature: char *itoa(int value, char * str, int base)
        /// </summary>
        [ExportedModule(Name = "ITOA", Ordinal = 366, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort itoa()
        {
            var baseValue = GetParameter(0);
            var string1Offset = GetParameter(1);
            var string1Segment = GetParameter(2);
            var integerValue = GetParameter(3);

            var output = Convert.ToString(integerValue, baseValue);
            output += "\0";

            if (string1Segment == (ushort) EnumHostSegments.MemoryRegion)
            {
                Memory.SetArray((ushort) EnumHostSegments.MemoryRegion, string1Offset,
                    Encoding.ASCII.GetBytes(output));
            }
            else
            {
                _cpu.Memory.SetArray(string1Segment, string1Offset, Encoding.ASCII.GetBytes(output));
            }

#if DEBUG
            _logger.Info(
                $"Convterted integer {integerValue} to {output} (base {baseValue}) and saved it to {string1Segment:X4}:{string1Offset:X4}");
#endif
            return 0;
        }

        /// <summary>
        ///     Does the user have the specified key
        /// 
        ///     Signature: int haskey(lock)
        ///     Returns: AX = 1 == True
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "HASKEY", Ordinal = 334, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort haskey()
        {
            var lockValue = GetParameter(0);

#if DEBUG
            _logger.Info($"Returning true for lock {lockValue}");
#endif
            _cpu.Registers.AX = 1;

            return 0;
        }

        /// <summary>
        ///     Returns if the user has the key specified in an offline Security and Accounting option
        ///
        ///     Signature: int hasmkey(int msgnum)
        ///     Returns: AX = 1 == True
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "HASMKEY", Ordinal = 335, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort hasmkey()
        {
            var key = GetParameter(0);

#if DEBUG
            _logger.Info($"Returning true for key {key}");
#endif
            _cpu.Registers.AX = 1;

            return 0;
        }

        /// <summary>
        ///     Returns a pseudo-random integral number in the range between 0 and RAND_MAX.
        ///
        ///     Signature: int rand (void)
        ///     Returns: AX = 16-bit Random Number
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "RAND", Ordinal = 486, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort rand()
        {
            var randomValue = new Random(Guid.NewGuid().GetHashCode()).Next(0, short.MaxValue);

#if DEBUG
            _logger.Info($"Generated random number {randomValue} and saved it to AX");
#endif
            _cpu.Registers.AX = (ushort) randomValue;

            return 0;
        }

        /// <summary>
        ///     Returns Packed Date as a char* in 'MM/DD/YY' format
        ///
        ///     Signature: char *ascdat=ncdate(int date)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "NCDATE", Ordinal = 428, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort ncdate()
        {
            /* From DOSFACE.H:
                #define ddyear(date) ((((date)>>9)&0x007F)+1980)
                #define ddmon(date)   (((date)>>5)&0x000F)
                #define ddday(date)    ((date)    &0x001F)
             */

            var packedDate = GetParameter(0);

            var year = ((packedDate >> 9) & 0x007F) + 1980;
            var month = (packedDate >> 5) & 0x000F;
            var day = packedDate & 0x001F;

            var outputDate = $"{month:D2}/{day:D2}/{year:D2}\0";

            var outputValueOffset = AllocateHostMemory((ushort) outputDate.Length);
            Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, outputValueOffset, Encoding.ASCII.GetBytes(outputDate));

#if DEBUG
            _logger.Info($"Received value: {packedDate}, decoded string {outputDate} saved to {(ushort)EnumHostSegments.MemoryRegion:X4}:{outputValueOffset:X4}");
#endif

            _cpu.Registers.AX = outputValueOffset;
            _cpu.Registers.DX = (ushort)EnumHostSegments.MemoryRegion;
            return 0;
        }

        /// <summary>
        ///     Default Status Handler for status conditions this module is not specifically expecting
        ///
        ///     Ignored for now
        /// 
        ///     Signature: void dfsthn()
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "DFSTHN", Ordinal = 167, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort dfsthn()
        {
            return 0;
        }

        /// <summary>
        ///     Closes the Specified Message File
        ///
        ///     Signature: void clsmsg(FILE *mbkprt)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "CLSMSG", Ordinal = 119, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort clsmsg()
        {
            //We ignore this for now, and we'll just keep it open for the time being

            return 0;
        }

        /// <summary>
        ///     Register a real-time routine that needs to execute more than 1 time per second
        ///
        ///     Routines registered this way are executed at 18hz
        /// 
        ///     Signature: void rtihdlr(void (*rouptr)(void))
        /// </summary>
        [ExportedModule(Name = "RTIHDLR", Ordinal = 515, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort rtihdlr()
        {
            var routinePointerOffset = GetParameter(0);
            var routinePointerSegment = GetParameter(1);
#if DEBUG
            _logger.Info($"Registered routine {routinePointerSegment:X4}:{routinePointerOffset:X4}");
#endif
            return 0;
        }

        /// <summary>
        ///     'Kicks Off' the specified routine after the specified delay
        ///
        ///     Signature: void rtkick(int time, void *rouptr())
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "RTKICK", Ordinal = 516, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort rtkick()
        {
            var routinePointerOffset = GetParameter(0);
            var routinePointerSegment = GetParameter(1);
            var delaySeconds = GetParameter(2);

#if DEBUG
            _logger.Info($"Registered routine {routinePointerSegment:X4}:{routinePointerOffset:X4} to execute every {delaySeconds} seconds");
#endif
            return 0;
        }

        /// <summary>
        ///     Sets 'current' MCV file to the specified pointer
        ///
        ///     Signature: FILE *setmbk(mbkptr)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "SETMBK", Ordinal = 543, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort setmbk()
        {
            var mcvFileOffset = GetParameter(0);
            var mcvFileSegment = GetParameter(1);

            if(mcvFileSegment != (int)EnumHostSegments.Msg)
                throw new ArgumentException($"Specified Segment for MCV File {mcvFileSegment} does not match host MCV Segment {(int)EnumHostSegments.Msg}");

            _previousMcvFile = _currentMcvFile;
            _currentMcvFile = _mcvFiles[mcvFileOffset];

            return 0;
        }

        /// <summary>
        ///     Restore previous MCV file block ptr from before last setmbk() call
        /// 
        ///     Signature: void rstmbk()
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "RSTMBK", Ordinal = 510, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort rstmbk()
        {
            _currentMcvFile = _previousMcvFile;

            return 0;
        }

        /// <summary>
        ///     Opens a Btrieve file for I/O
        ///
        ///     Signature: BTVFILE *bbptr=opnbtv(char *filnae, int reclen)
        ///     Return: AX = Offset to File Pointer
        ///             DX = Host Btrieve Segment  
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "OPNBTV", Ordinal = 455, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort opnbtv()
        {
            var btrieveFilenameOffset = GetParameter(0);
            var btrieveFilenameSegment = GetParameter(1);
            var recordLength = GetParameter(2);

            var btrieveFilename = new MemoryStream();
            btrieveFilename.Write(btrieveFilenameSegment == (ushort)EnumHostSegments.MemoryRegion
                ? Memory.GetString((ushort)EnumHostSegments.MemoryRegion, btrieveFilenameOffset)
                : _cpu.Memory.GetString(btrieveFilenameSegment, btrieveFilenameOffset));

            var fileName = Encoding.ASCII.GetString(btrieveFilename.ToArray()).TrimEnd('\0');

            var btrieveFile = new BtrieveFile(fileName, _module.ModulePath);

            var btrieveFilePointer = _btrieveFiles.Allocate(btrieveFile);

#if DEBUG
            _logger.Info($"Opened file {fileName} and allocated it to {(ushort)EnumHostSegments.BtrieveFile:X4}:{btrieveFilePointer:X4}");
#endif
            _cpu.Registers.AX = (ushort) btrieveFilePointer;
            _cpu.Registers.DX = (ushort) EnumHostSegments.BtrieveFile;

            return 0;
        }

        /// <summary>
        ///     Used to set the Btrieve file for all subsequent database functions
        ///
        ///     Signature: void setbtv(BTVFILE *bbprt)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "SETBTV", Ordinal = 534, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort setbtv()
        {
            var btrieveFileOffset = GetParameter(0);
            var btrieveFileSegment = GetParameter(1);

            if(btrieveFileSegment != (int)EnumHostSegments.BtrieveFile)
                throw new InvalidDataException($"Invalid Btrieve File Segment provided. Actual: {btrieveFileSegment:X4} Expecting: {(int)EnumHostSegments.BtrieveFile}");

            if(_currentBtrieveFile != null)
                _previousBtrieveFile = _currentBtrieveFile;

            _currentBtrieveFile = _btrieveFiles[btrieveFileOffset];

#if DEBUG
            _logger.Info($"Setting current Btrieve file to {_currentBtrieveFile.FileName} ({btrieveFileSegment:X4}:{btrieveFileOffset:X4})");
#endif

            return 0;
        }

        /// <summary>
        ///     'Step' based Btrieve operation
        ///
        ///     Signature: int stpbtv (void *recptr, int stpopt)
        ///     Returns: AX = 1 == Record Found, 0 == Database Empty
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "STPBTV", Ordinal = 569, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort stpbtv()
        {
            if(_currentBtrieveFile == null)
                throw new FileNotFoundException("Current Btrieve file hasn't been set using SETBTV()");

            var btrieveRecordPointerOffset = GetParameter(0);
            var btrieveRecordPointerSegment = GetParameter(1);
            var stpopt = GetParameter(2);

            ushort resultCode = 0;
            switch (stpopt)
            {
                case (ushort)EnumBtrieveOperationCodes.StepFirst:
                    resultCode = _currentBtrieveFile.StepFirst();
                    break;
                case (ushort)EnumBtrieveOperationCodes.StepNext:
                    resultCode = _currentBtrieveFile.StepNext();
                    break;
                default:
                    throw new InvalidEnumArgumentException($"Unknown Btrieve Operation Code: {stpopt}");
            }

            //Set Memory Values
            if (resultCode > 0)
            {
                //See if the segment lives on the host or in the module
                if (btrieveRecordPointerSegment == (ushort)EnumHostSegments.MemoryRegion)
                {
                    Memory.SetArray((ushort)EnumHostSegments.MemoryRegion, btrieveRecordPointerOffset, _currentBtrieveFile.GetRecord());
                }
                else
                {
                    _cpu.Memory.SetArray(btrieveRecordPointerSegment, btrieveRecordPointerOffset, _currentBtrieveFile.GetRecord());
                }
            }

            //Set Register for Result
            _cpu.Registers.AX = resultCode;

#if DEBUG
            _logger.Info($"Performed Btrieve Step - Record written to {btrieveRecordPointerSegment:X4}:{btrieveRecordPointerOffset:X4}, AX: {resultCode}");
#endif
            return 0;
        }

        /// <summary>
        ///     Restores the last Btrieve data block for use
        ///
        ///     Signature: void rstbtv (void)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "RSTBTV", Ordinal = 505, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort rstbtv()
        {
            if (_previousBtrieveFile == null)
            {
#if DEBUG
                _logger.Info($"Previous Btrieve file == null, ignoring");
#endif
                return 0;
            }

            _currentBtrieveFile = _previousBtrieveFile;

#if DEBUG
            _logger.Info($"Set current Btreieve file to {_previousBtrieveFile.FileName}");
#endif
            return 0;
        }

        /// <summary>
        ///     Update the Btrieve current record
        ///
        ///     Signature: void updbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "UPDBTV", Ordinal = 621, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort updbtv()
        {
            var btrieveRecordPointerOffset = GetParameter(0);
            var btrieveRecordPointerSegment = GetParameter(1);

            //See if the segment lives on the host or in the module
            var btrieveRecord = new MemoryStream();
            btrieveRecord.Write(btrieveRecordPointerSegment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetArray((ushort) EnumHostSegments.MemoryRegion, btrieveRecordPointerOffset,
                    _currentBtrieveFile.RecordLength)
                : _cpu.Memory.GetArray(btrieveRecordPointerSegment, btrieveRecordPointerOffset,
                    _currentBtrieveFile.RecordLength));

            _currentBtrieveFile.Update(btrieveRecord.ToArray());

#if DEBUG
            _logger.Info(
                $"Updated current Btrieve record ({_currentBtrieveFile.CurrentRecordNumber}) with {btrieveRecord.Length} bytes");
#endif

            return 0;
        }

        /// <summary>
        ///     Insert new fixed-length Btrieve record
        /// 
        ///     Signature: void insbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "INSBTV", Ordinal = 351, ExportedModuleType = EnumExportedModuleType.Method)]
        public ushort insbtv()
        {
            var btrieveRecordPointerOffset = GetParameter(0);
            var btrieveRecordPointerSegment = GetParameter(1);

            //See if the segment lives on the host or in the module
            var btrieveRecord = new MemoryStream();
            btrieveRecord.Write(btrieveRecordPointerSegment == (ushort) EnumHostSegments.MemoryRegion
                ? Memory.GetArray((ushort) EnumHostSegments.MemoryRegion, btrieveRecordPointerOffset,
                    _currentBtrieveFile.RecordLength)
                : _cpu.Memory.GetArray(btrieveRecordPointerSegment, btrieveRecordPointerOffset,
                    _currentBtrieveFile.RecordLength));

            _currentBtrieveFile.Insert(btrieveRecord.ToArray());

#if DEBUG
            _logger.Info(
                $"Inserted Btrieve record at {_currentBtrieveFile.CurrentRecordNumber} with {btrieveRecord.Length} bytes");
#endif

            return 0;
        }
    }
}

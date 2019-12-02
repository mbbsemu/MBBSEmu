using MBBSEmu.CPU;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using NLog;
using System;
using System.Collections.Generic;
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
    public class Majorbbs
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly MbbsHostMemory _mbbsHostMemory;
        private readonly CpuCore _cpu;
        private readonly MbbsModule _module;

        private readonly Dictionary<int, McvFile> _mcvFiles;
        private McvFile _currentMcvFile;

        /// <summary>
        ///     Imported Functions from the REGISTER_MODULE method are saved here.
        ///
        ///     Tuple is INT16:INT16 (SEGMENT:OFFSET) of method entry
        /// </summary>
        public readonly Dictionary<string, Tuple<int, int>> ModuleRoutines;


        public Majorbbs(CpuCore cpuCore,  MbbsModule module)
        {
            _mbbsHostMemory = new MbbsHostMemory();
            _cpu = cpuCore;
            _module = module;
            ModuleRoutines = new Dictionary<string, Tuple<int, int>>();
            _mcvFiles = new Dictionary<int, McvFile>();
        }

        /// <summary>
        ///     Initializes the Pseudo-Random Number Generator with the given seen
        ///
        ///     Since we'll handle this internally, we'll just ignore this
        ///
        ///     Signature: void srand (unsigned int seed);
        /// </summary>
        [ExportedModuleFunction(Name = "SRAND", Ordinal = 561)]
        public void srand()
        {
        }

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        /// 
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Value is 32-Bit TIME_T (AX:DX)
        /// </summary>
        [ExportedModuleFunction(Name = "TIME", Ordinal = 599)]
        public void time()
        {
            //For now, ignore the input pointer for time_t
            var input1 = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var input2 = _cpu.Memory.Pop(_cpu.Registers.BP + 6);

            var outputArray = new byte[4];
            var passedSeconds = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Array.Copy(BitConverter.GetBytes(passedSeconds), 0, outputArray, 0, 4);

            _cpu.Registers.AX = BitConverter.ToUInt16(outputArray, 2);
            _cpu.Registers.DX = BitConverter.ToUInt16(outputArray, 0);

#if DEBUG
            _logger.Info($"time() passed seconds: {passedSeconds} (AX:{_cpu.Registers.AX:X4}, DX:{_cpu.Registers.DX:X4})");
#endif
        }



        /// <summary>
        ///     Allocate a new memory block and zeros it out
        /// 
        ///     Signature: char *alczer(unsigned nbytes);
        ///     Return: AX = Offset in Segment (host)
        ///             DX = Data Segment
        /// </summary>
        [ExportedModuleFunction(Name = "ALCZER", Ordinal = 68)]
        public void alczer()
        {
            var size = _cpu.Memory.Pop(_cpu.Registers.BP + 4);

            //Get the current pointer
            var pointer = _mbbsHostMemory.AllocateHostMemory(size);

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = 0xFFFF;

#if DEBUG
            _logger.Info($"alczer() allocated {size} bytes starting at {pointer:X4}");
#endif
        }

        /// <summary>
        ///     Get's a module's name from the specified .MDF file
        /// 
        ///     Signature: char *gmdnam(char *mdfnam);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModuleFunction(Name = "GMDNAM", Ordinal = 331)]
        public void gmdnam()
        {
            var datSegmentOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var dataSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var size = _cpu.Memory.Pop(_cpu.Registers.BP + 8);

            //Get the current pointer
            var pointer = _mbbsHostMemory.AllocateHostMemory(size);

            //Get the Module Name from the Mdf
            var moduleName = _module.Mdf.ModuleName;

            //Sanity Check -- 
            if (moduleName.Length > size)
            {
                _logger.Warn($"Module Name \"{moduleName}\" greater than specified size {size}, truncating");
                moduleName = moduleName.Substring(0, size);
            }

            _mbbsHostMemory.SetHostArray(pointer, Encoding.ASCII.GetBytes(moduleName));

            _cpu.Registers.AX = pointer;
            _cpu.Registers.DX = 0xFFFF;

#if DEBUG
            _logger.Info($"gmdnam() retrieved module name \"{moduleName}\" and saved it at host memory offset {pointer:X4}");
#endif
        }

        /// <summary>
        ///     Copies the C string pointed by source into the array pointed by destination, including the terminating null character
        ///
        ///     Signature: char* strcpy(char* destination, const char* source );
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModuleFunction(Name = "STRCPY", Ordinal = 574)]
        public void strcpy()
        {
            var destinationOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var destinationSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var srcOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 8);
            var srcSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 10);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(srcSegment == 0xFFFF
                ? _mbbsHostMemory.GetString(0, srcOffset)
                : _cpu.Memory.GetString(srcSegment, srcOffset));

            if (destinationSegment == 0xFFFF)
            {
                _mbbsHostMemory.SetHostArray(destinationOffset, inputBuffer.ToArray());
            }
            else
            {
                _cpu.Memory.SetArray(destinationSegment, destinationOffset, inputBuffer.ToArray());
            }

#if DEBUG
            _logger.Info($"stzcpy() copied {inputBuffer.Length} bytes from {srcSegment:X4}:{srcOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif

            _cpu.Registers.AX = destinationOffset;
            _cpu.Registers.DX = destinationSegment;
        }

        /// <summary>
        ///     Copies a string with a fixed length
        ///
        ///     Signature: stzcpy(char *dest, char *source, int nbytes);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        [ExportedModuleFunction(Name = "STZCPY", Ordinal = 589)]
        public void stzcpy()
        {
            var destinationOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var destinationSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var srcOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 8);
            var srcSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 10);
            var limit = _cpu.Memory.Pop(_cpu.Registers.BP + 12);

            var inputBuffer = new MemoryStream();
            var bytesCopied = 0;

            if (srcSegment == 0xFFFF)
            {
                for (var i = 0; i < limit; i++)
                {
                    bytesCopied++;
                    var inputByte = (byte)_mbbsHostMemory.GetHostByte(srcOffset + i);
                    inputBuffer.WriteByte(inputByte);
                    if (inputByte == 0)
                        break;
                }
            }
            else
            {
                inputBuffer.Write(_cpu.Memory.GetArray(srcSegment, srcOffset, limit));
            }

            //If the value read is less than the limit, it'll be padded with null characters
            //per the MajorBBS Development Guide
            for (var i = inputBuffer.Length; i < limit; i++)
                inputBuffer.WriteByte(0x0);


            if (destinationSegment == 0xFFFF)
            {
                _mbbsHostMemory.SetHostArray(destinationOffset, inputBuffer.ToArray());
            }
            else
            {
                _cpu.Memory.SetArray(destinationSegment, destinationOffset, inputBuffer.ToArray());
            }

#if DEBUG
            _logger.Info($"stzcpy() copied {bytesCopied} bytes from {srcSegment:X4}:{srcOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif

            _cpu.Registers.AX = destinationOffset;
            _cpu.Registers.DX = destinationSegment;
        }

        /// <summary>
        ///     Registers the Module with the MajorBBS system
        ///
        ///     Signature: int register_module(struct module *mod)
        ///     Return: AX = Value of usrptr->state whenever user is 'in' this module
        /// </summary>
        [ExportedModuleFunction(Name = "REGISTER_MODULE", Ordinal = 492)]
        public void register_module()
        {
            var destinationOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var destinationSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);

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
        }

        /// <summary>
        ///     Opens the specified CNF file (.MCV in runtime form)
        ///
        ///     Signature: FILE *mbkprt=opnmsg(char *fileName)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment
        /// </summary>
        [ExportedModuleFunction(Name = "OPNMSG", Ordinal = 456)]
        public void opnmsg()
        {
            var sourceOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var sourceSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);

            var msgFileName = sourceSegment <= 0xFF
                ? Encoding.ASCII.GetString(_cpu.Memory.GetString(sourceSegment, sourceOffset))
                : Encoding.ASCII.GetString(_mbbsHostMemory.GetString(sourceSegment, sourceOffset));

            msgFileName = msgFileName.TrimEnd('\0');

            _currentMcvFile = new McvFile(msgFileName, _module.ModulePath);

            if (_mcvFiles.Count == 0 || _mcvFiles.Values.All(x => x.FileName != msgFileName))
                _mcvFiles.Add(_mcvFiles.Count, _currentMcvFile);

#if DEBUG
            _logger.Info(
                $"opnmsg() opened MSG file: {msgFileName}, assigned to {(int) EnumHostSegments.MsgPointer:X4}:1");
#endif

            _cpu.Registers.AX = _mcvFiles.Count - 1;
            _cpu.Registers.DX = (int) EnumHostSegments.MsgPointer;
        }

        /// <summary>
        ///     Retrieves a numeric option from MCV file
        ///
        ///     Signature: int numopt(int msgnum,int floor,int ceiling)
        ///     Return: AX = Value retrieved
        /// </summary>
        [ExportedModuleFunction(Name = "NUMOPT", Ordinal = 441)]
        public void numopt()
        {
            if(_mcvFiles.Count == 0)
                throw new Exception("Attempted to read configuration value from MSG file prior to calling opnmsg()");

            var msgnum = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var floor = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var ceiling = _cpu.Memory.Pop(_cpu.Registers.BP + 8);

            var outputValue = _currentMcvFile.GetNumeric(msgnum);

            //Validate
            if(outputValue < floor || outputValue >  ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"numopt() retrieved option {msgnum}  value: {outputValue}");
#endif
            _cpu.Registers.AX = outputValue;
        }

        /// <summary>
        ///     Retrieves a yes/no option from an MCV file
        ///
        ///     Signature: int ynopt(int msgnum)
        ///     Return: AX = 1/Yes, 0/No
        /// </summary>
        [ExportedModuleFunction(Name = "YNOPT", Ordinal = 650)]
        public void ynopt()
        {
            var msgnum = _cpu.Memory.Pop(_cpu.Registers.BP + 4);

            var outputValue = _currentMcvFile.GetBool(msgnum);

#if DEBUG
            _logger.Info($"ynopt() retrieved option {msgnum} value: {outputValue}");
#endif

            _cpu.Registers.AX = outputValue ? 1 : 0;
        }

        /// <summary>
        ///     Gets a long (32-bit) numeric option from the MCV File
        ///
        ///     Signature: long lngopt(int msgnum,long floor,long ceiling)
        ///     Return: AX = Most Significant 16-Bits
        ///             DX = Least Significant 16-Bits
        /// </summary>
        [ExportedModuleFunction(Name = "LNGOPT", Ordinal = 389)]
        public void lngopt()
        {
            var msgnum =  _cpu.Memory.Pop(_cpu.Registers.BP + 4);

            var floorLow = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var floorHigh = _cpu.Memory.Pop(_cpu.Registers.BP + 8);

            var ceilingLow = _cpu.Memory.Pop(_cpu.Registers.BP + 10);
            var ceilingHigh = _cpu.Memory.Pop(_cpu.Registers.BP + 12);

            var floor = floorHigh << 16 | floorLow;
            var ceiling = ceilingHigh << 16 | ceilingLow;

            var outputValue = _currentMcvFile.GetLong(msgnum);

            //Validate
            if (outputValue < floor || outputValue > ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"lngopt() retrieved option {msgnum} value: {outputValue}");
#endif

            _cpu.Registers.AX = (int) (outputValue & 0xFFFF0000);
            _cpu.Registers.DX = outputValue & 0xFFFF;
        }

        /// <summary>
        ///     Gets a string from an MCV file
        ///
        ///     Signature: char *string=stgopt(int msgnum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment     
        /// </summary>
        [ExportedModuleFunction(Name = "STGOPT", Ordinal = 566)]
        public void stgopt()
        {
            var msgnum = _cpu.Memory.Pop(_cpu.Registers.BP + 4);

            var outputValue = _currentMcvFile.GetString(msgnum);

            var outputValueOffset = _mbbsHostMemory.AllocateHostMemory(outputValue.Length);
            _mbbsHostMemory.SetHostArray(outputValueOffset, Encoding.ASCII.GetBytes(outputValue));

#if DEBUG
            _logger.Info($"stgopt() retrieved option {msgnum} value: {outputValue} saved to {(int)EnumHostSegments.MemoryPointer:X4}:{outputValueOffset:X4}");
#endif

            _cpu.Registers.AX = outputValueOffset;
            _cpu.Registers.DX = (int)EnumHostSegments.MemoryPointer;
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
        [ExportedModuleFunction(Name = "GETASC", Ordinal = 316)]
        public void getasc()
        {
#if DEBUG
            _logger.Info($"getasc() called, redirecting to stgopt()");
#endif
            stgopt();
        }

        /// <summary>
        ///     Converts a long to an ASCII string
        ///
        ///     Signature: char *l2as(long longin)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment 
        /// </summary>
        [ExportedModuleFunction(Name = "L2AS", Ordinal = 377)]
        public void l2as()
        {
            var lowByte = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var highByte = _cpu.Memory.Pop(_cpu.Registers.BP + 8);

            var outputValue = (highByte << 16 | lowByte) + "\0";

            var outputValueOffset = _mbbsHostMemory.AllocateHostMemory(outputValue.Length);
            _mbbsHostMemory.SetHostArray(outputValueOffset, Encoding.ASCII.GetBytes(outputValue));

#if DEBUG
            _logger.Info($"l2as() received value: {outputValue}, string saved to {(int)EnumHostSegments.MemoryPointer:X4}:{outputValueOffset:X4}");
#endif

            _cpu.Registers.AX = outputValueOffset;
            _cpu.Registers.DX = (int)EnumHostSegments.MemoryPointer;
        }

        /// <summary>
        ///     Converts string to a long integer
        ///
        ///     Signature: long int atol(const char *str)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        [ExportedModuleFunction(Name = "ATOL", Ordinal = 77)]
        public void atol()
        {
            var sourceOffset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var sourceSegment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);

            var inputBuffer = new MemoryStream();

            inputBuffer.Write(sourceSegment == 0xFFFF
                ? _mbbsHostMemory.GetString(0, sourceOffset)
                : _cpu.Memory.GetString(sourceSegment, sourceOffset));

            var inputValue = Encoding.ASCII.GetString(inputBuffer.ToArray());

            if (!int.TryParse(inputValue, out var outputValue))
                throw new InvalidCastException($"atol(): Unable to cast string value located at {sourceSegment:X4}:{sourceOffset:X4} to long");

#if DEBUG
            _logger.Info($"atol() cast {inputBuffer} ({sourceSegment:X4}:{sourceOffset:X4}) to long");
#endif

            _cpu.Registers.AX = (int)(outputValue & 0xFFFF0000);
            _cpu.Registers.DX = outputValue & 0xFFFF;
        }

        /// <summary>
        ///     Find out today's date coded as YYYYYYYMMMMDDDDD
        ///
        ///     Signature: int date=today()
        ///     Return: AX = Packed Date
        /// </summary>
        [ExportedModuleFunction(Name = "TODAY", Ordinal = 601)]
        public void today()
        {
            //From DOSFACE.H:
            //#define dddate(mon,day,year) (((mon)<<5)+(day)+(((year)-1980)<<9))

            var packedDate = (DateTime.Now.Month << 5) + DateTime.Now.Day + (DateTime.Now.Year << 9);

#if DEBUG
            _logger.Info($"today() returned packed date: {packedDate}");
#endif
            _cpu.Registers.AX = (ushort)packedDate;
        }

        /// <summary>
        ///     Copies Struct into another Struct (Borland C++ Implicit Function)
        /// </summary>
        public void f_scopy()
        {

        }

        /// <summary>
        ///     Case ignoring string match
        ///
        ///     Signature: int match=sameas(char *stgl, char* stg2)
        ///     Returns: AX = 1 if match
        /// </summary>
        [ExportedModuleFunction(Name = "SAMEAS", Ordinal = 520)]
        public void sameas()
        {
            var string1Offset = _cpu.Memory.Pop(_cpu.Registers.BP + 4);
            var string1Segment = _cpu.Memory.Pop(_cpu.Registers.BP + 6);
            var string2Offset = _cpu.Memory.Pop(_cpu.Registers.BP + 8);
            var string2Segment = _cpu.Memory.Pop(_cpu.Registers.BP + 10);

            var string1InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string1Segment == 0xFFFF
                ? _mbbsHostMemory.GetString(0, string1Offset)
                : _cpu.Memory.GetString(string1Segment, string1Offset));

            var string1InputValue = Encoding.ASCII.GetString(string1InputBuffer.ToArray()).ToUpper();

            var string2InputBuffer = new MemoryStream();
            string1InputBuffer.Write(string2Segment == 0xFFFF
                ? _mbbsHostMemory.GetString(0, string2Offset)
                : _cpu.Memory.GetString(string2Segment, string2Offset));

            var string2InputValue = Encoding.ASCII.GetString(string2InputBuffer.ToArray()).ToUpper();

            var result = string1InputValue == string2InputValue;
#if DEBUG
            _logger.Info($"sameas() returned {result} comparing {string1InputValue} to {string2InputValue}");
#endif
            _cpu.Registers.AX = result ? 1 : 0;
        }
    }
}

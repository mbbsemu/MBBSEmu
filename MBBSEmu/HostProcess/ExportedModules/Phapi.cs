using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    public class Phapi : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFD;

        internal Phapi(ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {

            if (offsetsOnly)
            {
                var methodPointer = new IntPtr16(0xFFFC, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.Data;
            }

            switch (ordinal)
            {
                case 49:
                    DosRealIntr();
                    break;
                case 16:
                    DosAllocRealSeg();
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in PHAPI: {ordinal}");
            }

            return null;
        }

        public void SetRegisters(CpuRegisters registers)
        {
            Registers = registers;
        }

        public void SetState(ushort channelNumber)
        {
            return;
        }

        public void UpdateSession(ushort channelNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Create a data selector corresponding to an executable (code) selector.
        ///
        ///     Because we don't do any relocation, just pass back the segment identifier
        /// </summary>
        private void DosCreatedAlias()
        {
            var destinationPointer = GetParameterPointer(0);
            var segmentIdentifier = GetParameter(2);

            Module.Memory.SetWord(destinationPointer, segmentIdentifier);

            RealignStack(6);

            Registers.AX = 0;
        }

        /// <summary>
        ///     Because MajorBBS and Worldgroup run in Protected-Mode, this method allows modules to
        ///     allocate memory in Real-Mode to be used by destination of Interrupt calls.
        ///
        ///     MBBSEmu exposes the entire 16-bit (4GB) address space to the module, so there is no difference
        ///     between Real-Mode and Protected-Mode
        ///
        ///     Signature: USHORT DosAllocRealSeg(ULONG size, PUSHORT parap, PSEL selp);
        ///                 typedef unsigned short _far *PSEL;
        ///                 typedef unsigned short _far *PUSHORT;
        /// </summary>
        private void DosAllocRealSeg()
        {
            var selector = GetParameterPointer(0);
            var segment = GetParameterPointer(2);
            var segmentSize = GetParameterLong(4);

            var allocatedMemorySegment = Module.Memory.AllocateRealModeSegment();

            Module.Memory.SetWord(selector, allocatedMemorySegment.Segment);
            Module.Memory.SetWord(segment, allocatedMemorySegment.Segment);

            _logger.Info($"Allocating {segmentSize} in Real-Mode memory at {allocatedMemorySegment}");

            RealignStack(12);

            Registers.AX = 0;

        }

        /// <summary>
        ///     Generates a Real Mode Interrupt from Protected Mode
        ///
        ///     Allows for calling/passing information to TSR's running in real mode. This is used by some MajorBBS modules
        ///     for directly accessing the Btrieve Driver
        /// </summary>
        private void DosRealIntr()
        {
            var interruptNumber = GetParameter(0);
            var registerPointer = GetParameterPointer(1);
            var reserved = GetParameter(3); //Must Be Zero
            var wordCount = GetParameter(4); //Count of word arguments on stack

            var regs = new Regs16Struct(Module.Memory.GetArray(registerPointer, Regs16Struct.Size).ToArray());

            switch (interruptNumber)
            {
                case 0x21: //INT 21h Call

                    switch (regs.AX >> 8) //INT 21h Function
                    {
                        case 0x35: //Get Interrupt Vector
                            {
                                switch ((byte)(regs.AX & 0xFF))
                                {
                                    case 0x7B: //Btrieve Vector
                                        {
                                            //Modules will use this vector to see if Btrieve is running
                                            regs.BX = 0x33;
                                            break;
                                        }
                                    default:
                                        throw new Exception($"Unknown Interrupt Vector: {(byte)(regs.AX & 0xFF):X2}h");
                                }

                                break;
                            }
                        default:
                            throw new Exception($"Unknown INT 21h Function: {regs.AX >> 8:X2}h");
                    }
                    break;
                case 0x7B: //Btrieve Interrupt
                    {
                        var btvda = new BtvdatStruct(Module.Memory.GetArray(regs.DS, 0, BtvdatStruct.Size));

                        switch ((EnumBtrieveOperationCodes)btvda.funcno)
                        {
                            case EnumBtrieveOperationCodes.Open:
                                {
                                    //Get the File Name to oPen
                                    var fileName = Encoding.ASCII.GetString(Module.Memory.GetString(btvda.keyseg, 0, true));
                                    var btvFile = new BtrieveFileProcessor(_fileFinder, Module.ModulePath, fileName, _configuration.BtrieveCacheSize);

                                    //Setup Pointers
                                    var btvFileStructPointer = new IntPtr16(btvda.posblkseg, btvda.posblkoff);
                                    var btvFileNamePointer =
                                        Module.Memory.AllocateVariable($"{fileName}-NAME", (ushort)(fileName.Length + 1));
                                    var btvDataPointer = Module.Memory.AllocateVariable($"{fileName}-RECORD", (ushort) btvFile.RecordLength);

                                    var newBtvStruct = new BtvFileStruct
                                    {
                                        filenam = btvFileNamePointer,
                                        reclen = (ushort) btvFile.RecordLength,
                                        data = btvDataPointer
                                    };
                                    BtrieveSaveProcessor(btvFileStructPointer, btvFile);
                                    Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.Data);
                                    Module.Memory.SetArray(btvFileNamePointer, Encoding.ASCII.GetBytes(fileName + '\0'));

                                    //Set the active Btrieve file (BB) to the now open file
                                    Module.Memory.SetPointer("BB", btvFileStructPointer);

#if DEBUG
                                    _logger.Info($"Opened file {fileName} and allocated it to {btvFileStructPointer}");
#endif

                                    Registers.AX = 0;
                                    break;
                                }
                            case EnumBtrieveOperationCodes.Stat:
                                {
                                    var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
                                    var btvStats = new BtvstatfbStruct
                                    {
                                        fs = new BtvfilespecStruct()
                                        {
                                            numofr = (uint) currentBtrieveFile.GetRecordCount(),
                                            numofx = (ushort) currentBtrieveFile.Keys.Count,
                                            pagsiz = (ushort) currentBtrieveFile.PageLength,
                                            reclen = (ushort) currentBtrieveFile.RecordLength
                                        }
                                    };

                                    var definedKeys = currentBtrieveFile.Keys.Values.SelectMany(k => k.Segments).Select(k =>
                                        new BtvkeyspecStruct()
                                        {
                                            flags = (ushort)k.Attributes,
                                            keylen = k.Length,
                                            keypos = k.Position,
                                            numofk = k.Number
                                        }).ToList();
                                    btvStats.keyspec = definedKeys.ToArray();

                                    Module.Memory.SetArray(btvda.databufsegment, btvda.databufoffset, btvStats.Data);
                                    Registers.AX = 0;
                                    break;
                                }
                            case EnumBtrieveOperationCodes.SetOwner: //Ignore
                                Registers.AX = 0;
                                break;
                            default:
                                throw new Exception($"Unknown Btrieve Operation: {(EnumBtrieveOperationCodes)btvda.funcno}");
                        }

                        break;
                    }
                default:
                    throw new Exception($"Unhandled Interrupt: {interruptNumber:X2}h");
            }

            Module.Memory.SetArray(registerPointer, regs.Data);
        }

    }
}

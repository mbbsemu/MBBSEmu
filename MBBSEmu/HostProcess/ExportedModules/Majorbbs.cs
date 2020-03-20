using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Iced.Intel;
using MBBSEmu.Btrieve.Enums;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions that are part of the MajorBBS/WG SDK and included in
    ///     MAJORBBS.H.
    ///
    ///     While a majority of these functions are specific to MajorBBS/WG, some are just proxies for
    ///     Borland C++ macros and are noted as such.
    /// </summary>
    public class Majorbbs : ExportedModuleBase, IExportedModule
    {
        public IntPtr16 GlobalCommandHandler;

        private IntPtr16 _currentMcvFile;
        private readonly Stack<IntPtr16> _previousMcvFile;

        private IntPtr16 _currentBtrieveFile;
        private readonly Stack<IntPtr16> _previousBtrieveFile;

        private readonly List<IntPtr16> _margvPointers;
        private readonly List<IntPtr16> _margnPointers;
        private int _inputCurrentCommand;

        private int _outputBufferPosition;

        private const ushort VOLATILE_DATA_SIZE = 0x3FFF;
        private const ushort NUMBER_OF_CHANNELS = 0x4;

        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFF;

        public Majorbbs(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module,
            channelDictionary)
        {
            _outputBufferPosition = 0;
            _margvPointers = new List<IntPtr16>();
            _margnPointers = new List<IntPtr16>();
            _inputCurrentCommand = 0;
            _previousMcvFile = new Stack<IntPtr16>(10);
            _previousBtrieveFile = new Stack<IntPtr16>(10);
            

            //Setup Memory for Variables
            Module.Memory.AllocateVariable("PRFBUF", 0x2000, true); //Output buffer, 8kb
            Module.Memory.AllocateVariable("PRFPTR", 0x4);
            Module.Memory.AllocateVariable("INPUT", 0xFF); //256 Byte Maximum user Input
            Module.Memory.AllocateVariable("USER", (User.Size * NUMBER_OF_CHANNELS), true);
            Module.Memory.AllocateVariable("*USRPTR", 0x4); //pointer to the current USER record
            Module.Memory.AllocateVariable("STATUS", 0x2); //ushort Status
            Module.Memory.AllocateVariable("CHANNEL", 0x1FE); //255 channels * 2 bytes
            Module.Memory.AllocateVariable("MARGC", 0x2);
            Module.Memory.AllocateVariable("MARGN", 0x3FC);
            Module.Memory.AllocateVariable("MARGV", 0x3FC);
            Module.Memory.AllocateVariable("INPLEN", 0x2);
            Module.Memory.AllocateVariable("USERNUM", 0x2);
            var usraccPointer = Module.Memory.AllocateVariable("USRACC", (UserAccount.Size * NUMBER_OF_CHANNELS), true);
            var usaptrPointer = Module.Memory.AllocateVariable("USAPTR", 0x4);
            Module.Memory.SetArray(usaptrPointer, usraccPointer.ToSpan());

            var usrExtPointer = Module.Memory.AllocateVariable("EXTUSR", (ExtUser.Size * NUMBER_OF_CHANNELS), true);
            var extPtrPointer = Module.Memory.AllocateVariable("EXTPTR", 0x4);
            Module.Memory.SetArray(extPtrPointer, usrExtPointer.ToSpan());

            Module.Memory.AllocateVariable("OTHUAP", 0x04, true); //Pointer to OTHER user
            Module.Memory.AllocateVariable("OTHEXP", 0x04, true); //Pointer to OTHER user
            var ntermsPointer = Module.Memory.AllocateVariable("NTERMS", 0x2); //ushort number of lines
            Module.Memory.SetWord(ntermsPointer, 0x04); //4 channels for now

            Module.Memory.AllocateVariable("OTHUSN", 0x2); //Set by onsys() or instat()
            Module.Memory.AllocateVariable("OTHUSP", 0x4, true);
            Module.Memory.AllocateVariable("NXTCMD", 0x4); //Holds Pointer to the "next command"
            Module.Memory.AllocateVariable("NMODS", 0x2); //Number of Modules Installed
            Module.Memory.SetWord(Module.Memory.GetVariable("NMODS"), 0x1); //set this to 1 for now
            var modulePointer = Module.Memory.AllocateVariable("MODULE", 0x4); //Pointer to Registered Module
            var modulePointerPointer = Module.Memory.AllocateVariable("MODULE-POINTER", 0x4); //Pointer to the Module Pointer
            Module.Memory.SetArray(modulePointerPointer, modulePointer.ToSpan());
            Module.Memory.AllocateVariable("UACOFF", 0x4);
            Module.Memory.AllocateVariable("EXTOFF", 0x4);
            Module.Memory.AllocateVariable("VDAPTR", 0x4);
            Module.Memory.AllocateVariable("VDATMP", VOLATILE_DATA_SIZE, true);
            Module.Memory.SetWord(Module.Memory.AllocateVariable("VDASIZ", 0x2), VOLATILE_DATA_SIZE);
            Module.Memory.AllocateVariable("BBSTTL", 0x32, true); //50 bytes for BBS Title
            Module.Memory.AllocateVariable("COMPANY", 0x32, true); //50 bytes for company name
            Module.Memory.AllocateVariable("ADDRES1", 0x32, true); //50 bytes for address line 1
            Module.Memory.AllocateVariable("ADDRES2", 0x32, true); //50 bytes for address line 2
            Module.Memory.AllocateVariable("DATAPH", 0x20, true); // 32 bytes for phone #
            Module.Memory.AllocateVariable("LIVEPH", 0x20, true); // 32 bytes for phone #
            

            var ctypePointer = Module.Memory.AllocateVariable("CTYPE", 0x101);

            /*
             * Fill CTYPE array with standard characters
             * Calls to CTYPE functions such as ISALPHA(), etc.
             * are replaced with relocation records to this memory block.
             * It'll be a relocation+1 if __USELOCALES__ is defined at compile time.
             * This means we just need character 48 ("0") to be at position 49 in the array.
             * We accomplish this by ++ the offset of the pointer first then writing. Every byte
             * should be shifted by 1, thus giving us a proper value lookup post-relocation
             */
            for (var i = 0; i < 256; i++)
            {
                Module.Memory.SetByte(ctypePointer.Segment, (ushort)(ctypePointer.Offset + i + 1), (byte)i);
            }

            //Setup Generic User Database
            var btvFileStructPointer = Module.Memory.AllocateVariable(null, BtvFileStruct.Size);
            var btvFileName = Module.Memory.AllocateVariable(null, 11); //BBSGEN.DAT\0
            var btvDataPointer = Module.Memory.AllocateVariable(null, 8192); //GENSIZ -- Defined in MAJORBBS.H

            var newBtvStruct = new BtvFileStruct { filenam = btvFileName, reclen = 8192, data = btvDataPointer };
            BtrievePointerDictionaryNew.Add(btvFileStructPointer, new BtrieveFile("BBSGEN.DAT", Directory.GetCurrentDirectory(), 8192));
            Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.ToSpan());

            var genBBPointer = Module.Memory.AllocateVariable("GENBB", 0x4); //Pointer to GENBB BTRIEVE File
            Module.Memory.SetArray(genBBPointer, btvFileStructPointer.ToSpan());
        }

        public void SetRegisters(CpuRegisters registers)
        {
            Registers = registers;
        }

        /// <summary>
        ///     Sets State for the Module Registers and the current Channel Number that's running
        /// </summary>
        /// <param name="channelNumber"></param>
        /// <param name="numberOfModules"></param>
        public void SetState(ushort channelNumber)
        {
            ChannelNumber = channelNumber;

            _previousMcvFile.Clear();
            _previousBtrieveFile.Clear();

            //Bail if it's max value, not processing any input or status
            if (channelNumber == ushort.MaxValue)
                return;

            if (!Module.Memory.TryGetVariable($"VDA-{channelNumber}", out var vdaChannelPointer))
                vdaChannelPointer = Module.Memory.AllocateVariable($"VDA-{channelNumber}", VOLATILE_DATA_SIZE);

            Module.Memory.SetArray(Module.Memory.GetVariable("VDAPTR"), vdaChannelPointer.ToSpan());

            Module.Memory.SetWord(Module.Memory.GetVariable("USERNUM"), channelNumber);
            Module.Memory.SetWord(Module.Memory.GetVariable("STATUS"), ChannelDictionary[channelNumber].Status);

            var userBasePointer = Module.Memory.GetVariable("USER");
            var currentUserPointer = new IntPtr16(userBasePointer.ToSpan());
            currentUserPointer.Offset += (ushort)(User.Size * channelNumber);

            //Update User Array and Update Pointer to point to this user
            Module.Memory.SetArray(currentUserPointer, ChannelDictionary[channelNumber].UsrPtr.ToSpan());
            Module.Memory.SetArray(Module.Memory.GetVariable("*USRPTR"), currentUserPointer.ToSpan());

            var userAccBasePointer = Module.Memory.GetVariable("USRACC");
            var currentUserAccPointer = new IntPtr16(userAccBasePointer.ToSpan());
            currentUserAccPointer.Offset += (ushort)(UserAccount.Size * channelNumber);
            Module.Memory.SetArray(currentUserAccPointer, ChannelDictionary[channelNumber].UsrAcc.ToSpan());
            Module.Memory.SetArray(Module.Memory.GetVariable("USAPTR"), currentUserAccPointer.ToSpan());

            var userExtAccBasePointer = Module.Memory.GetVariable("EXTUSR");
            var currentExtUserAccPointer = new IntPtr16(userExtAccBasePointer.ToSpan());
            currentExtUserAccPointer.Offset += (ushort)(ExtUser.Size * channelNumber);
            Module.Memory.SetArray(currentExtUserAccPointer, ChannelDictionary[channelNumber].ExtUsrAcc.ToSpan());
            Module.Memory.SetArray(Module.Memory.GetVariable("EXTPTR"), currentExtUserAccPointer.ToSpan());

            //Write Blank Input
            var inputMemory = Module.Memory.GetVariable("INPUT");
            Module.Memory.SetByte(inputMemory.Segment, inputMemory.Offset, 0x0);

            //Processing Channel Input
            if (ChannelDictionary[channelNumber].Status == 3)
            {
                ChannelDictionary[channelNumber].parsin();

                Module.Memory.SetWord(Module.Memory.GetVariable("MARGC"), (ushort)ChannelDictionary[channelNumber].mArgCount);
                Module.Memory.SetWord(Module.Memory.GetVariable("INPLEN"), (ushort)ChannelDictionary[channelNumber].InputCommand.Length);
                Module.Memory.SetArray(inputMemory.Segment, inputMemory.Offset, ChannelDictionary[channelNumber].InputCommand);

#if DEBUG
                _logger.Info($"Input Length {ChannelDictionary[channelNumber].InputCommand.Length} written to {inputMemory.Segment:X4}:{inputMemory.Offset}");
#endif
                var margnPointer = Module.Memory.GetVariable("MARGN");
                var margvPointer = Module.Memory.GetVariable("MARGV");

                //Build Command Word Pointers
                for (var i = 0; i < ChannelDictionary[channelNumber].mArgCount; i++)
                {
                    var currentMargnPointer = new IntPtr16(inputMemory.Segment,
                        (ushort)(inputMemory.Offset + ChannelDictionary[channelNumber].mArgn[i]));
                    var currentMargVPointer = new IntPtr16(inputMemory.Segment,
                        (ushort)(inputMemory.Offset + ChannelDictionary[channelNumber].mArgv[i]));

                    Module.Memory.SetArray(margnPointer.Segment, (ushort)(margnPointer.Offset + (i * 4)),
                        currentMargnPointer.ToSpan());

                    Module.Memory.SetArray(margvPointer.Segment, (ushort)(margvPointer.Offset + (i * 4)),
                        currentMargVPointer.ToSpan());

#if DEBUG
                    _logger.Info($"Command {i} {currentMargVPointer:X4}->{currentMargnPointer:X4}: {Encoding.ASCII.GetString(Module.Memory.GetString(currentMargnPointer))}");
#endif
                }
            }
        }

        /// <summary>
        ///     Updates the Session with any information from memory before execution finishes
        /// </summary>
        /// <param name="channel"></param>
        public void UpdateSession(ushort channel)
        {
            //If the status wasn't set internally, then set it to the default 5
            ChannelDictionary[ChannelNumber].Status = !ChannelDictionary[ChannelNumber].StatusChange
                ? (ushort)5
                : Module.Memory.GetWord(Module.Memory.GetVariable("STATUS"));

            var userPointer = Module.Memory.GetVariable("USER");

            ChannelDictionary[ChannelNumber].UsrPtr.FromSpan(Module.Memory.GetArray(userPointer.Segment, (ushort)(userPointer.Offset + (User.Size * channel)), 41));

#if DEBUG
            _logger.Info($"{channel}->state == {ChannelDictionary[ChannelNumber].UsrPtr.State}");
            _logger.Info($"{channel}->substt == {ChannelDictionary[ChannelNumber].UsrPtr.Substt}");
#endif
        }

        /// <summary>
        ///     Invokes method by the specified ordinal using a Jump Table
        /// </summary>
        /// <param name="ordinal"></param>
        /// <param name="offsetsOnly"></param>
        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            switch (ordinal)
            {
                case 628:
                    return usernum;
                case 629:
                    return usrptr;
                case 97:
                    return channel;
                case 565:
                    return status;
                case 475:
                    return prfbuf;
                case 437:
                    return nterms;
                case 625:
                    return user;
                case 637:
                    return vdaptr;
                case 401:
                    return margc;
                case 477:
                    return prfptr;
                case 350:
                    return input;
                case 349:
                    return inplen;
                case 402:
                    return margn;
                case 403:
                    return margv;
                case 16:
                    return _exitbuf;
                case 17:
                    return _exitfopen;
                case 18:
                    return _exitopen;
                case 624:
                    return usaptr;
                case 757:
                    return genbb;
                case 459:
                    return othusn;
                case 434:
                    return nmods;
                case 417:
                    return module;
                case 11:
                    return _ctype;
                case 640:
                    return vdatmp;
                case 83:
                    return bbsttl;
                case 136:
                    return company;
                case 61:
                    return addres1;
                case 62:
                    return addres2;
                case 153:
                    return dataph;
                case 387:
                    return liveph;
                case 639:
                    return vdasiz;
                case 460:
                    return othusp;
                case 458:
                    return othuap;
                case 826:
                    return othexp;
                case 825:
                    return extptr;
            }

            if (offsetsOnly)
            {
                var methodPointer = new IntPtr16(Segment, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.ToSpan();
            }

            switch (ordinal)
            {
                //Ignored Ones
                case 561: //srand() handled internally
                case 614: //unfrez -- unlocks video memory, ignored
                case 174: //DSAIRP
                case 189: //ENAIRP
                    break;
                case 442:
                    nxtcmd();
                    break;
                case 599:
                    time();
                    break;
                case 68:
                    alczer();
                    break;
                case 331:
                    gmdnam();
                    break;
                case 574:
                    strcpy();
                    break;
                case 589:
                    stzcpy();
                    break;
                case 492:
                    register_module();
                    break;
                case 456:
                    opnmsg();
                    break;
                case 441:
                    numopt();
                    break;
                case 650:
                    ynopt();
                    break;
                case 389:
                    lngopt();
                    break;
                case 566:
                    stgopt();
                    break;
                case 316:
                    getasc();
                    break;
                case 377:
                    l2as();
                    break;
                case 77:
                    atol();
                    break;
                case 601:
                    today();
                    break;
                case 665:
                    f_scopy();
                    break;
                case 520:
                    sameas();
                    break;
                case 713:
                    uacoff();
                    break;
                case 474:
                    prf();
                    break;
                case 463:
                    outprf();
                    break;
                case 160:
                    dedcrd();
                    break;
                case 476:
                    prfmsg();
                    break;
                case 550:
                    shocst();
                    break;
                case 59:
                    addcrd();
                    break;
                case 366:
                    itoa();
                    break;
                case 334:
                    haskey();
                    break;
                case 335:
                    hasmkey();
                    break;
                case 486:
                    rand();
                    break;
                case 428:
                    ncdate();
                    break;
                case 167:
                    dfsthn();
                    break;
                case 119:
                    clsmsg();
                    break;
                case 515:
                    rtihdlr();
                    break;
                case 516:
                    rtkick();
                    break;
                case 543:
                    setmbk();
                    break;
                case 510:
                    rstmbk();
                    break;
                case 455:
                    opnbtv();
                    break;
                case 534:
                    setbtv();
                    break;
                case 569:
                    stpbtv();
                    break;
                case 505:
                    rstbtv();
                    break;
                case 621:
                    updbtv();
                    break;
                case 351:
                    insbtv();
                    break;
                case 488:
                    rdedcrd();
                    break;
                case 559:
                    spr();
                    break;
                case 659:
                    f_lxmul();
                    break;
                case 654:
                    f_ldiv();
                    break;
                case 113:
                    clrprf();
                    break;
                case 65:
                    alcmem();
                    break;
                case 643:
                    vsprintf();
                    break;
                case 1189:
                    scnmdf();
                    break;
                case 435:
                    now();
                    break;
                case 657:
                    f_lumod();
                    break;
                case 544:
                    setmem();
                    break;
                case 582:
                    strncpy();
                    break;
                case 494:
                    register_textvar();
                    break;
                case 997:
                case 444:
                    obtbtvl();
                    break;
                case 158:
                    dclvda();
                    break;
                case 636:
                    vdaoff();
                    break;
                case 87:
                    bgncnc();
                    break;
                case 522:
                    sameto();
                    break;
                case 122:
                    cncchr();
                    break;
                case 225:
                    f_open();
                    break;
                case 205:
                    f_close();
                    break;
                case 560:
                    sprintf();
                    break;
                case 694:
                    fnd1st();
                    break;
                case 229:
                    f_read();
                    break;
                case 312:
                    f_write();
                    break;
                case 617:
                    unlink();
                    break;
                case 578:
                    strlen();
                    break;
                case 603:
                    tolower();
                    break;
                case 604:
                    toupper();
                    break;
                case 496:
                    rename();
                    break;
                case 511:
                    rstrin();
                    break;
                case 580:
                    strncat();
                    break;
                case 411:
                    memset();
                    break;
                case 326:
                    getmsg();
                    break;
                case 562:
                    sscanf();
                    break;
                case 355:
                    intdos();
                    break;
                case 786:
                    prfmlt();
                    break;
                case 783:
                    clrmlt();
                    break;
                case 330:
                    globalcmd();
                    break;
                case 94:
                    catastro();
                    break;
                case 210:
                    fgets();
                    break;
                case 571:
                    strcat();
                    break;
                case 226:
                    f_printf();
                    break;
                case 266:
                    fseek();
                    break;
                case 430:
                    nctime();
                    break;
                case 572:
                    strchr();
                    break;
                case 467:
                    parsin();
                    break;
                case 420:
                    movemem();
                    break;
                case 267:
                    ftell();
                    break;
                case 352:
                    instat();
                    break;
                case 521:
                    samein();
                    break;
                case 553:
                    skpwht();
                    break;
                case 405:
                    mdfgets();
                    break;
                case 181:
                    echon();
                    break;
                case 587:
                    strupr();
                    break;
                case 584:
                    strstr();
                    break;
                case 164:
                    depad();
                    break;
                case 357:
                    invbtv();
                    break;
                case 261:
                    fsdroom();
                    break;
                case 1101:
                    stpbtvl();
                    break;
                case 573:
                    strcmp();
                    break;
                case 151:
                    curusr();
                    break;
                case 658:
                    f_lxlsh();
                    break;
                case 91:
                    byenow();
                    break;
                case 785:
                    outmlt();
                    break;
                case 622:
                    updvbtv();
                    break;
                case 959:
                    stlcpy();
                    break;
                case 568:
                    stop_polling();
                    break;
                case 85:
                    begin_polling();
                    break;
                case 712:
                    stpans();
                    break;
                case 648:
                    xlttxv();
                    break;
                case 787: //pmlt is just mult-lingual prf, so we just call prf
                    prf();
                    break;
                case 485:
                    qrybtv();
                    break;
                case 53:
                    absbtv();
                    break;
                case 313:
                    gabbtv();
                    break;
                case 585:
                    strtok();
                    break;
                case 1125:
                    fputs();
                    break;
                case 429:
                    ncedat();
                    break;
                case 487:
                    rawmsg();
                    break;
                case 107:
                    chropt();
                    break;
                case 208:
                case 19:
                    fgetc();
                    break;
                case 576:
                    stricmp();
                    break;
                case 400:
                    galmalloc();
                    break;
                case 615:
                    fungetc();
                    break;
                case 230:
                    galfree();
                    break;
                case 227:
                    f_putc();
                    break;
                case 879:
                    alcblok();
                    break;
                case 880:
                    ptrblok();
                    break;
                case 827:
                    extoff();
                    break;
                case 231:
                    frzseg();
                    break;
                case 541:
                    setjmp();
                    break;
                case 191:
                    endcnc();
                    break;
                case 393:
                    longjmp();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in MAJORBBS: {ordinal}");
            }

            return null;
        }

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        /// 
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Value is 32-Bit TIME_T (AX:DX)
        /// </summary>
        private void time()
        {
            //For now, ignore the input pointer for time_t

            var outputArray = new byte[4];
            var passedSeconds = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Array.Copy(BitConverter.GetBytes(passedSeconds), 0, outputArray, 0, 4);

            Registers.AX = BitConverter.ToUInt16(outputArray, 2);
            Registers.DX = BitConverter.ToUInt16(outputArray, 0);

#if DEBUG
            _logger.Info($"Passed seconds: {passedSeconds} (AX:{Registers.AX:X4}, DX:{Registers.DX:X4})");
#endif
        }

        /// <summary>
        ///     Allocate a new memory block and zeros it out on the host
        /// 
        ///     Signature: char *alczer(unsigned nbytes);
        ///     Return: AX = Offset in Segment (host)
        ///             DX = Data Segment
        /// </summary>
        private void alczer()
        {
            var size = GetParameter(0);

            var allocatedMemory = Module.Memory.AllocateVariable(null, size);

#if DEBUG
            _logger.Info($"Allocated {size} bytes starting at {allocatedMemory}");
#endif

            Registers.AX = allocatedMemory.Offset;
            Registers.DX = allocatedMemory.Segment;
        }

        /// <summary>
        ///     Allocates Memory
        ///
        ///     Functionally, for our purposes, the same as alczer
        /// </summary>
        /// <returns></returns>
        private void alcmem() => alczer();

        /// <summary>
        ///     Get's a module's name from the specified .MDF file
        /// 
        ///     Signature: char *gmdnam(char *mdfnam);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        private void gmdnam()
        {
            //Points to the module MDF file, but we'll assume it's the same one loaded on startup
            var dataSegmentPointer = GetParameterPointer(0);

            //Get the Module Name from the Mdf
            var moduleName = Module.Mdf.ModuleName + "\0";

            //Only needs to be set once
            if (!Module.Memory.TryGetVariable("GMDNAM", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable("GMDNAM", (ushort)moduleName.Length);

            //Set Memory
            Module.Memory.SetArray(variablePointer, Encoding.Default.GetBytes(moduleName));
#if DEBUG
            _logger.Info(
                $"Retrieved Module Name \"{moduleName}\" and saved it at host memory offset {variablePointer}");
#endif

            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Copies the C string pointed by source into the array pointed by destination, including the terminating null character
        ///
        ///     Signature: char* strcpy(char* destination, const char* source );
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        private void strcpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);

            var inputBuffer = Module.Memory.GetString(sourcePointer);
            Module.Memory.SetArray(destinationPointer, inputBuffer);

#if DEBUG
            _logger.Info($"Copied {inputBuffer.Length} bytes from {sourcePointer} to {destinationPointer} -> {Encoding.ASCII.GetString(inputBuffer)}");
#endif

            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
        }

        /// <summary>
        ///     Copies a string with a fixed length
        ///
        ///     Signature: stzcpy(char *dest, char *source, int nbytes);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        private void stzcpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);
            var limit = GetParameter(4);

            using var inputBuffer = new MemoryStream();
            var potentialString = Module.Memory.GetArray(sourcePointer, limit);
            for (var i = 0; i < limit; i++)
            {
                if (potentialString[i] == 0x0)

                    break;
                inputBuffer.WriteByte(potentialString[i]);
            }

            //If the value read is less than the limit, it'll be padded with null characters
            //per the MajorBBS Development Guide
            for (var i = inputBuffer.Length; i < limit; i++)
                inputBuffer.WriteByte(0x0);

            Module.Memory.SetArray(destinationPointer, inputBuffer.ToArray());

#if DEBUG
            _logger.Info($"Copied \"{Encoding.ASCII.GetString(inputBuffer.ToArray())}\" ({inputBuffer.Length} bytes) from {sourcePointer} to {destinationPointer}");
#endif
            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
        }

        /// <summary>
        ///     Registers the Module with the MajorBBS system
        ///
        ///     Signature: int register_module(struct module *mod)
        ///     Return: AX = Value of usrptr->state whenever user is 'in' this module
        /// </summary>
        private void register_module()
        {
            var destinationPointer = GetParameterPointer(0);
            Module.Memory.SetArray(Module.Memory.GetVariable("MODULE"), destinationPointer.ToSpan());

            var moduleStruct = Module.Memory.GetArray(destinationPointer, 61);

            //Description for Main Menu
            var moduleDescription = Encoding.Default.GetString(moduleStruct.ToArray(), 0, 25);
            Module.ModuleDescription = moduleDescription;
#if DEBUG
            _logger.Info($"MODULE pointer ({Module.Memory.GetVariable("MODULE")}) set to {destinationPointer}");
            _logger.Info($"Module Description set to {moduleDescription}");
#endif

            var moduleRoutines = new[]
                {"lonrou", "sttrou", "stsrou", "injrou", "lofrou", "huprou", "mcurou", "dlarou", "finrou"};

            for (var i = 0; i < 9; i++)
            {
                var currentOffset = (ushort)(25 + (i * 4));

                var routineEntryPoint = new byte[4];
                Array.Copy(moduleStruct.ToArray(), currentOffset, routineEntryPoint, 0, 4);

                //Setup the Entry Points in the Module
                Module.EntryPoints[moduleRoutines[i]] = new IntPtr16(BitConverter.ToUInt16(routineEntryPoint, 2), BitConverter.ToUInt16(routineEntryPoint, 0));

#if DEBUG
                _logger.Info(
                    $"Routine {moduleRoutines[i]} set to {BitConverter.ToUInt16(routineEntryPoint, 2):X4}:{BitConverter.ToUInt16(routineEntryPoint, 0):X4}");
#endif
            }

            //usrptr->state is the Module Number in use, as assigned by the host process
            Registers.AX = (ushort)Module.StateCode;
        }

        /// <summary>
        ///     Opens the specified CNF file (.MCV in runtime form)
        ///
        ///     Signature: FILE *mbkprt=opnmsg(char *fileName)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment
        /// </summary>
        private void opnmsg()
        {
            var sourcePointer = GetParameterPointer(0);

            var msgFileName = Encoding.Default.GetString(Module.Memory.GetString(sourcePointer, true));

            //If the MCV file doesn't exist, but the MSG does -- we need to build the MCV
            if (!File.Exists($"{Module.ModulePath}{msgFileName}") &&
                File.Exists($"{Module.ModulePath}{msgFileName.Replace("mcv", "msg", StringComparison.InvariantCultureIgnoreCase)}"))
                new MsgFile(Module.ModulePath,
                    msgFileName.Replace(".mcv", string.Empty, StringComparison.InvariantCultureIgnoreCase));

            var offset = McvPointerDictionary.Allocate(new McvFile(msgFileName, Module.ModulePath));

            if (_currentMcvFile != null)
                _previousMcvFile.Push(_currentMcvFile);

            //Open just sets whatever the currently active MCV file is -- overwriting whatever was there
            _currentMcvFile = new IntPtr16(ushort.MaxValue, (ushort)offset);

#if DEBUG
            _logger.Info(
                $"Opened MSG file: {msgFileName}, assigned to {ushort.MaxValue:X4}:{offset:X4}");
#endif
            Registers.AX = (ushort)offset;
            Registers.DX = ushort.MaxValue;
        }

        /// <summary>
        ///     Retrieves a numeric option from MCV file
        ///
        ///     Signature: int numopt(int msgnum,int floor,int ceiling)
        ///     Return: AX = Value retrieved
        /// </summary>
        private void numopt()
        {
            var msgnum = GetParameter(0);
            var floor = (short)GetParameter(1);
            var ceiling = GetParameter(2);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetNumeric(msgnum);

            //Validate
            if (outputValue < floor || outputValue > ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            Registers.AX = (ushort)outputValue;
        }

        /// <summary>
        ///     Retrieves a yes/no option from an MCV file
        ///
        ///     Signature: int ynopt(int msgnum)
        ///     Return: AX = 1/Yes, 0/No
        /// </summary>
        private void ynopt()
        {
            var msgnum = GetParameter(0);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetBool(msgnum);

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            Registers.AX = (ushort)(outputValue ? 1 : 0);
        }

        /// <summary>
        ///     Gets a long (32-bit) numeric option from the MCV File
        ///
        ///     Signature: long lngopt(int msgnum,long floor,long ceiling)
        ///     Return: AX = Most Significant 16-Bits
        ///             DX = Least Significant 16-Bits
        /// </summary>
        private void lngopt()
        {
            var msgnum = GetParameter(0);

            var floorLow = GetParameter(1);
            var floorHigh = GetParameter(2);

            var ceilingLow = GetParameter(3);
            var ceilingHigh = GetParameter(4);

            var floor = floorHigh << 16 | floorLow;
            var ceiling = ceilingHigh << 16 | ceilingLow;

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetLong(msgnum);

            //Validate
            if (outputValue < floor || outputValue > ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            Registers.DX = (ushort)(outputValue >> 16);
            Registers.AX = (ushort)(outputValue & 0xFFFF);
        }

        /// <summary>
        ///     Gets a string from an MCV file
        ///
        ///     Signature: char *string=stgopt(int msgnum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment     
        /// </summary>
        private void stgopt()
        {
            var msgnum = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"STGOPT_{_currentMcvFile.Offset}_{msgnum}", out var variablePointer))
            {
                var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

                variablePointer = base.Module.Memory.AllocateVariable($"STGOPT_{_currentMcvFile.Offset}_{msgnum}", (ushort)outputValue.Length);

                //Set Value in Memory
                Module.Memory.SetArray(variablePointer, outputValue);

#if DEBUG
                _logger.Info(
                    $"Retrieved option {msgnum} string value: {outputValue.Length} bytes saved to {variablePointer}");
#endif
            }
            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
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
        private void getasc()
        {
#if DEBUG
            _logger.Info($"Called, redirecting to stgopt()");
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
        private void l2as()
        {
            var lowByte = GetParameter(0);
            var highByte = GetParameter(1);

            var outputValue = $"{highByte << 16 | lowByte}\0";

            if (!Module.Memory.TryGetVariable($"L2AS", out var variablePointer))
                //Pre-allocate space for the maximum number of characters for a ulong
                variablePointer = Module.Memory.AllocateVariable("L2AS", 0xFF);

            Module.Memory.SetArray(variablePointer, Encoding.Default.GetBytes(outputValue));

#if DEBUG
            _logger.Info(
                $"Received value: {outputValue}, string saved to {variablePointer}");
#endif

            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Converts string to a long integer
        ///
        ///     Signature: long int atol(const char *str)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        private void atol()
        {
            var sourcePointer = GetParameterPointer(0);
            var stringToLong = Module.Memory.GetString(sourcePointer, true);


            if (!int.TryParse(Encoding.Default.GetString(stringToLong), out var outputValue))
            {
                /*
                 * Unsuccessful parsing returns a 0 value
                 * More info: http://www.cplusplus.com/reference/cstdlib/atol/
                 */
#if DEBUG
                _logger.Warn($"atol(): Unable to cast string value located at {sourcePointer} to long");
#endif
                Registers.AX = 0;
                Registers.DX = 0;
                Registers.F.SetFlag(EnumFlags.CF);
                return;
            }

#if DEBUG
            _logger.Info($"Cast {Encoding.Default.GetString(stringToLong)} ({sourcePointer}) to long");
#endif

            Registers.DX = (ushort)(outputValue >> 16);
            Registers.AX = (ushort)(outputValue & 0xFFFF);
            Registers.F.ClearFlag(EnumFlags.CF);
        }

        /// <summary>
        ///     Find out today's date coded as YYYYYYYMMMMDDDDD
        ///
        ///     Signature: int date=today()
        ///     Return: AX = Packed Date
        /// </summary>
        private void today()
        {
            //From DOSFACE.H:
            //#define dddate(mon,day,year) (((mon)<<5)+(day)+(((year)-1980)<<9))
            var packedDate = (DateTime.Now.Month << 5) + DateTime.Now.Day + ((DateTime.Now.Year - 1980) << 9);

#if DEBUG
            _logger.Info($"Returned packed date: {packedDate}");
#endif

            Registers.AX = (ushort)packedDate;
        }

        /// <summary>
        ///     Copies Struct into another Struct (Borland C++ Implicit Function)
        ///     CX contains the number of bytes to be copied
        ///
        ///     Signature: None -- Compiler Generated
        ///     Return: None
        /// </summary>
        private void f_scopy()
        {
            var sourcePointer = GetParameterPointer(0);
            var destinationPointer = GetParameterPointer(2);

            var sourceString = Module.Memory.GetArray(sourcePointer, Registers.CX);

            Module.Memory.SetArray(destinationPointer, sourceString);

#if DEBUG
            _logger.Info($"Copied {sourceString.Length} bytes from {sourcePointer} to {destinationPointer}");
#endif

            //Because this is an ASM routine, we have to manually fix the stack
            var previousBP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 1));
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 3));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 5));
            //Set stack back to entry state, minus parameters
            Registers.SP += 14;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousBP);
            Registers.SP -= 2;
            Registers.BP = Registers.SP;
        }

        /// <summary>
        ///     Case ignoring string match
        ///
        ///     Signature: int match=sameas(char *stgl, char* stg2)
        ///     Returns: AX = 1 if match
        /// </summary>
        private void sameas()
        {
            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);

            var string1 = Encoding.ASCII.GetString(Module.Memory.GetString(string1Pointer, true)).ToUpper();
            var string2 = Encoding.ASCII.GetString(Module.Memory.GetString(string2Pointer, true)).ToUpper();

            //Quick Check
            if (string1.Length != string2.Length)
            {
                Registers.AX = 0;

#if DEBUG
                _logger.Info($"Returned FALSE comparing {string1} ({string1Pointer}) to {string2} ({string2Pointer}) (Length mismatch)");
#endif
                return;
            }

            var result = true;
            //Deep Check -- at this point we know they're the same length
            for (var i = 0; i < string1.Length; i++)
            {
                if (string1[i] == string2[i])
                    continue;

                result = false;
                break;
            }

#if DEBUG
            _logger.Info($"Returned {result} comparing {string1} ({string1Pointer}) to {string2} ({string2Pointer})");
#endif

            Registers.AX = (ushort)(result ? 1 : 0);
        }

        /// <summary>
        ///     Property with the User Number (Channel) of the user currently being serviced
        ///
        ///     Signature: int usrnum
        ///     Retrurns: int == User Number (Channel)
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usernum => base.Module.Memory.GetVariable("USERNUM").ToSpan();

        /// <summary>
        ///     Gets the online user account info
        /// 
        ///     Signature: struct usracc *uaptr=uacoff(unum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        private void uacoff()
        {
            var userNumber = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"USRACC-{userNumber}", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable($"USRACC-{userNumber}", UserAccount.Size);

            //If user isnt online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var uacoffPointer = Module.Memory.GetVariable("UACOFF");
            Module.Memory.SetArray(uacoffPointer, variablePointer.ToSpan());

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.UsrAcc.ToSpan());


            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Like printf(), except the converted text goes into a buffer
        ///
        ///     Signature: void prf(string)
        /// </summary>
        /// <returns></returns>
        private void prf()
        {
            var sourcePointer = GetParameterPointer(0);

            var output = Module.Memory.GetString(sourcePointer, true);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 2);

            var pointer = Module.Memory.GetVariable("PRFBUF");
            Module.Memory.SetArray(pointer.Segment, (ushort)(_outputBufferPosition + pointer.Offset), formattedMessage);
            _outputBufferPosition += formattedMessage.Length;

#if DEBUG
            _logger.Info($"Added {formattedMessage.Length} bytes to the buffer");
#endif
        }


        /// <summary>
        ///     Resets prf buffer
        /// </summary>
        /// <returns></returns>
        private void clrprf()
        {
            _outputBufferPosition = 0;

#if DEBUG
            _logger.Info("Reset Output Buffer");
#endif
        }

        /// <summary>
        ///     Send prfbuf to a channel & clear
        ///
        ///     Signature: void outprf (unum)
        /// </summary>
        /// <returns></returns>
        private void outprf()
        {
            var userChannel = GetParameter(0);
            var pointer = Module.Memory.GetVariable("PRFBUF");

            var outputBuffer = Module.Memory.GetArray(pointer, (ushort)_outputBufferPosition).ToArray();

            if (Module.TextVariables.Count > 0)
            {
                var newBuffer = ProcessTextVariables(outputBuffer);
                ChannelDictionary[userChannel].SendToClient(newBuffer.ToArray());

#if DEBUG
                _logger.Info($"Sent {newBuffer.Length} bytes to Channel {userChannel}");
#endif
            }
            else
            {
                ChannelDictionary[userChannel].SendToClient(outputBuffer);
#if DEBUG
                _logger.Info($"Sent {outputBuffer.Length} bytes to Channel {userChannel}");
#endif
            }

            //Zero It Back out
            for (var i = 0; i < _outputBufferPosition; i++)
            {
                Module.Memory.SetByte(pointer.Segment, (ushort)(pointer.Offset + i), 0x0);
            }

            _outputBufferPosition = 0;
        }

        /// <summary>
        ///     Deduct real credits from online acct
        ///
        ///     Signature: int enuf=dedcrd(long amount, int asmuch)
        ///     Returns: AX = 1 == Had enough, 0 == Not enough
        /// </summary>
        /// <returns></returns>
        private void dedcrd()
        {
            var sourceOffset = GetParameter(0);
            var lowByte = GetParameter(1);
            var highByte = GetParameter(2);

            var creditsToDeduct = (highByte << 16 | lowByte);

#if DEBUG
            _logger.Info($"Deducted {creditsToDeduct} from the current users account (unlimited)");
#endif

            Registers.AX = 1;
        }

        /// <summary>
        ///     Points to that channels 'user' struct
        ///
        ///     Signature: struct user *usrptr;
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usrptr => Module.Memory.GetVariable("*USRPTR").ToSpan();

        /// <summary>
        ///     Like prf(), but the control string comes from an .MCV file
        ///
        ///     Signature: void prfmsg(msgnum,p1,p2, ..• ,pn);
        /// </summary>
        /// <returns></returns>
        private void prfmsg()
        {
            var messageNumber = GetParameter(0);

            if (!McvPointerDictionary[_currentMcvFile.Offset].Messages.TryGetValue(messageNumber, out var outputMessage))
            {
                _logger.Warn($"prfmsg() unable to locate message number {messageNumber} in current MCV file {McvPointerDictionary[_currentMcvFile.Offset].FileName}");
                return;
            }

            var formattedMessage = FormatPrintf(outputMessage, 1);

            var variablePointer = Module.Memory.GetVariable("PRFBUF");

            Module.Memory.SetArray(variablePointer.Segment, (ushort)(variablePointer.Offset + _outputBufferPosition), formattedMessage);
            _outputBufferPosition += formattedMessage.Length;

#if DEBUG
            _logger.Info($"Added {formattedMessage.Length} bytes to the buffer from message number {messageNumber}");
#endif
        }

        /// <summary>
        ///     Displays a message in the Audit Trail
        ///
        ///     Signature: void shocst(char *summary, char *detail, p1, p1,...,pn);
        /// </summary>
        /// <returns></returns>
        private void shocst()
        {
            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);

            var stringSummary = Module.Memory.GetString(string1Pointer);
            var stringDetail = FormatPrintf(Module.Memory.GetString(string2Pointer), 4);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.WriteLine($"AUDIT SUMMARY: {Encoding.ASCII.GetString(stringSummary)}");
            Console.WriteLine($"AUDIT DETAIL: {Encoding.ASCII.GetString(stringDetail)}");
            Console.ResetColor();
        }

        /// <summary>
        ///     Array of chanel codes (as displayed)
        ///
        ///     Signature: int *channel
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> channel
        {
            get
            {
                var pointer = Module.Memory.GetVariable("CHANNEL");

                //Update the Channel Array with the latest channel/user info
                var arrayOutput = new byte[0x1FE];
                ushort channelsWritten = 0;
                foreach (var k in ChannelDictionary.Keys)
                {
                    Array.Copy(BitConverter.GetBytes((ushort)k), 0, arrayOutput, k + (2 * channelsWritten++), 2);
                }
                Module.Memory.SetArray(pointer, arrayOutput);
                return pointer.ToSpan();
            }
        }

        /// <summary>
        ///     Post credits to the specified Users Account
        ///
        ///     signature: int addcrd(char *keyuid,char *tckstg,int real)
        /// </summary>
        /// <returns></returns>
        private void addcrd()
        {
            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);
            var real = GetParameter(4);

            var string1 = Module.Memory.GetString(string1Pointer);
            var string2 = Module.Memory.GetString(string2Pointer);

#if DEBUG
            _logger.Info($"Added {Encoding.Default.GetString(string2)} credits to user account {Encoding.Default.GetString(string1)} (unlimited -- this function is ignored)");
#endif
        }

        /// <summary>
        ///     Converts an integer value to a null-terminated string using the specified base and stores the result in the array given by str
        ///
        ///     Signature: char *itoa(int value, char * str, int base)
        /// </summary>
        private void itoa()
        {
            var integerValue = GetParameter(0);
            var string1Pointer = GetParameterPointer(1);
            var baseValue = GetParameter(3);

            var output = Convert.ToString((short)integerValue, baseValue);
            output += "\0";

            Module.Memory.SetArray(string1Pointer, Encoding.Default.GetBytes(output));

#if DEBUG
            _logger.Info(
                $"Convterted integer {integerValue} to {output} (base {baseValue}) and saved it to {string1Pointer}");
#endif
        }

        /// <summary>
        ///     Does the user have the specified key
        /// 
        ///     Signature: int haskey(char *lock)
        ///     Returns: AX = 1 == True
        /// </summary>
        /// <returns></returns>
        private void haskey()
        {
            var lockNamePointer = GetParameterPointer(0);
            var lockNameBytes = Module.Memory.GetString(lockNamePointer, true);

#if DEBUG
            _logger.Info($"Returning TRUE for Haskey({Encoding.ASCII.GetString(lockNameBytes)})");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Returns if the user has the key specified in an offline Security and Accounting option
        ///
        ///     Signature: int hasmkey(int msgnum)
        ///     Returns: AX = 1 == True
        /// </summary>
        /// <returns></returns>
        private void hasmkey()
        {
            var key = GetParameter(0);
            Registers.AX = 1;
        }

        /// <summary>
        ///     Returns a pseudo-random integral number in the range between 0 and RAND_MAX.
        ///
        ///     Signature: int rand (void)
        ///     Returns: AX = 16-bit Random Number
        /// </summary>
        /// <returns></returns>
        private void rand()
        {
            var randomValue = new Random(Guid.NewGuid().GetHashCode()).Next(1, short.MaxValue);

#if DEBUG
            _logger.Info($"Generated random number {randomValue} and saved it to AX");
#endif
            Registers.AX = (ushort)randomValue;
        }

        /// <summary>
        ///     Returns Packed Date as a char* in 'MM/DD/YY' format
        ///
        ///     Signature: char *ascdat=ncdate(int date)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        private void ncdate()
        {
            /* From DOSFACE.H:
#define ddyear(date) ((((date)>>9)&0x007F)+1980)
#define ddmon(date)   (((date)>>5)&0x000F)
#define ddday(date)    ((date)    &0x001F)
             */

            var packedDate = GetParameter(0);

            //Pack the Date
            var year = ((packedDate >> 9) & 0x007F) + 1980;
            var month = (packedDate >> 5) & 0x000F;
            var day = packedDate & 0x001F;
            var outputDate = $"{month:D2}/{day:D2}/{year % 100}\0";

            if (!Module.Memory.TryGetVariable("NCDATE", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable("NCDATE", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset, Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Info($"Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
#endif
            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Returns Packed Date as a char* in 'DD-MMM-YY' format
        ///
        ///     Signature: char *ascdat=ncdate(int date)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        private void ncedat()
        {
            var packedDate = GetParameter(0);

            //Pack the Date
            var year = ((packedDate >> 9) & 0x007F) + 1980;
            var month = (packedDate >> 5) & 0x000F;
            var day = packedDate & 0x001F;
            var outputDate = $"{day:D2}/{month:D2}/{year % 100}\0";

            if (!Module.Memory.TryGetVariable("NCEDAT", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable("NCEDAT", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset, Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Info($"Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
#endif
            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Default Status Handler for status conditions this module is not specifically expecting
        ///
        ///     Ignored for now
        /// 
        ///     Signature: void dfsthn()
        /// </summary>
        /// <returns></returns>
        private void dfsthn()
        {

        }

        /// <summary>
        ///     Closes the Specified Message File
        ///
        ///     Signature: void clsmsg(FILE *mbkprt)
        /// </summary>
        /// <returns></returns>
        private void clsmsg()
        {
            var filePointer = GetParameterPointer(0);

#if DEBUG
            _logger.Info($"Closing MCV File: {filePointer}");
#endif

            McvPointerDictionary.Remove(filePointer.Offset);
            _currentMcvFile = null;
        }

        /// <summary>
        ///     Register a real-time routine that needs to execute more than 1 time per second
        ///
        ///     Routines registered this way are executed at 18hz
        /// 
        ///     Signature: void rtihdlr(void (*rouptr)(void))
        /// </summary>
        private void rtihdlr()
        {
            var routinePointerOffset = GetParameter(0);
            var routinePointerSegment = GetParameter(1);

            var routine = new RealTimeRoutine(routinePointerSegment, routinePointerOffset);
            var routineNumber = Module.RtihdlrRoutines.Allocate(routine);
            Module.EntryPoints.Add($"RTIHDLR-{routineNumber}", routine);
#if DEBUG
            _logger.Info($"Registered routine {routinePointerSegment:X4}:{routinePointerOffset:X4}");
#endif
        }

        /// <summary>
        ///     'Kicks Off' the specified routine after the specified delay
        ///
        ///     Signature: void rtkick(int time, void *rouptr())
        /// </summary>
        /// <returns></returns>
        private void rtkick()
        {
            var delaySeconds = GetParameter(0);
            var routinePointer = GetParameterPointer(1);

            var routine = new RealTimeRoutine(routinePointer.Segment, routinePointer.Offset, delaySeconds);
            var routineNumber = Module.RtkickRoutines.Allocate(routine);

            Module.EntryPoints.Add($"RTKICK-{routineNumber}", routine);

#if DEBUG
            _logger.Info($"Registered routine {routinePointer} to execute every {delaySeconds} seconds");
#endif
        }

        /// <summary>
        ///     Sets 'current' MCV file to the specified pointer
        ///
        ///     Signature: FILE *setmbk(mbkptr)
        /// </summary>
        /// <returns></returns>
        private void setmbk()
        {
            var mcvFilePointer = GetParameterPointer(0);

            if (mcvFilePointer.Segment != ushort.MaxValue && !McvPointerDictionary.ContainsKey(mcvFilePointer.Offset))
                throw new ArgumentException($"Invalid MCV File Pointer: {mcvFilePointer}");

            //If there's an MVC currently set, push it to the queue
            if (_currentMcvFile != null)
            {
                _previousMcvFile.Push(new IntPtr16(_currentMcvFile.ToSpan()));
#if DEBUG
                _logger.Info("Enqueue Previous MCV File: {0} (Pointer: {1})", McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile);
#endif
            }

            _currentMcvFile = mcvFilePointer;

#if DEBUG
            _logger.Info("Set Current MCV File: {0} (Pointer: {1})", McvPointerDictionary[_currentMcvFile.Offset].FileName, mcvFilePointer);
#endif
        }

        /// <summary>
        ///     Restore previous MCV file block ptr from before last setmbk() call
        /// 
        ///     Signature: void rstmbk()
        /// </summary>
        /// <returns></returns>
        private void rstmbk()
        {
            _currentMcvFile = _previousMcvFile.Pop();
#if DEBUG
            _logger.Info($"Reset Current MCV to {McvPointerDictionary[_currentMcvFile.Offset].FileName} ({_currentMcvFile}) (Queue Depth: {_previousMcvFile.Count})");
#endif
        }

        /// <summary>
        ///     Opens a Btrieve file for I/O
        ///
        ///     Signature: BTVFILE *bbptr=opnbtv(char *filnae, int reclen)
        ///     Return: AX = Offset to File Pointer
        ///             DX = Host Btrieve Segment  
        /// </summary>
        /// <returns></returns>
        private void opnbtv()
        {
            var btrieveFilenamePointer = GetParameterPointer(0);
            var maxRecordLength = GetParameter(2);
            var btrieveFilename = Module.Memory.GetString(btrieveFilenamePointer, true);
            var fileName = Encoding.ASCII.GetString(btrieveFilename);
            var btrieveFile = new BtrieveFile(fileName, Module.ModulePath, maxRecordLength);

            //Setup Pointers
            var btvFileStructPointer = Module.Memory.AllocateVariable(null, BtvFileStruct.Size);
            var btvFileNamePointer = Module.Memory.AllocateVariable(null, (ushort)(btrieveFilename.Length + 1));
            var btvDataPointer = Module.Memory.AllocateVariable(null, maxRecordLength);

            var newBtvStruct = new BtvFileStruct { filenam = btvFileNamePointer, reclen = maxRecordLength, data = btvDataPointer };
            BtrievePointerDictionaryNew.Add(btvFileStructPointer, btrieveFile);
            Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.ToSpan());
            Module.Memory.SetArray(btvFileNamePointer, btrieveFilename);
            _currentBtrieveFile = btvFileStructPointer;

#if DEBUG
            _logger.Info($"Opened file {fileName} and allocated it to {btvFileStructPointer}");
#endif
            Registers.AX = btvFileStructPointer.Offset;
            Registers.DX = btvFileStructPointer.Segment;
        }

        /// <summary>
        ///     Used to set the Btrieve file for all subsequent database functions
        ///
        ///     Signature: void setbtv(BTVFILE *bbprt)
        /// </summary>
        /// <returns></returns>
        private void setbtv()
        {
            var btrieveFilePointer = GetParameterPointer(0);

            if (_currentBtrieveFile != null)
                _previousBtrieveFile.Push(new IntPtr16(_currentBtrieveFile.ToSpan()));

            _currentBtrieveFile = btrieveFilePointer;

#if DEBUG
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));
            var btvFileName = Encoding.ASCII.GetString(Module.Memory.GetString(btvStruct.filenam));
            _logger.Info($"Setting current Btrieve file to {btvFileName} ({btrieveFilePointer})");
#endif
        }

        /// <summary>
        ///     'Step' based Btrieve operation
        ///
        ///     Signature: int stpbtv (void *recptr, int stpopt)
        ///     Returns: AX = 1 == Record Found, 0 == Database Empty
        /// </summary>
        /// <returns></returns>
        private void stpbtv()
        {
            if (_currentBtrieveFile == null)
                throw new FileNotFoundException("Current Btrieve file hasn't been set using SETBTV()");

            var btrieveRecordPointer = GetParameterPointer(0);
            var stpopt = GetParameter(2);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];

            ushort resultCode = 0;
            switch (stpopt)
            {
                case (ushort)EnumBtrieveOperationCodes.StepFirst:
                    resultCode = currentBtrieveFile.StepFirst();
                    break;
                case (ushort)EnumBtrieveOperationCodes.StepNext:
                    resultCode = currentBtrieveFile.StepNext();
                    break;
                default:
                    throw new InvalidEnumArgumentException($"Unknown Btrieve Operation Code: {stpopt}");
            }


            Registers.AX = resultCode;

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));

            //If there's a record, always save it to the btrieve file struct
            if (resultCode == 1)
                Module.Memory.SetArray(btvStruct.data, currentBtrieveFile.GetRecord());
#if DEBUG
            _logger.Info($"Performed Btrieve Step - Record written to {btvStruct.data}, AX: {resultCode}");
#endif

            //If a record pointer was passed in AND the result code isn't 0
            if (!btrieveRecordPointer.Equals(IntPtr16.Empty) && resultCode > 0)
            {
                switch (resultCode)
                {
                    case 1:
                        Module.Memory.SetArray(btrieveRecordPointer, currentBtrieveFile.GetRecord());
                        break;
                }
            }

#if DEBUG
            _logger.Info($"Performed Btrieve Step {(EnumBtrieveOperationCodes)stpopt}, AX: {resultCode}");
#endif
        }

        /// <summary>
        ///     Restores the last Btrieve data block for use
        ///
        ///     Signature: void rstbtv (void)
        /// </summary>
        /// <returns></returns>
        private void rstbtv()
        {
            if (_previousBtrieveFile.Count == 0)
            {
                _logger.Warn($"Previous Btrieve file == null, ignoring");
                return;
            }

            _currentBtrieveFile = _previousBtrieveFile.Pop();

#if DEBUG
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));
            var btvFileName = Encoding.ASCII.GetString(Module.Memory.GetString(btvStruct.filenam));
            _logger.Info($"Restoring Btreieve file to {btvFileName}");
#endif
        }

        /// <summary>
        ///     Update the Btrieve current record
        ///
        ///     Signature: void updbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        private void updbtv()
        {
            var btrieveRecordPointerPointer = GetParameterPointer(0);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, currentBtrieveFile.RecordLength);

            currentBtrieveFile.Update(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Updated current Btrieve record ({currentBtrieveFile.CurrentRecordNumber}) with {dataToWrite.Length} bytes");
#endif
        }

        /// <summary>
        ///     Insert new fixed-length Btrieve record
        /// 
        ///     Signature: void insbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        private void insbtv()
        {
            var btrieveRecordPointer = GetParameterPointer(0);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];
            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointer, currentBtrieveFile.RecordLength);

            currentBtrieveFile.Insert(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Inserted Btrieve record at {currentBtrieveFile.CurrentRecordNumber} with {dataToWrite.Length} bytes");
#endif
        }


        /// <summary>
        ///     Insert new variable-length Btrieve record
        ///
        ///     Signature: void invbtv(char *recptr, int length)
        /// </summary>
        private void invbtv()
        {
            var btrieveRecordPointer = GetParameterPointer(0);
            var recordLength = GetParameter(2);
            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];
            var record = Module.Memory.GetArray(btrieveRecordPointer, recordLength);

            currentBtrieveFile.Insert(record.ToArray(), true);

#if DEBUG
            _logger.Info(
                $"Inserted Variable Btrieve record at {currentBtrieveFile.CurrentRecordNumber} with {recordLength} bytes");
#endif
        }

        /// <summary>
        ///     Raw status from btusts, where appropriate
        ///     
        ///     Signature: int status
        ///     Returns: Segment holding the Users Status
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> status => Module.Memory.GetVariable("STATUS").ToSpan();

        /// <summary>
        ///     Deduct real credits from online acct
        ///
        ///     Signature: int enuf=rdedcrd(long amount, int asmuch)
        ///     Returns: Always 1, meaning enough credits
        /// </summary>
        /// <returns></returns>
        private void rdedcrd()
        {
            Registers.AX = 1;
        }

        /// <summary>
        ///     sprintf-like string formatter utility
        ///
        ///     Main differentiation is that spr() supports long integer and floating point conversions
        /// </summary>
        /// <returns></returns>
        private void spr()
        {
            var sourcePointer = GetParameterPointer(0);

            var output = Module.Memory.GetString(sourcePointer);


            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 2);

            if (formattedMessage.Length > 0x400)
                throw new OutOfMemoryException($"SPR write is > 1k ({formattedMessage.Length}) and would overflow pre-allocated buffer");

            if (!Module.Memory.TryGetVariable("SPR", out var variablePointer))
            {
                //allocate 1k for the SPR buffer
                variablePointer = base.Module.Memory.AllocateVariable("SPR", 0x400);
            }

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset, formattedMessage);

#if DEBUG
            _logger.Info($"Added {formattedMessage.Length} bytes to the buffer");
#endif

            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Long Multiplication (Borland C++ Implicit Function)
        /// 
        /// </summary>
        /// <returns></returns>
        private void f_lxmul()
        {

            var value1 = (Registers.DX << 16) | Registers.AX;
            var value2 = (Registers.CX << 16) | Registers.BX;

            var result = value1 * value2;

#if DEBUG
            //_logger.Info($"Performed Long Multiplication {value1}*{value2}={result}");
#endif

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Long Division (Borland C++ Implicit Function)
        ///
        ///     Input: Two long values on stack (arg1/arg2)
        ///     Output: DX:AX = quotient
        ///             DI:SI = remainder
        /// </summary>
        /// <returns></returns>
        private void f_ldiv()
        {

            var arg1 = (GetParameter(1) << 16) | GetParameter(0);
            var arg2 = (GetParameter(3) << 16) | GetParameter(2);

            var quotient = Math.DivRem(arg1, arg2, out var remainder);

#if DEBUG
            //_logger.Info($"Performed Long Division {arg1}/{arg2}={quotient} (Remainder: {remainder})");
#endif

            Registers.DX = (ushort)(quotient >> 16);
            Registers.AX = (ushort)(quotient & 0xFFFF);

            Registers.DI = (ushort)(remainder >> 16);
            Registers.SI = (ushort)(remainder & 0xFFFF);

            var previousBP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 1));
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 3));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 5));
            //Set stack back to entry state, minus parameters
            Registers.SP += 14;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousBP);
            Registers.SP -= 2;
            Registers.BP = Registers.SP;
        }

        /// <summary>
        ///     Writes formatted data from variable argument list to string
        ///
        ///     similar to prf, but the destination is a char*, but the output buffer
        /// </summary>
        /// <returns></returns>
        private void vsprintf()
        {
            var targetOffset = GetParameter(0);
            var targetSegment = GetParameter(1);
            var formatOffset = GetParameter(2);
            var formatSegment = GetParameter(3);

            var formatString = Module.Memory.GetString(formatSegment, formatOffset);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(formatString, 4, true);


            Module.Memory.SetArray(targetSegment, targetOffset, formattedMessage);

            Registers.AX = (ushort)formattedMessage.Length;
        }

        /// <summary>
        ///     Scans the specified MDF file for a line prefix that matches the specified string
        /// 
        ///     Signature: char *scnmdf(char *mdfnam,char *linpfx);
        ///     Returns: AX = Offset of String
        ///              DX = Segment of String
        /// </summary>
        /// <returns></returns>
        private void scnmdf()
        {
            var mdfnameOffset = GetParameter(0);
            var mdfnameSegment = GetParameter(1);
            var lineprefixOffset = GetParameter(2);
            var lineprefixSegment = GetParameter(3);

            var mdfNameBytes = Module.Memory.GetString(mdfnameSegment, mdfnameOffset);
            var mdfName = Encoding.ASCII.GetString(mdfNameBytes);

            var lineprefixBytes = Module.Memory.GetString(lineprefixSegment, lineprefixOffset);
            var lineprefix = Encoding.ASCII.GetString(lineprefixBytes);

            //Setup Host Memory Variables Pointer
            if (!Module.Memory.TryGetVariable($"SCNMDF", out var variablePointer))
                variablePointer = base.Module.Memory.AllocateVariable("SCNMDF", 0xFF);

            var recordFound = false;
            foreach (var line in File.ReadAllLines($"{Module.ModulePath}{mdfName}"))
            {
                if (line.StartsWith(lineprefix))
                {
                    var result = Encoding.ASCII.GetBytes(line.Split(':')[1] + "\0");

                    if (result.Length > 256)
                        throw new OverflowException("SCNMDF result is > 256 bytes");

                    Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset, result);
                    recordFound = true;
                    break;
                }
            }

            //Write Null String to address if nothing was found
            if (!recordFound)
                Module.Memory.SetByte(variablePointer.Segment, variablePointer.Offset, 0x0);

            Registers.DX = variablePointer.Offset;
            Registers.AX = variablePointer.Segment;
        }

        /// <summary>
        ///     Returns the time of day it is bitwise HHHHHMMMMMMSSSSS coding
        ///
        ///     Signature: int time=now()
        /// </summary>
        /// <returns></returns>
        private void now()
        {
            //From DOSFACE.H:
            //#define dttime(hour,min,sec) (((hour)<<11)+((min)<<5)+((sec)>>1))
            var packedTime = (DateTime.Now.Hour << 11) + (DateTime.Now.Minute << 5) + (DateTime.Now.Second >> 1);

#if DEBUG
            _logger.Info($"Returned packed time: {packedTime}");
#endif

            Registers.AX = (ushort)packedTime;
        }

        /// <summary>
        ///     Modulo, non-significant (Borland C++ Implicit Function)
        ///
        ///     Signature: DX:AX = arg1 % arg2
        /// </summary>
        /// <returns></returns>
        private void f_lumod()
        {
            var arg1 = (GetParameter(1) << 16) | GetParameter(0);
            var arg2 = (GetParameter(3) << 16) | GetParameter(2);

            var result = arg1 % arg2;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);


            var previousBP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 1));
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 3));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 5));
            //Set stack back to entry state, minus parameters
            Registers.SP += 14;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousBP);
            Registers.SP -= 2;
            Registers.BP = Registers.SP;
        }

        /// <summary>
        ///     Set a block of memory to a value
        ///
        ///     Signature: void setmem(char *destination, unsigned nbytes, char value)
        /// </summary>
        /// <returns></returns>
        private void setmem()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);
            var numberOfBytesToWrite = GetParameter(2);
            var byteToWrite = GetParameter(3);

            for (var i = 0; i < numberOfBytesToWrite; i++)
            {
                Module.Memory.SetByte(destinationSegment, (ushort)(destinationOffset + i), (byte)byteToWrite);
            }

#if DEBUG
            _logger.Info(
                $"Set {numberOfBytesToWrite} bytes to {byteToWrite:X2} starting at {destinationSegment:X4}:{destinationOffset:X4}");
#endif
        }

        /// <summary>
        ///     Output buffer of prf() and prfmsg()
        ///
        ///     Because it's a pointer, the segment only contains Int16:Int16 pointer to the actual prfbuf segment
        ///
        ///     Signature: char *prfbuf
        /// </summary>
        private ReadOnlySpan<byte> prfbuf => Module.Memory.GetVariable("*PRFBUF").ToSpan();

        /// <summary>
        ///     Copies characters from a string
        ///
        ///     Signature: char *strncpy(char *destination, const char *source, size_t num)
        /// </summary>
        /// <returns></returns>
        private void strncpy()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);
            var sourceOffset = GetParameter(2);
            var sourceSegment = GetParameter(3);
            var numberOfBytesToCopy = GetParameter(4);

            var sourceString = Module.Memory.GetString(sourceSegment, sourceOffset);

            for (var i = 0; i < numberOfBytesToCopy; i++)
            {
                if (sourceString[i] == 0x0)
                {
                    //Write remaining nulls
                    for (var j = i; j < numberOfBytesToCopy; j++)
                        Module.Memory.SetByte(destinationSegment, (ushort)(destinationOffset + j), 0x0);

                    break;
                }
                Module.Memory.SetByte(destinationSegment, (ushort)(destinationOffset + i), sourceString[i]);
            }

#if DEBUG
            _logger.Info($"Copied {numberOfBytesToCopy} from {sourceSegment:X4}:{sourceOffset:X4} to {destinationSegment:X4}:{destinationOffset:X4}");
#endif
        }

        private void register_textvar()
        {
            var textPointer = GetParameterPointer(0);
            var functionPointer = GetParameterPointer(2);

            var textBytes = Module.Memory.GetString(textPointer, true);

            Module.TextVariables.Add(Encoding.ASCII.GetString(textBytes), functionPointer);

#if DEBUG
            _logger.Info($"Registered Textvar \"{Encoding.ASCII.GetString(textBytes)}\" to {functionPointer}");
#endif
        }

        /// <summary>
        ///     Does a GetEqual based on the Key -- the record corresponding to the key is returned
        ///
        ///     Signature: int obtbtvl (void *recptr, void *key, int keynum, int obtopt, int loktyp)
        ///     Returns: AX == 0 record not found, 1 record found
        /// </summary>
        /// <returns></returns>
        private void obtbtvl()
        {
            var recordPointer = GetParameterPointer(0);
            var keyPointer = GetParameterPointer(2);
            var keyNum = GetParameter(4);
            var obtopt = GetParameter(5);
            var lockType = GetParameter(6);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];

            ushort result = 0;
            switch (obtopt)
            {
                //GetEqual
                case 5:
                    {
                        result = currentBtrieveFile.HasKey(keyNum, Module.Memory.GetString(keyPointer));
                        break;
                    }
            }

            //Store the Record if it's there
            if (!recordPointer.Equals(IntPtr16.Empty) && result == 1)
                Module.Memory.SetArray(recordPointer, currentBtrieveFile.GetRecordByAbsolutePosition(currentBtrieveFile.AbsolutePosition));

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));

            //If there's a record, always save it to the btrieve file struct
            if (result == 1)
                Module.Memory.SetArray(btvStruct.data, currentBtrieveFile.GetRecordByAbsolutePosition(currentBtrieveFile.AbsolutePosition));

            Registers.AX = result;
        }

        /// <summary>
        ///     Declare size of the Volatile Data Area (Maximum size the module will require)
        ///     Because this is just another memory block, we use the host memory
        /// 
        ///     Signature: void *dclvda(unsigned nbytes);
        /// </summary>
        private void dclvda()
        {
            var size = GetParameter(0);

            if (size > VOLATILE_DATA_SIZE)
                throw new OutOfMemoryException("Volatile Memory declaration > 16k");

#if DEBUG
            _logger.Info($"Volatile Memory Size requested of {size} bytes ({VOLATILE_DATA_SIZE} bytes currently allocated per channel)");
#endif
        }

        private ReadOnlySpan<byte> nterms => Module.Memory.GetVariable("NTERMS").ToSpan();

        /// <summary>
        ///     Compute volatile data pointer for the specified User Number
        ///
        ///     Because UserNumber and Channels in MBBSEmu are the same, we can just use the usernum AS channel
        ///
        ///     Signature: char *vdaoff(int unum)
        ///
        ///     Returns: AX == Segment of Volatile Data
        ///              DX == Offset of Volatile Data
        /// </summary>
        /// <returns></returns>
        private void vdaoff()
        {
            var channel = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"VDA-{channel}", out var volatileMemoryAddress))
                volatileMemoryAddress = Module.Memory.AllocateVariable($"VDA-{channel}", VOLATILE_DATA_SIZE);

#if DEBUG
            _logger.Info($"Returned VDAOFF {volatileMemoryAddress} for Channel {channel}");
#endif

            Registers.AX = volatileMemoryAddress.Offset;
            Registers.DX = volatileMemoryAddress.Segment;
        }

        /// <summary>
        ///     Contains information about the current user account (class, state, baud, etc.)
        ///
        ///     Signature: struct user;
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> user
        {
            get
            {
                var pointer = Module.Memory.GetVariable("*USER");
                return pointer.ToSpan();
            }
        }

        /// <summary>
        ///     Points to the Volatile Data Area for the current channel
        ///
        ///     Signature: char *vdaptr
        /// </summary>
        private ReadOnlySpan<byte> vdaptr
        {
            get
            {
                if (!Module.Memory.TryGetVariable($"VDAPTR", out var variablePointer))
                    variablePointer = Module.Memory.AllocateVariable($"VDAPTR", 0x4);

                return variablePointer.ToSpan();
            }
        }


        /// <summary>
        ///     After calling bgncnc(), the command is unparsed (has spaces again, not separate words),
        ///     and prepared for interpretation using the command concatenation utilities
        ///
        ///     Signature: void bgncnc()
        /// </summary>
        private void bgncnc()
        {
            _inputCurrentCommand = 0;
        }

        /// <summary>
        ///     Number of Words in the users input line
        ///
        ///     Signature: int margc
        /// </summary>
        private ReadOnlySpan<byte> margc => Module.Memory.GetVariable("MARGC").ToSpan();

        /// <summary>
        ///     Returns the pointer to the next parsed input command from the user
        ///     If this is the first time it's called, it returns the first command
        /// </summary>
        private ReadOnlySpan<byte> nxtcmd()
        {

            var variablePointer = Module.Memory.GetVariable("INPUT");
            var pointerSegment = Module.Memory.GetVariable("MARGN");
            var nextCommandPointer = Module.Memory.GetVariable("NXTCMD");
            if (_margvPointers.Count == 0 || _inputCurrentCommand > _margvPointers.Count)
            {
#if DEBUG
                _logger.Info(
                    $"No Input, returning pointer to 1st (null) byte in Input Memory ({variablePointer.Segment:X4}:{variablePointer.Offset})");
#endif
            }
            else
            {
#if DEBUG
                _logger.Info(
                    $"Returning Next Command ({_inputCurrentCommand} ({_margvPointers[_inputCurrentCommand++].Segment:X4}:{_margvPointers[_inputCurrentCommand++].Offset})");
#endif
            }

            var newPointer = new IntPtr16(Module.Memory.GetArray(pointerSegment.Segment,
                (ushort)(pointerSegment.Offset + (_inputCurrentCommand * 4)), 4));

            Module.Memory.SetArray(nextCommandPointer, newPointer.ToSpan());

            return nextCommandPointer.ToSpan();

        }

        /// <summary>
        ///     Case-ignoring substring match
        ///
        ///     Signature: int match=sameto(char *shorts, char *longs)
        ///     Returns: AX == 1, match
        /// </summary>
        private void sameto()
        {
            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);

            var string1Buffer = Module.Memory.GetString(string1Pointer, true);
            var string2Buffer = Module.Memory.GetString(string2Pointer, true);

            if (string1Buffer.Length > string2Buffer.Length)
            {
#if DEBUG
                _logger.Info($"Returning False, String 1 Length {string1Buffer.Length} > Stringe 2 Length {string2Buffer.Length}");
#endif
                Registers.AX = 0;
                return;
            }

            var resultValue = true;
            for (var i = 0; i < string1Buffer.Length; i++)
            {
                if (string1Buffer[i] == string2Buffer[i] || //same case
                    string1Buffer[i] == string2Buffer[i] - 32 || //check upper->lower
                    string1Buffer[i] == string2Buffer[i] + 32) //check lower->upper
                    continue;

                resultValue = false;
                break;
            }

#if DEBUG
            _logger.Info(
                $"Returned {resultValue} comparing {Encoding.ASCII.GetString(string1Buffer)} ({string1Pointer}) to {Encoding.ASCII.GetString(string2Buffer)} ({string2Pointer})");
#endif

            Registers.AX = (ushort)(resultValue ? 1 : 0);
        }

        /// <summary>
        ///     Expect a Character from the user (character from the current command)
        /// </summary>
        private void cncchr()
        {
            Registers.AX = 0;
        }

        /// <summary>
        ///     Pointer to the current position in prfbuf
        ///
        ///     Signature: char *prfptr;
        /// </summary>
        private ReadOnlySpan<byte> prfptr
        {
            get
            {
                var pointer = Module.Memory.GetVariable("PRFPTR");
                var prfbufPointer = Module.Memory.GetVariable("PRFBUF");
                Module.Memory.SetArray(pointer, new IntPtr16(prfbufPointer.Segment, (ushort)(prfbufPointer.Offset + _outputBufferPosition)).ToSpan());
                return pointer.ToSpan();
            }
        }

        /// <summary>
        ///     Opens a new file for reading/writing
        ///
        ///     Signature: file* fopen(const char* filename, USE)
        /// </summary>
        private void f_open()
        {
            var filenamePointer = GetParameterPointer(0);
            var modePointer = GetParameterPointer(2);

            var filenameInputBuffer = Module.Memory.GetString(filenamePointer, true);
            var filenameInputValue = Encoding.Default.GetString(filenameInputBuffer.ToArray()).ToUpper();

            //If non windows, change directory paths in filename
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && filenameInputValue.Contains(@"\"))
            {
                var newFilenameInputValue = filenameInputValue.Replace(@"\", "/");
                _logger.Info($"Modifying Filename to be Linux Friendly: {filenameInputValue} to {newFilenameInputValue}");
                filenameInputValue = CheckFileCasing(Module.ModulePath, newFilenameInputValue);
            }

#if DEBUG
            _logger.Debug($"Opening File: {Module.ModulePath}{filenameInputValue}");
#endif

            var modeInputBuffer = Module.Memory.GetString(modePointer, true);
            var fileAccessMode = FileStruct.CreateFlagsEnum(modeInputBuffer);

            //Allocate Memory for FILE struct
            if (!Module.Memory.TryGetVariable($"FILE_{filenameInputValue}", out var fileStructPointer))
                fileStructPointer = Module.Memory.AllocateVariable($"FILE_{filenameInputValue}", FileStruct.Size);

            //Write New Blank Pointer
            var fileStruct = new FileStruct();
            Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());

            if (!File.Exists($"{Module.ModulePath}{filenameInputValue}"))
            {
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Read))
                {
                    _logger.Warn($"Unable to find file {Module.ModulePath}{filenameInputValue}");
                    Registers.AX = fileStruct.curp.Offset;
                    Registers.DX = fileStruct.curp.Segment;
                    return;
                }

                //Create a new file for W or A
                _logger.Info($"Creating new file {filenameInputValue}");
                File.Create($"{Module.ModulePath}{filenameInputValue}").Dispose();
            }
            else
            {
                //Overwrite existing file for W
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Write))
                {
#if DEBUG
                    _logger.Info($"Overwritting file {filenameInputValue}");
#endif
                    File.Create($"{Module.ModulePath}{filenameInputValue}").Dispose();
                }
            }


            //Setup the File Stream
            var fileStream = File.Open($"{Module.ModulePath}{filenameInputValue}", FileMode.OpenOrCreate);

            if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Append))
                fileStream.Seek(fileStream.Length, SeekOrigin.Begin);

            var fileStreamPointer = FilePointerDictionary.Allocate(fileStream);

            //Set Struct Values
            fileStruct.SetFlags(fileAccessMode);
            fileStruct.curp = new IntPtr16(ushort.MaxValue, (ushort)fileStreamPointer);
            Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());

            Registers.AX = fileStructPointer.Offset;
            Registers.DX = fileStructPointer.Segment;
        }

        /// <summary>
        ///     Closes an Open File Pointer
        ///
        ///     Signature: int fclose(FILE* stream )
        /// </summary>
        private void f_close()
        {
            var filePointer = GetParameterPointer(0);

            var fileStruct = new FileStruct(Module.Memory.GetArray(filePointer, FileStruct.Size));

            if (fileStruct.curp.Segment == 0 && fileStruct.curp.Offset == 0)
            {
#if DEBUG
                _logger.Warn($"Called FCLOSE on null File Stream Pointer (0000:0000), usually means it tried to open a file that doesn't exist");
                Registers.AX = 0;
                return;
#endif
            }

            if (!FilePointerDictionary.ContainsKey(fileStruct.curp.Offset))
            {
                _logger.Warn($"Attempted to call FCLOSE on pointer not in File Stream Segment {fileStruct.curp} (File Alredy Closed?)");
                Registers.AX = 0;
                return;
            }

            //Clean Up File Stream Pointer
            FilePointerDictionary[fileStruct.curp.Offset].Dispose();
            FilePointerDictionary.Remove(fileStruct.curp.Offset);

#if DEBUG
            _logger.Info($"Closed File {filePointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = 0;
        }

        /// <summary>
        ///     sprintf() function in C++ to handle string formatting
        ///
        ///     Signature: int sprintf(char *str, const char *format, ... )
        /// </summary>
        private void sprintf()
        {
            var destinationOffset = GetParameter(0);
            var destinationSegment = GetParameter(1);
            var sourceOffset = GetParameter(2);
            var sourceSegment = GetParameter(3);


            var output = Module.Memory.GetString(sourceSegment, sourceOffset);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 4);

            Module.Memory.SetArray(destinationSegment, destinationOffset, formattedMessage);

#if DEBUG
            _logger.Info($"Added {output.Length} bytes to the buffer");
#endif

        }

        /// <summary>
        ///     Looks for the specified filename (filespec) in the BBS directory, returns 1 if the file is there
        /// 
        ///     Signature: int yes=fndlst(struct fndblk &fb, filespec, char attr)
        /// </summary>
        private void fnd1st()
        {
            var findBlockPointer = GetParameterPointer(0);
            var filespecPointer = GetParameterPointer(2);
            var attrChar = GetParameter(4);

            var fileName = Module.Memory.GetString(filespecPointer, true);
            var fileNameString = Encoding.ASCII.GetString(fileName);
            Registers.AX = (ushort)(File.Exists($"{Module.ModulePath}{fileNameString}") ? 1 : 0);
        }

        /// <summary>
        ///     Reads an array of count elements, each one with a size of size bytes, from the stream and stores
        ///     them in the block of memory specified by ptr.
        ///
        ///     Signature: size_t fread(void* ptr, size_t size, size_t count, FILE* stream)
        /// </summary>
        private void f_read()
        {
            var destinationPointer = GetParameterPointer(0);
            var size = GetParameter(2);
            var count = GetParameter(3);
            var fileStructPointer = GetParameterPointer(4);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            ushort elementsRead = 0;
            for (var i = 0; i < count; i++)
            {
                var dataRead = new byte[size];
                var bytesRead = fileStream.Read(dataRead);

                if (bytesRead != size)
                    break;

                Module.Memory.SetArray(destinationPointer.Segment, (ushort)(destinationPointer.Offset + (i * size)),
                    dataRead);
                elementsRead++;
            }

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Read {elementsRead} group(s) of {size} bytes from {fileStructPointer} (Stream: {fileStruct.curp}), written to {destinationPointer}");
#endif

            Registers.AX = elementsRead;
        }

        /// <summary>
        ///     Writes an array of count elements, each one with a size of size bytes, from the block of memory
        ///     pointed by ptr to the current position in the stream.
        /// </summary>
        private void f_write()
        {
            var sourcePointer = GetParameterPointer(0);
            var size = GetParameter(2);
            var count = GetParameter(3);
            var fileStructPointer = GetParameterPointer(4);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            ushort elementsWritten = 0;
            for (var i = 0; i < count; i++)
            {
                fileStream.Write(Module.Memory.GetArray(sourcePointer.Segment, (ushort)(sourcePointer.Offset + (i * size)), size));
                elementsWritten++;
            }

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Read {elementsWritten} group(s) of {size} bytes from {sourcePointer}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = elementsWritten;
        }

        /// <summary>
        ///     Deleted the specified file
        /// </summary>
        private void unlink()
        {
            var filenameOffset = GetParameter(0);
            var filenameSegment = GetParameter(1);

            using var filenameInputBuffer = new MemoryStream();
            filenameInputBuffer.Write(Module.Memory.GetString(filenameSegment, filenameOffset, true));
            var filenameInputValue = Encoding.Default.GetString(filenameInputBuffer.ToArray()).ToUpper();

#if DEBUG
            _logger.Info($"Deleting File: {Module.ModulePath}{filenameInputValue}");
#endif

            if (File.Exists($"{Module.ModulePath}{filenameInputValue}"))
                File.Delete($"{Module.ModulePath}{filenameInputValue}");

            Registers.AX = 0;
        }

        /// <summary>
        ///     Returns the length of the C string str
        ///
        ///     Signature: size_t strlen(const char* str)
        /// </summary>
        private void strlen()
        {
            var stringPointer = GetParameterPointer(0);

            var stringValue = Module.Memory.GetString(stringPointer, true);

#if DEBUG
            _logger.Info(
                $"Evaluated string length of {stringValue.Length} for string at {stringPointer}: {Encoding.ASCII.GetString(stringValue)}");
#endif

            Registers.AX = (ushort)stringValue.Length;
        }

        /// <summary>
        ///     User Input
        ///
        ///     Signature: char input[]
        /// </summary>
        private ReadOnlySpan<byte> input => Module.Memory.GetVariable("INPUT").ToSpan();

        /// <summary>
        ///     Converts a lowercase letter to uppercase
        ///
        ///     Signature: int toupper (int c)
        /// </summary>
        private void toupper()
        {
            var character = GetParameter(0);
            if (character >= 97 && character <= 122)
            {
                Registers.AX = (ushort)(character - 32);
            }
            else
            {
                Registers.AX = character;
            }
#if DEBUG
            _logger.Info($"Converted {(char)character} to {(char)Registers.AX}");
#endif
        }

        /// <summary>
        ///     Converts a uppercase letter to lowercase
        ///
        ///     Signature: int tolower (int c)
        /// </summary>
        private void tolower()
        {
            var character = GetParameter(0);
            if (character >= 65 || character <= 90)
            {
                Registers.AX = (ushort)(character + 32);
            }
            else
            {
                Registers.AX = character;
            }
#if DEBUG
            _logger.Info($"Converted {(char)character} to {(char)Registers.AX}");
#endif
        }

        /// <summary>
        ///     Changes the name of the file or directory specified by old name to new name
        ///
        ///     Signature: int rename(const char *oldname, const char *newname )
        /// </summary>
        private void rename()
        {
            var oldFilenamePointer = GetParameterPointer(0);
            var newFilenamePointer = GetParameterPointer(2);

            var oldFilenameInputBuffer = Module.Memory.GetString(oldFilenamePointer, true);
            var oldFilenameInputValue = Encoding.Default.GetString(oldFilenameInputBuffer).ToUpper();

            var newFilenameInputBuffer = Module.Memory.GetString(newFilenamePointer, true);
            var newFilenameInputValue = Encoding.Default.GetString(newFilenameInputBuffer).ToUpper();

            //If non windows, change directory paths in filename
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && oldFilenameInputValue.Contains(@"\"))
            {
                var fixedOldFilenameInputValue = oldFilenameInputValue.Replace(@"\", "/");
                _logger.Info($"Modifying Filename to be Linux Friendly: {oldFilenameInputValue} to {fixedOldFilenameInputValue}");
                oldFilenameInputValue = CheckFileCasing(Module.ModulePath, fixedOldFilenameInputValue);

                var fixedNewFilenameInputValue = newFilenameInputValue.Replace(@"\", "/");
                _logger.Info($"Modifying Filename to be Linux Friendly: {newFilenameInputValue} to {fixedNewFilenameInputValue}");
                newFilenameInputValue = CheckFileCasing(Module.ModulePath, fixedNewFilenameInputValue);
            }

            if (!File.Exists($"{Module.ModulePath}{oldFilenameInputValue}"))
                throw new FileNotFoundException($"Attempted to rename file that doesn't exist: {oldFilenameInputValue}");

            File.Move($"{Module.ModulePath}{oldFilenameInputValue}", $"{Module.ModulePath}{newFilenameInputValue}", true);

#if DEBUG
            _logger.Info($"Renamed file {oldFilenameInputValue} to {newFilenameInputValue}");
#endif

            Registers.AX = 0;
        }


        /// <summary>
        ///     The Length of the total Input Buffer received
        ///
        ///     Signature: int inplen
        /// </summary>
        private ReadOnlySpan<byte> inplen => Module.Memory.GetVariable("INPLEN").ToSpan();

        private ReadOnlySpan<byte> margv => Module.Memory.GetVariable("MARGV").ToSpan();

        private ReadOnlySpan<byte> margn => Module.Memory.GetVariable("MARGN").ToSpan();

        /// <summary>
        ///     Restore parsed input line (undoes effects of parsin())
        ///
        ///     Signature: void rstrin()
        /// </summary>
        private void rstrin()
        {
            ChannelDictionary[ChannelNumber].rstrin();

            var inputMemory = Module.Memory.GetVariable("INPUT");
            Module.Memory.SetArray(inputMemory.Segment, inputMemory.Offset, ChannelDictionary[ChannelNumber].InputCommand);
        }

        private ReadOnlySpan<byte> _exitbuf => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitfopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> extptr => Module.Memory.GetVariable("EXTPTR").ToSpan();
        private ReadOnlySpan<byte> usaptr => Module.Memory.GetVariable("USAPTR").ToSpan();

        /// <summary>
        ///     Appends the first num characters of source to destination, plus a terminating null-character.
        ///
        ///     Signature: char *strncat(char *destination, const char *source, size_t num)
        /// </summary>
        private void strncat()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);
            var bytesToCopy = GetParameter(4);

            var stringDestination = Module.Memory.GetString(destinationPointer);
            var stringSource = Module.Memory.GetString(sourcePointer, true);

            if (stringSource.Length < bytesToCopy)
                bytesToCopy = (ushort)stringSource.Length;

            var newString = new byte[stringDestination.Length + bytesToCopy];
            Array.Copy(stringSource.Slice(0, bytesToCopy).ToArray(), 0, newString, 0, bytesToCopy);
            Array.Copy(stringDestination.ToArray(), 0, newString, bytesToCopy, stringDestination.Length);

#if DEBUG
            _logger.Info($"Concatenated String 1 (Length:{stringSource.Length}b. Copied {bytesToCopy}b) with String 2 (Length: {stringDestination.Length}) to new String (Length: {newString.Length}b)");
#endif
            Module.Memory.SetArray(destinationPointer, newString);
        }

        /// <summary>
        ///     Sets the first num bytes of the block of memory with the specified value
        ///
        ///     Signature: void memset(void *ptr, int value, size_t num);
        /// </summary>
        private void memset()
        {
            var destinationPointer = GetParameterPointer(0);
            var valueToFill = GetParameter(2);
            var numberOfByteToFill = GetParameter(3);

            for (var i = 0; i < numberOfByteToFill; i++)
            {
                Module.Memory.SetByte(destinationPointer.Segment, (ushort)(destinationPointer.Offset + i), (byte)valueToFill);
            }

#if DEBUG
            _logger.Info($"Filled {numberOfByteToFill} bytes at {destinationPointer} with {(byte)valueToFill:X2}");
#endif
        }

        /// <summary>
        ///     Read value of CNF option
        ///
        ///     Signature: char *bufard=getmsg(msgnum)
        /// </summary>
        private void getmsg()
        {
            var msgnum = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"GETMSG", out var variablePointer))
                variablePointer = base.Module.Memory.AllocateVariable($"GETMSG", 0x1000);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}), saved {3} bytes to {4}", msgnum, McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue.Length, variablePointer);
#endif

            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Reads data from s and stores them accounting to parameter format into the locations given by the additional arguments
        ///
        ///     Signature: int sscanf(const char *s, const char *format, ...)
        /// </summary>
        private void sscanf()
        {
            var inputPointer = GetParameterPointer(0);
            var formatPointer = GetParameterPointer(2);

            var inputString = Module.Memory.GetString(inputPointer);
            var formatString = Module.Memory.GetString(formatPointer);

            sscanf(inputString, formatString, 4);


#if DEBUG
            _logger.Info($"Processed sscanf on {Encoding.ASCII.GetString(inputString)}-> {Encoding.ASCII.GetString(formatString)}");
#endif
        }

        /// <summary>
        ///     General MS-DOS interrupt interface
        ///
        ///     These calls are INT21 calls made from the module
        ///
        ///     Signature: int intdos(union REGS * inregs, union REGS * outregs)
        /// </summary>
        private void intdos()
        {
            var parameterOffset1 = GetParameterPointer(0);
            var parameterOffset2 = GetParameterPointer(2);

            //Get the AH value to determine the function being called
            var registers = new CpuRegisters();
            registers.FromRegs(Module.Memory.GetArray(parameterOffset1, 16));
            switch (registers.AH)
            {
                case 0x2C:
                    //Get Time
                    {
                        var returnValue = new CpuRegisters
                        {
                            CH = (byte)DateTime.Now.Hour,
                            CL = (byte)DateTime.Now.Minute,
                            DH = (byte)DateTime.Now.Second,
                            DL = (byte)(DateTime.Now.Millisecond / 100)
                        };
                        Module.Memory.SetArray(parameterOffset2, returnValue.ToRegs());
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Unknown DOS INT21 API: {registers.AH:X2}");
            }
        }

        /// <summary>
        ///     Like pmlt(), but the control string comes from an .MCV file
        ///
        ///     Signature: void prfmlt(int msgno,...)
        /// </summary>
        private void prfmlt()
        {
            var messageNumber = GetParameter(0);

            var output = McvPointerDictionary[_currentMcvFile.Offset].GetString(messageNumber);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 1);

            var pointer = Module.Memory.GetVariable("PRFBUF");
            Module.Memory.SetArray(pointer.Segment, (ushort)(_outputBufferPosition + pointer.Offset), formattedMessage);
            _outputBufferPosition += formattedMessage.Length;

#if DEBUG
            _logger.Info($"Added {output.Length} bytes to the buffer (Message #: {messageNumber})");
#endif
        }

        /// <summary>
        ///     Clear the prf buffer indep of outprf
        ///     Basically the same as clrprf()
        ///
        ///     Signature: void clrmlt()
        /// </summary>
        private void clrmlt() => clrprf();

        /// <summary>
        ///     Registers a Global Command Handler -- any input in the BBS
        ///     (even outside the module) is run through this
        /// </summary>
        private void globalcmd()
        {
            var globalCommandHandlerPointer = GetParameterPointer(0);

            Module.GlobalCommandHandlers.Add(globalCommandHandlerPointer);

#if DEBUG
            _logger.Info($"Registered Global Command Handler at: {globalCommandHandlerPointer}");
#endif
        }

        /// <summary>
        ///     Catastro Failure, basically a mega show stopping error
        /// </summary>
        private void catastro()
        {
            var messagePointer = GetParameterPointer(0);
            var message = Module.Memory.GetString(messagePointer);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine($"{Encoding.ASCII.GetString(message)}");
            Console.ResetColor();

            Registers.Halt = true;
        }

        /// <summary>
        ///     Reads the specified number of characters from the file until a new line or EOF
        ///
        ///     Signature: char* fgets(char* str, int num, FILE* stream )
        /// </summary>
        private void fgets()
        {
            var destinationPointer = GetParameterPointer(0);
            var maxCharactersToRead = GetParameter(2);
            var fileStructPointer = GetParameterPointer(3);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new Exception($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            var startingPointer = new IntPtr16(destinationPointer.Segment, destinationPointer.Offset);
            for (var i = 0; i < maxCharactersToRead; i++)
            {
                var inputValue = (byte)fileStream.ReadByte();

                if (inputValue == '\n' || fileStream.Position == fileStream.Length)
                    break;

                Module.Memory.SetByte(destinationPointer.Segment, destinationPointer.Offset++, inputValue);
            }
            Module.Memory.SetByte(destinationPointer, 0x0);

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Read string from {fileStructPointer} (Stream: {fileStruct.curp}), saved starting at {startingPointer}->{destinationPointer}");
#endif
        }

        /// <summary>
        ///     Concatenates two strings and saves them to the destination.
        ///
        ///     Signature: char *strcat(char *destination, const char *source)
        /// </summary>
        public void strcat()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);

            var destinationString = Module.Memory.GetString(destinationPointer, true);
            var sourceString = Module.Memory.GetString(sourcePointer);

            Module.Memory.SetArray(destinationPointer.Segment, (ushort)(destinationPointer.Offset + destinationString.Length),
                sourceString);

#if DEBUG
            _logger.Info($"Concatenated strings {Encoding.ASCII.GetString(destinationString)} and {Encoding.ASCII.GetString(sourceString)} to {destinationPointer}");
#endif
        }

        /// <summary>
        ///     Writes the C string pointed by format to the stream
        ///
        ///     Signature: int fprintf ( FILE * stream, const char * format, ... )
        /// </summary>
        private void f_printf()
        {
            var fileStructPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new Exception($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            var output = Module.Memory.GetString(sourcePointer);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 4);

            fileStream.Write(formattedMessage);

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Wrote {formattedMessage.Length} bytes to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
        }

        /// <summary>
        ///     Sets the position indicator associated with the stream to a new position.
        /// 
        ///     Signature: int fseek ( FILE * stream, long int offset, int origin )
        /// </summary>
        private void fseek()
        {
            var fileStructPointer = GetParameterPointer(0);
            var offset = GetParameterLong(2);
            var origin = GetParameter(4);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new Exception($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            switch (origin)
            {
                case 2: //EOF
                    fileStream.Seek(offset, SeekOrigin.End);
                    break;
                case 1: //CUR
                    fileStream.Seek(offset, SeekOrigin.Current);
                    break;
                case 0: //SET
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    break;
            }

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Seek to {fileStream.Position} in {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
        }

        /// <summary>
        ///     Takes a packed time from now() and returns it as 'HH:MM:SS'
        ///
        ///     Signature: char *asctim=nctime(int time)
        /// </summary>
        private void nctime()
        {
            //From DOSFACE.H:
            //#define dttime(hour,min,sec) (((hour)<<11)+((min)<<5)+((sec)>>1))
            //var packedTime = (DateTime.Now.Hour << 11) + (DateTime.Now.Minute << 5) + (DateTime.Now.Second >> 1);

            var packedTime = GetParameter(0);

            var unpackedHour = (packedTime >> 11) & 0x1F;
            var unpackedMinutes = (packedTime >> 5 & 0x1F);
            var unpackedSeconds = packedTime & 0xF;

            var unpackedTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, unpackedHour, unpackedMinutes, unpackedSeconds);

            var timeString = unpackedTime.ToString("HH:mm:ss");

            if (!Module.Memory.TryGetVariable("NCTIME", out var variablePointer))
            {
                variablePointer = Module.Memory.AllocateVariable("NCTIME", (ushort)timeString.Length);
            }

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset, Encoding.Default.GetBytes(timeString));

#if DEBUG
            _logger.Info($"Received value: {packedTime}, decoded string {timeString} saved to {variablePointer}");
#endif
            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Returns a pointer to the first occurence of character in the C string str
        ///
        ///     Signature: char * strchr ( const char * str, int character )
        /// 
        ///     More Info: http://www.cplusplus.com/reference/cstring/strchr/
        /// </summary>
        private void strchr()
        {
            var stringPointer = GetParameterPointer(0);
            var characterToFind = GetParameter(2);

            var stringToSearch = Module.Memory.GetString(stringPointer, true);

            for (var i = 0; i < stringToSearch.Length; i++)
            {
                if (stringToSearch[i] != (byte)characterToFind) continue;

                Registers.AX = (ushort)(stringPointer.Offset + i);
                Registers.DX = stringPointer.Segment;

#if DEBUG
                _logger.Info($"Found character {(char)characterToFind} at position {i} in string {stringPointer}");
#endif
                return;
            }

            //If character wasn't found, return a null pointer
            Registers.AX = 0;
            Registers.DX = 0;
        }

        /// <summary>
        ///     Parses the input line (null terminating each word)
        ///
        ///     Signature: void parsin()
        /// </summary>
        private void parsin()
        {
            ChannelDictionary[ChannelNumber].parsin();

            var inputMemory = Module.Memory.GetVariable("INPUT");
            Module.Memory.SetArray(inputMemory.Segment, inputMemory.Offset, ChannelDictionary[ChannelNumber].InputCommand);
        }

        /// <summary>
        ///     Copies the values of num bytes from the location pointed by source to the memory block pointed by destination.
        ///     Copying takes place as if an intermediate buffer were used, allowing the destination and source to overlap
        ///
        ///     Signature: void * memmove ( void * destination, const void * source, size_t num )
        ///
        ///     More Info: http://www.cplusplus.com/reference/cstring/memmove/
        /// </summary>
        private void movemem()
        {
            var sourcePointer = GetParameterPointer(0);
            var destinationPointer = GetParameterPointer(2);
            var bytesToMove = GetParameter(4);

            //Cast to array as the write can overlap and overwrite, mucking up the span read
            var sourceData = Module.Memory.GetArray(sourcePointer, bytesToMove);

            Module.Memory.SetArray(destinationPointer, sourceData);

#if DEBUG
            _logger.Info($"Moved {bytesToMove} bytes {sourcePointer}->{destinationPointer}");
#endif
        }

        /// <summary>
        ///     Returns the current position in the specified FILE stream
        ///
        ///     Signature: long int ftell(FILE *stream );
        /// </summary>
        private void ftell()
        {
            var fileStructPointer = GetParameterPointer(0);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            var currentPosition = FilePointerDictionary[fileStruct.curp.Offset].Position;

#if DEBUG
            _logger.Info($"Returning Current Position of {currentPosition} for {fileStructPointer} (Stream: {fileStruct.curp})");
#endif

            Registers.DX = (ushort)(currentPosition >> 16);
            Registers.AX = (ushort)(currentPosition & 0xFFFF);
        }

        /// <summary>
        ///     Determines if a user is using a specific module
        ///
        ///     Signature: int isin=instat(char *usrid, int qstate)
        /// </summary>
        private void instat()
        {
            var useridPointer = GetParameterPointer(0);
            var moduleId = GetParameter(2);

            var userId = Module.Memory.GetString(useridPointer).ToArray();

            var userSession = ChannelDictionary.Values.FirstOrDefault(x => userId.SequenceEqual(StringFromArray(x.UsrAcc.userid).ToArray()));

            //User not found?
            if (userSession == null || ChannelDictionary[userSession.Channel].UsrPtr.State != moduleId)
            {
                Registers.AX = 0;
                return;
            }

            var othusnPointer = Module.Memory.GetVariable("OTHUSN");
            Module.Memory.SetWord(othusnPointer, ChannelDictionary[userSession.Channel].Channel);

            var userBase = new IntPtr16(Module.Memory.GetVariable("USER").ToSpan());
            userBase.Offset += (ushort)(User.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariable("OTHUSP"), userBase.ToSpan());

            var userAccBase = new IntPtr16(Module.Memory.GetVariable("USRACC").ToSpan());
            userAccBase.Offset += (ushort)(UserAccount.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariable("OTHUAP"), userAccBase.ToSpan());

            var userExtAcc = new IntPtr16(Module.Memory.GetVariable("EXTUSR").ToSpan());
            userExtAcc.Offset += (ushort)(ExtUser.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariable("OTHEXP"), userExtAcc.ToSpan());

#if DEBUG
            _logger.Info($"User Found -- Channel {userSession.Channel}, user[] offset {userBase}");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Searches the string for any occurence of the substring
        ///
        ///     Signature: int found=samein(char *subs, char *string)
        /// </summary>
        private void samein()
        {
            var stringToFindPointer = GetParameterPointer(0);
            var stringToSearchPointer = GetParameterPointer(2);

            var stringToFind = Encoding.ASCII.GetString(Module.Memory.GetString(stringToFindPointer, true)).ToUpper();
            var stringToSearch = Encoding.ASCII.GetString(Module.Memory.GetString(stringToSearchPointer, true)).ToUpper();

            //Won't find it if the substring is greater than the string to search
            if (stringToFind.Length > stringToSearch.Length)
            {
                Registers.AX = 0;
                return;
            }

            for (var i = 0; i < stringToSearch.Length; i++)
            {
                //are there not enough charaters left to match?
                if (stringToFind.Length > (stringToSearch.Length - i))
                    break;

                var isMatch = true;
                for (var j = 0; j < stringToFind.Length; j++)
                {
                    if (stringToSearch[i + j] == stringToFind[j])
                        continue;

                    isMatch = false;
                    break;
                }

                //Found a match?
                if (isMatch)
                {
                    Registers.AX = 1;
                    return;
                }
            }

            Registers.AX = 0;
        }

        /// <summary>
        ///     Skip past whitespace, returns pointer to the first NULL or non-whitespace character in the string
        ///
        ///     Signature: char *skpwht(char *string)
        /// </summary>
        private void skpwht()
        {
            var stringToSearchPointer = GetParameterPointer(0);

            var stringToSearch = Module.Memory.GetString(stringToSearchPointer, false);

            for (var i = 0; i < stringToSearch.Length; i++)
            {
                if (stringToSearch[i] == 0x0 || stringToSearch[i] == 0x20) continue;

                Registers.AX = (ushort)(stringToSearchPointer.Offset + i);
                Registers.DX = stringToSearchPointer.Segment;
                return;
            }
            Registers.AX = stringToSearchPointer.Offset;
            Registers.DX = stringToSearchPointer.Segment;
        }

        /// <summary>
        ///     Just like the standard fgets(), except it uses '\r' as a line terminator (a hard carriage return on The Major BBS),
        ///     and it won't have a line terminator on the last line if the file doesn't have it.
        ///
        ///     Signature: char *mdfgets(char *buf,int size,FILE *fp)
        /// </summary>
        private void mdfgets()
        {
            var destinationPointer = GetParameterPointer(0);
            var maxSize = GetParameter(2);
            var fileStructPointer = GetParameterPointer(3);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new Exception($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            var startingDestinationPointer = new IntPtr16(destinationPointer.Segment, destinationPointer.Offset);

            for (var i = 0; i < (maxSize - 1); i++)
            {
                var inputByte = (byte)fileStream.ReadByte();

                //EOF or Line Terminator
                if (inputByte == 0x13 || fileStream.Position == fileStream.Length)
                    break;

                Module.Memory.SetByte(destinationPointer.Segment, destinationPointer.Offset++, inputByte);
            }
            Module.Memory.SetByte(destinationPointer.Segment, destinationPointer.Offset, 0x0);

#if DEBUG
            _logger.Info($"Read Line from {fileStructPointer} (Stream: {fileStruct.curp}) {startingDestinationPointer}->{destinationPointer}");
#endif
        }

        private ReadOnlySpan<byte> genbb => Module.Memory.GetVariable("GENBB").ToSpan();

        /// <summary>
        ///     Turns echo on for this channel
        ///
        ///     Signature: void echon()
        /// </summary>
        private void echon()
        {
            ChannelDictionary[ChannelNumber].TransparentMode = false;
        }

        /// <summary>
        ///     Converts all lowercase characters in a string to uppercase
        ///
        ///     Signature: char *upper=strlwr(char *string)
        /// </summary>
        private void strupr()
        {
            var stringToConvertPointer = GetParameterPointer(0);

            var stringData = Module.Memory.GetString(stringToConvertPointer).ToArray();

            for (var i = 0; i < stringData.Length; i++)
            {
                if (stringData[i] >= 97 && stringData[i] <= 122)
                    stringData[i] -= 32;
            }

#if DEBUG
            _logger.Info($"Converted string to {Encoding.ASCII.GetString(stringData)}");
#endif

            Module.Memory.SetArray(stringToConvertPointer, stringData);
        }

        /// <summary>
        ///     Returns a pointer to the first occurrence of str2 in str1, or a null pointer if str2 is not part of str1.
        /// </summary>
        private void strstr()
        {
            var stringToSearchPointer = GetParameterPointer(0);
            var stringToFindPointer = GetParameterPointer(2);

            var stringToSearch = Encoding.ASCII.GetString(Module.Memory.GetString(stringToSearchPointer, true)).ToUpper();
            var stringToFind = Encoding.ASCII.GetString(Module.Memory.GetString(stringToFindPointer, true)).ToUpper();

            //Won't find it if the substring is greater than the string to search
            if (stringToFind.Length > stringToSearch.Length)
            {
                Registers.AX = 0;
                return;
            }

            for (var i = 0; i < stringToSearch.Length; i++)
            {
                //are there not enough charaters left to match?
                if (stringToFind.Length > (stringToSearch.Length - i))
                    break;

                var isMatch = true;
                for (var j = 0; j < stringToFind.Length; j++)
                {
                    if (stringToSearch[i + j] == stringToFind[j])
                        continue;

                    isMatch = false;
                    break;
                }

                //Found a match?
                if (isMatch)
                {
                    Registers.AX = (ushort)(stringToSearchPointer.Offset + i);
                    Registers.DX = stringToSearchPointer.Segment;
                    return;
                }
            }

            Registers.AX = 0;
            Registers.DX = 0;
        }

        /// <summary>
        ///     Removes trailing blank spaces from string
        ///
        ///     Signature: int nremoved=depad(char *string)
        /// </summary>
        private void depad()
        {
            var stringPointer = GetParameterPointer(0);

            var stringToSearch = Module.Memory.GetString(stringPointer).ToArray();

            ushort numRemoved = 0;
            for (var i = 1; i < stringToSearch.Length; i++)
            {
                if (stringToSearch[^i] == 0x0)
                    continue;

                if (stringToSearch[^i] != 0x20)
                    break;

                stringToSearch[^i] = 0x0;
                numRemoved++;
            }

            Module.Memory.SetArray(stringPointer, stringToSearch);
            Registers.AX = numRemoved;
        }

        private ReadOnlySpan<byte> othusn => Module.Memory.GetVariable("OTHUSN").ToSpan();

        /// <summary>
        ///     Returns number of bytes session will need, or -1=error in data
        ///
        ///     Signature: int fsdroom(int tmpmsg, char *fldspc, int amode)
        /// </summary>
        private void fsdroom()
        {
            //TODO: Need to learn more about FSD items
            _logger.Warn("Not properly suppported, returning 1k by default");
            Registers.AX = 0x400;
        }

        /// <summary>
        ///     Btrieve Step Operation Supporting Locks
        ///
        ///     Signature: int stpbtvl (void *recptr, int stpopt, int loktyp)
        /// </summary>
        private void stpbtvl()
        {
            if (_currentBtrieveFile == null)
                throw new FileNotFoundException("Current Btrieve file hasn't been set using SETBTV()");

            var btrieveRecordPointer = GetParameterPointer(0);
            var stpopt = GetParameter(2);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];

            ushort resultCode = 0;
            switch (stpopt)
            {
                case (ushort)EnumBtrieveOperationCodes.StepFirst:
                    resultCode = currentBtrieveFile.StepFirst();
                    break;
                case (ushort)EnumBtrieveOperationCodes.StepNext:
                    resultCode = currentBtrieveFile.StepNext();
                    break;
                default:
                    throw new InvalidEnumArgumentException($"Unknown Btrieve Operation Code: {stpopt}");
            }

            Registers.AX = resultCode;

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));

            //If there's a record, always save it to the btrieve file struct
            if (resultCode == 1)
                Module.Memory.SetArray(btvStruct.data, currentBtrieveFile.GetRecord());
#if DEBUG
            _logger.Info($"Performed Btrieve Step - Record written to {btvStruct.data}, AX: {resultCode}");
#endif

            //If a record pointer was passed in AND the result code isn't 0
            if (!btrieveRecordPointer.Equals(IntPtr16.Empty) && resultCode > 0)
            {
                switch (resultCode)
                {
                    case 1:
                        Module.Memory.SetArray(btrieveRecordPointer, currentBtrieveFile.GetRecord());
                        break;
                }
            }

#if DEBUG
            _logger.Info($"Performed Btrieve Step {(EnumBtrieveOperationCodes)stpopt}, AX: {resultCode}");
#endif

        }

        /// <summary>
        ///     Compares the C string str1 to the C string str2
        ///
        ///     Signature: int strcmp ( const char * str1, const char * str2 )
        /// </summary>
        private void strcmp()
        {

            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);

            var string1 = Module.Memory.GetString(string1Pointer);
            var string2 = Module.Memory.GetString(string2Pointer);

#if DEBUG
            _logger.Info($"Comparing ({string1Pointer}){Encoding.ASCII.GetString(string1)} to ({string2Pointer}){Encoding.ASCII.GetString(string2)}");
#endif

            for (var i = 0; i < string1.Length; i++)
            {
                if (string1[i] == string2[i]) continue;

                //1 < 2 == -1 (0xFFFF)
                //1 > 2 == 1 (0x0001)
                Registers.AX = (ushort)(string1[i] < string2[i] ? 0xFFFF : 1);
                return;
            }
            Registers.AX = 0;
        }

        /// <summary>
        ///     Change to a different user number
        ///
        ///     Signature: void curusr(int newunum)
        /// </summary>
        private void curusr()
        {
            var newUserNumber = GetParameter(0);
            ChannelNumber = newUserNumber;

#if DEBUG
            _logger.Info($"Setting Current User to {newUserNumber}");
#endif
        }

        /// <summary>
        ///     Number of modules currently installed
        /// </summary>
        private ReadOnlySpan<byte> nmods => Module.Memory.GetVariable("NMODS").ToSpan();

        /// <summary>
        ///     This is the pointer, to the pointer, for the MODULE struct because module is declared
        ///     **module in MAJORBBS.H
        /// 
        ///     Pointer -> Pointer -> Struct
        /// </summary>
        private ReadOnlySpan<byte> module => Module.Memory.GetVariable("MODULE-POINTER").ToSpan();

        /// <summary>
        ///     Long Shift Left (Borland C++ Implicit Function)
        ///
        ///     DX:AX == Long Value
        ///     CL == How many to move
        /// </summary>
        private void f_lxlsh()
        {
            var inputValue = (Registers.DX << 16) | Registers.AX;

            var result = inputValue << Registers.CL;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Say good-bye to a user and disconnect (hang up)
        ///
        ///     Signature: void byenow(int msgnum, TYPE p1, TYPE p2,...,pn)
        /// </summary>
        private void byenow()
        {
            prfmsg();
            Registers.AX = 0;
        }

        /// <summary>
        ///     Internal Borland C++ Localle Aware ctype Macros
        ///
        ///     See: CTYPE.H
        /// </summary>
        private ReadOnlySpan<byte> _ctype => Module.Memory.GetVariable("CTYPE").ToSpan();

        /// <summary>
        ///     Points to the ad-hoc Volatile Data Area
        ///
        ///     Signature: char *vdatmp
        /// </summary>
        private ReadOnlySpan<byte> vdatmp => Module.Memory.GetVariable("*VDATMP").ToSpan();

        /// <summary>
        ///     Send prfbuf to a channel & clear
        ///     Multilingual version of outprf(), for now we just call outprf()
        /// </summary>
        private void outmlt() => outprf();

        /// <summary>
        ///     Update the Btrieve current record with a variable length record
        ///
        ///     Signature: void updvbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        private void updvbtv()
        {
            var btrieveRecordPointerPointer = GetParameterPointer(0);

            var currentBtrieveFile = BtrievePointerDictionaryNew[_currentBtrieveFile];

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, currentBtrieveFile.RecordLength);

            currentBtrieveFile.Update(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Updated current Btrieve record ({currentBtrieveFile.CurrentRecordNumber}) with {dataToWrite.Length} bytes");
#endif
        }

        /// <summary>
        ///     Copies a string with a fixed length
        ///
        ///     Signature: stlcpy(char *dest, char *source, int nbytes);
        ///     Return: AX = Offset in Segment
        ///             DX = Data Segment
        /// </summary>
        private void stlcpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);
            var limit = GetParameter(4);

            using var inputBuffer = new MemoryStream();
            var potentialString = Module.Memory.GetArray(sourcePointer, limit);
            for (var i = 0; i < limit; i++)
            {
                if (potentialString[i] == 0x0)

                    break;
                inputBuffer.WriteByte(potentialString[i]);
            }

            //If the value read is less than the limit, it'll be padded with null characters
            //per the MajorBBS Development Guide
            for (var i = inputBuffer.Length; i < limit; i++)
                inputBuffer.WriteByte(0x0);

            Module.Memory.SetArray(destinationPointer, inputBuffer.ToArray());

#if DEBUG
            _logger.Info($"Copied \"{Encoding.ASCII.GetString(inputBuffer.ToArray())}\" ({inputBuffer.Length} bytes) from {sourcePointer} to {destinationPointer}");
#endif
            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
        }

        /// <summary>
        ///     Turn on polling for the specified user number channel
        ///
        ///     This will be called as fast as possible, as often as possible until
        ///     stop_polling() is called
        /// 
        ///     Signature: void begin_polling(int unum,void (*rouptr)())
        /// </summary>
        private void begin_polling()
        {
            var channelNumber = GetParameter(0);
            var routinePointer = GetParameterPointer(1);

            //Unset on the specified channel
            if (routinePointer.Segment == 0 && routinePointer.Offset == 0)
            {

                ChannelDictionary[channelNumber].PollingRoutine = null;
                Registers.AX = 0;

#if DEBUG
                _logger.Info($"Unassigned Polling Routine on Channel {channelNumber}");
#endif
                return;
            }

            ChannelDictionary[channelNumber].PollingRoutine = new IntPtr16(routinePointer.ToSpan());

#if DEBUG
            _logger.Info($"Assigned Polling Routine {ChannelDictionary[channelNumber].PollingRoutine} to Channel {channelNumber}");
#endif

            Registers.AX = 0;
        }

        /// <summary>
        ///     Stop polling for the specified user number channel
        ///
        ///     Signature: void stop_polling(int unum)
        /// </summary>
        private void stop_polling()
        {
            var channelNumber = GetParameter(0);

            ChannelDictionary[channelNumber].PollingRoutine = null;
            Registers.AX = 0;

#if DEBUG
            _logger.Info($"Unassigned Polling Routine on Channel {channelNumber}");
#endif
            return;
        }

        /// <summary>
        ///     Title of the MajorBBS System
        ///
        ///     Signature: char *bbsttl
        /// </summary>
        private ReadOnlySpan<byte> bbsttl
        {
            get
            {
                var title = _configuration["BBS.Title"];

                if (string.IsNullOrEmpty(title))
                    title = "MMBSEmu";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("BBSTTL");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*BBSTTL").ToSpan();
            }
        }

        /// <summary>
        ///     The Company name of the MajorBBS system
        ///
        ///     Signature: char *company
        /// </summary>
        private ReadOnlySpan<byte> company
        {
            get
            {
                var title = _configuration["BBS.CompanyName"];

                if (string.IsNullOrEmpty(title))
                    title = "MMBSEmu";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("COMPANY");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*COMPANY").ToSpan();
            }
        }

        /// <summary>
        ///     Mailing Address Line 1 of the MajorBBS System
        ///
        ///     Signature: char *addres1 (not a typo)
        /// </summary>
        private ReadOnlySpan<byte> addres1
        {
            get
            {
                var title = _configuration["BBS.Address1"];

                if (string.IsNullOrEmpty(title))
                    title = "4101 SW 47th Ave., Suite 101";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("ADDRES1");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*ADDRES1").ToSpan();
            }
        }

        /// <summary>
        ///     Mailing Address Line 2 of the MajorBBS System
        ///
        ///     Signature: char *addres2 (not a typo)
        /// </summary>
        private ReadOnlySpan<byte> addres2
        {
            get
            {
                var title = _configuration["BBS.Address2"];

                if (string.IsNullOrEmpty(title))
                    title = "Fort Lauderdale, FL 33314";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("ADDRES2");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*ADDRES2").ToSpan();
            }
        }

        /// <summary>
        ///     The first phone line connected to the MajorBBS system
        ///
        ///     Signature: char *dataph
        /// </summary>
        private ReadOnlySpan<byte> dataph
        {
            get
            {
                var title = _configuration["BBS.DataPhone"];

                if (string.IsNullOrEmpty(title))
                    title = "(305) 583-7808";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("DATAPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*DATAPH").ToSpan();
            }
        }

        /// <summary>
        ///     The first phone line reserved for live users
        ///
        ///     Signature: char *liveph
        /// </summary>
        private ReadOnlySpan<byte> liveph
        {
            get
            {
                var title = _configuration["BBS.VoicePhone"];

                if (string.IsNullOrEmpty(title))
                    title = "(305) 583-5990";

                if (!title.EndsWith("\0"))
                    title += "\0";

                var titlePointer = Module.Memory.GetVariable("LIVEPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariable("*LIVEPH").ToSpan();
            }
        }

        /// <summary>
        ///     Translate buffer (process any possible text_variables)
        ///
        ///     Signature: char *xlttxv(char *buffer,int size)
        /// </summary>
        private void xlttxv()
        {
            var stringToProcessPointer = GetParameterPointer(0);
            var size = GetParameter(2);

            var stringToProcess = Module.Memory.GetString(stringToProcessPointer);

            var processedString = ProcessTextVariables(stringToProcess);

            if (!Module.Memory.TryGetVariable("XLTTXV", out var resultPointer))
                resultPointer = Module.Memory.AllocateVariable("XLTTXV", 0x800);

            Module.Memory.SetArray(resultPointer, processedString);

            Registers.AX = resultPointer.Offset;
            Registers.DX = resultPointer.Segment;
        }

        /// <summary>
        ///     Strips ANSI from a string
        ///
        ///     Signature: char *stpans(char *str)
        /// </summary>
        private void stpans()
        {
            var stringToStripPointer = GetParameterPointer(0);
            var stringToStrip = Module.Memory.GetString(stringToStripPointer);

            var ansiFound = false;
            foreach (var t in stringToStrip)
            {
                if (t == 0x1B)
                    ansiFound = true;
            }

            if (ansiFound)
                _logger.Warn($"ANSI found but was not stripped. Process not implemented.");

#if DEBUG
            _logger.Info("Ignoring, not stripping ANSI");
#endif

            Registers.AX = stringToStripPointer.Offset;
            Registers.DX = stringToStripPointer.Segment;

        }

        /// <summary>
        ///     Volatile Data Size after all INIT routines are complete
        ///
        ///     This is hard coded to VOLATILE_DATA_SIZE constant
        ///
        ///     Signature: int vdasiz;
        /// </summary>
        private ReadOnlySpan<byte> vdasiz => Module.Memory.GetVariable("VDASIZ").ToSpan();

        /// <summary>
        ///     Performs a Btrieve Query Operation based on KEYS
        ///
        ///     Signature: int is=qrybtv(char *key, int keynum, int qryopt)
        /// </summary>
        private void qrybtv()
        {
            var keyPointer = GetParameterPointer(0);
            var keyNumber = GetParameter(2);
            var queryOption = GetParameter(3);

            var key = Module.Memory.GetString(keyPointer);

            if (queryOption <= 50)
                throw new Exception($"Invalid Query Option: {queryOption}");

            if (keyNumber != 0)
                throw new Exception("No Support for Multiple Keys");

            var result = 0;
            switch (queryOption)
            {
                //Get Equal
                case 55:
                    result = BtrievePointerDictionaryNew[_currentBtrieveFile].HasKey(keyNumber, key);
                    break;
            }

#if DEBUG
            _logger.Info($"Performed Query {queryOption} on {BtrievePointerDictionaryNew[_currentBtrieveFile].FileName} ({_currentBtrieveFile}) with result {result}");
#endif

            Registers.AX = (ushort)result;
        }

        /// <summary>
        ///     Returns the Absolute Position (offset) in the current Btrieve file
        ///
        ///     Signature: long absbtv()
        /// </summary>
        private void absbtv()
        {
            var offset = BtrievePointerDictionaryNew[_currentBtrieveFile].AbsolutePosition;

            Registers.DX = (ushort)(offset >> 16);
            Registers.AX = (ushort)(offset & 0xFFFF);
        }

        /// <summary>
        ///     Gets the Btrieve Record located at the specified absolution position (offset)
        ///
        ///     Signature: void gabbtv(char *recptr, long abspos, int keynum)
        /// </summary>
        private void gabbtv()
        {
            var recordPointer = GetParameterPointer(0);
            var absolutePosition = GetParameterLong(2);
            var keynum = GetParameter(4);

            var record = BtrievePointerDictionaryNew[_currentBtrieveFile].GetRecordByAbsolutePosition((uint)absolutePosition);


            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(_currentBtrieveFile, BtvFileStruct.Size));
            var btvDataPointer = btvStruct.data;

            if (record != null)
            {
                Module.Memory.SetArray(btvStruct.data, record);

#if DEBUG
                _logger.Info($"Performed Btrieve Step - Record written to {btvDataPointer}");
#endif
                Module.Memory.SetArray(recordPointer, record);
            }
        }

        /// <summary>
        ///     Pointer to structure for that user in the user[] array (set by onsys() or instat())
        ///
        ///     Signature: struct user *othusp;
        /// </summary>
        private ReadOnlySpan<byte> othusp => Module.Memory.GetVariable("*OTHUSP").ToSpan();

        /// <summary>
        ///     Pointer to structure for that user in the extusr structure (set by onsys() or instat())
        ///
        ///     Signature: struct extusr *othexp;
        /// </summary>
        private ReadOnlySpan<byte> othexp => Module.Memory.GetVariable("*OTHEXP").ToSpan();

        /// <summary>
        ///     Pointer to structure for that user in the 'usracc' structure (set by onsys() or instat())
        ///
        ///     Signature: struct usracc *othuap;
        /// </summary>
        private ReadOnlySpan<byte> othuap => Module.Memory.GetVariable("*OTHUAP").ToSpan();

        /// <summary>
        ///     Splits the specified string into tokens based on the delimiters
        /// </summary>
        private void strtok()
        {
            var string2SplitPointer = GetParameterPointer(0);
            var stringDelimitersPointer = GetParameterPointer(2);

            if (!Module.Memory.TryGetVariable("STROK-RESULT", out var resultPointer))
                resultPointer = Module.Memory.AllocateVariable("STROK-RESULT", 0x400);

            if (!Module.Memory.TryGetVariable("STROK-ORIGINAL", out var originalPointer))
                originalPointer = Module.Memory.AllocateVariable("STROK-ORIGINAL", 0x400);

            if (!Module.Memory.TryGetVariable("STROK-ORDINAL", out var ordinalPointer))
                ordinalPointer = Module.Memory.AllocateVariable("STROK-ORDINAL", 0x2);

            //If it's the first call, reset the values in memory
            if (!string2SplitPointer.Equals(IntPtr16.Empty))
            {
                Module.Memory.SetArray(originalPointer, Module.Memory.GetString(string2SplitPointer));
                Module.Memory.SetWord(ordinalPointer, 0);
            }

            var ordinal = Module.Memory.GetWord(ordinalPointer);
            var string2Split = Encoding.ASCII.GetString(Module.Memory.GetString(originalPointer, true));
            var stringDelimiter = Encoding.ASCII.GetString(Module.Memory.GetString(stringDelimitersPointer, true));

            var splitString = string2Split.Split(stringDelimiter, StringSplitOptions.RemoveEmptyEntries);

            if (ordinal >= splitString.Length)
            {
                Registers.DX = 0;
                Registers.AX = 0;
                return;
            }

            //Save Result
            Module.Memory.SetArray(resultPointer, Encoding.ASCII.GetBytes(splitString[ordinal]));

            //Increment Ordinal for Next Call and save
            ordinal++;
            Module.Memory.SetWord(ordinalPointer, ordinal);

            Registers.DX = resultPointer.Segment;
            Registers.AX = resultPointer.Offset;
        }

        /// <summary>
        ///     fputs - puts a string on a stream
        ///
        ///     Signature: int fputs(const char *string, FILE *stream);
        /// </summary>
        private void fputs()
        {
            var stringPointer = GetParameterPointer(0);
            var fileStructPointer = GetParameterPointer(2);

            var stringToWrite = Module.Memory.GetString(stringPointer, true);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.Write(stringToWrite);
            fileStream.Flush();
            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Wrote {stringToWrite.Length} bytes from {stringPointer}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Read raw value of CNF option
        ///
        ///     Signature: char *bufard=rawmsg(msgnum)
        /// </summary>
        private void rawmsg()
        {
            var msgnum = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"RAWMSG", out var variablePointer))
                variablePointer = base.Module.Memory.AllocateVariable($"RAWMSG", 0x1000);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}), saved {3} bytes to {4}", msgnum, McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue.Length, variablePointer);
#endif

            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }


        /// <summary>
        ///     Read raw value of CNF option
        ///
        ///     Signature: char result=chropt(msgnum)
        /// </summary>
        private void chropt()
        {
            var msgnum = GetParameter(0);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum)[0];

#if DEBUG
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}): {3}", msgnum, McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue);
#endif

            Registers.AX = outputValue;
        }

        /// <summary>
        ///     Reads a single character from the current pointer of the specified file stream
        ///
        ///     Signature: int fgetc ( FILE * stream )
        /// </summary>
        private void fgetc()
        {
            var fileStructPointer = GetParameterPointer(0);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            var characterRead = fileStream.ReadByte();

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Read 1 byte from {fileStructPointer} (Stream: {fileStruct.curp}): {characterRead:X2}");
#endif

            Registers.AX = (ushort)characterRead;
        }

        /// <summary>
        ///     Case Insensitive Comparison of the C string str1 to the C string str2
        ///
        ///     Signature: int stricmp ( const char * str1, const char * str2 )
        /// </summary>
        private void stricmp()
        {

            var string1Pointer = GetParameterPointer(0);
            var string2Pointer = GetParameterPointer(2);

            var string1 = Module.Memory.GetString(string1Pointer, true);
            var string2 = Module.Memory.GetString(string2Pointer, true);

#if DEBUG
            _logger.Info($"Comparing ({string1Pointer}){Encoding.ASCII.GetString(string1)} to ({string2Pointer}){Encoding.ASCII.GetString(string2)}");
#endif

            if (string1.Length == 0)
            {
                Registers.AX = 0xFFFF;
                return;
            }

            if (string2.Length == 0)
            {
                Registers.AX = 1;
                return;
            }

            for (var i = 0; i < string1.Length; i++)
            {
                //We're at the end of string 2, string 1 is longer
                if (i == string2.Length)
                {
                    Registers.AX = 1;
                    return;
                }

                if (string1[i] == string2[i]) continue;
                if (string1[i] == string2[i] + 32) continue;
                if (string1[i] == string2[i] - 32) continue;

                //1 < 2 == -1 (0xFFFF)
                //1 > 2 == 1 (0x0001)
                Registers.AX = (ushort)(string1[i] < string2[i] ? 0xFFFF : 1);
                return;
            }
            Registers.AX = 0;
        }

        /// <summary>
        ///     Galacticomm's malloc() for debugging
        /// 
        ///     Signature: void * galmalloc(unsigned int size);
        ///     Return: AX = Offset in Segment (host)
        ///             DX = Data Segment
        /// </summary>
        private void galmalloc()
        {
            var size = GetParameter(0);

            var allocatedMemory = Module.Memory.AllocateVariable(null, size);

#if DEBUG
            _logger.Info($"Allocated {size} bytes starting at {allocatedMemory}");
#endif

            Registers.AX = allocatedMemory.Offset;
            Registers.DX = allocatedMemory.Segment;
        }


        /// <summary>
        ///     Ungets character from stream and decreases the internal file position by 1
        ///
        ///     Signature: int ungetc(int character,FILE *stream )
        /// </summary>
        private void fungetc()
        {
            var character = GetParameter(0);
            var fileStructPointer = GetParameterPointer(1);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.Position -= 1;
            fileStream.WriteByte((byte)character);
            fileStream.Position -= 1;

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Unget 1 byte from {fileStructPointer} (Stream: {fileStruct.curp}). New Position: {fileStream.Position}, Character: {(char)character}");
#endif

            Registers.AX = character;
        }

        /// <summary>
        ///     Galacticomm's free() for debugging
        ///
        ///     Signature: void galfree(void *block);
        /// </summary>
        private void galfree()
        {
            //Until we can refactor the memory controller, just return 

            //TODO: Memory is going to be leaked, need to fix this
            return;
        }


        /// <summary>
        ///     Write the input character to the specified stream
        ///
        ///     Signature: int fputc(int character, FILE *stream );
        /// </summary>
        private void f_putc()
        {
            var character = GetParameter(0);
            var fileStructPointer = GetParameterPointer(1);


            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException($"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.WriteByte((byte)character);
            fileStream.Flush();
            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.ToSpan());
            }

#if DEBUG
            _logger.Info($"Character {(char)character} written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Allocate a very large memory region, qty by sizblock bytes
        ///
        ///     Signature: void *alcblok(unsigned qty,unsigned size);
        /// </summary>
        public void alcblok()
        {
            var qty = GetParameter(0);
            var size = GetParameter(1);

            var bigRegion = Module.Memory.AllocateBigMemoryBlock(qty, size);

#if DEBUG
            _logger.Info($"Created Big Memory Region that is {qty} records of {size} bytes in length at {bigRegion}");
#endif

            Registers.AX = bigRegion.Offset;
            Registers.DX = bigRegion.Segment;
        }

        /// <summary>
        ///     Dereference an alcblok()'d region
        ///
        ///     Signature: void *ptrblok(void *bigptr,unsigned index);
        /// </summary>
        public void ptrblok()
        {
            var bigRegionPointer = GetParameterPointer(0);
            var index = GetParameter(2);

            var indexPointer = Module.Memory.GetBigMemoryBlock(bigRegionPointer, index);

#if DEBUG
            _logger.Info($"Retrieved Big Memory block {bigRegionPointer}, returned pointer to index {index}: {indexPointer}");
#endif

            Registers.AX = indexPointer.Offset;
            Registers.DX = indexPointer.Segment;
        }

        /// <summary>
        ///     Gets the extended User Account Information
        /// 
        ///     Signature: struct extusr *exptr=extoff(unum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment  
        /// </summary>
        /// <returns></returns>
        private void extoff()
        {
            var userNumber = GetParameter(0);

            if (!Module.Memory.TryGetVariable($"EXTUSR-{userNumber}", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable($"EXTUSR-{userNumber}", ExtUser.Size);

            //If user isnt online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var extoffPointer = Module.Memory.GetVariable("EXTOFF");
            Module.Memory.SetArray(extoffPointer, variablePointer.ToSpan());

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.ExtUsrAcc.ToSpan());


            Registers.AX = variablePointer.Offset;
            Registers.DX = variablePointer.Segment;
        }

        /// <summary>
        ///     Gets Video RAM Base Address
        ///
        ///     We write this to a dummy address. This is usually used to show custom graphics
        ///     on the Sysop console
        /// 
        ///     Signature: char *frzseg(void)
        /// </summary>
        private void frzseg()
        {
            if(!Module.Memory.HasSegment(0xF000))
                Module.Memory.AddSegment(0xF000);

            Registers.AX = 0x0;
            Registers.DX = 0xF000;
        }

        /// <summary>
        ///     Borland C++ Macro
        ///
        ///     This macro with functional form fills env with information about the current state of the calling environment
        ///     in that point of code execution, so that it can be restored by a later call to longjmp.
        /// 
        ///     Signature: int setjmp(jmp_buf env)
        /// </summary>
        private void setjmp()
        {
            var jmpBufPointer = GetParameterPointer(0);

            var jmpBuf = new JmpBufStruct(Module.Memory.GetArray(jmpBufPointer, JmpBufStruct.Size));

            if (jmpBuf.ip == 0 && jmpBuf.cs == 0)
            {
                //Not a jump -- first set
                jmpBuf.bp = Module.Memory.GetWord(Registers.SS, (ushort) (Registers.BP + 1)); //Base Pointer is pushed and set on the simulated ENTER
                jmpBuf.cs = Registers.CS;
                jmpBuf.di = Registers.DI;
                jmpBuf.ds = Registers.DS;
                jmpBuf.es = Registers.ES;
                jmpBuf.ip = Registers.IP;
                jmpBuf.si = Registers.SI;
                jmpBuf.sp = (ushort) (Registers.SP + 6); //remove the parameter to set stack prior to the call
                jmpBuf.ss = Registers.SS;
                Module.Memory.SetArray(jmpBufPointer, jmpBuf.Data);
                Registers.AX = 0;
            }

#if DEBUG
            _logger.Debug($"{jmpBuf.cs:X4}:{jmpBuf.ip:X4} -- {Registers.AX}");
#endif
        }

        /// <summary>
        ///     Returns if we're at the end, or "done", with the current command
        ///
        ///     Signature: int done=endcnc()
        /// </summary>
        private void endcnc()
        {
            Registers.AX = _inputCurrentCommand < _margvPointers.Count ? (ushort) 0 : (ushort) 1;
        }

        /// <summary>
        ///     longjmp - performs a non-local goto
        ///
        ///     Signature: 
        /// </summary>
        private void longjmp()
        {
            var jmpPointer = GetParameterPointer(0);
            var value = GetParameter(2);

            var jmpBuf = new JmpBufStruct(Module.Memory.GetArray(jmpPointer, JmpBufStruct.Size));

            Registers.BP = jmpBuf.bp;
            Registers.CS = jmpBuf.cs;
            Registers.DI = jmpBuf.di;
            Registers.DS = jmpBuf.ds;
            Registers.ES = jmpBuf.es;
            Registers.IP = (ushort) (jmpBuf.ip + 5);
            Registers.SI = jmpBuf.si;
            Registers.SP = jmpBuf.sp;
            Registers.SS = jmpBuf.ss;
            Registers.AX = value;

#if DEBUG
            _logger.Debug($"{jmpBuf.cs:X4}:{jmpBuf.ip:X4} -- {Registers.AX}");
#endif
        }
    }
}
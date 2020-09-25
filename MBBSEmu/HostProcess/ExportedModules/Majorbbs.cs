using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions that are part of the MajorBBS/WG SDK and included in
    ///     MAJORBBS.H
    /// </summary>
    public class Majorbbs : ExportedModuleBase, IExportedModule
    {
        public IntPtr16 GlobalCommandHandler;

        private IntPtr16 _currentMcvFile
        {
            get => Module.Memory.GetPointer("CURRENT-MCV");
            set => Module.Memory.SetPointer("CURRENT-MCV", value ?? IntPtr16.Empty);
        }
        private readonly Stack<IntPtr16> _previousMcvFile;

        private readonly Stack<IntPtr16> _previousBtrieveFile;

        private readonly List<IntPtr16> _margvPointers;
        private readonly List<IntPtr16> _margnPointers;
        private int _inputCurrentPosition;

        private const ushort VOLATILE_DATA_SIZE = 0x3FFF;
        private const ushort NUMBER_OF_CHANNELS = 0x4;

        private readonly Stopwatch _highResolutionTimer = new Stopwatch();

        private AgentStruct _galacticommClientServerAgent;

        /// <summary>
        ///     Stores all active searches created via fnd1st
        /// </summary>
        private Dictionary<Guid, IEnumerator<string>> _activeSearches =
            new Dictionary<Guid, IEnumerator<string>>();

        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFF;

        public Majorbbs(ILogger logger, IConfiguration configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
            _margvPointers = new List<IntPtr16>();
            _margnPointers = new List<IntPtr16>();
            _inputCurrentPosition = 0;
            _previousMcvFile = new Stack<IntPtr16>(10);
            _previousBtrieveFile = new Stack<IntPtr16>(10);
            _highResolutionTimer.Start();


            //Setup Memory for Variables
            Module.Memory.AllocateVariable("PRFBUF", 0x4000, true); //Output buffer, 8kb
            Module.Memory.AllocateVariable("PRFPTR", 0x4);
            Module.Memory.AllocateVariable("OUTBSZ", sizeof(ushort));
            Module.Memory.SetWord("OUTBSZ", (ushort)8192);
            Module.Memory.AllocateVariable("INPUT", 0xFF); //255 Byte Maximum user Input
            Module.Memory.AllocateVariable("USER", (User.Size * NUMBER_OF_CHANNELS), true);
            Module.Memory.AllocateVariable("*USRPTR", 0x4); //pointer to the current USER record
            Module.Memory.AllocateVariable("STATUS", 0x2); //ushort Status
            Module.Memory.AllocateVariable("CHANNEL", 0x1FE); //255 channels * 2 bytes
            Module.Memory.AllocateVariable("MARGC", sizeof(ushort));
            Module.Memory.AllocateVariable("MARGN", 0x200); //max 128 pointers * 4 bytes each
            Module.Memory.AllocateVariable("MARGV", 0x200); //max 128 pointers * 4 bytes each
            Module.Memory.AllocateVariable("INPLEN", sizeof(ushort));
            Module.Memory.AllocateVariable("USRNUM", 0x2);
            var usraccPointer = Module.Memory.AllocateVariable("USRACC", (UserAccount.Size * NUMBER_OF_CHANNELS), true);
            var usaptrPointer = Module.Memory.AllocateVariable("USAPTR", 0x4);
            Module.Memory.SetArray(usaptrPointer, usraccPointer.ToSpan());

            var usrExtPointer = Module.Memory.AllocateVariable("EXTUSR", (ExtUser.Size * NUMBER_OF_CHANNELS), true);
            var extPtrPointer = Module.Memory.AllocateVariable("EXTPTR", 0x4);
            Module.Memory.SetArray(extPtrPointer, usrExtPointer.ToSpan());

            Module.Memory.AllocateVariable("OTHUAP", 0x04, true); //Pointer to OTHER user
            Module.Memory.AllocateVariable("OTHEXP", 0x04, true); //Pointer to OTHER user
            var ntermsPointer = Module.Memory.AllocateVariable("NTERMS", 0x2); //ushort number of lines
            Module.Memory.SetWord(ntermsPointer, 4); // TODO(make this configurable)

            Module.Memory.AllocateVariable("OTHUSN", 0x2); //Set by onsys() or instat()
            Module.Memory.AllocateVariable("OTHUSP", 0x4, true);
            Module.Memory.AllocateVariable("NXTCMD", IntPtr16.Size); //Holds Pointer to the "next command"
            Module.Memory.SetPointer("NXTCMD", Module.Memory.GetVariablePointer("INPUT"));
            Module.Memory.AllocateVariable("NMODS", 0x2); //Number of Modules Installed
            Module.Memory.SetWord("NMODS", 0x1); //set this to 1 for now
            var modulePointer = Module.Memory.AllocateVariable("MODULE", 0x4); //Pointer to Registered Module
            var modulePointerPointer =
                Module.Memory.AllocateVariable("MODULE-POINTER", 0x4); //Pointer to the Module Pointer
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
            Module.Memory.AllocateVariable("MMUCCR", sizeof(ushort)); //Main Menu Credit Consumption Rate
            Module.Memory.SetWord("MMUCCR", 1);
            Module.Memory.AllocateVariable("PFNLVL", sizeof(ushort));
            Module.Memory.SetWord("PFNLVL", 0);
            Module.Memory.AllocateVariable("VERSION", 5);
            Module.Memory.SetArray("VERSION", Encoding.ASCII.GetBytes("2.00"));
            Module.Memory.AllocateVariable("SYSCYC", sizeof(uint));

            Module.Memory.AllocateVariable("NUMBYTS", sizeof(uint));
            Module.Memory.AllocateVariable("NUMFILS", sizeof(uint));
            Module.Memory.AllocateVariable("NUMBYTP", sizeof(uint));
            Module.Memory.AllocateVariable("NUMDIRS", sizeof(uint));
            Module.Memory.AllocateVariable("NGLOBS", sizeof(ushort));
            Module.Memory.AllocateVariable("FTG", FtgStruct.Size);
            Module.Memory.AllocateVariable("FTGPTR", IntPtr16.Size);
            Module.Memory.SetPointer("FTGPTR", Module.Memory.GetVariablePointer("FTG"));
            Module.Memory.AllocateVariable("TSHMSG", 81); //universal global Tagspec Handler message
            Module.Memory.AllocateVariable("FTFSCB", FtfscbStruct.Size);

            Module.Memory.AllocateVariable("SV", SysvblStruct.Size);
            Module.Memory.AllocateVariable("EURMSK", 1);
            Module.Memory.SetByte("EURMSK", (byte)0x7F);
            Module.Memory.AllocateVariable("FSDSCB", 82, true);
            Module.Memory.AllocateVariable("BB", 4); //pointer for current btrieve struct
            Module.Memory.AllocateVariable("FSDEMG", 80); //error message for FSD
            Module.Memory.AllocateVariable("SYSKEY", 6, true);
            Module.Memory.SetArray("SYSKEY", Encoding.ASCII.GetBytes("SYSOP\0"));
            Module.Memory.AllocateVariable("CLINGO", sizeof(ushort));
            Module.Memory.AllocateVariable("LANGUAGES", LingoStruct.Size, true);
            Module.Memory.SetArray("LANGUAGES", new LingoStruct().Data); //ever user will have the same lingo struct
            Module.Memory.AllocateVariable("**LANGUAGES", IntPtr16.Size);
            Module.Memory.SetPointer("**LANGUAGES", Module.Memory.GetVariablePointer("*LANGUAGES"));
            Module.Memory.AllocateVariable("ERRCOD", sizeof(ushort));
            Module.Memory.AllocateVariable("CURRENT-MCV", IntPtr16.Size);
            Module.Memory.AllocateVariable("DIGALW", sizeof(ushort));
            Module.Memory.SetWord("DIGALW", 1);

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
            var resultByte = 0;
            for (var i = 0; i < 256; i++)
            {
                if (i == 32)
                {
                    //IS_SP
                    resultByte = 1;
                }
                else if (i >= 48 && i <= 57)
                {
                    //IS_DIG
                    resultByte = 2;
                }
                else if (i >= 65 && i <= 90)
                {
                    //IS_LOW
                    resultByte = 4;
                }
                else if (i >= 97 && i <= 122)
                {
                    //IS_UP
                    resultByte = 8;
                }
                else if (i >= 33 && i <= 47)
                {
                    //IS_PUN
                    resultByte = 64;
                }
                else if (i >= 91 && i <= 96)
                {
                    //IS_PUN
                    resultByte = 64;
                }
                else if (i >= 123 && i <= 126)
                {
                    //IS_PUN
                    resultByte = 64;
                }
                else
                {
                    resultByte = 0;
                }

                Module.Memory.SetByte(ctypePointer.Segment, (ushort)(ctypePointer.Offset + i + 1), (byte)resultByte);
            }
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

            if (!Module.Memory.TryGetVariablePointer($"VDA-{channelNumber}", out var vdaChannelPointer))
                vdaChannelPointer = Module.Memory.AllocateVariable($"VDA-{channelNumber}", VOLATILE_DATA_SIZE);

            Module.Memory.SetArray(Module.Memory.GetVariablePointer("VDAPTR"), vdaChannelPointer.ToSpan());
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("USRNUM"), channelNumber);

            ChannelDictionary[channelNumber].StatusChange = false;
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), ChannelDictionary[channelNumber].Status);

            var userBasePointer = Module.Memory.GetVariablePointer("USER");
            var currentUserPointer = new IntPtr16(userBasePointer.ToSpan());
            currentUserPointer.Offset += (ushort)(User.Size * channelNumber);

            //Update User Array and Update Pointer to point to this user
            Module.Memory.SetArray(currentUserPointer, ChannelDictionary[channelNumber].UsrPtr.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("*USRPTR"), currentUserPointer.ToSpan());

            var userAccBasePointer = Module.Memory.GetVariablePointer("USRACC");
            var currentUserAccPointer = new IntPtr16(userAccBasePointer.ToSpan());
            currentUserAccPointer.Offset += (ushort)(UserAccount.Size * channelNumber);
            Module.Memory.SetArray(currentUserAccPointer, ChannelDictionary[channelNumber].UsrAcc.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("USAPTR"), currentUserAccPointer.ToSpan());

            var userExtAccBasePointer = Module.Memory.GetVariablePointer("EXTUSR");
            var currentExtUserAccPointer = new IntPtr16(userExtAccBasePointer.ToSpan());
            currentExtUserAccPointer.Offset += (ushort)(ExtUser.Size * channelNumber);
            Module.Memory.SetArray(currentExtUserAccPointer, ChannelDictionary[channelNumber].ExtUsrAcc.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("EXTPTR"), currentExtUserAccPointer.ToSpan());

            //Write Blank Input
            var inputMemory = Module.Memory.GetVariablePointer("INPUT");
            Module.Memory.SetByte(inputMemory.Segment, inputMemory.Offset, 0x0);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("NXTCMD"), inputMemory.ToSpan());

            //Reset PRFPTR
            Module.Memory.SetPointer("PRFPTR", Module.Memory.GetVariablePointer("PRFBUF"));
            Module.Memory.SetZero(Module.Memory.GetVariablePointer("PRFBUF"), 0x4000);

            //Set FSDSCB Pointer for Current User
            if (!Module.Memory.TryGetVariablePointer($"FSD-Fsdscb-{ChannelNumber}", out var channelFsdscb))
                channelFsdscb = Module.Memory.AllocateVariable($"FSD-Fsdscb-{ChannelNumber}", FsdscbStruct.Size);

            Module.Memory.SetPointer("*FSDSCB", channelFsdscb);

            //Processing Channel Input
            if (ChannelDictionary[channelNumber].Status == 3)
                ProcessChannelInput(channelNumber);
        }

        /// <summary>
        ///     Handles processing of Channel Input by setting the INPUT value of the dta in the channel InputCommand buffer
        ///     and then calling parsin()
        /// </summary>
        /// <param name="channelNumber"></param>
        public void ProcessChannelInput(ushort channelNumber)
        {
            //Take INPUT from Channel and save it to INPUT
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            Module.Memory.SetZero(inputPointer, 0xFF);
            var inputFromChannel = ChannelDictionary[channelNumber].InputCommand;
            Module.Memory.SetArray(inputPointer, inputFromChannel);
            var inputLength = (ushort)ChannelDictionary[ChannelNumber].InputCommand.Length;
            Module.Memory.SetWord("INPLEN", inputLength);

            ChannelDictionary[channelNumber].UsrPtr.Flags = 0;

            parsin();

            //Set Concex flag on 0 input
            //TODO -- Need to verify this is correct
            if (Module.Memory.GetWord("INPLEN") == 0)
                ChannelDictionary[channelNumber].UsrPtr.Flags |= (uint)EnumRuntimeFlags.Concex;

        }

        /// <summary>
        ///     Updates the Session with any information from memory before execution finishes
        /// </summary>
        /// <param name="channel"></param>
        public void UpdateSession(ushort channel)
        {
            //Set the Channel Status
            if (ChannelDictionary[ChannelNumber].OutputEmptyStatus && !ChannelDictionary[ChannelNumber].StatusChange)
            {
                ChannelDictionary[ChannelNumber].Status = 5;
                ChannelDictionary[ChannelNumber].StatusChange = true;
            }
            else if (ChannelDictionary[ChannelNumber].StatusChange)
            {
                ChannelDictionary[ChannelNumber].Status = Module.Memory.GetWord(Module.Memory.GetVariablePointer("STATUS"));
            }
            else
            {
                ChannelDictionary[ChannelNumber].Status = 1;
            }

            var userPointer = Module.Memory.GetVariablePointer("USER");

            ChannelDictionary[ChannelNumber].UsrPtr.Data = Module.Memory.GetArray(userPointer.Segment,
                (ushort)(userPointer.Offset + (User.Size * channel)), User.Size).ToArray();

#if DEBUG
            _logger.Info($"{channel}->status == {ChannelDictionary[ChannelNumber].Status}");
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
            //_logger.Info($"Invoking MAJORBBS.DLL:{Ordinals.MAJORBBS[ordinal]}");

            switch (ordinal)
            {
                case 628:
                    return usrnum;
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
                    SetupGENBB();
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
                case 414:
                    return mmuccr;
                case 468:
                    return pfnlvl;
                case 442:
                    return nxtcmd;
                case 909:
                    return version;
                case 1048:
                    return syscyc;
                case 440:
                    return numfils;
                case 439:
                    return numbyts;
                case 901:
                    return numbytp;
                case 1072:
                    return numdirs;
                case 733:
                    return nglobs;
                case 304:
                    return ftgptr;
                case 606:
                    return tshmsg;
                case 288:
                    return ftfscb;
                case 462:
                    return outbsz;
                case 593:
                    return sv;
                case 194:
                    return eurmsk;
                case 263:
                    return fsdscb;
                case 741:
                    return bb;
                case 242:
                    return fsdemg;
                case 718:
                    return syskey;
                case 761:
                    return clingo;
                case 762:
                    return languages;
                case 193:
                    return errcod;
                case 169:
                    return digalw;
            }

            if (offsetsOnly)
            {
                return new IntPtr16(Segment, ordinal).ToSpan();
            }

            switch (ordinal)
            {
                //Ignored Ones
                case 561: //srand() handled internally
                case 614: //unfrez -- unlocks video memory, ignored
                case 174: //DSAIRP
                case 189: //ENAIRP
                case 512: //RSTRXF
                case 114: //CLSXRF
                case 340: //HOWBUY -- emits how to buy credits, ignored for MBBSEmu
                case 564: //STANSI -- sets ANSI to user default, ignoring as ANSI is always on
                case 526: //SCBLANK -- set video buffer to blank
                case 563: //SSTATR -- set video attribute
                case 548: //SETWIN -- set window parameters when drawing to video (Screen)
                case 392: //LOCATE -- moves cursor (local screen, not telnet session)
                case 513: //RSTWIN -- restore window parameters (local screen)
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
                case 170:
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
                case 999: //gabbtvl
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
                case 832:
                    alcblok();
                    break;
                case 880:
                case 833:
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
                case 121:
                    cncall();
                    break;
                case 419:
                    morcnc();
                    break;
                case 125:
                    cncint();
                    break;
                case 656:
                    f_ludiv();
                    break;
                case 317:
                    getbtv();
                    break;
                case 484:
                    qnpbtv();
                    break;
                case 134:
                    cofdat();
                    break;
                case 862:
                    htrval();
                    break;
                case 409:
                    memcpy();
                    break;
                case 408:
                    memcmp();
                    break;
                case 433:
                    nliniu();
                    break;
                case 661:
                    f_lxursh();
                    break;
                case 850:
                    access();
                    break;
                case 447:
                    omdbtv();
                    break;
                case 602:
                    tokopt();
                    break;
                case 201:
                    farcoreleft();
                    break;
                case 173:
                    dostounix();
                    break;
                case 320:
                    getdate();
                    break;
                case 652:
                    zonkhl();
                    break;
                case 315:
                    genrnd();
                    break;
                case 501:
                    rmvwht();
                    break;
                case 655:
                    f_lmod();
                    break;
                case 930:
                    register_agent();
                    break;
                case 421:
                    msgscan();
                    break;
                case 321:
                    getdtd();
                    break;
                case 132:
                    cntdir();
                    break;
                case 117:
                    clsbtv();
                    break;
                case 322:
                    getenv();
                    break;
                case 302:
                    ftgnew();
                    break;
                case 154:
                    datofc();
                    break;
                case 305:
                    ftgsbm();
                    break;
                case 386:
                    listing();
                    break;
                case 70:
                    anpbtv();
                    break;
                case 607:
                case 461: //otstscrd -- always return true
                    tstcrd();
                    break;
                case 754:
                    strnicmp();
                    break;
                case 109:
                    clock();
                    break;
                case 581:
                    strncmp();
                    break;
                case 180:
                    dupdbtv();
                    break;
                case 451:
                    open();
                    break;
                case 413:
                    mkdir();
                    break;
                case 896:
                    getftime();
                    break;
                case 110:
                    close();
                    break;
                case 234:
                    fsdapr();
                    break;
                case 586:
                    strol();
                    break;
                case 238:
                    fsdbkg();
                    break;
                case 260:
                    fsdrft();
                    break;
                case 878:
                    fsdrhd();
                    break;
                case 241:
                    fsdego();
                    break;
                case 641:
                    vfyadn();
                    break;
                case 249:
                    fsdnan();
                    break;
                case 252:
                    fsdord();
                    break;
                case 265:
                    fsdxan();
                    break;
                case 558:
                    sortstgs();
                    break;
                case 381:
                    lastwd();
                    break;
                case 554:
                    skpwrd();
                    break;
                case 215:
                    findtvar();
                    break;
                case 182:
                    echonu();
                    break;
                case 348:
                    injoth();
                    break;
                case 577:
                    stripb();
                    break;
                case 399:
                    makhdl();
                    break;
                case 362:
                    issupc();
                    break;
                case 887:
                    initask();
                    break;
                case 390:
                    lngrnd();
                    break;
                case 51:
                    aabbtv();
                    break;
                case 609:
                    uidkey();
                    break;
                case 457:
                    othkey();
                    break;
                case 1040:
                    ul2as();
                    break;
                case 480:
                    profan();
                    break;
                case 131:
                    cncyesno();
                    break;
                case 579:
                    strlwr();
                    break;
                case 701:
                    printf();
                    break;
                case 314:
                    gen_haskey();
                    break;
                case 130:
                    cncwrd();
                    break;
                case 133:
                    cntrbtv();
                    break;
                case 469:
                    pltile();
                    break;
                case 155:
                    daytoday();
                    break;
                case 127:
                    cncnum();
                    break;
                case 339:
                    hexopt();
                    break;
                case 449:
                    onsys();
                    break;
                case 517:
                    rtstcrd();
                    break;
                case 660:
                    f_lxrsh();
                    break;
                case 157:
                    dcdate();
                    break;
                case 695:
                    fndnxt();
                    break;
                default:
                    _logger.Error($"Unknown Exported Function Ordinal in MAJORBBS: {ordinal}:{Ordinals.MAJORBBS[ordinal]}");
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in MAJORBBS: {ordinal}:{Ordinals.MAJORBBS[ordinal]}");
            }

            return null;
        }

        /// <summary>
        ///     Sets up the Generic MajorBBS Btrieve Database for Use
        /// </summary>
        private void SetupGENBB()
        {
            //If it's already setup, bail
            if (Module.Memory.TryGetVariablePointer("GENBB", out _))
                return;

            var btvFileStructPointer = Module.Memory.AllocateVariable("BBSGEN-STRUCT", BtvFileStruct.Size);
            var btvFileName = Module.Memory.AllocateVariable("BBSGEN-NAME", 11); //BBSGEN.DAT\0
            Module.Memory.SetArray("BBSGEN-NAME", Encoding.ASCII.GetBytes($"BBSGEN.DAT\0"));
            var btvDataPointer =
                Module.Memory.AllocateVariable("BBSGEN-POINTER", 8192); //GENSIZ -- Defined in MAJORBBS.H

            var newBtvStruct = new BtvFileStruct { filenam = btvFileName, reclen = 8192, data = btvDataPointer };

            BtrieveSaveProcessor(btvFileStructPointer,
                new BtrieveFileProcessor(_fileFinder, "BBSGEN.DAT", Directory.GetCurrentDirectory()));

            Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.Data);

            var genBBPointer = Module.Memory.AllocateVariable("GENBB", 0x4); //Pointer to GENBB BTRIEVE File
            Module.Memory.SetArray(genBBPointer, btvFileStructPointer.ToSpan());
        }

        /// <summary>
        ///     Get the current calendar time as a value of type time_t
        ///     Epoch Time
        ///
        ///     Signature: time_t time (time_t* timer);
        ///     Return: Value is 32-Bit TIME_T (DX:AX)
        /// </summary>
        private void time()
        {
            //For now, ignore the input pointer for time_t
            var passedSeconds = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            Registers.DX = (ushort)(passedSeconds >> 16);
            Registers.AX = (ushort)(passedSeconds & 0xFFFF);

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
            if (!Module.Memory.TryGetVariablePointer("GMDNAM", out var variablePointer))
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

            if (inputBuffer[0] == 0x0 || sourcePointer == IntPtr16.Empty)
            {
                Module.Memory.SetByte(destinationPointer, 0);
#if DEBUG
                _logger.Warn($"Source ({sourcePointer}) is NULL");
#endif
            }
            else
            {
                Module.Memory.SetArray(destinationPointer, inputBuffer);
#if DEBUG
                //_logger.Info($"Copied {inputBuffer.Length} bytes from {sourcePointer} to {destinationPointer} -> {Encoding.ASCII.GetString(inputBuffer)}");
#endif
            }

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
            _logger.Info(
                $"Copied \"{Encoding.ASCII.GetString(inputBuffer.ToArray())}\" ({inputBuffer.Length} bytes) from {sourcePointer} to {destinationPointer}");
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
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("MODULE"), destinationPointer.ToSpan());

            var moduleStruct = Module.Memory.GetArray(destinationPointer, 61);

            //Description for Main Menu
            var moduleDescription = Encoding.Default.GetString(moduleStruct.ToArray(), 0, 25);
            Module.ModuleDescription = moduleDescription;
#if DEBUG
            _logger.Info($"MODULE pointer ({Module.Memory.GetVariablePointer("MODULE")}) set to {destinationPointer}");
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
                Module.EntryPoints[moduleRoutines[i]] = new IntPtr16(BitConverter.ToUInt16(routineEntryPoint, 2),
                    BitConverter.ToUInt16(routineEntryPoint, 0));

#if DEBUG
                _logger.Info(
                    $"Routine {moduleRoutines[i]} set to {BitConverter.ToUInt16(routineEntryPoint, 2):X4}:{BitConverter.ToUInt16(routineEntryPoint, 0):X4}");
#endif
            }

            //usrptr->state is the Module Number in use, as assigned by the host process
            Registers.AX = (ushort)Module.StateCode;
            if(string.IsNullOrEmpty(Module.MenuOptionKey)) Module.MenuOptionKey = Module.StateCode.ToString();
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

            var mcvFileName = Encoding.Default.GetString(Module.Memory.GetString(sourcePointer, true));

            //If the MCV file doesn't exist, but the MSG does -- we need to build the MCV
            if (!File.Exists(Path.Combine(Module.ModulePath, mcvFileName)) &&
                File.Exists(
                    Path.Combine(Module.ModulePath, mcvFileName.Replace("mcv", "msg", StringComparison.InvariantCultureIgnoreCase))
            ))
            {
                // triggers MSG -> MCV compilation
                _ = new MsgFile(Module.ModulePath,
                    mcvFileName.Replace(".mcv", string.Empty, StringComparison.InvariantCultureIgnoreCase));
            }

            var offset = McvPointerDictionary.Allocate(new McvFile(_fileFinder, mcvFileName, Module.ModulePath));

            if (_currentMcvFile != null)
                _previousMcvFile.Push(_currentMcvFile);

            //Open just sets whatever the currently active MCV file is -- overwriting whatever was there
            _currentMcvFile = new IntPtr16(ushort.MaxValue, (ushort)offset);

#if DEBUG
            _logger.Info(
                $"Opened MCV file: {mcvFileName}, assigned to {ushort.MaxValue:X4}:{offset:X4}");
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

            if (!Module.Memory.TryGetVariablePointer($"STGOPT_{_currentMcvFile.Offset}_{msgnum}",
                out var variablePointer))
            {
                var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

                variablePointer = base.Module.Memory.AllocateVariable($"STGOPT_{_currentMcvFile.Offset}_{msgnum}",
                    (ushort)outputValue.Length);

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

            if (!Module.Memory.TryGetVariablePointer($"L2AS", out var variablePointer))
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
            var stringToLong = Encoding.ASCII.GetString(Module.Memory.GetString(sourcePointer, true)).Trim();

            var outputValue = GetLeadingNumberFromString(stringToLong, out var success);

            Registers.DX = (ushort)(outputValue >> 16);
            Registers.AX = (ushort)(outputValue & 0xFFFF);

            if (success)
            {
                Registers.F.ClearFlag(EnumFlags.CF);
#if DEBUG
                //_logger.Info($"Cast {stringToLong} ({sourcePointer}) to {outputValue} long");
#endif
            }
            else
            {
                Registers.F.SetFlag(EnumFlags.CF);

#if DEBUG
                _logger.Warn($"Unable to cast {stringToLong} ({sourcePointer}) to long");
#endif
            }
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

            RealignStack(8);
        }

        /// <summary>
        ///     Case ignoring string match
        ///
        ///     Signature: int match=sameas(char *stgl, char* stg2)
        ///     Returns: AX = 1 if match
        /// </summary>
        private void sameas()
        {
            stricmp();

            Registers.AX = (Registers.AX == 0 ? (ushort) 1 : (ushort) 0);
        }

        /// <summary>
        ///     Property with the User Number (Channel) of the user currently being serviced
        ///
        ///     Signature: int usrnum
        ///     Retrurns: int == User Number (Channel)
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usrnum => base.Module.Memory.GetVariablePointer("USRNUM").ToSpan();

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

            if (!Module.Memory.TryGetVariablePointer($"USRACC-{userNumber}", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable($"USRACC-{userNumber}", UserAccount.Size);

            //If user isnt online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var uacoffPointer = Module.Memory.GetVariablePointer("UACOFF");
            Module.Memory.SetArray(uacoffPointer, variablePointer.ToSpan());

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.UsrAcc.Data);


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

            var output = Module.Memory.GetString(sourcePointer, false);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 2);

            var prfptrPointer = Module.Memory.GetVariablePointer("PRFPTR");
            var pointerPosition = Module.Memory.GetPointer(prfptrPointer);
            Module.Memory.SetArray(pointerPosition, formattedMessage);

            //Update prfptr value
            pointerPosition.Offset += (ushort)(formattedMessage.Length - 1);
            Module.Memory.SetPointer("PRFPTR", pointerPosition);

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
            //Set prfptr to the base address of prfbuf
            Module.Memory.SetPointer("PRFPTR", Module.Memory.GetVariablePointer("PRFBUF"));
            Module.Memory.SetByte(Module.Memory.GetVariablePointer("PRFBUF"), 0);

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

            var basePointer = Module.Memory.GetVariablePointer("PRFBUF");
            var currentPointer = Module.Memory.GetPointer(Module.Memory.GetVariablePointer("PRFPTR"));

            var outputLength = (ushort)(currentPointer.Offset - basePointer.Offset);

            var outputBuffer = Module.Memory.GetArray(basePointer, outputLength);

            var outputBufferProcessed = FormatOutput(outputBuffer);

            if(ChannelDictionary.ContainsKey(userChannel))
                ChannelDictionary[userChannel].SendToClient(outputBufferProcessed.ToArray());

#if DEBUG
            _logger.Debug($"Sent {outputBuffer.Length} bytes to Channel {userChannel}");
#endif

            Module.Memory.SetZero(basePointer, outputLength);

            //Set prfptr to the base address of prfbuf
            Module.Memory.SetPointer("PRFPTR", Module.Memory.GetVariablePointer("PRFBUF"));
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
            _logger.Info($"Deducted {creditsToDeduct} from the current users account (Ignored)");
#endif

            Registers.AX = 1;
        }

        /// <summary>
        ///     Points to that channels 'user' struct
        ///
        ///     Signature: struct user *usrptr;
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usrptr => Module.Memory.GetVariablePointer("*USRPTR").ToSpan();

        /// <summary>
        ///     Like prf(), but the control string comes from an .MCV file
        ///
        ///     Signature: void prfmsg(msgnum,p1,p2, ..• ,pn);
        /// </summary>
        /// <returns></returns>
        private void prfmsg()
        {
            var messageNumber = GetParameter(0);

            if (!McvPointerDictionary[_currentMcvFile.Offset].Messages
                .TryGetValue(messageNumber, out var outputMessage))
            {
                _logger.Warn(
                    $"prfmsg() unable to locate message number {messageNumber} in current MCV file {McvPointerDictionary[_currentMcvFile.Offset].FileName}");
                return;
            }

            var formattedMessage = FormatPrintf(outputMessage, 1);

            var currentPrfPositionPointer = Module.Memory.GetPointer(Module.Memory.GetVariablePointer("PRFPTR"));

            Module.Memory.SetArray(currentPrfPositionPointer, formattedMessage);
            currentPrfPositionPointer.Offset += (ushort)formattedMessage.Length;
            Module.Memory.SetByte(currentPrfPositionPointer, 0x0); //Null terminate it

#if DEBUG
            _logger.Info($"Added {formattedMessage.Length} bytes to the buffer from message number {messageNumber}");
#endif
            //Update Pointer
            Module.Memory.SetPointer("PRFPTR", currentPrfPositionPointer);
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
                var pointer = Module.Memory.GetVariablePointer("CHANNEL");

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
            _logger.Info(
                $"Added {Encoding.Default.GetString(string2)} credits to user account {Encoding.Default.GetString(string1)} (unlimited -- this function is ignored)");
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
            //_logger.Info($"Generated random number {randomValue} and saved it to AX");
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

            if (!Module.Memory.TryGetVariablePointer("NCDATE", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable("NCDATE", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Info(
                $"Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
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

            //Unpack the Date
            var year = ((packedDate >> 9) & 0x007F) + 1980;
            var month = (packedDate >> 5) & 0x000F;
            var day = packedDate & 0x001F;
            var outputDate = $"{day:D2}/{month:D2}/{year % 100}\0";

            if (!Module.Memory.TryGetVariablePointer("NCEDAT", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable("NCEDAT", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Info(
                $"Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
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
                _logger.Info("Enqueue Previous MCV File: {0} (Pointer: {1})",
                    McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile);
#endif
            }

            _currentMcvFile = mcvFilePointer;

#if DEBUG
            _logger.Info("Set Current MCV File: {0} (Pointer: {1})",
                McvPointerDictionary[_currentMcvFile.Offset].FileName, mcvFilePointer);
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
            if (_previousMcvFile.Count == 0)
            {
#if DEBUG
                _logger.Warn($"Queue Empty, Ignoring");
#endif
                Registers.AX = 0;
                return;
            }

            _currentMcvFile = _previousMcvFile.Pop();
#if DEBUG
            _logger.Info(
                $"Reset Current MCV to {McvPointerDictionary[_currentMcvFile.Offset].FileName} ({_currentMcvFile}) (Queue Depth: {_previousMcvFile.Count})");
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

            var btrieveFile = new BtrieveFileProcessor(_fileFinder, fileName, Module.ModulePath);

            //Setup Pointers
            var btvFileStructPointer = Module.Memory.AllocateVariable($"{fileName}-STRUCT", BtvFileStruct.Size);
            var btvFileNamePointer =
                Module.Memory.AllocateVariable($"{fileName}-NAME", (ushort)(btrieveFilename.Length + 1));
            var btvDataPointer = Module.Memory.AllocateVariable($"{fileName}-RECORD", maxRecordLength);

            var newBtvStruct = new BtvFileStruct
            { filenam = btvFileNamePointer, reclen = maxRecordLength, data = btvDataPointer };
            BtrieveSaveProcessor(btvFileStructPointer, btrieveFile);
            Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.Data);
            Module.Memory.SetArray(btvFileNamePointer, btrieveFilename);
            Module.Memory.SetPointer("BB", btvFileStructPointer);

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

            var currentBtrieveFile = Module.Memory.GetPointer("BB");

            if (currentBtrieveFile != IntPtr16.Empty)
                _previousBtrieveFile.Push(currentBtrieveFile);

            Module.Memory.SetPointer("BB", btrieveFilePointer);

#if DEBUG
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(btrieveFilePointer, BtvFileStruct.Size));
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
            var btrieveRecordPointer = GetParameterPointer(0);
            var stpopt = GetParameter(2);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

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
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));

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

            Module.Memory.SetPointer("BB", _previousBtrieveFile.Pop());

#if DEBUG
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));
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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, currentBtrieveFile.LoadedFile.RecordLength);

            currentBtrieveFile.Update(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Updated current Btrieve record ({currentBtrieveFile.Position}) with {dataToWrite.Length} bytes");
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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointer, currentBtrieveFile.LoadedFile.RecordLength);

            currentBtrieveFile.Insert(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Inserted Btrieve record at {currentBtrieveFile.Position} with {dataToWrite.Length} bytes");
#endif
            Registers.AX = 1;
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
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
            var record = Module.Memory.GetArray(btrieveRecordPointer, recordLength);

            currentBtrieveFile.Insert(record.ToArray());

#if DEBUG
            _logger.Info(
                $"Inserted Variable Btrieve record at {currentBtrieveFile.Position} with {recordLength} bytes");
#endif
        }

        /// <summary>
        ///     Raw status from btusts, where appropriate
        ///
        ///     Signature: int status
        ///     Returns: Segment holding the Users Status
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> status => Module.Memory.GetVariablePointer("STATUS").ToSpan();

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
                throw new OutOfMemoryException(
                    $"SPR write is > 1k ({formattedMessage.Length}) and would overflow pre-allocated buffer");

            if (!Module.Memory.TryGetVariablePointer("SPR", out var variablePointer))
            {
                //allocate 1k for the SPR buffer
                variablePointer = base.Module.Memory.AllocateVariable("SPR", 0x400);
            }

            Module.Memory.SetArray(variablePointer, formattedMessage);

#if DEBUG
            _logger.Info($"Added {formattedMessage.Length} bytes to the buffer: {Encoding.ASCII.GetString(formattedMessage)}");
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

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Long Division (Borland C++ Implicit Function)
        ///
        ///     Input: Two long values on stack (arg1/arg2)
        ///     Output: DX:AX = quotient
        ///             DI:SI = remainder -- no remainder in early C++
        /// </summary>
        /// <returns></returns>
        private void f_ldiv()
        {

            int arg1 = (GetParameter(1) << 16) | GetParameter(0);
            int arg2 = (GetParameter(3) << 16) | GetParameter(2);

            var quotient = Math.DivRem(arg1, arg2, out var remainder);

            Registers.DX = (ushort)(quotient >> 16);
            Registers.AX = (ushort)(quotient & 0xFFFF);
            //Registers.DI = (ushort)(remainder >> 16);
            //Registers.SI = (ushort)(remainder & 0xFFFF);

            RealignStack(8);
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
            var lineprefixOffset = GetParameter(2);
            var lineprefixSegment = GetParameter(3);

            var mdfName = _fileFinder.FindFile(Module.ModulePath, GetParameterFilename(0));

            var lineprefixBytes = Module.Memory.GetString(lineprefixSegment, lineprefixOffset);
            var lineprefix = Encoding.ASCII.GetString(lineprefixBytes);

            //Setup Host Memory Variables Pointer
            if (!Module.Memory.TryGetVariablePointer($"SCNMDF", out var variablePointer))
                variablePointer = base.Module.Memory.AllocateVariable("SCNMDF", 0xFF);

            var recordFound = false;
            foreach (var line in File.ReadAllLines(Path.Combine(Module.ModulePath, mdfName)))
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
            uint arg1 = (uint)(GetParameter(1) << 16) | GetParameter(0);
            uint arg2 = (uint)(GetParameter(3) << 16) | GetParameter(2);

            var result = arg1 % arg2;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);

            RealignStack(8);
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
        private ReadOnlySpan<byte> prfbuf => Module.Memory.GetVariablePointer("*PRFBUF").ToSpan();

        /// <summary>
        ///     Copies characters from a string
        ///
        ///     Signature: char *strncpy(char *destination, const char *source, size_t num)
        /// </summary>
        /// <returns></returns>
        private void strncpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var source = GetParameterString(2);
            var numberOfBytesToCopy = GetParameter(4);

            Registers.DX = destinationPointer.Segment;
            Registers.AX = destinationPointer.Offset;

            for (var i = 0; i < numberOfBytesToCopy; i++, destinationPointer++)
            {
                if (source[i] == 0x0)
                {
                    //Write remaining nulls
                    for (var j = i; j < numberOfBytesToCopy; j++, destinationPointer++)
                        Module.Memory.SetByte(destinationPointer, 0x0);

                    break;
                }

                Module.Memory.SetByte(destinationPointer, (byte) source[i]);
            }
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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            ushort result;

            var keyValue = Module.Memory.GetArray(keyPointer,
                currentBtrieveFile.GetKeyLength(keyNum));

            switch ((EnumBtrieveOperationCodes)obtopt)
            {
                //GetEqual
                case EnumBtrieveOperationCodes.GetEqual:
                    {
                        result = currentBtrieveFile.SeekByKey(keyNum, keyValue, EnumBtrieveOperationCodes.GetKeyEqual);
                        break;
                    }
                case EnumBtrieveOperationCodes.GetLast when keyNum == 0 && keyPointer == IntPtr16.Empty:
                    {
                        result = currentBtrieveFile.StepLast();
                        break;
                    }
                case EnumBtrieveOperationCodes.GetFirst:
                case EnumBtrieveOperationCodes.GetGreaterOrEqual:
                case EnumBtrieveOperationCodes.GetLessOrEqual:
                case EnumBtrieveOperationCodes.GetGreater:
                case EnumBtrieveOperationCodes.GetLess:
                    {
                        result = currentBtrieveFile.SeekByKey(keyNum, keyValue,
                            (EnumBtrieveOperationCodes)obtopt);
                        break;
                    }
                default:
                    throw new Exception($"Unsupported Btrieve Operation: {(EnumBtrieveOperationCodes)obtopt}");
            }

            //Store the Record if it's there
            if (!recordPointer.Equals(IntPtr16.Empty) && result == 1)
                Module.Memory.SetArray(recordPointer,
                    currentBtrieveFile.GetRecordByOffset(currentBtrieveFile.Position));

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));

            //If there's a record, always save it to the btrieve file struct
            if (result == 1)
                Module.Memory.SetArray(btvStruct.data,
                    currentBtrieveFile.GetRecordByOffset(currentBtrieveFile.Position));

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
            _logger.Info(
                $"Volatile Memory Size requested of {size} bytes ({VOLATILE_DATA_SIZE} bytes currently allocated per channel)");
#endif
        }

        private ReadOnlySpan<byte> nterms => Module.Memory.GetVariablePointer("NTERMS").ToSpan();

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

            if (!Module.Memory.TryGetVariablePointer($"VDA-{channel}", out var volatileMemoryAddress))
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
                var pointer = Module.Memory.GetVariablePointer("*USER");
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
                if (!Module.Memory.TryGetVariablePointer($"VDAPTR", out var variablePointer))
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
            rstrin();
        }

        /// <summary>
        ///     Number of Words in the users input line
        ///
        ///     Signature: int margc
        /// </summary>
        private ReadOnlySpan<byte> margc => Module.Memory.GetVariablePointer("MARGC").ToSpan();

        /// <summary>
        ///     Returns the pointer to the next parsed input command from the user
        ///     If this is the first time it's called, it returns the first command
        /// </summary>
        private ReadOnlySpan<byte> nxtcmd => Module.Memory.GetVariablePointer("NXTCMD").ToSpan();

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
                _logger.Info(
                    $"Returning False, String 1 Length {string1Buffer.Length} > Stringe 2 Length {string2Buffer.Length}");
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
        ///
        ///     cncchr() is executed after begincnc(), which runs rstrin() replacing the null separtors
        ///     in the string with spaces once again.
        /// </summary>
        private void cncchr()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            //Skip any excessive spacing
            while (Module.Memory.GetByte(nxtcmdPointer) == 0x20 && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            //Verify we're not at the end of the input
            if (remainingCharactersInCommand == 0)
            {
#if DEBUG
                _logger.Info($"End of Input");
#endif

                Registers.AX = 0;
                return;
            }

            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            Registers.AX = char.ToUpper((char)inputString[0]);

#if DEBUG
            _logger.Info($"Returned char: {(char)Registers.AX}");
#endif
            //End of String
            if (Registers.AX == 0)
                return;

            //Modify the Counters
            remainingCharactersInCommand--;
            nxtcmdPointer.Offset++;

            //Advance to the next, non-space character
            while (Module.Memory.GetByte(nxtcmdPointer) == 0x20 && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            Module.Memory.SetPointer("NXTCMD", new IntPtr16(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
        }

        /// <summary>
        ///     Pointer to the current position in prfbuf
        ///
        ///     Signature: char *prfptr;
        /// </summary>
        private ReadOnlySpan<byte> prfptr => Module.Memory.GetVariablePointer("PRFPTR").ToSpan();

        private bool FileAlreadyOpen(string fullPath, out IntPtr16 fileStructPointer)
        {
            // let's see if this file has already been opened, and if so return the current handle
            if (Module.Memory.TryGetVariablePointer($"FILE_{fullPath}", out fileStructPointer))
            {
                var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));
                if (FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                {
                    return true;
                }
            }

            fileStructPointer = null;
            return false;
        }

        /// <summary>
        ///     Opens a new file for reading/writing
        ///
        ///     Signature: file* fopen(const char* filename, USE)
        /// </summary>
        private void f_open()
        {
            var filenameInputValue = GetParameterFilename(0);
            var modePointer = GetParameterPointer(2);

            var fileName = _fileFinder.FindFile(Module.ModulePath, filenameInputValue);
            var fullPath = Path.Combine(Module.ModulePath, fileName);

            if (FileAlreadyOpen(fullPath, out var fileStructPointer))
            {
                _logger.Warn($"Reopened File: {fullPath} - most likely a module bug.");

                Registers.AX = fileStructPointer.Offset;
                Registers.DX = fileStructPointer.Segment;
                return;
            }

#if DEBUG
            _logger.Debug($"Opening File: {fullPath}");
#endif

            var modeInputBuffer = Module.Memory.GetString(modePointer, true);
            var fileAccessMode = FileStruct.CreateFlagsEnum(modeInputBuffer);
            FileStream fileStream = null;

            if (!File.Exists(fullPath))
            {
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Read))
                {
                    _logger.Warn($"Unable to find file {fullPath}");
                    Registers.AX = 0;
                    Registers.DX = 0;
                    return;
                }

                //Create a new file for W or A
                _logger.Info($"Creating new file {fileName}");

                fileStream = File.Create(fullPath);
            }
            else
            {
                //Overwrite existing file for W
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Write))
                {
#if DEBUG
                    _logger.Info($"Overwriting file {fileName}");
#endif
                    fileStream = File.Create(fullPath);
                }
            }

            //Allocate Memory for FILE struct
            fileStructPointer ??= Module.Memory.GetOrAllocateVariablePointer($"FILE_{fullPath}", FileStruct.Size);

            //Write New Blank Pointer
            var fileStruct = new FileStruct();
            Module.Memory.SetArray(fileStructPointer, fileStruct.Data);

            //Setup the File Stream
            fileStream ??= File.Open(fullPath, FileMode.OpenOrCreate);

            if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Append))
                fileStream.Seek(fileStream.Length, SeekOrigin.Begin);

            var fileStreamPointer = FilePointerDictionary.Allocate(fileStream);

            //Set Struct Values
            fileStruct.SetFlags(fileAccessMode);
            fileStruct.curp = new IntPtr16(ushort.MaxValue, (ushort)fileStreamPointer);
            fileStruct.fd = (byte)fileStreamPointer;
            Module.Memory.SetArray(fileStructPointer, fileStruct.Data);

#if DEBUG
            _logger.Info($"{fullPath} FILE struct written to {fileStructPointer}");
#endif
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

            // clear the memory
            Module.Memory.SetArray(filePointer, new FileStruct().Data);

            if (fileStruct.curp.Segment == 0 && fileStruct.curp.Offset == 0)
            {
#if DEBUG
                _logger.Warn(
                    $"Called FCLOSE on null File Stream Pointer (0000:0000), usually means it tried to open a file that doesn't exist");
                Registers.AX = 0;
                return;
#endif
            }

            if (!FilePointerDictionary.ContainsKey(fileStruct.curp.Offset))
            {
                _logger.Warn(
                    $"Attempted to call FCLOSE on pointer not in File Stream Segment {fileStruct.curp} (File Already Closed?)");
                Registers.AX = 0;
                return;
            }

            //Clean Up File Stream Pointer
            var fileStream = FilePointerDictionary[fileStruct.curp.Offset];

#if DEBUG
            _logger.Info($"Closed File {filePointer} {fileStream.Name} (Stream: {fileStruct.curp})");
#endif

            FilePointerDictionary[fileStruct.curp.Offset].Close();
            FilePointerDictionary.Remove(fileStruct.curp.Offset);

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
            _logger.Info($"Added {output.Length} bytes to the buffer: {Encoding.ASCII.GetString(formattedMessage)}");

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
            var fileName = GetParameterString(2, stripNull: true);
            var attrChar = GetParameter(4);

            var components = FileUtility.SplitIntoComponents(fileName);
            var path = "";
            var search = components[^1];
            for (var i = 0; i < components.Length - 1; ++i)
            {
                path = Path.Combine(path, components[i]);
            }

            if (components.Length > 1)
            {
                path = _fileFinder.FindFile(Module.ModulePath, path);
            }

            path = Path.Combine(Module.ModulePath, path);

            try {
                var fileEnumerator = Directory.EnumerateFileSystemEntries(path, search, FileUtility.CASE_INSENSITIVE_ENUMERATION_OPTIONS);
                var guid = Guid.NewGuid();

                _activeSearches.Add(guid, fileEnumerator.GetEnumerator());

                var fndblk = new FndblkStruct() { Guid = guid };
                Module.Memory.SetArray(findBlockPointer,fndblk.Data);
            }
            catch (DirectoryNotFoundException)
            {
                _logger.Warn($"Can't find directory {path}");
                Registers.AX = 0;
                return;
            }

            fndnxt();
        }

        /// <summary>
        ///     Finds the next file from the original fnd1st call, returns 1 if the file is there
        ///
        ///     Signature: int yes=fndnxt(struct fndblk &fb);
        /// </summary>
        private void fndnxt()
        {
            Registers.AX = 0;

            var fndblkPointer = GetParameterPointer(0);
            var fndblk = new FndblkStruct(Module.Memory.GetArray(fndblkPointer, FndblkStruct.StructSize));
            if (!_activeSearches.TryGetValue(fndblk.Guid, out var enumerator))
            {
                _logger.Warn($"Called fndnxt but the GUID wasn't found {fndblk.Guid}");
                return;
            }

            while (true)
            {
                if (!enumerator.MoveNext())
                {
                    _activeSearches.Remove(fndblk.Guid);
                    return;
                }

                var fileInfo = new FileInfo(enumerator.Current);
                fndblk.DateTime = fileInfo.LastWriteTime;
                fndblk.Size = (int) fileInfo.Length;
                fndblk.SetAttributes(fileInfo.Attributes);

                // DOS doesn't support long file names, so filter those out from the result set
                var name = FileUtility.SplitIntoComponents(enumerator.Current)[^1];
                if (name.Length >= FndblkStruct.FilenameSize)
                    continue;

                fndblk.Name = name;
                Module.Memory.SetArray(fndblkPointer,fndblk.Data);
                Registers.AX = 1;
                return;
            }
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
                throw new FileNotFoundException(
                    $"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            if (fileStream.Position >= fileStream.Length)
            {
                _logger.Warn("Attempting to read EOF file, returning null pointer");
                Registers.AX = 0;
                return;
            }

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
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Read {elementsRead} group(s) of {size} bytes from {fileStructPointer} (Stream: {fileStruct.curp}), written to {destinationPointer}");
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
                throw new FileNotFoundException(
                    $"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            ushort elementsWritten = 0;
            for (var i = 0; i < count; i++)
            {
                fileStream.Write(Module.Memory.GetArray(sourcePointer.Segment,
                    (ushort)(sourcePointer.Offset + (i * size)), size));
                elementsWritten++;
            }

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Read {elementsWritten} group(s) of {size} bytes from {sourcePointer}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = elementsWritten;
        }

        /// <summary>
        ///     Deleted the specified file
        /// </summary>
        private void unlink()
        {
            var filename = _fileFinder.FindFile(Module.ModulePath, GetParameterFilename(0));
            var fullPath = Path.Combine(Module.ModulePath, filename);
#if DEBUG
            _logger.Info($"Deleting File: {fullPath}");
#endif

            if (File.Exists(fullPath))
                File.Delete(fullPath);

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

            var stringValue = Module.Memory.GetString(stringPointer, stripNull: true);

#if DEBUG
            _logger.Info($"Evaluated string length of {stringValue.Length} for string at {stringPointer}: {Encoding.ASCII.GetString(stringValue)}");
#endif

            Registers.AX = (ushort)stringValue.Length;
        }

        /// <summary>
        ///     User Input
        ///
        ///     Signature: char input[]
        /// </summary>
        private ReadOnlySpan<byte> input => Module.Memory.GetVariablePointer("INPUT").ToSpan();

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
            if (character >= 65 && character <= 90)
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
            var oldFilenameInputValue = Encoding.ASCII.GetString(oldFilenameInputBuffer).ToUpper();

            var newFilenameInputBuffer = Module.Memory.GetString(newFilenamePointer, true);
            var newFilenameInputValue = Encoding.ASCII.GetString(newFilenameInputBuffer).ToUpper();

            oldFilenameInputValue = _fileFinder.FindFile(Module.ModulePath, oldFilenameInputValue);
            newFilenameInputValue = _fileFinder.FindFile(Module.ModulePath, newFilenameInputValue);

            if (!File.Exists(Path.Combine(Module.ModulePath, oldFilenameInputValue)))
                throw new FileNotFoundException(
                    $"Attempted to rename file that doesn't exist: {oldFilenameInputValue}");

            File.Move(Path.Combine(Module.ModulePath, oldFilenameInputValue), Path.Combine(Module.ModulePath, newFilenameInputValue),
                true);

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
        private ReadOnlySpan<byte> inplen => Module.Memory.GetVariablePointer("INPLEN").ToSpan();

        private ReadOnlySpan<byte> margv => Module.Memory.GetVariablePointer("MARGV").ToSpan();

        private ReadOnlySpan<byte> margn => Module.Memory.GetVariablePointer("MARGN").ToSpan();

        /// <summary>
        ///     Restore parsed input line (undoes effects of parsin())
        ///
        ///     Signature: void rstrin()
        /// </summary>
        private void rstrin()
        {
            var inputLength = Module.Memory.GetWord("INPLEN");
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");

            if (inputLength == 0)
                return;

            for (var i = inputPointer.Offset; i < inputPointer.Offset + (inputLength - 1); i++)
            {
                if (Module.Memory.GetByte(inputPointer.Segment, i) == 0)
                    Module.Memory.SetByte(inputPointer.Segment, i, (byte)' ');
            }
        }

        private ReadOnlySpan<byte> _exitbuf => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitfopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> extptr => Module.Memory.GetVariablePointer("EXTPTR").ToSpan();
        private ReadOnlySpan<byte> usaptr => Module.Memory.GetVariablePointer("USAPTR").ToSpan();

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

            var destinationString = Module.Memory.GetString(destinationPointer, stripNull: true);
            var sourceString = Module.Memory.GetString(sourcePointer, stripNull: true);

            bytesToCopy = Math.Min(bytesToCopy, (ushort) sourceString.Length);

            Module.Memory.SetArray(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length),
                sourceString.Slice(0, bytesToCopy));
            // null terminate always
            Module.Memory.SetByte(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length + bytesToCopy), 0x0);

            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
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
                Module.Memory.SetByte(destinationPointer.Segment, (ushort)(destinationPointer.Offset + i),
                    (byte)valueToFill);
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

            if (!Module.Memory.TryGetVariablePointer($"GETMSG", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable($"GETMSG", 0x1000);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}), saved {3} bytes to {4}", msgnum,
                McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue.Length,
                variablePointer);
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

            var inputString = Encoding.ASCII.GetString(Module.Memory.GetString(inputPointer));
            var inputStringElements = inputString.Split(SSCANF_SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
            var formatString = Encoding.ASCII.GetString(Module.Memory.GetString(formatPointer));
            var formatStringElements = formatString.Split(SSCANF_SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
            var startingParameterOrdinal = 4;
            ushort matches = 0;

            for (var index = 0; index < formatStringElements.Length; index++)
            {
                var s = formatStringElements[index];

                if (s[0] == '%' && s[1] != '*')
                {
                    switch (s[1])
                    {
                        case 'd':
                            Module.Memory.SetWord(GetParameterPointer(startingParameterOrdinal), (ushort)GetLeadingNumberFromString(inputStringElements[index], out _));
#if DEBUG
                            //_logger.Info($"Saved {GetLeadingNumberFromString(inputStringElements[index], out _)} to {startingParameterOrdinal}");
#endif
                            break;

                        case 's':
                            var stringValue = $"{inputString[index]}\0";
                            Module.Memory.SetArray(GetParameterPointer(startingParameterOrdinal), Encoding.ASCII.GetBytes(stringValue));
#if DEBUG
                            //_logger.Info($"Saved {Encoding.ASCII.GetBytes(stringValue)} to {startingParameterOrdinal}");
#endif
                            break;
                        default:
                            throw new Exception($"Unsupported sscanf specifier: {s[1]}");
                    }

                    //Increment the pointer for the next destination parameter
                    startingParameterOrdinal += 2;
                    matches++;
                }
            }

            Registers.AX = matches;
#if DEBUG
            //_logger.Info($"Processed sscanf on {inputString}-> {formatString}");
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

            var currentPrfPositionPointer = Module.Memory.GetPointer(Module.Memory.GetVariablePointer("PRFPTR"));
            Module.Memory.SetArray(currentPrfPositionPointer, formattedMessage);
            currentPrfPositionPointer.Offset += (ushort)(formattedMessage.Length - 1); //dont count the null terminator

#if DEBUG
            _logger.Info($"Added {output.Length} bytes to the buffer (Message #: {messageNumber})");
#endif
            //Update Pointer
            Module.Memory.SetPointer("PRFPTR", currentPrfPositionPointer);
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

            var formattedMessage = FormatPrintf(message, 2);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine($"{Encoding.ASCII.GetString(formattedMessage)}");
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

            if (fileStream.Position == fileStream.Length)
            {
                _logger.Warn("Attempting to read EOF file, returning null pointer");
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            using var valueFromFile = new MemoryStream();
            for (var i = 0; i < (maxCharactersToRead - 1); i++)
            {
                var inputValue = (byte)fileStream.ReadByte();

                valueFromFile.WriteByte(inputValue);

                if (inputValue == '\n' || fileStream.Position == fileStream.Length)
                    break;
            }

            valueFromFile.WriteByte(0);

            Module.Memory.SetArray(destinationPointer, valueFromFile.ToArray());

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Read string from {fileStructPointer}, {valueFromFile.Length} bytes (Stream: {fileStruct.curp}), saved at {destinationPointer} (EOF: {fileStream.Position == fileStream.Length})");
#endif
            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
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

            var destinationString = Module.Memory.GetString(destinationPointer, stripNull: true);
            var sourceString = Module.Memory.GetString(sourcePointer);

            Module.Memory.SetArray(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length),
                sourceString);

            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
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
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
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
            var offset = GetParameterULong(2);
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
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info($"Seek to {fileStream.Position} in {fileStructPointer} (Stream: {fileStruct.curp})");
#endif

            Registers.AX = 0;
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

            var unpackedTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, unpackedHour,
                unpackedMinutes, unpackedSeconds);

            var timeString = unpackedTime.ToString("HH:mm:ss");

            if (!Module.Memory.TryGetVariablePointer("NCTIME", out var variablePointer))
            {
                variablePointer = Module.Memory.AllocateVariable("NCTIME", (ushort)timeString.Length);
            }

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(timeString));

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

            var stringToSearch = Module.Memory.GetString(stringPointer, stripNull: true);

            for (var i = 0; i < stringToSearch.Length; i++)
            {
                if (stringToSearch[i] != (byte)characterToFind) continue;

                Registers.AX = (ushort)(stringPointer.Offset + i);
                Registers.DX = stringPointer.Segment;
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
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var inputLength = Module.Memory.GetWord("INPLEN");

            //If Length is 0, there's nothing to process
            if (Module.Memory.GetByte(inputPointer) == 0)
            {
                Module.Memory.SetWord("INPLEN", 0);
                Module.Memory.SetWord("MARGC", 0);
                return;
            }

            //Parse out the Input, eliminating excess spaces and separating words by null
            var inputComponents = Encoding.ASCII.GetString(Module.Memory.GetString("INPUT"))
                .Split(' ');
            var parsedInput = string.Join('\0', inputComponents);

            //Setup MARGV & MARGN
            var margvPointer = new IntPtr16(Module.Memory.GetVariablePointer("MARGV"));
            var margnPointer = new IntPtr16(Module.Memory.GetVariablePointer("MARGN"));

            var margCount = (ushort)inputComponents.Count(x => !string.IsNullOrEmpty(x));

            Module.Memory.SetPointer(margvPointer, inputPointer); //Set 1st command to start at start of input
            margvPointer.Offset += IntPtr16.Size;

            for (var i = 0; i < parsedInput.Length; i++)
            {
                if (parsedInput[i] != 0)
                    continue;

                //Set Command End
                var commandEndPointer = new IntPtr16(inputPointer.Segment, (ushort)(inputPointer.Offset + i));
                Module.Memory.SetPointer(margnPointer, commandEndPointer);
                margnPointer.Offset += IntPtr16.Size;

                i++;

                //Skip any white spaces after the current command
                while (i < parsedInput.Length && parsedInput[i] == 0)
                    i++;

                //Are we at the end of the INPUT? If so, we're done here.
                if (i == parsedInput.Length)
                    break;

                //If not, set the next command start
                var commandStartPointer = new IntPtr16(inputPointer.Segment, (ushort)(inputPointer.Offset + i));
                Module.Memory.SetPointer(margvPointer, commandStartPointer);
                margvPointer.Offset += IntPtr16.Size;

            }
            Module.Memory.SetWord("INPLEN", (ushort)parsedInput.Length);
            Module.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(parsedInput));
            Module.Memory.SetWord("MARGC", margCount);
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
            _logger.Info(
                $"Returning Current Position of {currentPosition} for {fileStructPointer} (Stream: {fileStruct.curp})");
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

            var userSession = ChannelDictionary.Values.FirstOrDefault(x =>
                userId.SequenceEqual(StringFromArray(x.UsrAcc.userid).ToArray()));

            //User not found?
            if (userSession == null || ChannelDictionary[userSession.Channel].UsrPtr.State != moduleId)
            {
                Registers.AX = 0;
                return;
            }

            var othusnPointer = Module.Memory.GetVariablePointer("OTHUSN");
            Module.Memory.SetWord(othusnPointer, ChannelDictionary[userSession.Channel].Channel);

            var userBase = new IntPtr16(Module.Memory.GetVariablePointer("USER").ToSpan());
            userBase.Offset += (ushort)(User.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHUSP"), userBase.ToSpan());

            var userAccBase = new IntPtr16(Module.Memory.GetVariablePointer("USRACC").ToSpan());
            userAccBase.Offset += (ushort)(UserAccount.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHUAP"), userAccBase.ToSpan());

            var userExtAcc = new IntPtr16(Module.Memory.GetVariablePointer("EXTUSR").ToSpan());
            userExtAcc.Offset += (ushort)(ExtUser.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHEXP"), userExtAcc.ToSpan());

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
            var stringToSearch = Encoding.ASCII.GetString(Module.Memory.GetString(stringToSearchPointer, true))
                .ToUpper();

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
            var maxCharactersToRead = GetParameter(2);
            var fileStructPointer = GetParameterPointer(3);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new Exception($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            if (fileStream.Position == fileStream.Length)
            {
                _logger.Warn("Attempting to read EOF file, returning null pointer");
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            using var valueFromFile = new MemoryStream();
            for (var i = 0; i < (maxCharactersToRead - 1); i++)
            {
                var inputValue = (byte)fileStream.ReadByte();

                if (inputValue == '\r' || fileStream.Position == fileStream.Length)
                    break;

                valueFromFile.WriteByte(inputValue);
            }

            valueFromFile.WriteByte(0);

            Module.Memory.SetArray(destinationPointer, valueFromFile.ToArray());

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Read string from {fileStructPointer}, {valueFromFile.Length} bytes (Stream: {fileStruct.curp}), saved at {destinationPointer} (EOF: {fileStream.Position == fileStream.Length})");
#endif
            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
        }

        private ReadOnlySpan<byte> genbb => Module.Memory.GetVariablePointer("GENBB").ToSpan();

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
        ///     Signature: char *upper=strupr(char *string)
        /// </summary>
        private void strupr()
        {
            strmod(Char.ToUpper);
        }

        private void strmod(Func<char, char> modifier)
        {
            var stringToConvertPointer = GetParameterPointer(0);

            var stringData = Module.Memory.GetString(stringToConvertPointer, stripNull: true).ToArray();

            for (var i = 0; i < stringData.Length; i++)
            {
                stringData[i] = (byte) modifier.Invoke((char) stringData[i]);
            }

            Module.Memory.SetArray(stringToConvertPointer, stringData);

            Registers.AX = stringToConvertPointer.Offset;
            Registers.DX = stringToConvertPointer.Segment;
        }

        /// <summary>
        ///     Returns a pointer to the first occurrence of str2 in str1, or a null pointer if str2 is not part of str1.
        /// </summary>
        private void strstr()
        {
            var stringToSearchPointer = GetParameterPointer(0);
            var stringToFindPointer = GetParameterPointer(2);

            var stringToSearch = Encoding.ASCII.GetString(Module.Memory.GetString(stringToSearchPointer, stripNull: true));
            var stringToFind = Encoding.ASCII.GetString(Module.Memory.GetString(stringToFindPointer, stripNull: true));

            var offset = stringToSearch.IndexOf(stringToFind);
            if (offset >= 0)
            {
                Registers.AX = (ushort)(stringToSearchPointer.Offset + offset);
                Registers.DX = stringToSearchPointer.Segment;
            }
            else
            {
                // not found, return NULL
                Registers.AX = 0;
                Registers.DX = 0;
            }
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

        private ReadOnlySpan<byte> othusn => Module.Memory.GetVariablePointer("OTHUSN").ToSpan();

        /// <summary>
        ///     Returns number of bytes session will need, or -1=error in data
        ///
        ///     Signature: int fsdroom(int tmpmsg, char *fldspc, int amode)
        /// </summary>
        private void fsdroom()
        {
            var tmpmsg = GetParameter(0);
            var fldspc = GetParameterPointer(1);
            var amode = GetParameter(3);

            Registers.AX = 0x2000;

            //amode == 0 is just to calculate/report back the buffer space available
            if (amode == 0)
                return;

            if (!Module.Memory.TryGetVariablePointer($"FSD-TemplateBuffer-{ChannelNumber}", out var fsdBufferPointer))
                fsdBufferPointer = Module.Memory.AllocateVariable($"FSD-TemplateBuffer-{ChannelNumber}", 0x2000);

            if (!Module.Memory.TryGetVariablePointer($"FSD-FieldSpec-{ChannelNumber}", out var fsdFieldSpecPointer))
                fsdFieldSpecPointer = Module.Memory.AllocateVariable($"FSD-FieldSpec-{ChannelNumber}", 0x2000);

            //Zero out FSD Memory Areas
            Module.Memory.SetZero(fsdBufferPointer, 0x2000);
            Module.Memory.SetZero(fsdFieldSpecPointer, 0x2000);

            //Hydrate FSD Memory Areas with Values
            var template = McvPointerDictionary[_currentMcvFile.Offset].GetString(tmpmsg);
            Module.Memory.SetArray(fsdBufferPointer, template);
            var fieldSpec = Module.Memory.GetString(fldspc);
            Module.Memory.SetArray(fsdFieldSpecPointer, fieldSpec);

            //Establish a new FSD Status Struct for this Channel
            if (!Module.Memory.TryGetVariablePointer($"FSD-Fsdscb-{ChannelNumber}", out var channelFsdscb))
                channelFsdscb = Module.Memory.AllocateVariable($"FSD-Fsdscb-{ChannelNumber}", FsdscbStruct.Size);

            if (!Module.Memory.TryGetVariablePointer($"FSD-Fsdscb-{ChannelNumber}-newans", out var newansPointer))
                newansPointer = Module.Memory.AllocateVariable($"FSD-Fsdscb-{ChannelNumber}-newans", 0x400);

            //Declare flddat -- allocating enough room for up to 100 fields
            if (!Module.Memory.TryGetVariablePointer($"FSD-Fsdscb-{ChannelNumber}-flddat", out var fsdfldPointer))
                fsdfldPointer = Module.Memory.AllocateVariable($"FSD-Fsdscb-{ChannelNumber}-flddat", FsdfldStruct.Size * 100);

            var fsdStatus = new FsdscbStruct(Module.Memory.GetArray(channelFsdscb, FsdscbStruct.Size))
            {
                fldspc = fsdFieldSpecPointer,
                flddat = fsdfldPointer,
                newans = newansPointer
            };

            Module.Memory.SetArray($"FSD-Fsdscb-{ChannelNumber}", fsdStatus.Data);
        }

        /// <summary>
        ///     Btrieve Step Operation Supporting Locks
        ///
        ///     Signature: int stpbtvl (void *recptr, int stpopt, int loktyp)
        /// </summary>
        private void stpbtvl()
        {

            var btrieveRecordPointer = GetParameterPointer(0);
            var stpopt = GetParameter(2);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

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
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));

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
            var string1 = GetParameterString(0, stripNull: true);
            var string2 = GetParameterString(2, stripNull: true);

            Registers.AX = (ushort)string.Compare(string1, string2);
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
        private ReadOnlySpan<byte> nmods => Module.Memory.GetVariablePointer("NMODS").ToSpan();

        /// <summary>
        ///     This is the pointer, to the pointer, for the MODULE struct because module is declared
        ///     **module in MAJORBBS.H
        ///
        ///     Pointer -> Pointer -> Struct
        /// </summary>
        private ReadOnlySpan<byte> module => Module.Memory.GetVariablePointer("MODULE-POINTER").ToSpan();

        /// <summary>
        ///     Long Arithmatic Shift Left (Borland C++ Implicit Function)
        ///
        ///     DX:AX == Long Value
        ///     CL == How many to move
        /// </summary>
        private void f_lxlsh()
        {
            var inputValue = (int)((Registers.DX << 16) | Registers.AX);

            var result = inputValue << Registers.CL;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Long Logical Shift Right (Borland C++ Implicit Function)
        ///
        ///     DX:AX == Unsigned Long Value
        ///     CL == How many to move
        /// </summary>
        private void f_lxursh()
        {
            var inputValue = (uint)((Registers.DX << 16) | Registers.AX);

            var result = inputValue >> Registers.CL;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Long Arithmatic Shift Right (Borland C++ Implicit Function)
        ///
        ///     DX:AX == Long Value
        ///     CL == How many to move
        /// </summary>
        private void f_lxrsh()
        {
            var inputValue = (int)((Registers.DX << 16) | Registers.AX);

            var result = inputValue >> Registers.CL;

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
        private ReadOnlySpan<byte> _ctype => Module.Memory.GetVariablePointer("CTYPE").ToSpan();

        /// <summary>
        ///     Points to the ad-hoc Volatile Data Area
        ///
        ///     Signature: char *vdatmp
        /// </summary>
        private ReadOnlySpan<byte> vdatmp => Module.Memory.GetVariablePointer("*VDATMP").ToSpan();

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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, currentBtrieveFile.LoadedFile.RecordLength);

            currentBtrieveFile.Update(dataToWrite.ToArray());

#if DEBUG
            _logger.Info(
                $"Updated current Btrieve record ({currentBtrieveFile.Position}) with {dataToWrite.Length} bytes");
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
            _logger.Info(
                $"Copied \"{Encoding.ASCII.GetString(inputBuffer.ToArray())}\" ({inputBuffer.Length} bytes) from {sourcePointer} to {destinationPointer}");
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
            if (routinePointer == IntPtr16.Empty)
            {

                ChannelDictionary[channelNumber].PollingRoutine = null;
                Registers.AX = 0;

#if DEBUG
                _logger.Info($"Unassigned Polling Routine on Channel {channelNumber}");
#endif
                return;
            }

            ChannelDictionary[channelNumber].PollingRoutine = routinePointer;
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), 192);
            ChannelDictionary[channelNumber].StatusChange = true;

#if DEBUG
            _logger.Info(
                $"Assigned Polling Routine {ChannelDictionary[channelNumber].PollingRoutine} to Channel {channelNumber}");
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

                var titlePointer = Module.Memory.GetVariablePointer("BBSTTL");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*BBSTTL").ToSpan();
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

                var titlePointer = Module.Memory.GetVariablePointer("COMPANY");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*COMPANY").ToSpan();
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

                var titlePointer = Module.Memory.GetVariablePointer("ADDRES1");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*ADDRES1").ToSpan();
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

                var titlePointer = Module.Memory.GetVariablePointer("ADDRES2");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*ADDRES2").ToSpan();
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

                var titlePointer = Module.Memory.GetVariablePointer("DATAPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*DATAPH").ToSpan();
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

                var titlePointer = Module.Memory.GetVariablePointer("LIVEPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(title));
                return Module.Memory.GetVariablePointer("*LIVEPH").ToSpan();
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

            if (!Module.Memory.TryGetVariablePointer("XLTTXV", out var resultPointer))
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
        private ReadOnlySpan<byte> vdasiz => Module.Memory.GetVariablePointer("VDASIZ").ToSpan();

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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var key = Module.Memory.GetArray(keyPointer,
                currentBtrieveFile.GetKeyLength(keyNumber));


            if (queryOption <= 50)
                throw new Exception($"Invalid Query Option: {queryOption}");


            var result = 0;
            switch ((EnumBtrieveOperationCodes)queryOption)
            {
                case EnumBtrieveOperationCodes.GetKeyGreater:
                case EnumBtrieveOperationCodes.GetKeyLast:
                case EnumBtrieveOperationCodes.GetKeyEqual:
                case EnumBtrieveOperationCodes.GetKeyLess:
                    result = currentBtrieveFile.SeekByKey(keyNumber, key, (EnumBtrieveOperationCodes)queryOption);
                    break;
                case EnumBtrieveOperationCodes.GetKeyFirst:
                    result = currentBtrieveFile
                        .SeekByKey(keyNumber, keyPointer == IntPtr16.Empty ? null : key, EnumBtrieveOperationCodes.GetKeyFirst);
                    break;
                default:
                    throw new Exception($"Unsupported Btrieve Query Option: {(EnumBtrieveOperationCodes)queryOption}");
            }

            //Set Record Pointer to Result
            if (result == 1)
            {
                var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));
                Module.Memory.SetArray(btvStruct.data,
                    currentBtrieveFile.GetRecordByOffset(currentBtrieveFile.Position));
            }

#if DEBUG
            _logger.Info(
                $"Performed Query {queryOption} on {currentBtrieveFile.LoadedFileName} ({Module.Memory.GetPointer("BB")}) with result {result}");
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
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
            var offset = currentBtrieveFile.Position;

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

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var record = currentBtrieveFile.GetRecordByOffset((uint)absolutePosition);


            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));
            var btvDataPointer = btvStruct.data;

            if (record != null)
            {
                //NULL record pointer saves it to the btvStruct data pointer
                if (recordPointer == IntPtr16.Empty)
                {
                    Module.Memory.SetArray(btvStruct.data, record);
                }
                else
                {
#if DEBUG
                    _logger.Info($"Performed Btrieve Step - Btrieve Record Updated {btvDataPointer}, {record.Length} bytes written to {recordPointer}");
#endif
                    Module.Memory.SetArray(recordPointer, record);
                }
            }
        }

        /// <summary>
        ///     Pointer to structure for that user in the user[] array (set by onsys() or instat())
        ///
        ///     Signature: struct user *othusp;
        /// </summary>
        private ReadOnlySpan<byte> othusp => Module.Memory.GetVariablePointer("*OTHUSP").ToSpan();

        /// <summary>
        ///     Pointer to structure for that user in the extusr structure (set by onsys() or instat())
        ///
        ///     Signature: struct extusr *othexp;
        /// </summary>
        private ReadOnlySpan<byte> othexp => Module.Memory.GetVariablePointer("*OTHEXP").ToSpan();

        /// <summary>
        ///     Pointer to structure for that user in the 'usracc' structure (set by onsys() or instat())
        ///
        ///     Signature: struct usracc *othuap;
        /// </summary>
        private ReadOnlySpan<byte> othuap => Module.Memory.GetVariablePointer("*OTHUAP").ToSpan();

        /// <summary>
        ///     Splits the specified string into tokens based on the delimiters
        /// </summary>
        private void strtok()
        {
            var stringToSplitPointer = GetParameterPointer(0);
            var stringDelimitersPointer = GetParameterPointer(2);

            var workPointerPointer = Module.Memory.GetOrAllocateVariablePointer("STRTOK-WRK", IntPtr16.Size);
            var lengthPointer = Module.Memory.GetOrAllocateVariablePointer("STRTOK-END", 2);

            //If it's the first call, reset the values in memory
            if (!stringToSplitPointer.Equals(IntPtr16.Empty))
            {
                Module.Memory.SetPointer(workPointerPointer, stringToSplitPointer);
                Module.Memory.SetWord(lengthPointer, (ushort)(stringToSplitPointer.Offset + Module.Memory.GetString(stringToSplitPointer, stripNull: true).Length));
            }

            var workPointer = Module.Memory.GetPointer(workPointerPointer);
            var endOffset = Module.Memory.GetWord(lengthPointer);

            var stringDelimiter = Encoding.ASCII.GetString(Module.Memory.GetString(stringDelimitersPointer, stripNull: true));

            // skip starting delims
            while (workPointer.Offset < endOffset && stringDelimiter.Contains((char) Module.Memory.GetByte(workPointer)))
            {
                Module.Memory.SetByte(workPointer++, 0x0);
            }

            // are we at the end of the string with no more to tokenize?
            if (workPointer.Offset >= endOffset)
            {
                Module.Memory.SetPointer(workPointerPointer, workPointer);
                Registers.DX = 0;
                Registers.AX = 0;
                return;
            }

            Registers.DX = workPointer.Segment;
            Registers.AX = workPointer.Offset;

            // scan until we find the next deliminater and then null it out for the return
            while (workPointer.Offset < endOffset && !stringDelimiter.Contains((char) Module.Memory.GetByte(workPointer)))
            {
                workPointer++;
            }

            Module.Memory.SetByte(workPointer++, 0x0);
            Module.Memory.SetPointer(workPointerPointer, workPointer);
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
                throw new FileNotFoundException(
                    $"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.Write(stringToWrite);
            fileStream.Flush();
            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Wrote {stringToWrite.Length} bytes from {stringPointer}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
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

            if (!Module.Memory.TryGetVariablePointer($"RAWMSG", out var variablePointer))
                variablePointer = base.Module.Memory.AllocateVariable($"RAWMSG", 0x1000);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}), saved {3} bytes to {4}", msgnum,
                McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue.Length,
                variablePointer);
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
            _logger.Info("Retrieved option {0} from {1} (MCV Pointer: {2}): {3}", msgnum,
                McvPointerDictionary[_currentMcvFile.Offset].FileName, _currentMcvFile, outputValue);
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
                throw new FileNotFoundException(
                    $"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            var characterRead = fileStream.ReadByte();

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
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
            var string1 = GetParameterString(0, stripNull: true);
            var string2 = GetParameterString(2, stripNull: true);

            Registers.AX = (ushort)string.Compare(string1, string2, ignoreCase: true);
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
                throw new FileNotFoundException(
                    $"File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.Position -= 1;
            fileStream.WriteByte((byte)character);
            fileStream.Position -= 1;

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Info(
                $"Unget 1 byte from {fileStructPointer} (Stream: {fileStruct.curp}). New Position: {fileStream.Position}, Character: {(char)character}");
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
                throw new FileNotFoundException(
                    $"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            fileStream.WriteByte((byte)character);
            fileStream.Flush();
            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
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
            _logger.Info(
                $"Retrieved Big Memory block {bigRegionPointer}, returned pointer to index {index}: {indexPointer}");
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

            if (!Module.Memory.TryGetVariablePointer($"EXTUSR-{userNumber}", out var variablePointer))
                variablePointer = Module.Memory.AllocateVariable($"EXTUSR-{userNumber}", ExtUser.Size);

            //If user isnt online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var extoffPointer = Module.Memory.GetVariablePointer("EXTOFF");
            Module.Memory.SetArray(extoffPointer, variablePointer.ToSpan());

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.ExtUsrAcc.Data);


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
            if (!Module.Memory.HasSegment(0xF000))
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

            var jmpBuf = new JmpBufStruct(Module.Memory.GetArray(jmpBufPointer, JmpBufStruct.Size))
            {
                //Base Pointer is pushed and set on the simulated ENTER
                //remove the parameter to set stack prior to the call
                bp = Module.Memory.GetWord(Registers.SS,
                    (ushort)(Registers.BP + 1)),
                cs = Registers.CS,
                di = Registers.DI,
                ds = Registers.DS,
                es = Registers.ES,
                ip = Registers.IP,
                si = Registers.SI,
                sp = (ushort)(Registers.SP + 6),
                ss = Registers.SS
            };
            Module.Memory.SetArray(jmpBufPointer, jmpBuf.Data);
            Registers.AX = 0;

#if DEBUG
            _logger.Debug($"Jump Set -- {jmpBuf.cs:X4}:{jmpBuf.ip:X4} AX:{Registers.AX}");
#endif
        }


        /// <summary>
        ///     Returns if we're at the end, or "done", with the current command
        ///
        ///     Signature: int done=endcnc()
        /// </summary>
        private void endcnc()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            //Check to see if nxtcmd is on a space, if so, increment it by 1
            if (Module.Memory.GetByte(nxtcmdPointer) == 0x20)
            {
                remainingCharactersInCommand--;
                nxtcmdPointer.Offset++;
            }

            //End of String
            if (Module.Memory.GetByte(nxtcmdPointer) == 0)
            {
                remainingCharactersInCommand = 0;
            }

            //Get Remaining Characters & Save to Input
            var remainingInput = Module.Memory.GetArray(nxtcmdPointer, (ushort)(remainingCharactersInCommand));
            Module.Memory.SetArray(inputPointer, remainingInput);
            Module.Memory.SetWord("INPLEN", (ushort)remainingCharactersInCommand);
            Module.Memory.SetPointer("NXTCMD", inputPointer);
            parsin();

            Registers.AX = remainingCharactersInCommand == 0 ? (ushort)1 : (ushort)0;
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
            Registers.IP = (ushort)(jmpBuf.ip + 5);
            Registers.SI = jmpBuf.si;
            Registers.SP = jmpBuf.sp;
            Registers.SS = jmpBuf.ss;
            Registers.AX = value;

#if DEBUG
            _logger.Debug($"{jmpBuf.cs:X4}:{jmpBuf.ip:X4} -- {Registers.AX}");
#endif
        }

        /// <summary>
        ///     Expect a variable-length word sequence (consume all remaining input)
        ///
        ///     Signature: char *cncall(void)
        /// </summary>
        private void cncall()
        {
            rstrin();

            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var newNxtcmd = new IntPtr16(inputPointer.Segment, (ushort)(inputPointer.Offset + inputLength));

            Module.Memory.SetPointer("NXTCMD", newNxtcmd);

            Registers.AX = newNxtcmd.Offset;
            Registers.DX = newNxtcmd.Segment;
        }

        /// <summary>
        ///     Checks to see if there's any more command to parse
        ///     Similar to PEEK
        ///
        ///     Signature: char morcnc(void)
        /// </summary>
        private void morcnc()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            if (remainingCharactersInCommand == 0)
            {
#if DEBUG
                _logger.Info($"End of Command");
#endif
                Registers.AX = 0;
                return;
            }

            var inputCommand = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            for (var i = 0; i < remainingCharactersInCommand; i++)
            {
                if (inputCommand[i] == 0)
                    continue;

#if DEBUG
                _logger.Info($"Returning char: {(char)inputCommand[i]}");
#endif

                Registers.AX = inputCommand[i];
                Module.Memory.SetPointer("NXTCMD", new IntPtr16(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset + i)));
                break;
            }
        }

        /// <summary>
        ///     Expect an integer from the user
        ///
        ///     Signature: int n=cncint()
        /// </summary>
        private void cncint()
        {
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var inputLengthPointer = Module.Memory.GetVariablePointer("INPLEN");

            var inputLength = Module.Memory.GetWord(inputLengthPointer);

            if (inputLength == 0)
            {
                Registers.AX = 0;
                return;
            }

            var inputCommand = Module.Memory.GetArray(inputPointer, inputLength);

            var msResult = new MemoryStream();
            while (_inputCurrentPosition < inputLength && inputCommand[_inputCurrentPosition] != 0x0 &&
                   inputCommand[_inputCurrentPosition] != 0x20)
            {
                msResult.WriteByte(inputCommand[_inputCurrentPosition]);
                _inputCurrentPosition++;
            }

            ushort.TryParse(Encoding.ASCII.GetString(msResult.ToArray()), out var result);

#if DEBUG
            _logger.Info($"Returned Int: {result}");
#endif

            Registers.AX = result;
        }

        /// <summary>
        ///     Main menu credit consumption rate per minute
        ///
        ///     Signature: int mmucrr
        /// </summary>
        private ReadOnlySpan<byte> mmuccr => Module.Memory.GetVariablePointer("MMUCCR").ToSpan();

        /// <summary>
        ///     'Profanity Level' of the Input (from configuration)
        ///
        ///     This is hard coded to 0, which means no profanity detected
        /// </summary>
        private ReadOnlySpan<byte> pfnlvl => Module.Memory.GetVariablePointer("PFNLVL").ToSpan();

        /// <summary>
        ///     Long Unsigned Division (Borland C++ Implicit Function)
        ///
        ///     Input: Two long values on stack (arg1/arg2)
        ///     Output: DX:AX = quotient
        ///             DI:SI = remainder
        /// </summary>
        /// <returns></returns>
        private void f_ludiv()
        {
            uint arg1 = (uint)(GetParameter(1) << 16) | GetParameter(0);
            uint arg2 = (uint)(GetParameter(3) << 16) | GetParameter(2);

            var quotient = arg1 / arg2;

            Registers.DX = (ushort)(quotient >> 16);
            Registers.AX = (ushort)(quotient & 0xFFFF);

            RealignStack(8);
        }


        /// <summary>
        ///     Get a Btrieve record (bomb if not there)
        ///
        ///     Signature: void getbtv(char *recptr, char *key, int keynum, int getopt)
        /// </summary>
        /// <returns></returns>
        private void getbtv()
        {
            var btrieveRecordPointer = GetParameterPointer(0);
            var keyPointer = GetParameterPointer(2);
            var keyNumber = GetParameter(4);
            var queryOption = GetParameter(5);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            if ((short)keyNumber < 0 && keyPointer.Equals(IntPtr16.Empty))
            {
                currentBtrieveFile.Seek((EnumBtrieveOperationCodes)queryOption);
            }
            else
            {
                var key = Module.Memory.GetArray(keyPointer, currentBtrieveFile.GetKeyLength(keyNumber));

                currentBtrieveFile.SeekByKey(keyNumber, key, (EnumBtrieveOperationCodes)queryOption);
            }

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));


            Module.Memory.SetArray(btvStruct.data,
                currentBtrieveFile.GetRecordByOffset(currentBtrieveFile.Position));

#if DEBUG
            _logger.Info($"Performed Btrieve Get - Record written to {btvStruct.data}");
#endif

            Module.Memory.SetArray(btrieveRecordPointer, currentBtrieveFile.GetRecord());

#if DEBUG
            _logger.Info(
                $"Performed Btrieve Get {(EnumBtrieveOperationCodes)queryOption}, Position: {currentBtrieveFile.Position}");
#endif
        }

        /// <summary>
        ///     Query Next/Previous Btrieve utility
        ///
        ///     Signature: int qnpbtv (int getopt)
        /// </summary>
        private void qnpbtv()
        {
            var queryOption = GetParameter(0);

            if (queryOption <= 50)
                throw new Exception($"Invalid Query Option: {queryOption}");

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var result = 0;
            switch ((EnumBtrieveOperationCodes)queryOption)
            {
                //Get Next -- repeating the same previous query
                case EnumBtrieveOperationCodes.GetKeyNext:
                    result = currentBtrieveFile
                        .SeekByKey(0, null, EnumBtrieveOperationCodes.GetKeyNext, false);
                    break;
                default:
                    throw new Exception($"Unsupported Btrieve Query Option: {(EnumBtrieveOperationCodes)queryOption}");
            }

            //Set Record Pointer to Result
            if (result == 1)
            {
                var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));
                Module.Memory.SetArray(btvStruct.data,
                    currentBtrieveFile.GetRecordByOffset(currentBtrieveFile.Position));
            }

#if DEBUG
            _logger.Info(
                $"Performed Query {(EnumBtrieveOperationCodes)queryOption} on {currentBtrieveFile.LoadedFileName} ({Module.Memory.GetPointer("BB")}) with result {result}");
#endif

            Registers.AX = (ushort)result;
        }

        /// <summary>
        ///     Counts the number of days since 1/1/80
        ///
        ///     Signature: int count=cofdat(int date)
        /// </summary>
        private void cofdat()
        {
            var packedDate = GetParameter(0);

            if (packedDate == 0)
            {
                Registers.AX = 0;
                return;
            }

            //Unpack the Date
            var year = ((packedDate >> 9) & 0x007F) + 1980;
            var month = (packedDate >> 5) & 0x000F;
            var day = packedDate & 0x001F;

            var originDate = new DateTime(1980, 1, 1);
            var specifiedDate = new DateTime(year, month, day);

            Registers.AX = (ushort)(specifiedDate - originDate).TotalDays;
        }

        /// <summary>
        ///     Read the free-running 65khz timer (not a typo)
        ///
        ///     Signature: unsigned long tix64k=hrtval()
        /// </summary>
        private void htrval()
        {

            var outputSeconds = (ushort)(_highResolutionTimer.Elapsed.TotalSeconds % ushort.MaxValue);
            var outputMicroseconds = (ushort)_highResolutionTimer.Elapsed.Milliseconds;

            Registers.DX = outputSeconds;
            Registers.AX = outputMicroseconds;
        }

        /// <summary>
        ///    Copies the values of num bytes from the location pointed to by source directly to the memory block pointed to by destination.
        ///
        ///     Signature: void* memcpy (void* destination, const void* source,size_t num );
        ///
        ///     More Info: http://www.cplusplus.com/reference/cstring/memcpy/
        /// </summary>
        private void memcpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);
            var bytesToMove = GetParameter(4);

            //Verify the Destination will not overlap with the Source
            if (sourcePointer.Segment == destinationPointer.Segment)
            {
                if (destinationPointer.Offset < sourcePointer.Offset &&
                    destinationPointer.Offset + bytesToMove >= sourcePointer.Offset)
                {
                    throw new Exception(
                        $"Destination {destinationPointer} would overlap with source {sourcePointer} ({bytesToMove} bytes)");
                }
            }

            //Cast to array as the write can overlap and overwrite, mucking up the span read
            var sourceData = Module.Memory.GetArray(sourcePointer, bytesToMove);

            Module.Memory.SetArray(destinationPointer, sourceData);

#if DEBUG
            _logger.Info($"Copied {bytesToMove} bytes {sourcePointer}->{destinationPointer}");
#endif
            Registers.AX = destinationPointer.Offset;
            Registers.DX = destinationPointer.Segment;
        }

        /// <summary>
        ///     Compares the first num bytes of the block of memory pointed by ptr1 to the first num bytes pointed by ptr2,
        ///     returning zero if they all match or a value different from zero representing which is greater if they do not.
        ///
        ///     Signature: int memcmp ( const void * ptr1, const void * ptr2, size_t num )
        ///
        ///     More Info: http://www.cplusplus.com/reference/cstring/memcmp/
        /// </summary>
        private void memcmp()
        {
            var ptr1 = GetParameterPointer(0);
            var ptr2 = GetParameterPointer(2);
            var num = GetParameter(4);

            var ptr1Data = Module.Memory.GetArray(ptr1, num);
            var ptr2Data = Module.Memory.GetArray(ptr2, num);

            Registers.AX = 0;
            for (var i = 0; Registers.AX == 0 && i < num; i++)
            {
                Registers.AX = (ushort)(ptr1Data[i] - ptr2Data[i]);
            }
        }

        /// <summary>
        ///     Returns number of system lines 'in use'
        ///
        ///     int nliniu(void);
        /// </summary>
        private void nliniu()
        {
            Registers.AX = (ushort)ChannelDictionary.Count;
        }

        private ReadOnlySpan<byte> version => Module.Memory.GetVariablePointer("VERSION").ToSpan();

        /// <summary>
        ///     access - determines accessibility of a file
        ///
        ///     Signature: int access(const char *filename, int amode);
        /// </summary>
        private void access()
        {
            var mode = GetParameter(2);

            var fileName = _fileFinder.FindFile(Module.ModulePath, GetParameterFilename(0));

            //Strip Relative Pathing, it'll always be relative to the module location
            if (fileName.StartsWith(@".\"))
                fileName = fileName.Replace(@".\", string.Empty);

#if DEBUG
            _logger.Info($"Checking access for: {fileName}");
#endif

            ushort result = 0xFFFF;
            switch (mode)
            {
                case 0:
                    if (File.Exists(Path.Combine(Module.ModulePath, fileName)))
                        result = 0;
                    break;
            }

            Registers.AX = result;
        }

        /// <summary>
        ///     Set opnbtv() file-open mode
        ///
        ///     Signature: void omdbtv (int mode);
        /// </summary>
        private void omdbtv()
        {
            var mode = GetParameter(0);

            switch (mode)
            {
                case 0:
                    _logger.Info("Set opnbtv() mode to Normal");
                    break;
                case 0xFFFF:
                    _logger.Info("Set opnbtv() mode to Accelerated");
                    break;
                case 0xFFFE:
                    _logger.Info("Set opnbtv() mode to Read-Only");
                    break;
                case 0xFFFD:
                    _logger.Info("Set opnbtv() mode to Verify (Read-After-Write)");
                    break;
                case 0xFFFC:
                    _logger.Info("Set opnbtv() mode to Exclusive");
                    break;
                default:
                    throw new Exception($"Unknown opnbtv() mode: {mode}");
            }
        }

        /// <summary>
        ///     Checks a type E CNF option for one of several possible values
        ///
        ///     Signature: int index=tokopt(int msgnum, char *token1, chat *token2,....,NULL);
        /// </summary>
        private void tokopt()
        {
            var msgNum = GetParameter(0);

            var tokenList = new List<byte[]>();

            for (var i = 1; i < ushort.MaxValue; i += 2)
            {
                var messagePointer = GetParameterPointer(i);

                //Break on NULL, as it's the last in the sequence
                if (messagePointer.Equals(IntPtr16.Empty))
                    break;

                tokenList.Add(Module.Memory.GetString(messagePointer).ToArray());
            }

            var message = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgNum);

            for (var i = 0; i < tokenList.Count; i++)
            {
                if (message.SequenceEqual(tokenList[i]))
                {
                    Registers.AX = (ushort)(i + 1);
                    return;
                }
            }

            Registers.AX = 0;
        }


        /// <summary>
        ///     Returns the amount of memory left
        ///     TODO: This returns MAX always -- should it not?
        ///
        ///     Signature: long farcoreleft(void);
        /// </summary>
        private void farcoreleft()
        {
            Registers.DX = (ushort)(uint.MaxValue >> 16);
            Registers.AX = (ushort)(uint.MaxValue & 0xFFFF);
        }

        /// <summary>
        ///     Converts date and time to UNIX time format
        ///
        ///     Signature: long dostounix(struct date *d, struct time *t);
        /// </summary>
        private void dostounix()
        {
            var datePointer = GetParameterPointer(0);
            var timePointer = GetParameterPointer(2);

            var dateStruct = new DateStruct(Module.Memory.GetArray(datePointer, DateStruct.Size));
            var timeStruct = new TimeStruct(Module.Memory.GetArray(timePointer, TimeStruct.Size));

            var specifiedDate = new DateTime(dateStruct.year, dateStruct.month, dateStruct.day, timeStruct.hours,
                timeStruct.minutes, timeStruct.seconds);

            var epochTime = (uint)((DateTimeOffset)specifiedDate).ToUnixTimeSeconds();

#if DEBUG
            _logger.Info($"Returned DOSTOUNIX time: {epochTime}");
#endif

            Registers.DX = (ushort)(epochTime >> 16);
            Registers.AX = (ushort)(epochTime & 0xFFFF);
        }

        /// <summary>
        ///     Gets MS-DOS date
        ///
        ///     void getdate(struct date *dateblk);
        /// </summary>
        private void getdate()
        {
            var datePointer = GetParameterPointer(0);

            var dateStruct = new DateStruct(DateTime.Now);

            Module.Memory.SetArray(datePointer, dateStruct.Data);
        }

        /// <summary>
        ///     Capitalizes the first letter after each space in the specified string
        ///
        ///     Signature: void zonkhl(char *stg);
        /// </summary>
        private void zonkhl()
        {
            var stgPointer = GetParameterPointer(0);

            var stgToParse = Module.Memory.GetString(stgPointer).ToArray();

            var toUpper = true;
            for (var i = 0; i < stgToParse.Length; i++)
            {
                if (toUpper)
                {
                    if (stgToParse[i] >= 'a' && stgToParse[i] <= 'z')
                        stgToParse[i] -= 32;

                    toUpper = false;
                }
                else if (stgToParse[i] == 32)
                {
                    toUpper = true;
                }
            }

            Module.Memory.SetArray(stgPointer, stgToParse);
        }

        /// <summary>
        ///     Generates a random number between min and max
        ///
        ///     Signature: int genrdn(int min,int max);
        /// </summary>
        private void genrnd()
        {
            var min = GetParameter(0);
            var max = GetParameter(1);

            var randomValue = (ushort)new Random(Guid.NewGuid().GetHashCode()).Next(min, max);

#if DEBUG
            _logger.Info($"Generated Random Number: {randomValue}");
#endif

            Registers.AX = randomValue;
        }

        /// <summary>
        ///     Remove all whitespace characters
        ///
        ///     Signature: void rmvwht(char *string);
        /// </summary>
        private void rmvwht()
        {
            var stringPointer = GetParameterPointer(0);

            var stringToParse = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointer, true));

            var parsedString = stringToParse.Trim() + '\0';

            Module.Memory.SetArray(stringPointer, Encoding.ASCII.GetBytes(parsedString));
        }

        /// <summary>
        ///     Signed Modulo, non-significant (Borland C++ Implicit Function)
        ///
        ///     Signature: DX:AX = arg1 % arg2
        /// </summary>
        /// <returns></returns>
        private void f_lmod()
        {
            int arg1 = (GetParameter(1) << 16) | GetParameter(0);
            int arg2 = (GetParameter(3) << 16) | GetParameter(2);

            var result = arg1 % arg2;

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);

            RealignStack(8);
        }

        /// <summary>
        ///     Registers Module Agent information for Galacticomm Client/Server
        ///
        ///     While we set this -- we're going to ignore it
        ///
        ///     Signature: void register_agent(struct agent *agdptr);
        /// </summary>
        private void register_agent()
        {
            var agentPointer = GetParameterPointer(0);

            _galacticommClientServerAgent = new AgentStruct(Module.Memory.GetArray(agentPointer, AgentStruct.Size));
        }

        private ReadOnlySpan<byte> syscyc => Module.Memory.GetVariablePointer("SYSCYC").ToSpan();

        /// <summary>
        ///     Read a CNF option from a .MSG file
        ///
        ///     Signature: char *msgscan(char *msgfile,char *vblname);
        /// </summary>
        private void msgscan()
        {
            var msgFilePointer = GetParameterPointer(0);
            var variableNamePointer = GetParameterPointer(2);

            var msgFileName = Encoding.ASCII.GetString(Module.Memory.GetString(msgFilePointer, true));
            var variableName = Encoding.ASCII.GetString(Module.Memory.GetString(variableNamePointer, true));
            var msgFile = Module.Msgs.First(x => x.FileName == msgFileName.ToUpper());

            //Return Null Pointer if Value isn't found
            if (!msgFile.MsgValues.TryGetValue(variableName, out var msgVariableValue))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            if (!Module.Memory.TryGetVariablePointer("MSGSCAN", out var msgScanResultPointer))
                msgScanResultPointer = Module.Memory.AllocateVariable("MSGSCAN", 0x1000);


            Module.Memory.SetArray(msgScanResultPointer, new byte[0x1000]); //Zero it out
            Module.Memory.SetArray(msgScanResultPointer, msgVariableValue); //Write

            Registers.AX = msgScanResultPointer.Offset;
            Registers.DX = msgScanResultPointer.Segment;

        }

        /// <summary>
        ///     Returns a file date and time
        ///
        ///     Signature: long timendate=getdtd(int handle);
        /// </summary>
        private void getdtd()
        {
            var fileHandle = GetParameter(0);

            var filePointer = FilePointerDictionary[fileHandle];

            var fileTime = File.GetLastWriteTime(filePointer.Name);

            var packedTime = (ushort)((fileTime.Hour << 11) + (fileTime.Minute << 5) + (fileTime.Second >> 1));
            var packedDate = (ushort)((fileTime.Month << 5) + fileTime.Day + ((fileTime.Year - 1980) << 9));

#if DEBUG
            _logger.Info($"Returned Packed Date for {filePointer.Name} ({fileTime}) AX: {packedTime} DX: {packedDate}");
#endif
            Registers.AX = packedTime;
            Registers.DX = packedDate;
        }

        /// <summary>
        ///     Count the number of bytes and files in a directory
        ///
        ///     Signature: void cntdir(char *path)
        /// </summary>
        private void cntdir()
        {
            var pathPointer = GetParameterPointer(0);

            var pathString = _fileFinder.FindFile(Module.ModulePath, GetParameterFilename(0));

            var files = Directory.GetFiles(Module.ModulePath, pathString);
            uint totalBytes = 0;
            foreach (var f in files)
            {
                totalBytes += (uint)new FileInfo(f).Length;
            }

            Module.Memory.SetArray("NUMFILS", BitConverter.GetBytes((uint)files.Length));
            Module.Memory.SetArray("NUMBYTS", BitConverter.GetBytes(totalBytes));

        }

        private ReadOnlySpan<byte> numfils => Module.Memory.GetVariablePointer("NUMFILS").ToSpan();
        private ReadOnlySpan<byte> numbyts => Module.Memory.GetVariablePointer("NUMBYTS").ToSpan();
        private ReadOnlySpan<byte> numbytp => Module.Memory.GetVariablePointer("NUMBYTP").ToSpan();
        private ReadOnlySpan<byte> numdirs => Module.Memory.GetVariablePointer("NUMDIRS").ToSpan();

        /// <summary>
        ///     Closes the Specified Btrieve File
        ///
        ///     Signature: void clsbtv(struct btvblk *bbp)
        /// </summary>
        private void clsbtv()
        {
            var filePointer = GetParameterPointer(0);

#if DEBUG
            _logger.Info($"Closing BTV File: {filePointer}");
#endif

            BtrieveDeleteProcessor(filePointer);
            _currentMcvFile = null;
        }

        private ReadOnlySpan<byte> nglobs => Module.Memory.GetVariablePointer("NGLOBS").ToSpan();

        /// <summary>
        ///     Retrieves a C-string containing the value of the environment variable whose name is specified in the argument
        ///
        ///     Signature: char* getenv(const char* name);
        /// </summary>
        private void getenv()
        {
            var name = GetParameterFilename(0);

            if (!Module.Memory.TryGetVariablePointer("GETENV", out var resultPointer))
                resultPointer = Module.Memory.AllocateVariable("GETENV", 0xFF);

            switch (name)
            {
                case "PATH":
                    Module.Memory.SetArray(resultPointer, Encoding.ASCII.GetBytes("C:\\BBSV6\\\0"));
                    break;
            }

            Registers.AX = resultPointer.Offset;
            Registers.DX = resultPointer.Segment;
        }

        /// <summary>
        ///     (File Transfer) Issue a new tagspec pointer
        ///
        ///     Always returns 0
        ///
        ///     Signature: int ftgnew(void);
        /// </summary>
        private void ftgnew()
        {
            Registers.AX = 1;
        }

        /// <summary>
        ///     Compute DOS date
        ///
        ///     Signature: int date=datofc(int count);
        /// </summary>
        private void datofc()
        {
            var days = GetParameter(0);
            var dtDateTime = new DateTime(1980, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddDays(days);

            var packedDate = (dtDateTime.Month << 5) + dtDateTime.Day + ((dtDateTime.Year - 1980) << 9);

            Registers.AX = (ushort)packedDate;

        }

        /// <summary>
        ///     (File Transfer) Submit a tagspec for download
        ///
        ///     Always returns 0 to denote program now has control
        ///
        ///     Signature: int ftgsbm(char *prot);
        /// </summary>
        private void ftgsbm()
        {
            //Ignore Protocol Pointer passed into this method

            //Get Pointer to Download Method from FileSpec
            var fileSpec =
                new FtgStruct(Module.Memory.GetArray(Module.Memory.GetVariablePointer("FTG"), FtgStruct.Size));

            //Call TSHBEG to get file to send
            //Decrement the initial Stack Pointer enough to ensure it wont overwrite anything in ththate current stack space
            Module.Execute(fileSpec.tshndl, ChannelNumber, true, true,
                new Queue<ushort>(new List<ushort> { (ushort)FtgStruct.TagSpecFunctionCodes.TSHBEG }),
                (ushort)(Registers.SP - 0x800));

            //TSHMSG now holds the file to be transfered
            var fileToSend =
                Encoding.ASCII.GetString(Module.Memory.GetString(Module.Memory.GetVariablePointer("TSHMSG"), true));

            fileToSend = _fileFinder.FindFile(Module.ModulePath, fileToSend);

#if DEBUG
            _logger.Info($"Channel {ChannelNumber} downloding: {fileToSend}");
#endif

            ChannelDictionary[ChannelNumber].SendToClient(File.ReadAllBytes(Path.Combine(Module.ModulePath, fileToSend)));

            //Call TSHFIN to get file to send
            //Decrement the initial Stack Pointer enough to ensure it wont overwrite anything in the current stack space
            Module.Execute(fileSpec.tshndl, ChannelNumber, true, true,
                new Queue<ushort>(new List<ushort> { (ushort)FtgStruct.TagSpecFunctionCodes.TSHFIN }),
                (ushort)(Registers.SP - 0x800));

            Registers.AX = 0;
        }

        /// <summary>
        ///     (File Transfer) Global Tagspec Pointer
        ///
        ///     Signature: struct ftg *ftgptr;
        /// </summary>
        private ReadOnlySpan<byte> ftgptr => Module.Memory.GetVariablePointer("FTGPTR").ToSpan();

        /// <summary>
        ///     Universal global Tagspec Handler messag
        ///
        ///     Signature: char tshmsg[TSHLEN+1];
        /// </summary>
        private ReadOnlySpan<byte> tshmsg => Module.Memory.GetVariablePointer("TSHMSG").ToSpan();

        /// <summary>
        ///     (File Transfer) Contains fields for external use
        ///
        ///     Signature: struct ftfscb {...};
        /// </summary>
        private ReadOnlySpan<byte> ftfscb => Module.Memory.GetVariablePointer("FTFSCB").ToSpan();

        /// <summary>
        ///     Output buffer size per channel
        ///
        ///     Signature: int outbsz;
        /// </summary>
        private ReadOnlySpan<byte> outbsz => Module.Memory.GetVariablePointer("OUTBSZ").ToSpan();

        /// <summary>
        ///     System-Variable Btrieve Record Layout Struct (1 of 3)
        ///
        ///     Signature: struct sysvbl sv;
        /// </summary>
        private ReadOnlySpan<byte> sv => Module.Memory.GetVariablePointer("SV").ToSpan();

        /// <summary>
        ///     List an ASCII file to the users screen
        ///
        ///     Signature: void listing(char *path, void (*whndun)())
        /// </summary>
        private void listing()
        {
            var filenamePointer = GetParameterPointer(0);
            var finishedFunctionPointer = GetParameterPointer(2);

            var fileToSend = Encoding.ASCII.GetString(Module.Memory.GetString(filenamePointer, true));

            fileToSend = _fileFinder.FindFile(Module.ModulePath, fileToSend);

#if DEBUG
            _logger.Info($"Channel {ChannelNumber} listing: {fileToSend}");
#endif

            ChannelDictionary[ChannelNumber].SendToClient(File.ReadAllBytes(Path.Combine(Module.ModulePath, fileToSend)));

            Module.Execute(finishedFunctionPointer, ChannelNumber, true, true,
                null, (ushort)(Registers.SP - 0x800));
        }


        /// <summary>
        ///     Gets a btrieve record relative to the current offset
        ///
        ///     Signature: int anpbtv (void *recptr, int anpopt)
        /// </summary>
        private void anpbtv()
        {
            var recordPointer = GetParameterPointer(0);
            var btrieveOperation = GetParameter(2);

            //Landing spot for the data
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
            var btrieveRecord = new byte[currentBtrieveFile.LoadedFile.RecordLength];

            switch ((EnumBtrieveOperationCodes)btrieveOperation)
            {
                case EnumBtrieveOperationCodes.GetNext:
                    {
                        if (currentBtrieveFile.StepNext() == 0)
                        {
                            //EOF
                            Registers.AX = 0;
                            return;
                        }

                        btrieveRecord = currentBtrieveFile.GetRecord();
                        break;
                    }
            }

            //Set Memory Values
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));

            //Update the Pointer on the BTVFILE struct to point to the result
            Module.Memory.SetArray(btvStruct.data, btrieveRecord);

            //Update the record pointer passed in
            Module.Memory.SetArray(recordPointer, btrieveRecord);

            Registers.AX = 1;
        }

        /// <summary>
        ///     Test if the user has enough credits -- always returns yes
        ///
        ///     Signature: int enuf=tstcrd(long amount);
        /// </summary>
        private void tstcrd() => Registers.AX = 1;

        /// <summary>
        ///     0x7F if U.S.A. only, 0xFF if European
        ///
        ///     Signature: char eurmsk;
        /// </summary>
        private ReadOnlySpan<byte> eurmsk => Module.Memory.GetVariablePointer("EURMSK").ToSpan();

        /// <summary>
        ///     strnicmp - compare one string to another without case sensitivity
        ///
        ///     Signature: int strnicmp(const char *str1, const char *str2, size_t maxlen);
        /// </summary>
        private void strnicmp()
        {
            var string1 = GetParameterString(0, stripNull: true);
            var string2 = GetParameterString(2, stripNull: true);
            var maxLength = GetParameter(4);

            Registers.AX = (ushort)string.Compare(string1, 0, string2, 0, maxLength, true);
        }

        /// <summary>
        ///     Determines processor time.
        ///
        ///     The clock function returns the processor time elapsed since the beginning of the program invocation.
        ///
        ///     Signature: clock_t clock(void);
        /// </summary>
        private void clock()
        {
            Registers.DX = (ushort)((int)_highResolutionTimer.ElapsedMilliseconds & 0xFFFF);
            Registers.AX = (ushort)(((int)_highResolutionTimer.ElapsedMilliseconds & 0xFFFF0000) >> 16);
        }

        /// <summary>
        ///     Comparison of the C string str1 to the C string str2
        ///
        ///     Signature: int strncmp(const char *str1, const char *str2, size_t maxlen);
        /// </summary>
        private void strncmp()
        {
            var string1 = GetParameterString(0, stripNull: true);
            var string2 = GetParameterString(2, stripNull: true);
            var maxLength = GetParameter(4);

            Registers.AX = (ushort)string.Compare(string1, 0, string2, 0, maxLength);
        }

        /// <summary>
        ///     More Tolerant Update to Current Record
        ///
        ///     Signature: int dupdbtv (void *recptr);
        /// </summary>
        private void dupdbtv()
        {
            //Since we're not checking for dupes (yet?), just update and signal success
            updbtv();

            Registers.AX = 1;
        }

        /// <summary>
        ///     Opens a new file for reading/writing
        ///
        ///     This method differs from f_open in that it doesn't create a FILE struct, it only returns the handle
        ///
        ///     Signature: int open(const char *path, int access [, unsigned mode]);
        /// </summary>
        private void open()
        {
            var filenameInputValue = GetParameterFilename(0);
            var mode = GetParameter(2);

            var fileName = _fileFinder.FindFile(Module.ModulePath, filenameInputValue);
            var fileMode = (EnumOpenFlags)mode;

            var fullPath = Path.Combine(Module.ModulePath, fileName);
#if DEBUG
            _logger.Debug($"Opening File: {fullPath}");
#endif
            if (!File.Exists($"{fullPath}") && !fileMode.HasFlag(EnumOpenFlags.O_CREAT))
            {
                _logger.Warn($"Unable to find file {fullPath}");
                Registers.AX = 0xFFFF;
                return;
            }

            //Setup the File Stream
            var fileStream = File.Open(fullPath, FileMode.OpenOrCreate);

            var fileStreamPointer = FilePointerDictionary.Allocate(fileStream);

            Registers.AX = (ushort)fileStreamPointer;
        }

        /// <summary>
        ///     Creates a Directory
        ///
        ///     Signature: int mkdir(const char *pathname);
        /// </summary>
        private void mkdir()
        {
            var directoryNamePointer = GetParameterPointer(0);

            var directoryName = _fileFinder.FindFile(Module.ModulePath, Encoding.ASCII.GetString(Module.Memory.GetString(directoryNamePointer, true)));
            var fullPath = Path.Combine(Module.ModulePath, directoryName);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.Info($"Created Directory: {fullPath}");
            }

            Registers.AX = 0;
        }

        /// <summary>
        ///     getftime - gets file date and time
        ///
        ///     Signature: int getftime(int handle, struct ftime *ftimep);
        /// </summary>
        private void getftime()
        {
            var fileHandle = GetParameter(0);
            var ftimePointer = GetParameterPointer(1);

            var info = new FileInfo(FilePointerDictionary[fileHandle].Name);

            var packedTime = (ushort)((info.CreationTime.Hour << 11) + (info.CreationTime.Minute << 5) + (info.CreationTime.Second >> 1));
            var packedTimeData = BitConverter.GetBytes(packedTime);

            var packedDate = (ushort)(((DateTime.Now.Year - 1980) << 9) + (DateTime.Now.Month << 5) + DateTime.Now.Day);
            var packedDateData = BitConverter.GetBytes(packedDate);

            var ftimeStruct = new byte[4];
            Array.Copy(packedTimeData, 0, ftimeStruct, 0, sizeof(short));
            Array.Copy(packedDateData, 0, ftimeStruct, 2, sizeof(short));

            Module.Memory.SetArray(ftimePointer, ftimeStruct);
        }

        /// <summary>
        ///     close - close a file handle
        ///
        ///     Signature: int close(int handle);
        /// </summary>
        private void close()
        {
            var fileHandle = GetParameter(0);

            //Clean Up File Stream Pointer
            FilePointerDictionary[fileHandle].Close();
            FilePointerDictionary.Remove(fileHandle);
        }

        /// <summary>
        ///     Prepare answers (call after fsdroom()) (Full-Screen Data Entry)
        ///
        ///     Signature: void fsdapr(char *sesbuf, int sbleng, char *answers)
        /// </summary>
        private void fsdapr()
        {
            var sesbuf = GetParameterPointer(0);
            var sbleng = GetParameter(2);
            var answers = GetParameterPointer(3);

            if (!Module.Memory.TryGetVariablePointer($"FSD-Answers-{ChannelNumber}", out var fsdAnswersPointer))
                fsdAnswersPointer = Module.Memory.AllocateVariable($"FSD-Answers-{ChannelNumber}", 0x800);

            Module.Memory.SetZero(fsdAnswersPointer, 0x800);

            ushort answerBytesToRead = 0x800;
            if (answers.Offset + 0x800 > ushort.MaxValue)
                answerBytesToRead = (ushort)(ushort.MaxValue - answers.Offset);

            Module.Memory.SetArray(fsdAnswersPointer, Module.Memory.GetArray(answers, answerBytesToRead));
        }

        /// <summary>
        ///     fsd information struct
        ///
        ///     Signature: struct fsdscb *fsdscb;
        /// </summary>
        private ReadOnlySpan<byte> fsdscb => Module.Memory.GetVariablePointer("*FSDSCB").ToSpan();

        /// <summary>
        ///     Current btvu file pointer set
        ///
        ///     Signature: struct btvblk *bb;
        /// </summary>
        private ReadOnlySpan<byte> bb => Module.Memory.GetVariablePointer("BB").ToSpan();

        /// <summary>
        ///     Convert a string to a long integer
        ///
        ///     Signature: long strtol(const char *strP, char **suffixPP, int radix);
        /// </summary>
        private void strol()
        {
            var stringPointer = GetParameterPointer(0);
            var suffixPointer = GetParameterPointer(2);
            var radix = GetParameter(4);

            var stringContainingLongs = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointer, stripNull: true));

            var longToParse = stringContainingLongs.Split(' ')[0];
            var longToParseLength = longToParse.Length; //We do this as length might change with logic below

            if (longToParseLength == 0)
            {
                Registers.DX = 0;
                Registers.AX = 0;
                return;
            }

            if (radix == 0)
            {
                if (longToParse.StartsWith("0x"))
                {
                    radix = 16;
                }
                else
                {
                    radix = 10;
                }
            }

            var isNegative = false;
            if (radix != 10 && longToParse.StartsWith('-'))
            {
                longToParse = longToParse.TrimStart('-');
                isNegative = true;
            }

            if (radix != 10 && longToParse.StartsWith('+'))
                longToParse = longToParse.TrimStart('+');

            var resultLong = Convert.ToInt32(longToParse, radix);

            if (isNegative)
                resultLong *= -1;

            Registers.DX = (ushort)(resultLong >> 16);
            Registers.AX = (ushort)(resultLong & 0xFFFF);

            if (suffixPointer != IntPtr16.Empty)
                Module.Memory.SetPointer(suffixPointer,
                    new IntPtr16(stringPointer.Segment, (ushort)(stringPointer.Offset + longToParseLength + 1)));

        }

        /// <summary>
        ///     Returns an unmodified copy of the FSD template (set in fsdroom())
        ///
        ///     Signature: char *fsdrft(void);
        /// </summary>
        private void fsdrft()
        {
            var templatePointer = Module.Memory.GetVariablePointer($"FSD-TemplateBuffer-{ChannelNumber}");

            Registers.AX = templatePointer.Offset;
            Registers.DX = templatePointer.Segment;
        }

        /// <summary>
        ///     Display background for Full-Screen entry mode (Full-Screen Data Entry)
        ///
        ///     Signature: void fsdbkg(char *templt);
        /// </summary>
        private void fsdbkg()
        {
            var templatePointer = GetParameterPointer(0);
            ChannelDictionary[ChannelNumber].SendToClient("\x1B[0m\x1B[2J\x1B[0m"); //FSDBBS.C
            ChannelDictionary[ChannelNumber].SendToClient(FormatNewLineCarriageReturn(Module.Memory.GetString(templatePointer)));
        }

        /// <summary>
        ///     Sets the Header for FSD Session if using RIP
        ///
        ///     Signature: void fsdrhd (char *title);
        /// </summary>
        private void fsdrhd()
        {
            var ripHeaderStringPointer = GetParameterPointer(0);

            if (!Module.Memory.TryGetVariablePointer($"FSD-RIPHeader-{ChannelNumber}", out var ripHeaderPointer))
                ripHeaderPointer = Module.Memory.AllocateVariable($"FSD-RIPHeader-{ChannelNumber}", 0xFF);

            Module.Memory.SetArray(ripHeaderPointer, Module.Memory.GetString(ripHeaderStringPointer));
        }

        /// <summary>
        ///     Begin FSD entry session (call after fsdroom(), fsdapr())
        ///
        ///     Signature: void fsdego(int (*fldvfy)(int fldno, char *answer), void (*whndun)(int save));
        /// </summary>
        private void fsdego()
        {
            var fieldVerificationPointer = GetParameterPointer(0);
            var whenDoneRoutinePointer = GetParameterPointer(2);

            if (!Module.Memory.TryGetVariablePointer($"FSD-FieldVerificationRoutine-{ChannelNumber}",
                out var fsdFieldVerificationRoutinePointer))
                fsdFieldVerificationRoutinePointer =
                    Module.Memory.AllocateVariable($"FSD-FieldVerificationRoutine-{ChannelNumber}", IntPtr16.Size);

            if (!Module.Memory.TryGetVariablePointer($"FSD-WhenDoneRoutine-{ChannelNumber}",
                out var fsdWhenDoneRoutinePointer))
                fsdWhenDoneRoutinePointer =
                    Module.Memory.AllocateVariable($"FSD-WhenDoneRoutine-{ChannelNumber}", IntPtr16.Size);

            Module.Memory.SetPointer(fsdFieldVerificationRoutinePointer, fieldVerificationPointer);
            Module.Memory.SetPointer(fsdWhenDoneRoutinePointer, whenDoneRoutinePointer);

            ChannelDictionary[ChannelNumber].SessionState = EnumSessionState.EnteringFullScreenDisplay;

            //Update fsdscb struct
            var fsdscbStructPointer = Module.Memory.GetVariablePointer($"FSD-Fsdscb-{ChannelNumber}");
            var fsdscbStruct = new FsdscbStruct(Module.Memory.GetArray(fsdscbStructPointer, FsdscbStruct.Size));
            fsdscbStruct.fldvfy = fieldVerificationPointer;
            Module.Memory.SetArray(fsdscbStructPointer, fsdscbStruct.Data);


#if DEBUG
            _logger.Info($"Channel {ChannelNumber} entering Full Screen Display (v:{fieldVerificationPointer}, d:{whenDoneRoutinePointer}");
#endif
        }

        /// <summary>
        ///     Error message for fsdppc(), fsdprc(), etc. (Full-Screen Data Entry)
        ///
        ///     Signature: char fsdemg[];
        /// </summary>
        private ReadOnlySpan<byte> fsdemg => Module.Memory.GetVariablePointer("FSDEMG").ToSpan();

        /// <summary>
        ///     Factory-issue field verify routine, for ask-done-at-end scheme
        ///
        ///     Signature: int vfyadn(int fldno, char *answer);
        /// </summary>
        private void vfyadn()
        {
            var fieldNo = GetParameter(0);
            var answerPointer = GetParameterPointer(1);

            var answer = Module.Memory.GetString(answerPointer);

            //Get fsdscb Struct for this channel
            var fsdscbPointer = Module.Memory.GetVariablePointer($"FSD-Fsdscb-{ChannelNumber}");
            var fsdscbStruct = new FsdscbStruct(Module.Memory.GetArray(fsdscbPointer, FsdscbStruct.Size));

            //If we're on the last field
            if (fieldNo == fsdscbStruct.numtpl - 1)
            {
                switch (fsdscbStruct.xitkey)
                {
                    case (ushort)EnumKeyCodes.CRSDN: //Down
                    case 9: //Tab
                        Registers.AX = (ushort)EnumFsdStateCodes.VFYCHK;
                        return;

                    case 27: //Escape
                        Registers.AX = (ushort)EnumFsdStateCodes.VFYDEF;
                        return;
                }

                switch (answer[0])//First Character of Answer
                {
                    case (byte)'S': //SAVE
                    case (byte)'Y': //YES=Done and Save
                    case (byte)'D': //DONE
                        {
                            fsdscbStruct.state = (byte)EnumFsdStateCodes.FSDSAV;
                            Registers.AX = (ushort)EnumFsdStateCodes.FSDSAV;
                            break;
                        }

                    case (byte)'Q': //QUIT
                    case (byte)'X': //eXit
                    case (byte)'A': //Abort or Abandon
                        {
                            fsdscbStruct.state = (byte)EnumFsdStateCodes.FSDQIT;
                            Registers.AX = (ushort)EnumFsdStateCodes.FSDQIT;
                            break;
                        }

                    case (byte)'N': //No == Edit Some More
                    case (byte)'E': //Edit
                        {
                            Registers.AX = (ushort)EnumFsdStateCodes.VFYDEF;
                            break;
                        }
                    default:
                        //Default
                        Registers.AX = (ushort)EnumFsdStateCodes.VFYCHK;
                        break;
                }
            }
            else
            {
                //Any field that's not the last
                switch (fsdscbStruct.xitkey)
                {
                    case 27 when fsdscbStruct.chgcnt > 0: //Escape
                        {
                            Module.Memory.SetArray("FSDEMG", Encoding.ASCII.GetBytes("Are you sure? Enter QUIT to quit now\0"));
                            Registers.AX = (ushort)EnumFsdStateCodes.VFYCHK;
                            break;
                        }
                    default:
                        //Default
                        Registers.AX = (ushort)EnumFsdStateCodes.VFYCHK;
                        break;
                }
            }

            //Save any changes to fsdscb struct
            Module.Memory.SetArray(fsdscbPointer, fsdscbStruct.Data);
        }

        /// <summary>
        ///     Get a field's answer (Full-Screen Data Entry)
        ///
        ///     Signature: char *stg=fsdnan(int fldno);
        /// </summary>
        private void fsdnan()
        {
            var fieldNo = GetParameter(0);

            //Get FSD Status from the Global Cache
            var fsdStatus = _globalCache.Get<FsdStatus>($"FSD-Status-{ChannelNumber}");

            if (!Module.Memory.TryGetVariablePointer($"fsdnan-buffer-{fieldNo}", out var fsdnanPointer))
                fsdnanPointer = Module.Memory.AllocateVariable($"fsdnan-buffer-{fieldNo}", 0xFF);

            Module.Memory.SetArray(fsdnanPointer, Encoding.ASCII.GetBytes(fsdStatus.Fields[fieldNo].Value + '\0'));

#if DEBUG
            _logger.Info($"Retrieved Field {fieldNo}: {fsdStatus.Fields[fieldNo].Value} ({fsdnanPointer})");
#endif

            Registers.AX = fsdnanPointer.Offset;
            Registers.DX = fsdnanPointer.Segment;
        }

        /// <summary>
        ///     Gets a fields ordinal for a Multiple-Choice Field
        ///
        ///     Signature: int fsdord(int fldi);
        /// </summary>
        private void fsdord()
        {
            var fieldNo = GetParameter(0);

            //Get FSD Status from the Global Cache
            var fsdStatus = _globalCache.Get<FsdStatus>($"FSD-Status-{ChannelNumber}");

            Registers.AX = (ushort)fsdStatus.Fields[fieldNo].SelectedValue;
        }

        /// <summary>
        ///     no-preparation- extract answer from answer string (Full Screen Data Entry)
        ///
        ///     Signature: char *fsdxan(char *answer, char *name);
        /// </summary>
        private void fsdxan()
        {
            var answerPointer = GetParameterPointer(0);
            var answerNamePointer = GetParameterPointer(2);

            var answerName = Encoding.ASCII.GetString(Module.Memory.GetString(answerNamePointer, true));
            var answerString = Encoding.ASCII.GetString(Module.Memory.GetArray(answerPointer, 0x400));

            //Trim the Answer String to just the component we need
            var answerStart = answerString.IndexOf(answerName, StringComparison.InvariantCultureIgnoreCase);
            var trimmedAnswerString = answerString.Substring(answerStart);
            trimmedAnswerString = trimmedAnswerString.Substring(0, trimmedAnswerString.IndexOf('\0'));

            var answerComponents = trimmedAnswerString.Split('=');

            if (!Module.Memory.TryGetVariablePointer("fsdxan-buffer", out var fsdxanPointer))
                fsdxanPointer = Module.Memory.AllocateVariable("fsdxan-buffer", 0xFF);

            Module.Memory.SetZero(fsdxanPointer, 0xFF);
            Module.Memory.SetArray(fsdxanPointer, Encoding.ASCII.GetBytes($"{answerComponents[1]}"));

            Registers.AX = fsdxanPointer.Offset;
            Registers.DX = fsdxanPointer.Segment;
        }

        /// <summary>
        ///     Sorts a bunch of strings by re-arranging an array of pointers
        ///
        ///     Signature: void sortstgs(char *stgs[],int num);
        /// </summary>
        private void sortstgs()
        {
            var stringPointerBase = GetParameterPointer(0);
            var numberOfStrings = GetParameter(2);

            if (numberOfStrings == 0)
                return;

            var listOfStrings = new List<Tuple<IntPtr16, string>>(numberOfStrings);

            //Grab the pointers and the strings they point to, save in a Tuple
            for (var i = 0; i < numberOfStrings; i++)
            {
                var pointerOffset = (ushort)(stringPointerBase.Offset + (i * IntPtr16.Size));
                var destinationPointer = Module.Memory.GetPointer(stringPointerBase.Segment, pointerOffset);
                listOfStrings.Add(new Tuple<IntPtr16, string>(destinationPointer, Encoding.ASCII.GetString(Module.Memory.GetString(destinationPointer, true))));
            }

            //Order them alphabetically by string
            var sortedStrings = listOfStrings.OrderBy(x => x.Item2).ToList();

            //Write the new string destination pointers
            for (var i = 0; i < numberOfStrings; i++)
            {
                var pointerOffset = (ushort)(stringPointerBase.Offset + (i * IntPtr16.Size));
                Module.Memory.SetPointer(stringPointerBase.Segment, pointerOffset, sortedStrings[i].Item1);
            }
        }

        /// <summary>
        ///     Get the last word of a string
        ///
        ///     Signature: char *lastwd(char *string);
        /// </summary>
        private void lastwd()
        {
            var stringPointerBase = GetParameterPointer(0);

            var stringToParse = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointerBase));

            if (!Module.Memory.TryGetVariablePointer("lastwd-buffer", out var lastwdBufferPointer))
                lastwdBufferPointer = Module.Memory.AllocateVariable("lastwd-buffer", 0xFF);

            Module.Memory.SetArray(lastwdBufferPointer, Encoding.ASCII.GetBytes($"{stringToParse.Split(' ').Last()}\0"));

            Registers.AX = lastwdBufferPointer.Offset;
            Registers.DX = lastwdBufferPointer.Segment;
        }

        /// <summary>
        ///     Skip past non-white spaces, returns first NULL or whitespace character in the string
        ///
        ///     Signature: char *skpwrd(char *cp);
        /// </summary>
        private void skpwrd()
        {
            var stringPointerBase = GetParameterPointer(0);

            for (var i = stringPointerBase.Offset; i < ushort.MaxValue; i++)
            {
                var currentCharacter = Module.Memory.GetByte(stringPointerBase.Segment, i);
                if (currentCharacter != 0 && currentCharacter != ' ') continue;
                Registers.AX = i;
                Registers.DX = stringPointerBase.Segment;
                return;
            }

            Registers.AX = 0;
            Registers.DX = 0;
        }

        /// <summary>
        ///     Find text variable & return number
        ///
        ///     Signature: int findtvar(char *name);
        /// </summary>
        private void findtvar()
        {
            var textVariableNamePointer = GetParameterPointer(0);

            var textVariableName = Encoding.ASCII.GetString(Module.Memory.GetString(textVariableNamePointer, true));

            for (var i = 0; i < Module.TextVariables.Count; i++)
            {
                if (Module.TextVariables.Keys.Select(x => x).ToList()[i] != textVariableName) continue;

                Registers.AX = (ushort)i;
                return;
            }

            Registers.AX = 0xFFFF;
        }

        /// <summary>
        ///     Turns on echo utility for the specified user
        ///
        ///
        ///     Signature: void echonu(int usrnum);
        /// </summary>
        private void echonu()
        {
            var channelNumber = GetParameter(0);

#if DEBUG
            _logger.Info($"Disabling Character Interceptor & Enabling Echo on Channel {channelNumber}");
#endif

            ChannelDictionary[channelNumber].CharacterInterceptor = null;
            ChannelDictionary[channelNumber].TransparentMode = false;
        }

        /// <summary>
        ///     Inject a message to another user (implicit inputs othusn, prfbuf).
        ///
        ///     Signature: int gotIt=injoth();
        /// </summary>
        private void injoth()
        {
            var userChannel = Module.Memory.GetWord("OTHUSN");
            var basePointer = Module.Memory.GetVariablePointer("PRFBUF");
            var currentPointer = Module.Memory.GetPointer("PRFPTR");

            var outputLength = (ushort)(currentPointer.Offset - basePointer.Offset);

            var outputBuffer = Module.Memory.GetArray(basePointer, outputLength);

            var outputBufferProcessed = FormatOutput(outputBuffer);

            ChannelDictionary[userChannel].SendToClient(outputBufferProcessed);

            Module.Memory.SetZero(basePointer, outputLength);

            //Set prfptr to the base address of prfbuf
            Module.Memory.SetPointer("PRFPTR", Module.Memory.GetVariablePointer("PRFBUF"));
        }

        /// <summary>
        ///     Defined "SYSOP KEY" in MajorBBS Config
        ///
        ///     This is "SYSOP" by default
        /// </summary>
        private ReadOnlySpan<byte> syskey => Module.Memory.GetVariablePointer("*SYSKEY").ToSpan();

        /// <summary>
        ///     "strip" blank spaces after input
        ///
        ///     Signature: void stripb(char *stg);
        /// </summary>
        private void stripb()
        {
            var stringToParsePointer = GetParameterPointer(0);

            var stringToParse = Module.Memory.GetString(stringToParsePointer).ToArray();

            for (var i = 2; i < stringToParse.Length; i++)
            {
                if (stringToParse[^i] == (byte)' ')
                    continue;

                //String had no trailing spaces
                if (i == 1)
                    return;

                stringToParse[^(i - 1)] = 0;

                //Write the new terminated string back
                Module.Memory.SetArray(stringToParsePointer, stringToParse);
            }
        }

        /// <summary>
        ///     Formats a String for use in btrieve (best I can tell)
        ///
        ///     Signature: void makhdl(char *stg);
        /// </summary>
        private void makhdl()
        {
            stripb();
            zonkhl();
        }

        /// <summary>
        ///     char is a valid signup uid char
        ///
        ///     Signature: int issupc(int c);
        /// </summary>
        private void issupc()
        {
            var character = (char)GetParameter(0);

            if (!char.IsLetterOrDigit(character) && !char.IsSeparator(character) && !char.IsSymbol(character))
            {
                Registers.AX = 0;
            }
            else
            {
                Registers.AX = 1;
            }
        }

        /// <summary>
        ///     I believe, like RTIHDLR, that this routine is used to register a task that runs at a very
        ///     quick interval. This might have been added to handle sub-second routines not running on DOS,
        ///     and not having the DOS Interrupt ability that RTIHDLR used.
        ///
        ///     Signature: int initask(void (*tskaddr)(int taskid));
        /// </summary>
        private void initask()
        {
            var routinePointerOffset = GetParameter(0);
            var routinePointerSegment = GetParameter(1);

            var routine = new RealTimeRoutine(routinePointerSegment, routinePointerOffset);
            var routineNumber = Module.TaskRoutines.Allocate(routine);
            Module.EntryPoints.Add($"TASK-{routineNumber}", routine);
#if DEBUG
            _logger.Info($"Registered routine {routinePointerSegment:X4}:{routinePointerOffset:X4}");
#endif
            Registers.AX = (ushort)routineNumber;
        }

        /// <summary>
        ///     Generate a long random number
        ///
        ///     Signature: long lngrnd(long min,long max);
        /// </summary>
        private void lngrnd()
        {
            var min = GetParameterLong(0);
            var max = GetParameterLong(2);
            var randomValue = new Random(Guid.NewGuid().GetHashCode()).Next(min, max);

            Registers.DX = (ushort)(randomValue >> 16);
            Registers.AX = (ushort)(randomValue & 0xFFFF);
        }

        /// <summary>
        ///     'Acquire' a Btrieve record from a file position
        ///
        ///     Signature: int aabbtv (void *recptr, long abspos, int keynum);
        /// </summary>
        private void aabbtv()
        {
            var recordPointer = GetParameterPointer(0);
            var absolutePosition = GetParameterLong(2);
            var keynum = GetParameter(6);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var record = currentBtrieveFile.GetRecord((uint)absolutePosition);

            if (record != null)
            {
                //Record Found
                var btvStruct = new BtvFileStruct(Module.Memory.GetArray(Module.Memory.GetPointer("BB"), BtvFileStruct.Size));
                Module.Memory.SetArray(btvStruct.data, record.Data);

                if (recordPointer != IntPtr16.Empty)
                    Module.Memory.SetArray(recordPointer, record.Data);

                Registers.AX = 1;
            }
            else
            {
                //Record Not Found
                Registers.AX = 0;
            }

        }

        /// <summary>
        ///     Checks if an offline user has the specified key
        ///
        ///     Signature: int uidkey (char *uid, char *lock);
        /// </summary>
        private void uidkey()
        {
            // no key support for now, so everybody has the key
            Registers.AX = 1;
        }

        /// <summary>
        ///     Checks if the other user has the specified key, the one specified by othusn
        ///     and othusp.
        ///
        ///     Signature: int othkey (char *lock);
        /// </summary>
        private void othkey()
        {
            // no key support for now, so everybody has the key
            Registers.AX = 1;
        }

        /// <summary>
        ///     Converts a ulong to an ASCII string
        ///
        ///     Signature: char *ul2as(ulong longin)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment
        /// </summary>
        private void ul2as()
        {
            var lowByte = GetParameter(0);
            var highByte = GetParameter(1);

            var outputValue = $"{(uint)(highByte << 16 | lowByte)}\0";

            var resultPointer = Module.Memory.GetOrAllocateVariablePointer("UL2AS", 0xF);

            Module.Memory.SetArray(resultPointer, Encoding.Default.GetBytes(outputValue));

#if DEBUG
            _logger.Info(
                $"Received value: {outputValue}, string saved to {resultPointer}");
#endif

            Registers.AX = resultPointer.Offset;
            Registers.DX = resultPointer.Segment;
        }

        /// <summary>
        ///     Current Language
        ///
        ///     This will always be defaulted to 0 in MBBSEmu, which is ANSI
        ///
        ///     Signature: int clingo;
        /// </summary>
        private ReadOnlySpan<byte> clingo => Module.Memory.GetVariablePointer("CLINGO").Data;

        /// <summary>
        ///     Array of pointers to the lingo Structures
        ///
        ///     Since clingo for MBBSEmu will always be 0, we only set one structure in the array
        ///
        ///     Signature: struct lingo **languages;
        /// </summary>
        private ReadOnlySpan<byte> languages => Module.Memory.GetVariablePointer("**LANGUAGES").Data;

        /// <summary>
        ///     MS-DOS exit code (for batch files)
        ///
        ///     Signature: int errcod;
        /// </summary>
        private ReadOnlySpan<byte> errcod => Module.Memory.GetVariablePointer("ERRCOD").Data;

        /// <summary>
        ///     Determines the profanity level of a string
        ///
        ///     MBBSEmu doesn't support multiple profanity levels, so the default value of 0 is returned.
        ///
        ///     Signature:  int profan(char *string);
        /// </summary>
        private void profan()
        {
            Registers.AX = 0;
        }

        /// <summary>
        ///     Expect a YES or NO from the user
        ///
        ///     Signature: int yesno=cncyesno();
        /// </summary>
        private void cncyesno()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            if (remainingCharactersInCommand == 0)
            {
                Registers.AX = 0;
                return;
            }

            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);
            var inputStringComponents = Encoding.ASCII.GetString(inputString).ToUpper().Split('\0');

            if (inputStringComponents.Length == 0)
            {
                Registers.AX = 0;
                return;
            }

            switch (inputStringComponents[0])
            {
                case "YES":
                case "Y":
                    Registers.AX = 'Y';
                    break;
                case "NO":
                case "N":
                    Registers.AX = 'N';
                    break;
                default:
                    Registers.AX = string.IsNullOrEmpty(inputStringComponents[0]) ? (ushort)0 : inputStringComponents[0][0];
                    return;
            }

            Module.Memory.SetPointer("NXTCMD", new IntPtr16(inputPointer.Segment, (ushort)(inputPointer.Offset + inputStringComponents[0].Length + 1)));
        }


        /// <summary>
        ///     Converts all uppercase letters in the string to lowercase
        ///
        ///     Result buffer is 1k
        ///
        ///     Signature: char *lower=strlwr(char *string);
        /// </summary>
        private void strlwr()
        {
            strmod(Char.ToLower);
        }

        /// <summary>
        ///     Sends formatted output to stdout
        ///
        ///     Some modules used this to print to the main console
        /// </summary>
        private void printf()
        {
            var formatStringPointer = GetParameterPointer(0);

            _logger.Info(Encoding.ASCII.GetString(FormatPrintf(Module.Memory.GetString(formatStringPointer), 2)));
        }

        /// <summary>
        ///     Generic "Has Key" routine which takes in the USER struct vs. the current channel
        ///
        ///     Signature: int ok=gen_haskey(char *lock, int unum, struct user *uptr);
        ///     Returns: AX = 1 == True
        /// </summary>
        /// <returns></returns>
        private void gen_haskey()
        {
            var lockNamePointer = GetParameterPointer(0);
            var lockNameBytes = Module.Memory.GetString(lockNamePointer, true);

#if DEBUG
            _logger.Info($"Returning TRUE for Haskey({Encoding.ASCII.GetString(lockNameBytes)})");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Expect a Word from the user (character from the current command)
        ///
        ///     cncwrd() is executed after begincnc(), which runs rstrin() replacing the null separtors
        ///     in the string with spaces once again.
        /// </summary>
        private void cncwrd()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            //Skip any excessive spacing
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }
            var returnPointer = Module.Memory.GetOrAllocateVariablePointer("CNCWRD", 0x1E); //max length is 30 characters
            Registers.DX = returnPointer.Segment;
            Registers.AX = returnPointer.Offset;

            //Verify we're not at the end of the input
            if (remainingCharactersInCommand == 0 || Module.Memory.GetByte(nxtcmdPointer) == 0)
            {
                //Write null to output
                Module.Memory.SetByte(returnPointer, 0);
                return;
            }

            var returnedWord = new MemoryStream();
            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            //Build Return Word stopping when a space is encountered
            foreach (var b in inputString)
            {
                if (b == ' ' || b == 0)
                    break;

                returnedWord.WriteByte(b);
            }

            //Truncate to 29 bytes
            if(returnedWord.Length > 29)
                returnedWord.SetLength(29);

            returnedWord.WriteByte(0);

            Module.Memory.SetArray(returnPointer, returnedWord.ToArray());

            //Modify the Counters
            remainingCharactersInCommand -= (int)returnedWord.Length;
            nxtcmdPointer.Offset += (ushort) returnedWord.Length;

            //Advance to the next, non-space character
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            Module.Memory.SetPointer("NXTCMD", new IntPtr16(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
        }

        /// <summary>
        ///     Returns the number of records within the current Btrieve file
        ///
        ///     Signature: long cntrbtv (void);
        /// </summary>
        private void cntrbtv()
        {
            var currentBtrieveFilePointer = Module.Memory.GetPointer("BB");
            var currentBtrieveFile = BtrieveGetProcessor(currentBtrieveFilePointer);

            Registers.DX = (ushort)(currentBtrieveFile.LoadedFile.RecordLength >> 16);
            Registers.AX = (ushort)(currentBtrieveFile.LoadedFile.RecordLength & 0xFFFF);
        }

        /// <summary>
        ///     Phar-Lap Tiled Memory Allocation
        ///
        ///     Signature: int pltile(ULONG size, INT bsel, UINT stride, UINT tsize)
        /// </summary>
        private void pltile()
        {
            var size = GetParameterLong(0);
            var bsel = GetParameter(2);
            var stride = GetParameter(3);
            var tsize = GetParameter(4);

            if(size > ushort.MaxValue)
                throw new Exception("Allocation Exceeds Segment Size");

            var realSegmentBase = Module.Memory.AllocateRealModeSegment();

            /*
             * There is some magic in PHAPI for mapping of protected-mode segments to real-mode
             * memory addresses. It translates back that each tile is 8 segments apart.
             * Because of this, we'll go ahead and reserve the # of tiles * 8. While this is technically
             * an over allocation, it shouldn't hurt.
            */
            var numberOfSegments = (size / tsize) * 8;

            for(var i = 0; i < numberOfSegments; i++)
                Module.Memory.AllocateRealModeSegment(tsize);

            _logger.Debug($"Allocated base {realSegmentBase} for {size} bytes ({numberOfSegments} segments)");

            Registers.AX = realSegmentBase.Segment;
        }

        ///     Returns the day of the week 0->6 Sunday->Saturday
        ///
        ///     Signature: int daytoday(void);
        /// </summary>
        public void daytoday()
        {
            Registers.AX = (ushort)DateTime.Now.DayOfWeek;
        }

        /// <summary>
        ///     Digits allowed in User-IDs. We default this to TRUE
        ///
        ///     Signature: int digalw;
        /// </summary>
        public ReadOnlySpan<byte> digalw => Module.Memory.GetVariablePointer("DIGALW").Data;

        /// <summary>
        ///     Expect a Decimal from the user (character from the current command)
        ///     Allows for a leading '-' to support negative numbers, and stops at the first
        ///     non-numeric character.
        ///
        ///     cncnum() is executed after begincnc(), which runs rstrin() replacing the null separtors
        ///     in the string with spaces once again.
        /// </summary>
        private void cncnum()
        {
            //Get Input
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            //Skip any excessive spacing
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }
            var returnPointer = Module.Memory.GetOrAllocateVariablePointer("CNCNUM", 12); //max length is 12 characters
            Registers.DX = returnPointer.Segment;
            Registers.AX = returnPointer.Offset;

            //Verify we're not at the end of the input
            if (remainingCharactersInCommand == 0 || Module.Memory.GetByte(nxtcmdPointer) == 0)
            {
                //Write null to output
                Module.Memory.SetByte(returnPointer, 0);
                return;
            }

            var returnedWord = new MemoryStream();
            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            //Build Return Decimal stopping when a space/non-digit is encountered
            for (var i = 0; i < 12; i++)
            {
                var b = inputString[i];

                //Allow the 1st character to be a negative sign
                if (i == 0 && b == '-')
                {
                    returnedWord.WriteByte(b);
                    continue;
                }

                if (b == ' ' || b == 0 || !char.IsDigit((char) b))
                    break;

                returnedWord.WriteByte(b);
            }

            returnedWord.WriteByte(0);

            Module.Memory.SetArray(returnPointer, returnedWord.ToArray());

            //Modify the Counters -- if there's more than just the null terminator
            if (returnedWord.Length > 1)
            {
                remainingCharactersInCommand -= (int) returnedWord.Length;
                nxtcmdPointer.Offset += (ushort) returnedWord.Length;
            }

            //Advance to the next, non-space character
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            Module.Memory.SetPointer("NXTCMD", new IntPtr16(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
        }


        /// <summary>
        ///     Retrieves a Hex value as Numeric option from MCV file
        ///
        ///     Signature: unsigned hexopt(int msgnum,unsigned floor,unsigned ceiling);
        ///     Return: AX = Value retrieved
        /// </summary>
        private void hexopt()
        {
            var msgnum = GetParameter(0);
            var floor = (short)GetParameter(1);
            var ceiling = GetParameter(2);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetHex(msgnum);

            //Validate
            if (outputValue < floor || outputValue > ceiling)
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Info($"Retrieved option {msgnum} value: {outputValue}");
#endif

            Registers.AX = outputValue;
        }

        /// <summary>
        ///     Determines if the specified user is online
        ///
        ///     Signature: int ison=onsys(char *usrid);
        /// </summary>
        private void onsys()
        {
            var usernamePointer = GetParameterPointer(0);

            var username = Encoding.ASCII.GetString(Module.Memory.GetString(usernamePointer, true));

            //Scan the current channels for any usernames that match the specified one
            Registers.AX = ChannelDictionary.Any(x =>
                string.Equals(x.Value.Username, username, StringComparison.CurrentCultureIgnoreCase)) ? (ushort) 1 : (ushort) 0;
        }

        /// <summary>
        ///     Test if the user has enough real credits
        ///
        ///     We always return TRUE since MBBSEmu doesn't use/consume credits
        ///
        ///     Signature: int enuf=rtstcrd(long amount);
        /// </summary>
        private void rtstcrd()
        {
            Registers.AX = 1;
        }


        /// <summary>
        ///     Decode string date formatted 'MM/DD/YY' to int format YYYYYYYMMMMDDDDD
        ///
        ///     Signature: int date=dcdate(char *ascdat);
        /// </summary>
        private void dcdate()
        {
            var inputDate = GetParameterString(0, true);

            if (!DateTime.TryParse(inputDate, out var inputDateTime))
            {
                Registers.AX = 0xFFFF;
                return;
            }

            Registers.AX = (ushort)((inputDateTime.Month << 5) + inputDateTime.Day + ((inputDateTime.Year - 1980) << 9));
        }
    }
}

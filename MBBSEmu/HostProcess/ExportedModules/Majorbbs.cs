using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Date;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using MBBSEmu.Util;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        /// <summary>Used to specify the return type of cncint/cnclon/cncnum</summary>
        private enum CncIntegerReturnType
        {
            INT, // for cncint()
            LONG, // for cnclon()
            STRING, // for cncnum()
        }

        public enum TFStateCodes : ushort
        {
            TFSDUN = 0,
            TFSBGN = 1,
            TFSBOF = 2,
            TFSLIN = 3,
            TFSEOF = 4
        }

        public const ushort TFSBUF_MAX_LENGTH = 128;

        public FarPtr GlobalCommandHandler;

        private FarPtr _currentMcvFile
        {
            get => Module.Memory.GetPointer("CURRENT-MCV");
            set => Module.Memory.SetPointer("CURRENT-MCV", value ?? FarPtr.Empty);
        }
        private readonly Stack<FarPtr> _previousMcvFile;

        private readonly Stack<FarPtr> _previousBtrieveFile;

        /// <summary>
        ///     Maximum Size of the VDA Area of Memory
        /// </summary>
        public const ushort VOLATILE_DATA_SIZE = 0x3FFF;

        private readonly Stopwatch _highResolutionTimer = new Stopwatch();

        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());

        private AgentStruct _galacticommClientServerAgent;

        private int findtvarValue;

        /// <summary>
        ///     Stores all active searches created via fnd1st
        /// </summary>
        private readonly Dictionary<Guid, IEnumerator<string>> _activeSearches = new Dictionary<Guid, IEnumerator<string>>();

        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFF;

        /// <summary>
        ///     Repository with Account Key Information
        /// </summary>
        private readonly IAccountKeyRepository _accountKeyRepository;

        /// <summary>
        ///     Repository with Account Information
        /// </summary>
        private readonly IAccountRepository _accountRepository;

        /// <summary>
        ///     Index for SPR Variable
        /// </summary>
        private int _sprIndex;

        /// <summary>
        ///     Int 21h handler
        /// </summary>
        private readonly Int21h _int21h;

        private readonly Dictionary<string, string> setCnfDictionary = new();
        private bool applyemCalled = false;

        //  --------- For TFS functions ---------
        private FarPtr _tfsState;
        private FarPtr _tfsbuf;
        private FarPtr _tfspst;
        private StreamReader _tfsStreamReader;
        //  -------------------------------------

        public new void Dispose()
        {
            _tfsStreamReader?.Dispose();

            base.Dispose();
        }

        public Majorbbs(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, IAccountKeyRepository accountKeyRepository, IAccountRepository accountRepository, ITextVariableService textVariableService) : base(
           clock, logger, configuration, fileUtility, globalCache, module, channelDictionary, textVariableService)
        {
            _accountKeyRepository = accountKeyRepository;
            _accountRepository = accountRepository;
            _previousMcvFile = new Stack<FarPtr>(10);
            _previousBtrieveFile = new Stack<FarPtr>(10);
            _highResolutionTimer.Start();

            _int21h = new Int21h(Registers, Module.Memory, _clock, _logger, _fileFinder, null, null, new TextWriterStream(Console.Error), Module.ModulePath);

            //Add extra channel for "system full" message
            var _numberOfChannels = _configuration.BBSChannels + 1;

            //Setup Memory for Variables
            Module.Memory.AllocateVariable("PRFBUF", 0x4000, true); //Output buffer, 8kb
            Module.Memory.AllocateVariable("PRFPTR", FarPtr.Size);
            Module.Memory.AllocateVariable("OUTBSZ", sizeof(ushort));
            Module.Memory.SetWord("OUTBSZ", OUTBUF_SIZE);
            Module.Memory.AllocateVariable("INPUT", 0xFF); //255 Byte Maximum user Input
            Module.Memory.AllocateVariable("USER", (ushort)(User.Size * _numberOfChannels), true);
            Module.Memory.AllocateVariable("*USRPTR", 0x4); //pointer to the current USER record
            Module.Memory.AllocateVariable("STATUS", 0x2); //ushort Status
            Module.Memory.AllocateVariable("CHANNEL", 0x1FE); //255 channels * 2 bytes
            Module.Memory.AllocateVariable("MARGC", sizeof(ushort));
            Module.Memory.AllocateVariable("MARGN", 0x200); //max 128 pointers * 4 bytes each
            Module.Memory.AllocateVariable("MARGV", 0x200); //max 128 pointers * 4 bytes each
            Module.Memory.AllocateVariable("INPLEN", sizeof(ushort));
            Module.Memory.AllocateVariable("USRNUM", 0x2);
            var usraccPointer = Module.Memory.AllocateVariable("USRACC", (ushort)(UserAccount.Size * _numberOfChannels), true);
            var usaptrPointer = Module.Memory.AllocateVariable("USAPTR", 0x4);
            Module.Memory.SetArray(usaptrPointer, usraccPointer.Data);

            var usrExtPointer = Module.Memory.AllocateVariable("EXTUSR", (ushort)(ExtUser.Size * _numberOfChannels), true);
            var extPtrPointer = Module.Memory.AllocateVariable("EXTPTR", 0x4);
            Module.Memory.SetArray(extPtrPointer, usrExtPointer.Data);

            Module.Memory.AllocateVariable("OTHUAP", FarPtr.Size, true); //Pointer to OTHER user
            Module.Memory.AllocateVariable("OTHEXP", FarPtr.Size, true); //Pointer to OTHER user
            var ntermsPointer = Module.Memory.AllocateVariable("NTERMS", 0x2); //ushort number of lines
            Module.Memory.SetWord(ntermsPointer, (ushort)_numberOfChannels); // Number of channels from Settings

            Module.Memory.AllocateVariable("OTHUSN", 0x2); //Set by onsys() or instat()
            Module.Memory.AllocateVariable("OTHUSP", 0x4, true);
            Module.Memory.AllocateVariable("NXTCMD", FarPtr.Size); //Holds Pointer to the "next command"
            Module.Memory.SetPointer("NXTCMD", Module.Memory.GetVariablePointer("INPUT"));
            Module.Memory.AllocateVariable("NMODS", 0x2); //Number of Modules Installed
            Module.Memory.SetWord("NMODS", 0x1); //set this to 1 for now
            Module.Memory.AllocateVariable("MODULE", (FarPtr.Size * 0xFF), true); //Array of Module Info Pointers
            Module.Memory.AllocateVariable("**MODULE", FarPtr.Size);
            Module.Memory.SetPointer("**MODULE", Module.Memory.GetPointer("*MODULE"));
            Module.Memory.AllocateVariable("UACOFF", FarPtr.Size);
            Module.Memory.AllocateVariable("EXTOFF", FarPtr.Size);
            Module.Memory.AllocateVariable("VDAPTR", FarPtr.Size);
            Module.Memory.AllocateVariable("TJOINROU", FarPtr.Size);
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
            Module.Memory.AllocateVariable("SYSCYC", FarPtr.Size);
            Module.Memory.AllocateVariable("NUMBYTS", sizeof(uint));
            Module.Memory.AllocateVariable("NUMFILS", sizeof(uint));
            Module.Memory.AllocateVariable("NUMBYTP", sizeof(uint));
            Module.Memory.AllocateVariable("NUMDIRS", sizeof(uint));
            Module.Memory.AllocateVariable("NGLOBS", sizeof(ushort));
            Module.Memory.AllocateVariable("FTG", FtgStruct.Size);
            Module.Memory.AllocateVariable("FTGPTR", FarPtr.Size);
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
            Module.Memory.AllocateVariable("**LANGUAGES", FarPtr.Size);
            Module.Memory.SetPointer("**LANGUAGES", Module.Memory.GetVariablePointer("*LANGUAGES"));
            Module.Memory.AllocateVariable("ERRCOD", sizeof(ushort));
            Module.Memory.AllocateVariable("CURRENT-MCV", FarPtr.Size);
            Module.Memory.AllocateVariable("DIGALW", sizeof(ushort));
            Module.Memory.SetWord("DIGALW", 1);
            Module.Memory.AllocateVariable("_8087", sizeof(ushort));
            Module.Memory.SetWord("_8087", 3);
            Module.Memory.AllocateVariable("UIDXRF", UidxrefStruct.Size, true);
            Module.Memory.AllocateVariable("NTVARS", sizeof(ushort));
            Module.Memory.SetWord("NTVARS", 1); //We're setting the 1 defaulted below for SYSTEM
            Module.Memory.AllocateVariable("TXTVARS", TextvarStruct.Size * MaxTextVariables, true); //Up to 64 Text Variables per Module
            Module.Memory.SetArray("TXTVARS", new TextvarStruct("SYSTEM", new FarPtr(Segment, 9000)).Data); //Set 1st var as SYSTEM variable reference with special pointer
            Module.Memory.AllocateVariable("HDLCON", FarPtr.Size); //Handles the connection for the current User
            Module.Memory.AllocateVariable("RANDSEED", sizeof(uint)); //Seed value for Borland C++ Pseudo-Random Number Generator
            Module.Memory.SetDWord("RANDSEED", (uint)_random.Next(1, ushort.MaxValue)); //Seed RNG with a random value

            _tfsState = Module.Memory.AllocateVariable("TFSTATE", sizeof(ushort));
            _tfspst = Module.Memory.AllocateVariable("TFSPST", FarPtr.Size);
            Module.Memory.SetWord(_tfsState, (ushort)TFStateCodes.TFSDUN);
            _tfsbuf = Module.Memory.AllocateVariable("TFSBUF", TFSBUF_MAX_LENGTH + 1);

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

        public void SetRegisters(ICpuRegisters registers)
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

            var vdaChannelPointer = Module.Memory.GetOrAllocateVariablePointer($"VDA-{channelNumber}", VOLATILE_DATA_SIZE);

            Module.Memory.SetArray(vdaChannelPointer, ChannelDictionary[channelNumber].VDA);

            Module.Memory.SetArray(Module.Memory.GetVariablePointer("VDAPTR"), vdaChannelPointer.Data);
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("USRNUM"), channelNumber);

            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), (ushort)ChannelDictionary[channelNumber].GetStatus());

            var userBasePointer = Module.Memory.GetVariablePointer("USER");
            var currentUserPointer = new FarPtr(userBasePointer.Data);
            currentUserPointer.Offset += (ushort)(User.Size * channelNumber);

            //Update User Array and Update Pointer to point to this user
            Module.Memory.SetArray(currentUserPointer, ChannelDictionary[channelNumber].UsrPtr.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("*USRPTR"), currentUserPointer.Data);

            var userAccBasePointer = Module.Memory.GetVariablePointer("USRACC");
            var currentUserAccPointer = new FarPtr(userAccBasePointer.Data);
            currentUserAccPointer.Offset += (ushort)(UserAccount.Size * channelNumber);
            Module.Memory.SetArray(currentUserAccPointer, ChannelDictionary[channelNumber].UsrAcc.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("USAPTR"), currentUserAccPointer.Data);

            var userExtAccBasePointer = Module.Memory.GetVariablePointer("EXTUSR");
            var currentExtUserAccPointer = new FarPtr(userExtAccBasePointer.Data);
            currentExtUserAccPointer.Offset += (ushort)(ExtUser.Size * channelNumber);
            Module.Memory.SetArray(currentExtUserAccPointer, ChannelDictionary[channelNumber].ExtUsrAcc.Data);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("EXTPTR"), currentExtUserAccPointer.Data);

            //Reset NXTCMD
            Module.Memory.SetPointer("NXTCMD", Module.Memory.GetVariablePointer("INPUT"));

            //Reset PRFPTR
            Module.Memory.SetPointer("PRFPTR", Module.Memory.GetVariablePointer("PRFBUF"));
            Module.Memory.SetZero(Module.Memory.GetVariablePointer("PRFBUF"), 0x4000);

            //Set FSDSCB Pointer for Current User
            var channelFsdscb = Module.Memory.GetOrAllocateVariablePointer($"FSD-Fsdscb-{ChannelNumber}", FsdscbStruct.Size);

            Module.Memory.SetPointer("*FSDSCB", channelFsdscb);

            //Processing Channel Input
            if (ChannelDictionary[channelNumber].GetStatus() == EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)
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
            var inputLength = (ushort)(ChannelDictionary[ChannelNumber].InputCommand.Length - 1);
            Module.Memory.SetArray(inputPointer, inputFromChannel);
            Module.Memory.SetByte(inputPointer + inputFromChannel.Length, 0);
            inputLength = setINPLEN(inputPointer);

            ChannelDictionary[channelNumber].UsrPtr.Flags = 0;

            parsin();

            //Set Concex flag on 0 input
            //TODO -- Need to verify this is correct
            if (inputLength == 0)
                ChannelDictionary[channelNumber].UsrPtr.Flags |= (uint)EnumRuntimeFlags.Concex;
        }

        /// <summary>
        ///     Updates the Session with any information from memory before execution finishes
        /// </summary>
        /// <param name="channel"></param>
        public void UpdateSession(ushort channel)
        {
            //Don't process on RTKICK, etc. runs
            if (channel == ushort.MaxValue)
                return;

            //Set the Channel Status
            var resultStatus = (EnumUserStatus)Module.Memory.GetWord(Module.Memory.GetVariablePointer("STATUS"));

            //If STATUS was changed programatically, queue it up
            if (resultStatus != ChannelDictionary[ChannelNumber].GetStatus())
                ChannelDictionary[ChannelNumber].Status
                    .Enqueue(resultStatus);


            //Save off the USRPTR area to the Channel
            var userPointer = Module.Memory.GetVariablePointer("USER");
            ChannelDictionary[ChannelNumber].UsrPtr.Data = Module.Memory.GetArray(userPointer.Segment,
                (ushort)(userPointer.Offset + (User.Size * channel)), User.Size).ToArray();

            //We Verify it exists as Unit Tests won't call SetState() which would establish the VDA for that Channel
            if (Module.Memory.TryGetVariablePointer($"VDA-{ChannelNumber}", out var vdaPointer))
                ChannelDictionary[ChannelNumber].VDA = Module.Memory.GetArray(vdaPointer, VOLATILE_DATA_SIZE).ToArray();

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) {channel}->status == {ChannelDictionary[ChannelNumber].GetStatus()}");
            _logger.Debug($"({Module.ModuleIdentifier}) {channel}->state == {ChannelDictionary[ChannelNumber].UsrPtr.State}");
            _logger.Debug($"({Module.ModuleIdentifier}) {channel}->substt == {ChannelDictionary[ChannelNumber].UsrPtr.Substt}");
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

            //First block of Switch Values are for Exported Properties
            switch (ordinal)
            {
                case 768:
                    return _tfsState.Data;
                case 769:
                    return _tfsbuf.Data;
                case 770:
                    return _tfspst.Data;
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
                    BtrieveSetupGlobalPointer("GENBB", "BBSGEN.DAT", GENBB_BASE_SEGMENT);
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
                case 55:
                    BtrieveSetupGlobalPointer("ACCBB", "BBSUSR.DAT", ACCBB_BASE_SEGMENT);
                    return accbb;
                case 737:
                    return _8087;
                case 610:
                    return uidxrf;
                case 600:
                    return tjoinrou;
                case 861:
                    return ntvars;
                case 753:
                    return txtvars;
                case 735:
                    return hdlcon;
            }

            if (offsetsOnly)
            {
                return new FarPtr(Segment, ordinal).Data;
            }

            //Second Block of Switch Values are for Exported Methods
            switch (ordinal)
            {
                //Ignored Ordinals
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
                case 82: //BAUDAT -- Set color attribute based on baud, ignoring as ANSI is always on
                case 549: //SHOCHL -- Show legend for channel with any attribute, ignoring
                    break;
                case 73:
                    applyem();
                    break;
                case 535:
                    setcnf();
                    break;
                case 773:
                    tfsopn();
                    break;
                case 774:
                    tfsrdl();
                    break;
                case 775:
                    tfspfx();
                    break;
                case 892:
                    tfsabt();
                    break;
                case 599:
                    time();
                    break;
                case 432:
                    nkyrec();
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
                case 787: //pmlt is just multi-lingual prf, so we just call prf
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
                case 170:
                    dinsbtv();
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
                    obtbtvl();
                    break;
                case 444:
                    obtbtv();
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
                case 813:
                    samend();
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
                case 207:
                    f_flush();
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
                case 232:
                    fscanf();
                    break;
                case 355:
                    intdos();
                    break;
                case 786:
                case 178: // same signature/same functionality as prfmlt(), ansi is always true
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
                    movmem();
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
                    upvbtv();
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
                case 202:
                    farfree();
                    break;
                case 203:
                    farmalloc();
                    break;
                case 400:
                    galmalloc();
                    break;
                case 615:
                    ungetc();
                    break;
                case 230:
                    galfree();
                    break;
                case 227:
                    f_putc();
                    break;
                case 832:
                    alctile();
                    break;
                case 833:
                    ptrtile();
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
                case 121:
                    cncall();
                    break;
                case 419:
                    morcnc();
                    break;
                case 125:
                    cncint(CncIntegerReturnType.INT);
                    break;
                case 126: // cnclon
                    cncint(CncIntegerReturnType.LONG);
                    break;
                case 656:
                    f_ludiv();
                    break;
                case 996: //getbtvl()
                case 317: //getbtv() calls getbtvl() which shares the same signature with ignored lock type
                    getbtvl();
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
                case 866:
                    read();
                    break;
                case 867:
                    write();
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
                    strtol();
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
                case 746: // uhskey() same signature and purpose, uidkey() is for offline users
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
                case 127: // cncnum
                    cncint(CncIntegerReturnType.STRING);
                    break;
                case 339:
                    hexopt();
                    break;
                case 450: // onsysn() = same functionality as onsys(), adds invisible user parameter which we ignore
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
                case 162:
                    delbtv();
                    break;
                case 364:
                    isuidc();
                    break;
                case 960:
                    stp4cs();
                    break;
                case 211:
                    filelength();
                    break;
                case 811:
                    echsec();
                    break;
                case 137:
                    condex();
                    break;
                case 338:
                    hdluid();
                    break;
                case 70:
                    anpbtv();
                    break;
                case 913: // anpbtvl
                case 998: // anpbtvlk
                    anpbtvl();
                    break;
                case 64:
                    alcdup();
                    break;
                case 128:
                    cncsig();
                    break;
                case 905:
                    stzcat();
                    break;
                case 1012:
                    setmode();
                    break;
                case 327:
                    gettime();
                    break;
                case 9000:
                    txtvars_delegate();
                    break;
                case 1191:
                    bgnedt();
                    break;
                case 129:
                    cncuid();
                    break;
                case 561:
                    srand();
                    break;
                default:
                    _logger.Error($"Unknown Exported Function Ordinal in MAJORBBS: {ordinal}:{Ordinals.MAJORBBS[ordinal]}");
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in MAJORBBS: {ordinal}:{Ordinals.MAJORBBS[ordinal]}");
            }

            return null;
        }

        private void setcnf()
        {
            var optnam = GetParameterString(0, stripNull: true);
            var optval = GetParameterString(2, stripNull: true);

            // if updating a new value after an applyem, clear the dictionary and start anew
            if (applyemCalled)
            {
                setCnfDictionary.Clear();
                applyemCalled = false;
            }

            setCnfDictionary[optnam] = optval;
        }

        private void applyem()
        {
            var filenam = GetParameterString(0, stripNull: true);
            filenam = _fileFinder.FindFile(Module.ModulePath, filenam);

            MsgFile.UpdateValues(Path.Combine(Module.ModulePath, filenam), setCnfDictionary);

            applyemCalled = true;
        }

        /// <summary>
        ///     Acquire next/previous, verifying the next key matches the prior one.
        ///
        ///     <para/>Useful for querying across duplicate keys.
        ///
        ///     Signature: int anpbtvl(void *recptr, int chkcas, int anpopt, int optional_loktyp)
        ///         recptr - where to store the results
        ///         chkcas - check case in strcmp() operation for key comparison
        ///         anpopt - operation to perform
        ///         loktyp - lock type - unsupported in mbbsemu
        ///     Return: 1 if successful, 0 on failure
        /// </summary>
        private void anpbtvl()
        {
            var recordPointer = GetParameterPointer(0);
            var caseSensitive = GetParameterBool(2);
            var operation = (EnumBtrieveOperationCodes)GetParameter(3);

            Registers.AX = (ushort)(anpbtv(recordPointer, caseSensitive, operation) ? 1 : 0);
        }

        private bool anpbtv(FarPtr recordPointer, bool caseSensitive, EnumBtrieveOperationCodes operationCodes)
        {
            var bb = Module.Memory.GetPointer("BB");
            if (bb.IsNull())
                return false;

            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(bb, BtvFileStruct.Size));
            var lastKeyAsString = Encoding.ASCII.GetString(
                Module.Memory.GetString(btvStruct.key, stripNull: true));

            var ret = false;
            if (obtainBtv(recordPointer, FarPtr.Empty, 0xFFFF, operationCodes))
            {
                var nextKeyAsString = Encoding.ASCII.GetString(Module.Memory.GetString(btvStruct.key, stripNull: true));

                ret = string.Compare(lastKeyAsString, nextKeyAsString, ignoreCase: !caseSensitive) == 0;
            }

            return ret;
        }

        private void tfsopn()
        {
            tfsabt();

            var filespec = _fileFinder.FindFile(Module.ModulePath, GetParameterString(0, stripNull: true));
            var fullPath = Path.Combine(Module.ModulePath, filespec);

            try
            {
                _tfsStreamReader = new StreamReader(new FileStream(fullPath, FileMode.Open));
                Module.Memory.SetWord(_tfsState, (ushort)TFStateCodes.TFSBGN);
                Registers.AX = 1;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                Registers.AX = 0;
            }
        }

        private void tfsrdl()
        {
            // clear buffer first
            Module.Memory.SetByte(_tfsbuf, 0);

            var tfstate = (TFStateCodes)Module.Memory.GetWord(_tfsState);
            switch (tfstate)
            {
                case TFStateCodes.TFSBGN:
                    tfstate = TFStateCodes.TFSBOF;
                    break;
                case TFStateCodes.TFSBOF:
                    tfstate = TFStateCodes.TFSLIN;
                    goto case TFStateCodes.TFSLIN;
                case TFStateCodes.TFSLIN:
                    var line = _tfsStreamReader.ReadLine();
                    if (line != null)
                    {
                        line = line.TrimEnd() + "\0";
                        if (line.Length > (TFSBUF_MAX_LENGTH + 1))
                            line = line.Substring(0, TFSBUF_MAX_LENGTH) + "\0";
                        Module.Memory.SetArray(_tfsbuf, Encoding.ASCII.GetBytes(line));
                    }
                    else
                    {
                        tfstate = TFStateCodes.TFSEOF;
                    }
                    break;
                case TFStateCodes.TFSEOF:
                    tfsabt(); // close the stream
                    tfstate = TFStateCodes.TFSDUN;
                    break;
            }

            Module.Memory.SetWord(_tfsState, (ushort)tfstate);
            Registers.AX = (ushort)tfstate;
        }

        private void tfspfx()
        {
            // sets tfspst from tfsbuf
            var prefix = GetParameterString(0, stripNull: true);
            var line = Encoding.ASCII.GetString(Module.Memory.GetString(_tfsbuf, stripNull: true));

            if (!line.StartsWith(prefix))
            {
                Registers.AX = 0; // FALSE
                Module.Memory.SetArray(_tfspst, FarPtr.Empty.Data); // tfspst = NULL
                return;
            }

            var rest = line.Substring(prefix.Length).Trim();
            var newTfspst = _tfsbuf + (line.Length - rest.Length);

            Module.Memory.SetArray(_tfspst, newTfspst.Data);
            Registers.AX = rest.Length > 0 ? (ushort)1 : (ushort)0;
        }

        private void tfsabt()
        {
            _tfsStreamReader?.Close();
            _tfsStreamReader = null;

            Module.Memory.SetWord(_tfsState, (ushort)TFStateCodes.TFSDUN);
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
            var passedSeconds = (int)(_clock.Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            Registers.DX = (ushort)(passedSeconds >> 16);
            Registers.AX = (ushort)(passedSeconds & 0xFFFF);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Passed seconds: {passedSeconds} (AX:{Registers.AX:X4}, DX:{Registers.DX:X4})");
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
            galmalloc();

            if (Registers.GetPointer().IsNull())
                throw new OutOfMemoryException("Module failed to allocate memory");

            // zero fill
            Module.Memory.FillArray(Registers.GetPointer(), GetParameter(0), 0);
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
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("GMDNAM", (ushort)moduleName.Length);

            //Set Memory
            Module.Memory.SetArray(variablePointer, Encoding.Default.GetBytes(moduleName));
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved Module Name \"{moduleName}\" and saved it at host memory offset {variablePointer}");
#endif

            Registers.SetPointer(variablePointer);
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

            if (inputBuffer[0] == 0x0 || sourcePointer == FarPtr.Empty)
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
                //_logger.Debug($"({Module.ModuleIdentifier}) Copied {inputBuffer.Length} bytes from {sourcePointer} to {destinationPointer} -> {Encoding.ASCII.GetString(inputBuffer)}");
#endif
            }

            Registers.SetPointer(destinationPointer);
        }

        /// <summary>
        ///     Copies a string with a fixed length - differs from strncpy in that it guarantees null
        ///     termination.
        ///
        ///     Signature: char *stzcpy(char *dest, char *source, int nbytes);
        ///     Return: dest in DX:AX
        /// </summary>
        private void stzcpy()
        {
            var destinationPointer = GetParameterPointer(0);
            var limit = GetParameter(4);

            strncpy();

            if (limit > 0)
                Module.Memory.SetByte(destinationPointer + limit - 1, 0);
        }

        /// <summary>
        ///     Concatenates Two Strings with a fixed length
        ///
        ///     Signature: char *stzcat(char *dst,char *src,int num);
        /// </summary>
        private void stzcat()
        {
            var destinationPointer = GetParameterPointer(0);
            var sourcePointer = GetParameterPointer(2);
            var destinationBufferLength = GetParameter(4);

            var stringLength = Module.Memory.GetString(destinationPointer, true).Length;

            //Truncate Destination if LIMIT is less than DST
            if (stringLength > destinationBufferLength)
            {
                if (destinationBufferLength > 0)
                    destinationBufferLength--; //subtract one for the null we're inserting

                Module.Memory.SetByte(destinationPointer.Segment, (ushort)(destinationPointer.Offset + destinationBufferLength), 0);
                Registers.SetPointer(destinationPointer);
                return;
            }

            var newDestinationPointer = destinationPointer + stringLength;
            destinationBufferLength -= (ushort)stringLength;

            SetParameterPointer(0, newDestinationPointer);
            SetParameter(4, destinationBufferLength);

            stzcpy();
            Registers.SetPointer(destinationPointer);
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

            var moduleStruct = new ModuleStruct(Module.Memory.GetArray(destinationPointer, ModuleStruct.Size));

            //We create a copy in Variable Memory in case the MODULE pointer that was passed in was on the stack
            var localModuleStructPointer = Module.Memory.AllocateVariable($"MODULE-LOCAL-{Module.ModuleDlls[ModuleDll].StateCode}", ModuleStruct.Size);
            Module.Memory.SetArray($"MODULE-LOCAL-{Module.ModuleDlls[ModuleDll].StateCode}", moduleStruct.Data);

            //Set Module Values from Struct
            Module.ModuleDescription = Encoding.ASCII.GetString(moduleStruct.descrp).TrimEnd('\0');
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("lonrou", moduleStruct.lonrou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("sttrou", moduleStruct.sttrou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("stsrou", moduleStruct.stsrou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("injrou", moduleStruct.injrou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("lofrou", moduleStruct.lofrou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("huprou", moduleStruct.huprou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("mcurou", moduleStruct.mcurou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("dlarou", moduleStruct.dlarou);
            Module.ModuleDlls[ModuleDll].EntryPoints.Add("finrou", moduleStruct.finrou);

            //usrptr->state is the Module Number in use, as assigned by the host process
            Registers.AX = (ushort)Module.ModuleDlls[ModuleDll].StateCode;

            var moduleStructOffset = Module.ModuleDlls[ModuleDll].StateCode * 4;

            Module.Memory.SetPointer(Module.Memory.GetVariablePointer("MODULE") + moduleStructOffset, localModuleStructPointer);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) MODULE pointer ({Module.Memory.GetVariablePointer("MODULE") + moduleStructOffset}) set to {localModuleStructPointer}");
            _logger.Debug($"({Module.ModuleIdentifier}) Module Description set to {Module.ModuleDescription}");
#endif
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
            var mcvFileName = GetParameterString(0, true);

            //If it's our first time opening it, generate the MCV
            if (!McvPointerDictionary.Any(x => x.Value.FileName == mcvFileName))
            {
                // triggers MSG -> MCV compilation
                _ = new MsgFile(_fileFinder, Module.ModulePath,
                    mcvFileName.Replace(".mcv", string.Empty, StringComparison.InvariantCultureIgnoreCase));
            }

            var offset = McvPointerDictionary.Allocate(new McvFile(_fileFinder, mcvFileName, Module.ModulePath));

            if (_currentMcvFile != null)
                _previousMcvFile.Push(_currentMcvFile);

            //Open just sets whatever the currently active MCV file is -- overwriting whatever was there
            _currentMcvFile = new FarPtr(ushort.MaxValue, (ushort)offset);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Opened MCV file: {mcvFileName}, assigned to {ushort.MaxValue:X4}:{offset:X4}");
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
            if (outputValue < floor || (ceiling > 0 && outputValue > ceiling))
                throw new ArgumentOutOfRangeException($"{msgnum} value {outputValue} is outside specified bounds");

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} value: {outputValue}");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} value: {outputValue}");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} value: {outputValue}");
#endif

            Registers.DX = (ushort)(outputValue >> 16);
            Registers.AX = (ushort)(outputValue & 0xFFFF);
        }

        /// <summary>
        ///     Gets a string from an MCV file, returned as freshly allocated from malloc.
        ///     Return value should be free'd by the module.
        ///
        ///     Signature: char *string=stgopt(int msgnum)
        ///     Return: AX = Offset in Segment
        ///             DX = Host Segment
        /// </summary>
        private void stgopt()
        {
            var msgnum = GetParameter(0);

            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            var variablePointer = Module.Memory.Malloc((ushort)outputValue.Length);

            //Set Value in Memory
            Module.Memory.SetArray(variablePointer, outputValue);

#if DEBUG
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Retrieved option {msgnum} string value: {outputValue.Length} bytes saved to {variablePointer}");
#endif

            Registers.SetPointer(variablePointer);
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
            _logger.Debug($"Called, redirecting to stgopt()");
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

            //Pre-allocate space for the maximum number of characters for a ulong
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("L2AS", 0xFF);

            Module.Memory.SetArray(variablePointer, Encoding.Default.GetBytes(outputValue));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Received value: {outputValue}, string saved to {variablePointer}");
#endif

            Registers.SetPointer(variablePointer);
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
            var stringToLong = GetParameterString(0, true).Trim();

            var result = GetLeadingNumberFromString(stringToLong, 10, -1);

            Registers.DX = (ushort)(result.Value >> 16);
            Registers.AX = (ushort)(result.Value & 0xFFFF);

            if (!result.Valid)
            {
#if DEBUG
                _logger.Warn($"({Module.ModuleIdentifier}) Unable to cast {stringToLong} ({GetParameterPointer(0)}) to long");
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
            var packedDate = (_clock.Now.Month << 5) + _clock.Now.Day + ((_clock.Now.Year - 1980) << 9);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Returned packed date: {packedDate}");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Copied {sourceString.Length} bytes from {sourcePointer} to {destinationPointer}");
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

            Registers.AX = (Registers.AX == 0 ? (ushort)1 : (ushort)0);
        }

        /// <summary>
        ///     Property with the User Number (Channel) of the user currently being serviced
        ///
        ///     Signature: int usrnum
        ///     Returns: int == User Number (Channel)
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usrnum => base.Module.Memory.GetVariablePointer("USRNUM").Data;

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

            var variablePointer = Module.Memory.GetOrAllocateVariablePointer($"USRACC-{userNumber}", UserAccount.Size);

            //If user isn't online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var uacoffPointer = Module.Memory.GetVariablePointer("UACOFF");
            Module.Memory.SetArray(uacoffPointer, variablePointer.Data);

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.UsrAcc.Data);

            Registers.SetPointer(variablePointer);
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

            var output = Module.Memory.GetString(sourcePointer, stripNull: false);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 2);

            var pointerPosition = Module.Memory.GetPointer("PRFPTR");
            Module.Memory.SetArray(pointerPosition, formattedMessage);

            //Update prfptr value
            pointerPosition.Offset += (ushort)(formattedMessage.Length - 1);
            Module.Memory.SetPointer("PRFPTR", pointerPosition);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Added {formattedMessage.Length} bytes to the buffer");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Reset Output Buffer");
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

            if (ChannelDictionary.ContainsKey(userChannel))
                ChannelDictionary[userChannel].SendToClient(outputBufferProcessed.ToArray());

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Sent {outputBuffer.Length} bytes to Channel {userChannel}");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Deducted {creditsToDeduct} from the current users account (Ignored)");
#endif

            Registers.AX = 1;
        }

        /// <summary>
        ///     Points to that channels 'user' struct
        ///
        ///     Signature: struct user *usrptr;
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> usrptr => Module.Memory.GetVariablePointer("*USRPTR").Data;

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
                    $"({Module.ModuleIdentifier}) prfmsg() unable to locate message number {messageNumber} in current MCV file {McvPointerDictionary[_currentMcvFile.Offset].FileName}");
                return;
            }

            var formattedMessage = FormatPrintf(outputMessage, 1);

            var currentPrfPositionPointer = Module.Memory.GetPointer(Module.Memory.GetVariablePointer("PRFPTR"));

            Module.Memory.SetArray(currentPrfPositionPointer, formattedMessage);
            currentPrfPositionPointer.Offset += (ushort)formattedMessage.Length;
            Module.Memory.SetByte(currentPrfPositionPointer, 0x0); //Null terminate it

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Added {formattedMessage.Length} bytes to the buffer from message number {messageNumber}");
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
            var stringSummary = GetParameterStringSpan(0);
            var stringDetail = FormatPrintf(GetParameterStringSpan(2), 4);

            //Get Logger from LogFactory
            var logger = new LogFactory().GetLogger<AuditLogger>();
            logger.Log(Encoding.ASCII.GetString(stringSummary), Encoding.ASCII.GetString(stringDetail));
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
                return pointer.Data;
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
            var string1 = GetParameterStringSpan(0);
            var string2 = GetParameterStringSpan(2);
            var real = GetParameter(4);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Added {Encoding.Default.GetString(string2)} credits to user account {Encoding.Default.GetString(string1)} (unlimited -- this function is ignored)");
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
            Registers.SetPointer(string1Pointer);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Convterted integer {integerValue} to {output} (base {baseValue}) and saved it to {string1Pointer}");
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
            var accountLock = GetParameterString(0, true);

            if (string.IsNullOrEmpty(accountLock))
            {
                Registers.AX = 1;
                return;
            }

            IEnumerable<string> keys;

            //If the user isnt registered on the system, most likely RLOGIN -- so apply the default keys
            if (_accountRepository.GetAccountByUsername(ChannelDictionary[ChannelNumber].Username) == null)
            {
                keys = _configuration.DefaultKeys;
            }
            else
            {
                var accountKeys = _accountKeyRepository.GetAccountKeysByUsername(ChannelDictionary[ChannelNumber].Username);
                keys = accountKeys.Select(x => x.accountKey);
            }

            Registers.AX = keys.Any(k =>
                string.Equals(accountLock, k, StringComparison.InvariantCultureIgnoreCase))
                ? (ushort)1
                : (ushort)0;

            //Log and let user know if they have no access
            var lockName = Encoding.ASCII.GetString(Module.Memory.GetString(GetParameterPointer(0), true));
            if (Registers.AX == 0)
            {
#if DEBUG
                _logger.Debug($"User {ChannelDictionary[ChannelNumber].Username} was denied access -- {lockName} key is required");
#endif
            }
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Returning {Registers.AX} for Haskey({lockName})");
#endif
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
            var msgnum = GetParameter(0);

            var accountLock = Encoding.ASCII.GetString(McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum)).TrimEnd('\0');

            if (string.IsNullOrEmpty(accountLock))
            {
                Registers.AX = 1;
                return;
            }

            IEnumerable<string> keys;

            //If the user isnt registered on the system, most likely RLOGIN -- so apply the default keys
            if (_accountRepository.GetAccountByUsername(ChannelDictionary[ChannelNumber].Username) == null)
            {
                keys = _configuration.DefaultKeys;
            }
            else
            {
                var accountKeys = _accountKeyRepository.GetAccountKeysByUsername(ChannelDictionary[ChannelNumber].Username);
                keys = accountKeys.Select(x => x.accountKey);
            }

            Registers.AX = keys.Any(k =>
                string.Equals(accountLock, k, StringComparison.InvariantCultureIgnoreCase))
                ? (ushort)1
                : (ushort)0;
#if DEBUG
            var lockName = Encoding.ASCII.GetString(Module.Memory.GetString(GetParameterPointer(0), true));
            _logger.Debug($"({Module.ModuleIdentifier}) Returning {Registers.AX} for Hasmkey({lockName})");
#endif
        }

        /// <summary>
        ///     Returns a pseudo-random integral number in the range between 0 and RAND_MAX.
        ///
        ///     Signature: int rand()
        ///     Returns: AX = 16-bit Random Number
        /// </summary>
        /// <returns></returns>
        private void rand()
        {
            uint multiplier = 0x15A4E35;
            var seed = Module.Memory.GetDWord("RANDSEED");

            var newSeed = (seed * multiplier) + 1;

            Module.Memory.SetDWord("RANDSEED", newSeed);

            Registers.AX = (ushort)((newSeed >> 16) & 0x7FFF);
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
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("NCDATE", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
#endif
            Registers.SetPointer(variablePointer);
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
            var outputDate = $"{day:D2}/{month:D2}/{year % 100:D2}\0";

            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("NCEDAT", (ushort)outputDate.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(outputDate));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Received value: {packedDate}, decoded string {outputDate} saved to {variablePointer.Segment:X4}:{variablePointer.Offset:X4}");
#endif
            Registers.SetPointer(variablePointer);
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
            _logger.Debug($"Closing MCV File: {filePointer}");
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
            var routinePointer = GetParameterPointer(0);

            var routine = new RealTimeRoutine(routinePointer);
            Module.RtihdlrRoutines.Allocate(routine);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Registered routine {routinePointer}");
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

            var routine = new RealTimeRoutine(routinePointer, delaySeconds);
            Module.RtkickRoutines.Allocate(routine);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Registered routine {routinePointer} to execute every {delaySeconds} seconds");
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
                _previousMcvFile.Push(new FarPtr(_currentMcvFile.Data));
#if DEBUG
                _logger.Debug($"({Module.ModuleIdentifier}) Enqueue Previous MCV File: {McvPointerDictionary[_currentMcvFile.Offset].FileName} (Pointer: {_currentMcvFile})");
#endif
            }

            _currentMcvFile = mcvFilePointer;

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Set Current MCV File: {McvPointerDictionary[_currentMcvFile.Offset].FileName} (Pointer: {mcvFilePointer})");
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
                _logger.Debug($"Queue Empty, Ignoring");
#endif
                return;
            }

            _currentMcvFile = _previousMcvFile.Pop();
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Reset Current MCV to {McvPointerDictionary[_currentMcvFile.Offset].FileName} ({_currentMcvFile}) (Queue Depth: {_previousMcvFile.Count})");
#endif
        }

        /// <summary>
        ///     Opens a Btrieve file for I/O
        ///
        ///     Signature: BTVFILE *bbptr=opnbtv(char *filename, int reclen)
        ///     Return: AX = Offset to File Pointer
        ///             DX = Host Btrieve Segment
        /// </summary>
        /// <returns></returns>
        private void opnbtv()
        {
            var btrievefileName = GetParameterString(0, true);
            var maxRecordLength = GetParameter(2);

            var btrieveFile = new BtrieveFileProcessor(_fileFinder, Module.ModulePath, btrievefileName, _configuration.BtrieveCacheSize);

            var btvFileStructPointer = AllocateBB(btrieveFile, maxRecordLength, btrievefileName);

            Registers.SetPointer(btvFileStructPointer);
        }

        /// <summary>
        ///     Allocates a new BtvFileStruct and associated it with btrieveFile.
        /// </summary>
        /// <returns>A pointer to the allocated BtvFileStruct</returns>
        [VisibleForTesting]
        public FarPtr AllocateBB(BtrieveFileProcessor btrieveFile, ushort maxRecordLength, string fileName)
        {
            //Setup Pointers
            var btvFileStructPointer = Module.Memory.AllocateVariable($"{fileName}-STRUCT", BtvFileStruct.Size);
            var btvFileNamePointer =
                Module.Memory.AllocateVariable($"{fileName}-NAME", (ushort)(fileName.Length + 1));
            var btvDataPointer = Module.Memory.AllocateVariable($"{fileName}-RECORD", maxRecordLength);
            var btvKeyPointer = Module.Memory.AllocateVariable($"{fileName}-KEY", maxRecordLength);

            var newBtvStruct = new BtvFileStruct
            { filenam = btvFileNamePointer, reclen = maxRecordLength, data = btvDataPointer, key = btvKeyPointer };
            foreach (var key in btrieveFile.Keys.Values)
                newBtvStruct.SetKeyLength(key.Number, (ushort)key.Length);
            BtrieveSaveProcessor(btvFileStructPointer, btrieveFile);
            Module.Memory.SetArray(btvFileStructPointer, newBtvStruct.Data);
            Module.Memory.SetArray(btvFileNamePointer, Encoding.ASCII.GetBytes(fileName));
            Module.Memory.SetPointer("BB", btvFileStructPointer);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Opened file {fileName} and allocated it to {btvFileStructPointer}");
#endif
            return btvFileStructPointer;
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

            if (currentBtrieveFile != FarPtr.Empty)
                _previousBtrieveFile.Push(currentBtrieveFile);

            Module.Memory.SetPointer("BB", btrieveFilePointer);

#if DEBUG
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(btrieveFilePointer, BtvFileStruct.Size));
            var btvFileName = Encoding.ASCII.GetString(Module.Memory.GetString(btvStruct.filenam, stripNull: true));
            _logger.Debug($"({Module.ModuleIdentifier}) Setting current Btrieve file to {btvFileName} ({btrieveFilePointer})");
#endif
        }

        private bool UpdateBB(BtrieveFileProcessor currentBtrieveFile, FarPtr destinationRecordBuffer, EnumBtrieveOperationCodes operationCode, short keyNumber = -1) =>
            UpdateBB(currentBtrieveFile, destinationRecordBuffer, currentBtrieveFile.Position, operationCode.AcquiresData(), keyNumber);

        private bool UpdateBB(BtrieveFileProcessor currentBtrieveFile, FarPtr destinationRecordBuffer, uint position, bool acquiresData, short keyNumber = -1)
        {
            var record = currentBtrieveFile.GetRecord(position);
            if (record == null)
                return false;

            var bbPointer = Module.Memory.GetPointer("BB");
            var btvStruct = new BtvFileStruct(Module.Memory.GetArray(bbPointer, BtvFileStruct.Size));
            if (keyNumber >= 0)
            {
                btvStruct.lastkn = (ushort)keyNumber;
                Module.Memory.SetArray(bbPointer, btvStruct.Data);
            }
            else if (currentBtrieveFile.Keys.Count > 0)
            {
                keyNumber = (short)btvStruct.lastkn;
            }

            if (acquiresData)
            {
                if (destinationRecordBuffer.IsNull())
                    destinationRecordBuffer = btvStruct.data;

                Module.Memory.SetArray(destinationRecordBuffer, record.Data);
            }

            // TODO SET LOGICAL POSITION FOR NEXT/PREVIOUS

            if (keyNumber >= 0 && currentBtrieveFile.Keys.Count > 0)
                Module.Memory.SetArray(btvStruct.key, currentBtrieveFile.Keys[(ushort)keyNumber].ExtractKeyDataFromRecord(record.Data));

            return true;
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
            var stpopt = (EnumBtrieveOperationCodes)GetParameter(2);
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var result = currentBtrieveFile.PerformOperation(-1, ReadOnlySpan<byte>.Empty, stpopt);
            if (result)
                UpdateBB(currentBtrieveFile, btrieveRecordPointer, stpopt);

            Registers.AX = result ? (ushort)1 : (ushort)0;
        }

        /// <summary>
        ///     Restores the last Btrieve data block for use
        ///
        ///     Signature: void rstbtv()
        /// </summary>
        /// <returns></returns>
        private void rstbtv()
        {
            if (_previousBtrieveFile.Count == 0)
            {
                _logger.Warn($"({Module.ModuleIdentifier}) Previous Btrieve file == null, ignoring");
                return;
            }

            Module.Memory.SetPointer("BB", _previousBtrieveFile.Pop());
        }

        /// <summary>
        ///     Update the Btrieve current record
        ///
        ///     Signature: int dupdbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        private void dupdbtv()
        {
            Registers.AX = updateBtv() ? (ushort)1 : (ushort)0;
        }

        private bool updateBtv()
        {
            var btrieveRecordPointerPointer = GetParameterPointer(0);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, (ushort)currentBtrieveFile.RecordLength);

            return currentBtrieveFile.Update(dataToWrite.ToArray()) == BtrieveError.Success;
        }

        /// <summary>
        ///     Insert new fixed-length Btrieve record - harshly
        ///
        ///     Signature: void insbtv(char *recptr)
        /// </summary>
        private void insbtv()
        {
            if (!insertBtv(LogLevel.Critical))
                throw new SystemException("Failed to insert database record");
        }

        /// <summary>
        ///     Insert new fixed-length Btrieve record
        ///
        ///     Signature: int dinsbtv(char *recptr)
        /// </summary>
        /// <returns></returns>
        private void dinsbtv()
        {
            Registers.AX = insertBtv(LogLevel.Debug) ? (ushort)1 : (ushort)0;
        }

        private bool insertBtv(LogLevel logLevel)
        {
            var btrieveRecordPointer = GetParameterPointer(0);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointer, (ushort)currentBtrieveFile.RecordLength);

            return currentBtrieveFile.Insert(dataToWrite.ToArray(), logLevel) != 0;
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

            currentBtrieveFile.Insert(record.ToArray(), LogLevel.Error);
        }

        /// <summary>
        ///     Raw status from btusts, where appropriate
        ///
        ///     Signature: int status
        ///     Returns: Segment holding the Users Status
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> status => Module.Memory.GetVariablePointer("STATUS").Data;

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
            var output = GetParameterStringSpan(0);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(output, 2);

            if (formattedMessage.Length > 1024)
                throw new OutOfMemoryException(
                    $"SPR write is > 1k ({formattedMessage.Length}) and would overflow pre-allocated buffer");

            _sprIndex++;
            _sprIndex &= 0x3;
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer($"SPR-{_sprIndex}", 1024);

            Module.Memory.SetArray(variablePointer, formattedMessage);

#if DEBUG
            //_logger.Debug($"({Module.ModuleIdentifier}) Added {formattedMessage.Length} bytes to the buffer: {Encoding.ASCII.GetString(formattedMessage)}");
#endif

            Registers.SetPointer(variablePointer);
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
            var targetPointer = GetParameterPointer(0);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(GetParameterStringSpan(2), 4, true);

            Module.Memory.SetArray(targetPointer, formattedMessage);

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
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("SCNMDF", 0xFF);

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

            Registers.SetPointer(variablePointer);
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
            var packedTime = (_clock.Now.Hour << 11) + (_clock.Now.Minute << 5) + (_clock.Now.Second >> 1);

#if DEBUG
            _logger.Debug($"Returned packed time: {packedTime}");
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
            var destinationPointer = GetParameterPointer(0);
            var numberOfBytesToWrite = GetParameter(2);
            var byteToWrite = GetParameter(3);

            Module.Memory.FillArray(destinationPointer, numberOfBytesToWrite, (byte)byteToWrite);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Set {numberOfBytesToWrite} bytes at {destinationPointer} with {(byte)byteToWrite:X2}");
#endif
        }

        /// <summary>
        ///     Output buffer of prf() and prfmsg()
        ///
        ///     Because it's a pointer, the segment only contains Int16:Int16 pointer to the actual prfbuf segment
        ///
        ///     Signature: char *prfbuf
        /// </summary>
        private ReadOnlySpan<byte> prfbuf => Module.Memory.GetVariablePointer("*PRFBUF").Data;

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

            Registers.SetPointer(destinationPointer);

            for (var i = 0; i < numberOfBytesToCopy; i++, destinationPointer++)
            {
                if (source[i] == 0x0)
                {
                    //Write remaining nulls
                    for (var j = i; j < numberOfBytesToCopy; j++, destinationPointer++)
                        Module.Memory.SetByte(destinationPointer, 0x0);

                    break;
                }

                Module.Memory.SetByte(destinationPointer, (byte)source[i]);
            }
        }

        /// <summary>
        ///     Registers a new Text Variable that will be used by the module
        /// </summary>
        private void register_textvar()
        {
            var name = GetParameterString(0, true);
            var functionPointer = GetParameterPointer(2);

            var newTextVar = new TextvarStruct(name, functionPointer);
            var newTextVarOffset = Module.Memory.GetWord("NTVARS") * TextvarStruct.Size;

            //Save
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("TXTVARS") + newTextVarOffset, newTextVar.Data);

            //Increment
            Module.Memory.SetWord("NTVARS", (ushort)(Module.Memory.GetWord("NTVARS") + 1));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Registered Textvar \"{name}\" to {functionPointer} ({Module.Memory.GetVariablePointer("TXTVARS") + newTextVarOffset})");
#endif
        }

        /// <summary>
        ///     Does a GetEqual based on the Key -- the record corresponding to the key is returned
        ///
        ///     Signature: int obtbtv (void *recptr, void *key, int keynum, int obtopt)
        ///     Returns: AX == 0 record not found, 1 record found
        /// </summary>
        /// <returns>true if record found</returns>
        private void obtbtv()
        {
            Registers.AX = obtainBtv() ? (ushort)1 : (ushort)0;
        }

        /// <summary>
        ///     Does a GetEqual based on the Key -- the record corresponding to the key is returned
        ///
        ///     Signature: int obtbtvl (void *recptr, void *key, int keynum, int obtopt, int loktyp)
        ///     Returns: AX == 0 record not found, 1 record found
        /// </summary>
        /// <returns>true if record found</returns>
        private void obtbtvl()
        {
            Registers.AX = obtainBtv() ? (ushort)1 : (ushort)0;
        }

        private bool obtainBtv()
        {
            var recordPointer = GetParameterPointer(0);
            var keyPointer = GetParameterPointer(2);
            var keyNumber = GetParameter(4);
            var obtopt = (EnumBtrieveOperationCodes)GetParameter(5);

            return obtainBtv(recordPointer, keyPointer, keyNumber, obtopt);
        }

        private bool obtainBtv(FarPtr recordPointer, FarPtr keyPointer, int keyNumber, EnumBtrieveOperationCodes obtopt)
        {
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var keyValue = !keyPointer.IsNull() ? Module.Memory.GetArray(keyPointer, currentBtrieveFile.GetKeyLength((ushort)keyNumber)) : null;
            var result = currentBtrieveFile.PerformOperation(keyNumber, keyValue, obtopt);
            if (result)
                UpdateBB(currentBtrieveFile, recordPointer, obtopt, (short)keyNumber);

            return result;
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
            _logger.Debug($"({Module.ModuleIdentifier}) Volatile Memory Size requested of {size} bytes ({VOLATILE_DATA_SIZE} bytes currently allocated per channel)");
#endif
        }

        private ReadOnlySpan<byte> nterms => Module.Memory.GetVariablePointer("NTERMS").Data;

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

            var volatileMemoryAddress = Module.Memory.GetOrAllocateVariablePointer($"VDA-{channel}", VOLATILE_DATA_SIZE);

#if DEBUG
            _logger.Debug($"Returned VDAOFF {volatileMemoryAddress} for Channel {channel}");
#endif

            Registers.SetPointer(volatileMemoryAddress);
        }

        /// <summary>
        ///     Contains information about the current user account (class, state, baud, etc.)
        ///
        ///     Signature: struct user;
        /// </summary>
        /// <returns></returns>
        private ReadOnlySpan<byte> user => Module.Memory.GetVariablePointer("*USER").Data;


        /// <summary>
        ///     Points to the Volatile Data Area for the current channel
        ///
        ///     Signature: char *vdaptr
        /// </summary>
        private ReadOnlySpan<byte> vdaptr
        {
            get
            {
                var variablePointer = Module.Memory.GetOrAllocateVariablePointer("VDAPTR", 0x4);

                return variablePointer.Data;
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
            clrprf();
            rstrin();
        }

        /// <summary>
        ///     Number of Words in the users input line
        ///
        ///     Signature: int margc
        /// </summary>
        private ReadOnlySpan<byte> margc => Module.Memory.GetVariablePointer("MARGC").Data;

        /// <summary>
        ///     Returns the pointer to the next parsed input command from the user
        ///     If this is the first time it's called, it returns the first command
        /// </summary>
        private ReadOnlySpan<byte> nxtcmd => Module.Memory.GetVariablePointer("NXTCMD").Data;

        /// <summary>
        ///     Case-ignoring substring match
        ///
        ///     Signature: int match=sameto(char *shorts, char *longs)
        ///     Returns: AX == 1, match
        /// </summary>
        private void sameto()
        {
            var string1Buffer = GetParameterString(0, true);
            var string2Buffer = GetParameterString(2, true);

            Registers.AX = (ushort)(string2Buffer.Contains(string1Buffer, StringComparison.CurrentCultureIgnoreCase) ? 1 : 0);
        }

        /// <summary>
        ///     Compares ending of strings, checking if the first string ends with the second string (ignoring case)
        ///
        ///     Signature: int samend(char *longs, char *ends)
        ///     Returns: AX == 1, match
        /// </summary>
        private void samend()
        {
            var string1Buffer = GetParameterString(0, true);
            var string2Buffer = GetParameterString(2, true);

            Registers.AX = (ushort)(string1Buffer.EndsWith(string2Buffer, StringComparison.CurrentCultureIgnoreCase) ? 1 : 0);
        }

        /// <summary>
        ///     Expect a Character from the user (character from the current command)
        ///
        ///     cncchr() is executed after begincnc(), which runs rstrin() replacing the null separators
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
                _logger.Debug($"End of Input");
#endif

                Registers.AX = 0;
                return;
            }

            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            Registers.AX = char.ToUpper((char)inputString[0]);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Returned char: {(char)Registers.AX}");
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

            Module.Memory.SetPointer("NXTCMD", new FarPtr(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
        }

        /// <summary>
        ///     Pointer to the current position in prfbuf
        ///
        ///     Signature: char *prfptr;
        /// </summary>
        private ReadOnlySpan<byte> prfptr => Module.Memory.GetVariablePointer("PRFPTR").Data;

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

            FarPtr fileStructPointer = null;

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Opening File: {fullPath}");
#endif

            var modeInputBuffer = Module.Memory.GetString(modePointer, true);
            var fileAccessMode = FileStruct.CreateFlagsEnum(modeInputBuffer);
            FileStream fileStream = null;

            if (!File.Exists(fullPath))
            {
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Read))
                {
                    _logger.Warn($"({Module.ModuleIdentifier}) Unable to find file {fullPath}");
                    Registers.AX = 0;
                    Registers.DX = 0;
                    return;
                }

                //Create a new file for W or A
                _logger.Info($"({Module.ModuleIdentifier}) Creating new file {fileName}");

                fileStream = File.Create(fullPath);
            }
            else
            {
                //Overwrite existing file for W
                if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Write))
                {
#if DEBUG
                    _logger.Debug($"Overwriting file {fileName}");
#endif
                    fileStream = File.Create(fullPath);
                }
            }

            //Allocate Memory for FILE struct
            fileStructPointer ??= Module.Memory.GetOrAllocateVariablePointer($"FILE_{fullPath}-{FilePointerDictionary.Count}", FileStruct.Size);

            //Write New Blank Pointer
            var fileStruct = new FileStruct();
            Module.Memory.SetArray(fileStructPointer, fileStruct.Data);

            //Setup the File Stream
            fileStream ??= File.Open(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (fileAccessMode.HasFlag(FileStruct.EnumFileAccessFlags.Append))
                fileStream.Seek(fileStream.Length, SeekOrigin.Begin);

            var fileStreamPointer = FilePointerDictionary.Allocate(fileStream);

            //Set Struct Values
            fileStruct.SetFlags(fileAccessMode);
            fileStruct.curp = new FarPtr(ushort.MaxValue, (ushort)fileStreamPointer);
            fileStruct.fd = (byte)fileStreamPointer;
            Module.Memory.SetArray(fileStructPointer, fileStruct.Data);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) {fullPath} FILE struct written to {fileStructPointer}");
#endif
            Registers.SetPointer(fileStructPointer);
        }

        /// <summary>
        ///     Closes an Open File Pointer
        ///
        ///     Signature: int fclose(FILE* stream ). Returns 0 on success, EOF (-1) on failure
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
                _logger.Warn($"({Module.ModuleIdentifier}) Called FCLOSE on null File Stream Pointer (0000:0000), usually means it tried to open a file that doesn't exist");
                Registers.AX = 0xFFFF;
                return;
#endif
            }

            if (!FilePointerDictionary.ContainsKey(fileStruct.curp.Offset))
            {
                _logger.Warn(
                    $"({Module.ModuleIdentifier}) Attempted to call FCLOSE on pointer not in File Stream Segment {fileStruct.curp} (File Already Closed?)");
                Registers.AX = 0xFFFF;
                return;
            }

            //Clean Up File Stream Pointer
            var fileStream = FilePointerDictionary[fileStruct.curp.Offset];

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Closed File {filePointer} {fileStream.Name} (Stream: {fileStruct.curp})");
#endif

            FilePointerDictionary[fileStruct.curp.Offset].Close();
            FilePointerDictionary.Remove(fileStruct.curp.Offset);

            Registers.AX = 0;
        }

        /// <summary>
        ///     Flushes an Open File Pointer
        ///
        ///     Signature: int fclose(FILE* stream ). Returns 0 on success, EOF (-1) on failure
        /// </summary>
        private void f_flush()
        {
            var filePointer = GetParameterPointer(0);

            var fileStruct = new FileStruct(Module.Memory.GetArray(filePointer, FileStruct.Size));
            if (!FilePointerDictionary.ContainsKey(fileStruct.curp.Offset))
            {
                Registers.AX = 0xFFFF;
                return;
            }

            FilePointerDictionary[fileStruct.curp.Offset].Flush();
            Registers.AX = 0;
        }

        /// <summary>
        ///     sprintf() function in C++ to handle string formatting
        ///
        ///     Signature: int sprintf(char *str, const char *format, ... )
        /// </summary>
        private void sprintf()
        {
            var destinationPointer = GetParameterPointer(0);
            var source = GetParameterStringSpan(2, true);

            //If the supplied string has any control characters for formatting, process them
            var formattedMessage = FormatPrintf(source, 4);

            Module.Memory.SetArray(destinationPointer, formattedMessage);
            Module.Memory.SetByte(destinationPointer + formattedMessage.Length, 0);

            Registers.AX = (ushort)formattedMessage.Length;
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

            try
            {
                var fileEnumerator = Directory.EnumerateFileSystemEntries(path, search, FileUtility.CASE_INSENSITIVE_ENUMERATION_OPTIONS);
                var guid = Guid.NewGuid();

                _activeSearches.Add(guid, fileEnumerator.GetEnumerator());

                var fndblk = new FndblkStruct() { Guid = guid };
                Module.Memory.SetArray(findBlockPointer, fndblk.Data);
            }
            catch (DirectoryNotFoundException)
            {
                _logger.Warn($"({Module.ModuleIdentifier}) Can't find directory {path}");
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
                _logger.Warn($"({Module.ModuleIdentifier}) Called fndnxt but the GUID wasn't found {fndblk.Guid}");
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
                fndblk.Size = (int)fileInfo.Length;
                fndblk.SetAttributes(fileInfo.Attributes);

                // DOS doesn't support long file names, so filter those out from the result set
                var name = FileUtility.SplitIntoComponents(enumerator.Current)[^1];
                if (name.Length >= FndblkStruct.FilenameSize)
                    continue;

                fndblk.Name = name;
                Module.Memory.SetArray(fndblkPointer, fndblk.Data);
                Registers.AX = 1;
                return;
            }
        }

        /// <summary>
        ///     Reads an array of count elements, each one with a size of size bytes, from the stream and stores
        ///     them in the block of memory specified by ptr.
        ///
        ///     Signature: size_t fread(void* ptr, size_t elementSize, size_t numberOfElements, FILE* stream)
        /// </summary>
        private void f_read()
        {
            var destinationPointer = GetParameterPointer(0);
            var elementSize = GetParameter(2);
            var numberOfElements = GetParameter(3);
            var fileStructPointer = GetParameterPointer(4);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException(
                    $"({Module.ModuleIdentifier}) File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            if (fileStream.Position >= fileStream.Length)
            {
                //Set EOF Flag
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);

                Registers.AX = 0;
                return;
            }

            var totalToRead = elementSize * numberOfElements;
            var buffer = new byte[totalToRead];
            var bytesRead = fileStream.Read(buffer);
            if (bytesRead > 0)
                Module.Memory.SetArray(destinationPointer, new ReadOnlySpan<byte>(buffer, 0, bytesRead));

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

            Registers.AX = (ushort)(bytesRead / elementSize);
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
                    $"({Module.ModuleIdentifier}) File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            var oldPosition = fileStream.Position;
            var bytesToWrite = size * count;
            fileStream.Write(Module.Memory.GetArray(sourcePointer, (ushort)bytesToWrite));
            var elementsWritten = (fileStream.Position - oldPosition) / size;

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Wrote {elementsWritten} group(s) of {size} bytes from {sourcePointer}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = (ushort)elementsWritten;
        }

        /// <summary>
        ///     Deleted the specified file
        /// </summary>
        private void unlink()
        {
            var filename = _fileFinder.FindFile(Module.ModulePath, GetParameterFilename(0));
            var fullPath = Path.Combine(Module.ModulePath, filename);
#if DEBUG
            _logger.Debug($"Deleting File: {fullPath}");
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
            var stringValue = GetParameterString(0, true);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Evaluated string length of {stringValue.Length} for string at {GetParameterPointer(0)} ({stringValue})");
#endif
            Registers.AX = (ushort)stringValue.Length;
        }

        /// <summary>
        ///     User Input
        ///
        ///     Signature: char input[]
        /// </summary>
        private ReadOnlySpan<byte> input => Module.Memory.GetVariablePointer("INPUT").Data;

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
            _logger.Debug($"({Module.ModuleIdentifier}) Converted {(char)character} to {(char)Registers.AX}");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Converted {(char)character} to {(char)Registers.AX}");
#endif
        }

        /// <summary>
        ///     Changes the name of the file or directory specified by old name to new name
        ///
        ///     Signature: int rename(const char *oldname, const char *newname )
        /// </summary>
        private void rename()
        {
            var oldFilenameInputValue = GetParameterFilename(0);
            var newFilenameInputValue = GetParameterFilename(2);

            oldFilenameInputValue = _fileFinder.FindFile(Module.ModulePath, oldFilenameInputValue);
            newFilenameInputValue = _fileFinder.FindFile(Module.ModulePath, newFilenameInputValue);

            try
            {
                File.Move(Path.Combine(Module.ModulePath, oldFilenameInputValue), Path.Combine(Module.ModulePath, newFilenameInputValue),
                    true);

#if DEBUG
                _logger.Debug($"({Module.ModuleIdentifier}) Renamed file {oldFilenameInputValue} to {newFilenameInputValue}");
#endif

                Registers.AX = 0;
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                Registers.AX = 0xFFFF;
            }
        }


        /// <summary>
        ///     The Length of the total Input Buffer received
        ///
        ///     Signature: int inplen
        /// </summary>
        private ReadOnlySpan<byte> inplen => Module.Memory.GetVariablePointer("INPLEN").Data;

        private ReadOnlySpan<byte> margv => Module.Memory.GetVariablePointer("MARGV").Data;

        private ReadOnlySpan<byte> margn => Module.Memory.GetVariablePointer("MARGN").Data;

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

            for (var i = inputPointer.Offset; i < inputPointer.Offset + inputLength; i++)
            {
                if (Module.Memory.GetByte(inputPointer.Segment, i) == 0)
                    Module.Memory.SetByte(inputPointer.Segment, i, (byte)' ');
            }
        }

        private ReadOnlySpan<byte> _exitbuf => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitfopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> _exitopen => new byte[] { 0x0, 0x0, 0x0, 0x0 };
        private ReadOnlySpan<byte> extptr => Module.Memory.GetVariablePointer("EXTPTR").Data;
        private ReadOnlySpan<byte> usaptr => Module.Memory.GetVariablePointer("USAPTR").Data;

        /// <summary>
        ///     Appends the first num characters of source to destination, plus a terminating null-character.
        ///
        ///     Signature: char *strncat(char *destination, const char *source, size_t num)
        /// </summary>
        private void strncat()
        {
            var destinationPointer = GetParameterPointer(0);
            var destinationString = GetParameterStringSpan(0, true);
            var sourceString = GetParameterStringSpan(2, true);
            var bytesToCopy = GetParameter(4);

            bytesToCopy = Math.Min(bytesToCopy, (ushort)sourceString.Length);

            Module.Memory.SetArray(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length),
                sourceString.Slice(0, bytesToCopy));
            // null terminate always
            Module.Memory.SetByte(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length + bytesToCopy), 0x0);

            Registers.SetPointer(destinationPointer);
        }

        /// <summary>
        ///     Sets the first num bytes of the block of memory with the specified value
        ///
        ///     Signature: void _FAR * _RTLENTRYF _EXPFUNC memset(void _FAR *__s, int __c, size_t __n);
        /// </summary>
        private void memset()
        {
            var destinationPointer = GetParameterPointer(0);
            var valueToFill = GetParameter(2);
            var numberOfBytesToFill = GetParameter(3);

            Module.Memory.FillArray(destinationPointer, numberOfBytesToFill, (byte)valueToFill);

            Registers.SetPointer(destinationPointer);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Filled {numberOfBytesToFill} bytes at {destinationPointer} with {(byte)valueToFill:X2}");
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

            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("GETMSG", 0x1000);
            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} from {McvPointerDictionary[_currentMcvFile.Offset].FileName} (MCV Pointer: {_currentMcvFile}), saved {outputValue.Length} bytes to {variablePointer}");
#endif

            Registers.SetPointer(variablePointer);
        }

        /// <summary>
        ///     Reads data from s and stores them accounting to parameter format into the locations given by the additional arguments
        ///
        ///     Signature: int sscanf(const char *s, const char *format, ...)
        /// </summary>
        private void sscanf()
        {
            var inputString = GetParameterString(0, stripNull: true);
            var formatString = GetParameterString(2, stripNull: true);
            scanf(inputString.GetEnumerator(), formatString, 4);
        }

        private static IEnumerator<char> FromFileStream(FileStream input)
        {
            int b;
            while ((b = input.ReadByte()) >= 0)
                yield return Convert.ToChar(b);
        }

        /// <summary>
        ///     Reads data from stream and stores them accounting to parameter format into the locations given by the additional arguments
        ///
        ///     Signature: int sscanf(FILE *stream, const char *format, ...)
        /// </summary>
        private void fscanf()
        {
            var fileStructPointer = GetParameterPointer(0);
            var formatString = GetParameterString(2, stripNull: true);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
            {
                _logger.Warn($"({Module.ModuleIdentifier}) File Stream Pointer for {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");
                Registers.AX = 0;
                return;
            }

            scanf(FromFileStream(fileStream), formatString, 4);
        }

        private enum ScanfParseState
        {
            NORMAL,
            PERCENT
        };

        private struct ScanfState
        {
            private ScanfParseState _scanfParseState = ScanfParseState.NORMAL;

            public ScanfState()
            {
                Length = 0;
                IsLongInteger = false;
            }

            public ScanfParseState State
            {
                get => _scanfParseState;
                set
                {
                    _scanfParseState = value;
                    Length = 0;
                    IsLongInteger = false;
                }
            }
            public int Length { get; set; }
            public bool IsLongInteger { get; set; }
        }

        private int GetNumberBaseFromCharacter(char formatChar) => formatChar switch
        {
            'x' => 16,
            'o' => 8,
            _ => 10
        };

        private void scanf(IEnumerator<char> input, string formatString, int startingParameterOrdinal)
        {
            if (!input.MoveNext())
            {
                Registers.AX = 0;
                return;
            }

            var matches = 0;
            var parseState = new ScanfState();
            var moreInput = true;

            foreach (var formatChar in formatString)
            {
                if (!moreInput)
                    break;

                switch (parseState.State)
                {
                    case ScanfParseState.NORMAL when char.IsWhiteSpace(formatChar):
                        ConsumeWhitespace(input);
                        break;
                    case ScanfParseState.NORMAL when formatChar == '%':
                        parseState.State = ScanfParseState.PERCENT;
                        break;
                    case ScanfParseState.NORMAL:
                        // match a single character
                        (moreInput, _) = ConsumeWhitespace(input);
                        if (moreInput)
                        {
                            moreInput = (formatChar == input.Current);
                            moreInput &= input.MoveNext();
                        }
                        break;
                    case ScanfParseState.PERCENT when Char.IsAsciiDigit(formatChar):
                        parseState.Length = parseState.Length * 10 + formatChar - '0';
                        break;
                    case ScanfParseState.PERCENT when formatChar == 'i' || formatChar == 'd' || formatChar == 'u' || formatChar == 'x':
                        var result = GetLeadingNumberFromString(input, GetNumberBaseFromCharacter(formatChar), parseState.Length > 0 ? parseState.Length : -1);
                        moreInput = result.MoreInput;
                        if (parseState.IsLongInteger)
                        {
                            // low word first followed by high word
                            Module.Memory.SetWord(
                                GetParameterPointer(startingParameterOrdinal),
                                (ushort)((uint)result.Value & 0xFFFF));
                            Module.Memory.SetWord(
                                GetParameterPointer(startingParameterOrdinal) + 2,
                                (ushort)((uint)result.Value >> 16));
                        }
                        else
                        {
                            Module.Memory.SetWord(
                                GetParameterPointer(startingParameterOrdinal),
                                (ushort)result.Value);
                        }

                        if (result.Valid)
                            ++matches;

                        startingParameterOrdinal += 2;
                        parseState.State = ScanfParseState.NORMAL;
                        break;
                    case ScanfParseState.PERCENT when formatChar == 's' || formatChar == 'c':
                        var defaultLength = (formatChar == 's') ? int.MaxValue : 1;
                        var length = parseState.Length > 0 ? parseState.Length : defaultLength;
                        var count = 0;
                        (var stringValue, moreInput) = ReadString(input, c =>
                        {
                            return (count++ < length) ?
                                ExportedModuleBase.CharacterAccepterResponse.ACCEPT :
                                ExportedModuleBase.CharacterAccepterResponse.ABORT;
                        });

                        var destinationPtr = GetParameterPointer(startingParameterOrdinal);
                        Module.Memory.SetArray(destinationPtr, Encoding.ASCII.GetBytes(stringValue));

                        if (stringValue.Length > 0)
                            ++matches;
                        if (formatChar == 's') // null terminate
                            Module.Memory.SetByte(destinationPtr + stringValue.Length, 0);

                        startingParameterOrdinal += 2;
                        parseState.State = ScanfParseState.NORMAL;
                        break;
                    case ScanfParseState.PERCENT when formatChar == 'l':
                        parseState.IsLongInteger = true;
                        break;
                    case ScanfParseState.PERCENT when formatChar == '%':
                        parseState.State = ScanfParseState.NORMAL;
                        goto case ScanfParseState.NORMAL; // yolo
                    case ScanfParseState.PERCENT:
                        throw new ArgumentException($"({Module.ModuleIdentifier}) Unsupported sscanf specifier: {formatChar}");
                }
            }

            Registers.AX = (ushort)matches;
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

            //Load registers and pass to Int21h
            var registers = new CpuRegisters();
            registers.FromRegs(Module.Memory.GetArray(parameterOffset1, 16));

            _int21h.Registers = registers;
            _int21h.Handle();

            Module.Memory.SetArray(parameterOffset2, registers.ToRegs());
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
            _logger.Debug($"({Module.ModuleIdentifier}) Added {output.Length} bytes to the buffer (Message #: {messageNumber})");
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
            _logger.Debug($"({Module.ModuleIdentifier}) Registered Global Command Handler at: {globalCommandHandlerPointer}");
#endif
        }

        /// <summary>
        ///     Catastro Failure, basically a mega show stopping error
        /// </summary>
        private void catastro()
        {
            var message = GetParameterStringSpan(0);

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
                throw new FileNotFoundException($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            if (fileStream.Position == fileStream.Length)
            {
                _logger.Warn($"({Module.ModuleIdentifier}) Attempting to read EOF file, returning null pointer");
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            using var valueFromFile = new MemoryStream(maxCharactersToRead);
            for (var i = 0; i < (maxCharactersToRead - 1); i++)
            {
                var inputValue = (byte)fileStream.ReadByte();

                if (inputValue == '\r' && (fileStruct.flags & (ushort)FileStruct.EnumFileFlags.Binary) == 0)
                    continue;

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
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Read string from {fileStructPointer}, {valueFromFile.Length} bytes (Stream: {fileStruct.curp}), saved at {destinationPointer} (EOF: {fileStream.Position == fileStream.Length})");
#endif
            Registers.SetPointer(destinationPointer);
        }

        /// <summary>
        ///     Concatenates two strings and saves them to the destination.
        ///
        ///     Signature: char *strcat(char *destination, const char *source)
        /// </summary>
        public void strcat()
        {
            var destinationPointer = GetParameterPointer(0);
            var destinationString = GetParameterStringSpan(0, true);
            var sourceString = GetParameterStringSpan(2);

            Module.Memory.SetArray(destinationPointer.Segment,
                (ushort)(destinationPointer.Offset + destinationString.Length),
                sourceString);

            Registers.SetPointer(destinationPointer);
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
                throw new FileNotFoundException($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            var output = Module.Memory.GetString(sourcePointer, true);

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
            _logger.Debug($"({Module.ModuleIdentifier}) Wrote {formattedMessage.Length} bytes to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif

            Registers.AX = (ushort)formattedMessage.Length;
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
                throw new FileNotFoundException($"Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

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
            _logger.Debug($"({Module.ModuleIdentifier}) Seek to {fileStream.Position} in {fileStructPointer} (Stream: {fileStruct.curp})");
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
            //var packedTime = (_clock.Now.Hour << 11) + (_clock.Now.Minute << 5) + (_clock.Now.Second >> 1);

            var packedTime = GetParameter(0);

            var unpackedHour = (packedTime >> 11) & 0x1F;
            var unpackedMinutes = (packedTime >> 5) & 0x3F;
            var unpackedSeconds = (packedTime << 1) & 0x3E;

            var unpackedTime = new DateTime(_clock.Now.Year, _clock.Now.Month, _clock.Now.Day, unpackedHour,
                unpackedMinutes, unpackedSeconds);

            var timeString = $"{unpackedTime.ToString("HH:mm:ss")}\0";
            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("NCTIME", (ushort)timeString.Length);

            Module.Memory.SetArray(variablePointer.Segment, variablePointer.Offset,
                Encoding.Default.GetBytes(timeString));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Received value: {packedTime}, decoded string {timeString} saved to {variablePointer}");
#endif
            Registers.SetPointer(variablePointer);
        }

        /// <summary>
        ///     Returns a pointer to the first occurrence of character in the C string str
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

                Registers.SetPointer(stringPointer + i);
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
            var margvPointer = new FarPtr(Module.Memory.GetVariablePointer("MARGV"));
            var margnPointer = new FarPtr(Module.Memory.GetVariablePointer("MARGN"));

            var margCount = (ushort)inputComponents.Count(x => !string.IsNullOrEmpty(x));

            Module.Memory.SetPointer(margvPointer, inputPointer); //Set 1st command to start at start of input
            margvPointer.Offset += FarPtr.Size;

            for (var i = 0; i < parsedInput.Length; i++)
            {
                if (parsedInput[i] != 0)
                    continue;

                //Set Command End
                var commandEndPointer = new FarPtr(inputPointer.Segment, (ushort)(inputPointer.Offset + i));
                Module.Memory.SetPointer(margnPointer, commandEndPointer);
                margnPointer.Offset += FarPtr.Size;

                i++;

                //Skip any white spaces after the current command
                while (i < parsedInput.Length && parsedInput[i] == 0)
                    i++;

                //Are we at the end of the INPUT? If so, we're done here.
                if (i == parsedInput.Length)
                    break;

                //If not, set the next command start
                var commandStartPointer = new FarPtr(inputPointer.Segment, (ushort)(inputPointer.Offset + i));
                Module.Memory.SetPointer(margvPointer, commandStartPointer);
                margvPointer.Offset += FarPtr.Size;

            }

            Module.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(parsedInput));
            Module.Memory.SetWord("MARGC", margCount);
            //setINPLEN();
        }

        /// <summary>
        ///     Move a block of memory
        ///
        ///     Signature: void movmem(char *source, char *destination, unsigned nbytes)
        /// </summary>
        private void movmem()
        {
            var sourcePointer = GetParameterPointer(0);
            var destinationPointer = GetParameterPointer(2);
            var bytesToMove = GetParameter(4);

            //Cast to array as the write can overlap and overwrite, mucking up the span read
            var sourceData = Module.Memory.GetArray(sourcePointer, bytesToMove);

            Module.Memory.SetArray(destinationPointer, sourceData);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Moved {bytesToMove} bytes {sourcePointer}->{destinationPointer}");
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
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Returning Current Position of {currentPosition} for {fileStructPointer} (Stream: {fileStruct.curp})");
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

            var userBase = new FarPtr(Module.Memory.GetVariablePointer("USER").Data);
            userBase.Offset += (ushort)(User.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHUSP"), userBase.Data);

            var userAccBase = new FarPtr(Module.Memory.GetVariablePointer("USRACC").Data);
            userAccBase.Offset += (ushort)(UserAccount.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHUAP"), userAccBase.Data);

            var userExtAcc = new FarPtr(Module.Memory.GetVariablePointer("EXTUSR").Data);
            userExtAcc.Offset += (ushort)(ExtUser.Size * userSession.Channel);
            Module.Memory.SetArray(Module.Memory.GetVariablePointer("OTHEXP"), userExtAcc.Data);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) User Found -- Channel {userSession.Channel}, user[] offset {userBase}");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Searches the string for any occurrence of the substring
        ///
        ///     Signature: int found=samein(char *subs, char *string)
        /// </summary>
        private void samein()
        {
            var stringToFind = GetParameterString(0, true);
            var stringToSearch = GetParameterString(2, true);

            if (string.IsNullOrEmpty(stringToFind) || string.IsNullOrEmpty(stringToSearch))
            {
                Registers.AX = 0;
                return;
            }

            Registers.AX = (ushort)(stringToSearch.Contains(stringToFind, StringComparison.InvariantCultureIgnoreCase) ? 1 : 0);
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

                Registers.SetPointer(stringToSearchPointer + i);
                return;
            }

            Registers.SetPointer(stringToSearchPointer);
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
                throw new Exception($"({Module.ModuleIdentifier}) Unable to locate FileStream for {fileStructPointer} (Stream: {fileStruct.curp})");

            if (fileStream.Position == fileStream.Length)
            {
                _logger.Warn($"({Module.ModuleIdentifier}) Attempting to read EOF file, returning null pointer");
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            using var valueFromFile = new MemoryStream(maxCharactersToRead);
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
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Read string from {fileStructPointer}, {valueFromFile.Length} bytes (Stream: {fileStruct.curp}), saved at {destinationPointer} (EOF: {fileStream.Position == fileStream.Length})");
#endif
            Registers.SetPointer(destinationPointer);
        }

        /// <summary>
        ///     Pointer to the Generic BBS Database (BBSGEN.DAT)
        /// </summary>
        private ReadOnlySpan<byte> genbb => Module.Memory.GetVariablePointer("GENBB").Data;

        /// <summary>
        ///     Pointer to the BBS Accounts Database (BBSUSR.DAT)
        /// </summary>
        private ReadOnlySpan<byte> accbb => Module.Memory.GetVariablePointer("ACCBB").Data;

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
                stringData[i] = (byte)modifier.Invoke((char)stringData[i]);
            }

            Module.Memory.SetArray(stringToConvertPointer, stringData);

            Registers.SetPointer(stringToConvertPointer);
        }

        /// <summary>
        ///     Returns a pointer to the first occurrence of str2 in str1, or a null pointer if str2 is not part of str1.
        /// </summary>
        private void strstr()
        {
            var stringToSearchPointer = GetParameterPointer(0);
            var stringToSearch = GetParameterString(0, true);
            var stringToFind = GetParameterString(2, true);

            var offset = stringToSearch.IndexOf(stringToFind);
            if (offset >= 0)
            {
                Registers.SetPointer(stringToSearchPointer + offset);
            }
            else
            {
                // not found, return NULL
                Registers.SetPointer(FarPtr.Empty);
            }
        }

        /// <summary>
        ///     Removes trailing blank spaces from string
        ///
        ///     Signature: int nremoved=depad(char *string)
        /// </summary>
        private void depad(bool updateAX = true)
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

            if (updateAX)
                Registers.AX = numRemoved;
        }

        private ReadOnlySpan<byte> othusn => Module.Memory.GetVariablePointer("OTHUSN").Data;

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
            // we don't support locks, so just call the normal version
            stpbtv();
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
        ///     Change the currently active Channel to the specified Channel Number
        ///
        ///     Signature: void curusr(int newunum)
        /// </summary>
        private void curusr()
        {
            var newUserNumber = GetParameter(0);

            if (newUserNumber != ushort.MaxValue && !ChannelDictionary.ContainsKey(newUserNumber))
            {
#if DEBUG
                _logger.Debug($"({Module.ModuleIdentifier}) Invalid Channel: {newUserNumber}");
#endif
                return;
            }

            //Save the Current Channel
            UpdateSession(ChannelNumber);

            //Load the new Channel
            SetState(newUserNumber);

#if DEBUG
            _logger.Debug($"Setting Current User to {newUserNumber}");
#endif
        }

        /// <summary>
        ///     Number of modules currently installed
        /// </summary>
        private ReadOnlySpan<byte> nmods => Module.Memory.GetVariablePointer("NMODS").Data;

        /// <summary>
        ///     This is the pointer, to the pointer, for the MODULE struct because module is declared
        ///     **module in MAJORBBS.H
        ///
        ///     Pointer -> Pointer -> Struct
        /// </summary>
        private ReadOnlySpan<byte> module => Module.Memory.GetVariablePointer("**MODULE").Data;

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
        }

        /// <summary>
        ///     Internal Borland C++ Localle Aware ctype Macros
        ///
        ///     See: CTYPE.H
        /// </summary>
        private ReadOnlySpan<byte> _ctype => Module.Memory.GetVariablePointer("CTYPE").Data;

        /// <summary>
        ///     Points to the ad-hoc Volatile Data Area
        ///
        ///     Signature: char *vdatmp
        /// </summary>
        private ReadOnlySpan<byte> vdatmp => Module.Memory.GetVariablePointer("*VDATMP").Data;

        /// <summary>
        ///     Send prfbuf to a channel & clear
        ///     Multilingual version of outprf(), for now we just call outprf()
        /// </summary>
        private void outmlt() => outprf();

        /// <summary>
        ///     Update the Btrieve current record with a variable length record
        ///
        ///     Signature: void upvbtv(char *recptr, int length)
        /// </summary>
        /// <returns></returns>
        private void upvbtv()
        {
            var btrieveRecordPointerPointer = GetParameterPointer(0);
            var length = GetParameter(2);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var dataToWrite = Module.Memory.GetArray(btrieveRecordPointerPointer, length);

            currentBtrieveFile.Update(dataToWrite.ToArray());
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

            using var inputBuffer = new MemoryStream(limit);
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
            _logger.Debug(
                $"({Module.ModuleIdentifier}) Copied \"{Encoding.ASCII.GetString(inputBuffer.ToArray())}\" ({inputBuffer.Length} bytes) from {sourcePointer} to {destinationPointer}");
#endif
            Registers.SetPointer(destinationPointer);
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
            if (routinePointer == FarPtr.Empty)
            {
                ChannelDictionary[channelNumber].PollingRoutine = null;
#if DEBUG
                _logger.Debug($"Unassigned Polling Routine on Channel {channelNumber}");
#endif
                return;
            }

            ChannelDictionary[channelNumber].PollingRoutine = routinePointer;
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), 192);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Assigned Polling Routine {ChannelDictionary[channelNumber].PollingRoutine} to Channel {channelNumber}");
#endif
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

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Unassigned Polling Routine on Channel {channelNumber}");
#endif
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
                var titlePointer = Module.Memory.GetVariablePointer("BBSTTL");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSTitle));
                return Module.Memory.GetVariablePointer("*BBSTTL").Data;
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
                var titlePointer = Module.Memory.GetVariablePointer("COMPANY");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSCompanyName));
                return Module.Memory.GetVariablePointer("*COMPANY").Data;
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
                var titlePointer = Module.Memory.GetVariablePointer("ADDRES1");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSAddress1));
                return Module.Memory.GetVariablePointer("*ADDRES1").Data;
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
                var titlePointer = Module.Memory.GetVariablePointer("ADDRES2");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSAddress2));
                return Module.Memory.GetVariablePointer("*ADDRES2").Data;
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
                var titlePointer = Module.Memory.GetVariablePointer("DATAPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSDataPhone));
                return Module.Memory.GetVariablePointer("*DATAPH").Data;
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
                var titlePointer = Module.Memory.GetVariablePointer("LIVEPH");

                Module.Memory.SetArray(titlePointer, Encoding.ASCII.GetBytes(_configuration.BBSVoicePhone));
                return Module.Memory.GetVariablePointer("*LIVEPH").Data;
            }
        }

        /// <summary>
        ///     Translate buffer (process any possible text_variables)
        ///
        ///     Signature: char *xlttxv(char *buffer,int size)
        /// </summary>
        private void xlttxv()
        {
            var stringToProcess = GetParameterStringSpan(0);
            var size = GetParameter(2);

            var processedString = ProcessTextVariables(stringToProcess);
            var resultPointer = Module.Memory.GetOrAllocateVariablePointer("XLTTXV", 0x800);

            Module.Memory.SetArray(resultPointer, processedString);

            Registers.SetPointer(resultPointer);
        }

        /// <summary>
        ///     Strips ANSI from a string
        ///
        ///     Signature: char *stpans(char *str)
        /// </summary>
        private void stpans()
        {
            var stringToStripPointer = GetParameterPointer(0);
            var inputString = Module.Memory.GetString(stringToStripPointer);

            Module.Memory.GetOrAllocateVariablePointer("STPANS", 1920); //Max Screen Size of 80x24

            if (inputString.Length > 1920)
            {
                _logger.Warn(
                    $"String to Strip is larger than 1920 bytes, truncating to 1920 bytes as to not overflow buffer");
                inputString = inputString.Slice(0, 1920);
            }

            //Declare Return
            var cleanedStringBuilder = new StringBuilder(1920);

            for (int i = 0; i < inputString.Length;)
            {
                if (inputString[i] == '\x1b') // Start of an escape sequence
                {
                    i++; // Increment to skip the escape character

                    // Check if it's a CSI sequence which starts with '['
                    if (i < inputString.Length && inputString[i] == '[')
                    {
                        i++; // Skip the '['
                        // Skip the parameters and intermediate bytes
                        while (i < inputString.Length && inputString[i] > 0x1F && inputString[i] < 0x40)
                        {
                            i++;
                        }
                        // Now skip the final byte
                        if (i < inputString.Length && inputString[i] >= 0x40 && inputString[i] <= 0x7E)
                        {
                            i++;
                        }
                    }
                    // Check if it's an OSC sequence which starts with ']'
                    else if (i < inputString.Length && inputString[i] == ']')
                    {
                        i++; // Skip the ']'
                        // OSC sequence ends with BEL (0x07)
                        while (i < inputString.Length && inputString[i] != 0x07)
                        {
                            i++;
                        }
                        if (i < inputString.Length) // Check to avoid IndexOutOfRangeException
                        {
                            i++; // Skip the BEL character
                        }
                    }
                }
                else if (i < inputString.Length) // Check to avoid IndexOutOfRangeException
                {
                    // Regular character, append it
                    cleanedStringBuilder.Append((char)inputString[i]);
                    i++;
                }
            }

            Module.Memory.SetArray("STPANS", Encoding.ASCII.GetBytes(cleanedStringBuilder.ToString()));

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Ignoring, not stripping ANSI");
#endif

            Registers.SetPointer(Module.Memory.GetVariablePointer("STPANS"));
        }

        /// <summary>
        ///     Volatile Data Size after all INIT routines are complete
        ///
        ///     This is hard coded to VOLATILE_DATA_SIZE constant
        ///
        ///     Signature: int vdasiz;
        /// </summary>
        private ReadOnlySpan<byte> vdasiz => Module.Memory.GetVariablePointer("VDASIZ").Data;

        /// <summary>
        ///     Performs a Btrieve Query Operation based on KEYS
        ///
        ///     Signature: int is=qrybtv(char *key, int keynum, int qryopt)
        /// </summary>
        private void qrybtv()
        {
            var keyPointer = GetParameterPointer(0);
            var keyNumber = GetParameter(2);
            var queryOption = (EnumBtrieveOperationCodes)GetParameter(3);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            var key = Module.Memory.GetArray(keyPointer,
                currentBtrieveFile.GetKeyLength(keyNumber));

            var result = currentBtrieveFile.PerformOperation(keyNumber, key, queryOption);
            if (result)
                UpdateBB(currentBtrieveFile, FarPtr.Empty, queryOption, (short)keyNumber);

            Registers.AX = result ? (ushort)1 : (ushort)0;
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

            UpdateBB(currentBtrieveFile, recordPointer, (uint)absolutePosition, acquiresData: true, (short)keynum);
        }

        /// <summary>
        ///     Pointer to structure for that user in the user[] array (set by onsys() or instat())
        ///
        ///     Signature: struct user *othusp;
        /// </summary>
        private ReadOnlySpan<byte> othusp => Module.Memory.GetVariablePointer("*OTHUSP").Data;

        /// <summary>
        ///     Pointer to structure for that user in the extusr structure (set by onsys() or instat())
        ///
        ///     Signature: struct extusr *othexp;
        /// </summary>
        private ReadOnlySpan<byte> othexp => Module.Memory.GetVariablePointer("*OTHEXP").Data;

        /// <summary>
        ///     Pointer to structure for that user in the 'usracc' structure (set by onsys() or instat())
        ///
        ///     Signature: struct usracc *othuap;
        /// </summary>
        private ReadOnlySpan<byte> othuap => Module.Memory.GetVariablePointer("*OTHUAP").Data;

        /// <summary>
        ///     Splits the specified string into tokens based on the delimiters
        /// </summary>
        private void strtok()
        {
            var stringToSplitPointer = GetParameterPointer(0);
            var stringDelimitersPointer = GetParameterPointer(2);

            var workPointerPointer = Module.Memory.GetOrAllocateVariablePointer("STRTOK-WRK", FarPtr.Size);
            var lengthPointer = Module.Memory.GetOrAllocateVariablePointer("STRTOK-END", 2);

            //If it's the first call, reset the values in memory
            if (!stringToSplitPointer.Equals(FarPtr.Empty))
            {
                Module.Memory.SetPointer(workPointerPointer, stringToSplitPointer);
                Module.Memory.SetWord(lengthPointer, (ushort)(stringToSplitPointer.Offset + Module.Memory.GetString(stringToSplitPointer, stripNull: true).Length));
            }

            var workPointer = Module.Memory.GetPointer(workPointerPointer);
            var endOffset = Module.Memory.GetWord(lengthPointer);

            var stringDelimiter = Encoding.ASCII.GetString(Module.Memory.GetString(stringDelimitersPointer, stripNull: true));

            // skip starting delimiters
            while (workPointer.Offset < endOffset && stringDelimiter.Contains((char)Module.Memory.GetByte(workPointer)))
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

            Registers.SetPointer(workPointer);

            // scan until we find the next delimiter and then null it out for the return
            while (workPointer.Offset < endOffset && !stringDelimiter.Contains((char)Module.Memory.GetByte(workPointer)))
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
            var stringToWrite = GetParameterStringSpan(0);
            var fileStructPointer = GetParameterPointer(2);

            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            if (!FilePointerDictionary.TryGetValue(fileStruct.curp.Offset, out var fileStream))
                throw new FileNotFoundException(
                    $"File Pointer {fileStructPointer} (Stream: {fileStruct.curp}) not found in the File Pointer Dictionary");

            if ((fileStruct.flags & (ushort)FileStruct.EnumFileFlags.Binary) == 0)
                fileStream.Write(FormatNewLineCarriageReturn(stringToWrite));
            else
                fileStream.Write(stringToWrite);

            fileStream.Flush();

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Wrote {stringToWrite.Length} bytes from {GetParameterPointer(0)}, written to {fileStructPointer} (Stream: {fileStruct.curp})");
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

            var variablePointer = Module.Memory.GetOrAllocateVariablePointer("RAWMSG", 0x1000);
            var outputValue = McvPointerDictionary[_currentMcvFile.Offset].GetString(msgnum);

            if (outputValue.Length > 0x1000)
                throw new Exception($"MSG {msgnum} is larger than pre-defined buffer: {outputValue.Length}");

            Module.Memory.SetArray(variablePointer, outputValue);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier})Retrieved option {msgnum} from {McvPointerDictionary[_currentMcvFile.Offset].FileName} (MCV Pointer: {_currentMcvFile}), saved {outputValue.Length} bytes to {variablePointer}");
#endif

            Registers.SetPointer(variablePointer);
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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} from {McvPointerDictionary[_currentMcvFile.Offset].FileName} (MCV Pointer: {_currentMcvFile}): {outputValue}");
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

            if (characterRead == '\r' && (fileStruct.flags & (ushort)FileStruct.EnumFileFlags.Binary) == 0)
                characterRead = '\n';

            //Update EOF Flag if required
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Read 1 byte from {fileStructPointer} (Stream: {fileStruct.curp}): {characterRead:X2}");
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
            var string1 = GetParameterPointer(0) == FarPtr.Empty ? string.Empty : GetParameterString(0, stripNull: true);
            var string2 = GetParameterPointer(2) == FarPtr.Empty ? string.Empty : GetParameterString(2, stripNull: true);

            Registers.AX = (ushort)string.Compare(string1, string2, ignoreCase: true);
        }

        /// <summary>
        ///     Frees memory allocated via farmalloc
        ///
        ///     <para/>Signature: void farfree(void *)
        /// </summary>
        private void farfree()
        {
            Module.Memory.Free(GetParameterPointer(0));
        }

        /// <summary>
        ///     Allocates A LOT of memory!!!
        ///
        ///     <para/>Signature: void* farmalloc(ULONG size);
        ///     <para/>Return: AX = Offset in Segment (host)
        ///             DX = Data Segment
        /// </summary>
        private void farmalloc()
        {
            var requestedSize = GetParameterULong(0);
            if (requestedSize > 0xFFFF)
                throw new OutOfMemoryException("farmalloc trying to allocate more than a segment");

            Registers.SetPointer(Module.Memory.Malloc((ushort)requestedSize));
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

            var allocatedMemory = Module.Memory.Malloc(size);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Allocated {size} bytes starting at {allocatedMemory}");
#endif

            Registers.SetPointer(allocatedMemory);
        }


        /// <summary>
        ///     Ungets character from stream and decreases the internal file position by 1
        ///
        ///     Signature: int ungetc(int character,FILE *stream )
        /// </summary>
        private void ungetc()
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

            //Update EOF Flag if required -- TODO DO WE NEED?
            if (fileStream.Position == fileStream.Length)
            {
                fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.EOF;
                Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
            }

#if DEBUG
            _logger.Debug($"Unget 1 byte from {fileStructPointer} (Stream: {fileStruct.curp}). New Position: {fileStream.Position}, Character: {(char)character}");
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
            Module.Memory.Free(GetParameterPointer(0));
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
            _logger.Debug($"({Module.ModuleIdentifier}) Character {(char)character} written to {fileStructPointer} (Stream: {fileStruct.curp})");
#endif
            Registers.AX = 1;
        }

        /// <summary>
        ///     Allocate a very large memory region, qty by size bytes.
        ///     Each tile gets its own segment at offset 0, though we
        ///     internally allocate a buffer of the appropriate size to
        ///     ensure we don't waste memory.
        ///
        ///     Signature: void *alctile(unsigned qty,unsigned size);
        /// </summary>
        public void alctile()
        {
            var qty = GetParameter(0);
            var size = GetParameter(1);

            if (qty == 0 || size == 0)
            {
                throw new ArgumentException("qty and size must be non-zero");
            }

            Registers.SetPointer(Module.Memory.AllocateRealModeSegment(size));
            for (var i = 1; i < qty; ++i)
            {
                Module.Memory.AllocateRealModeSegment(size);
            }
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

            if (qty == 0 || size == 0)
            {
                throw new ArgumentException("qty and size must be non-zero");
            }

            var bigRegion = Module.Memory.AllocateBigMemoryBlock(qty, size);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Created Big Memory Region that is {qty} records of {size} bytes in length at {bigRegion}");
#endif

            Registers.SetPointer(bigRegion);
        }

        /// <summary>
        ///     Dereference an alctile()'d region
        ///
        ///     Signature: void *ptrtile(void *bigptr,unsigned index);
        /// </summary>
        public void ptrtile()
        {
            var firstSegment = GetParameterPointer(0);
            var index = GetParameter(2);

            Registers.DX = (ushort)(firstSegment.Segment + index);
            Registers.AX = 0;
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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved Big Memory block {bigRegionPointer}, returned pointer to index {index}: {indexPointer}");
#endif

            Registers.SetPointer(indexPointer);
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

            var variablePointer = Module.Memory.GetOrAllocateVariablePointer($"EXTUSR-{userNumber}", ExtUser.Size);

            //If user isn't online, return a null pointer
            if (!ChannelDictionary.TryGetValue(userNumber, out var userChannel))
            {
                Registers.AX = 0;
                Registers.DX = 0;
                return;
            }

            //Set Pointer to new user array
            var extoffPointer = Module.Memory.GetVariablePointer("EXTOFF");
            Module.Memory.SetArray(extoffPointer, variablePointer.Data);

            //Set User Array Value
            Module.Memory.SetArray(variablePointer, userChannel.ExtUsrAcc.Data);


            Registers.SetPointer(variablePointer);
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
            if (!Module.ProtectedMemory.HasSegment(0xF000))
                Module.ProtectedMemory.AddSegment(0xF000);

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
                bp = Module.Memory.GetWord(Registers.SS, Registers.BP),
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
            _logger.Debug($"({Module.ModuleIdentifier}) Jump Set -- {jmpBuf.cs:X4}:{jmpBuf.ip:X4} AX:{Registers.AX}");
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

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset) + 1;

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
            setINPLEN(inputPointer);
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
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");

            //Return current NXTCMD
            Registers.SetPointer(nxtcmdPointer);

            //Set NXTCMD to the end of the INPUT
            var newNxtcmd = new FarPtr(inputPointer.Segment, (ushort)(inputPointer.Offset + inputLength));
            Module.Memory.SetPointer("NXTCMD", newNxtcmd);
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
                _logger.Debug($"End of Command");
#endif
                Registers.AX = 0;
                return;
            }

            var inputCommand = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            for (var i = 0; i < remainingCharactersInCommand; i++)
            {
                if (inputCommand[i] == 32)
                    continue;

                var resultChar = char.ToUpper((char)inputCommand[i]);

#if DEBUG
                _logger.Debug($"Returning char: {resultChar}");
#endif

                Registers.AX = resultChar;

                //Increment nxtcmd if we hit any blank spaces
                Module.Memory.SetPointer("NXTCMD", new FarPtr(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset + i)));
                return;
            }

            //Otherwise return zero
            Registers.AX = 0;
        }

        private void cncint_ErrorResult(CncIntegerReturnType returnType)
        {
            switch (returnType)
            {
                case CncIntegerReturnType.INT:
                    Registers.AX = 0;
                    break;
                case CncIntegerReturnType.LONG:
                    Registers.AX = 0;
                    Registers.DX = 0;
                    break;
                case CncIntegerReturnType.STRING:
                    var cncNumArray = Module.Memory.GetOrAllocateVariablePointer("CNCNUM", 16);
                    Module.Memory.SetByte(cncNumArray, 0);
                    Registers.SetPointer(cncNumArray);
                    break;
            }
        }

        /// <summary>
        ///     Expect an integer from the user
        ///
        ///     Signature: returnType n=cncint()
        /// </summary>
        private void cncint(CncIntegerReturnType returnType)
        {
            var inputPointer = Module.Memory.GetVariablePointer("INPUT");
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");
            var inputLength = Module.Memory.GetWord("INPLEN");

            var remainingCharactersInCommand = inputLength - (nxtcmdPointer.Offset - inputPointer.Offset);

            if (remainingCharactersInCommand == 0)
            {
                cncint_ErrorResult(returnType);
                return;
            }

            var inputString = Encoding.ASCII.GetString(Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand));

            IEnumerator<char> charEnumerator = inputString.GetEnumerator();
            charEnumerator.MoveNext();

            var (moreInput, skipped) = ConsumeWhitespace(charEnumerator);
            if (!moreInput)
            {
                cncint_ErrorResult(returnType);
                return;
            }

            var result = GetLeadingNumberFromString(charEnumerator, 10, -1);

            if (result.StringValue.Length > 0)
                Module.Memory.SetPointer("NXTCMD", nxtcmdPointer + skipped + result.StringValue.Length);

            switch (returnType)
            {
                case CncIntegerReturnType.INT:
                    Registers.AX = result.Valid ? (ushort)result.Value : (ushort)0;
                    break;
                case CncIntegerReturnType.LONG:
                    Registers.AX = result.Valid ? (ushort)((uint)result.Value & 0xFFFF) : (ushort)0;
                    Registers.DX = result.Valid ? (ushort)((uint)result.Value >> 16) : (ushort)0;
                    break;
                case CncIntegerReturnType.STRING:
                    var cncNumArray = Module.Memory.GetOrAllocateVariablePointer("CNCNUM", 16);
                    Module.Memory.SetArray(cncNumArray, Encoding.ASCII.GetBytes(result.StringValue));
                    Registers.SetPointer(cncNumArray);
                    break;
            }
        }

        /// <summary>
        ///     Main menu credit consumption rate per minute
        ///
        ///     Signature: int mmucrr
        /// </summary>
        private ReadOnlySpan<byte> mmuccr => Module.Memory.GetVariablePointer("MMUCCR").Data;

        /// <summary>
        ///     'Profanity Level' of the Input (from configuration)
        ///
        ///     This is hard coded to 0, which means no profanity detected
        /// </summary>
        private ReadOnlySpan<byte> pfnlvl => Module.Memory.GetVariablePointer("PFNLVL").Data;

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
        ///     Signature: void getbtvl(char *recptr, char *key, int keynum, int getopt, int loktyp)
        /// </summary>
        /// <returns></returns>
        private void getbtvl()
        {
            if (!obtainBtv())
                throw new ArgumentException($"No record found in getbtvl, bombing");
        }

        /// <summary>
        ///     Query Next/Previous Btrieve utility, should only take query next/previous argument
        ///
        ///     Signature: int qnpbtv (int getopt)
        /// </summary>
        private void qnpbtv()
        {
            var queryOption = (EnumBtrieveOperationCodes)GetParameter(0);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));
            var result = currentBtrieveFile.PerformOperation(currentBtrieveFile.PreviousQuery.Key.Number, currentBtrieveFile.PreviousQuery.KeyData, queryOption);
            if (result)
                UpdateBB(currentBtrieveFile, FarPtr.Empty, queryOption, keyNumber: -1);

            Registers.AX = result ? (ushort)1 : (ushort)0;
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

            //Check to see if it's an unmapped ordinal or function pointer to an exported function
            if (sourcePointer.Segment >= 0xFF00)
            {
                _logger.Warn($"Attempting to copy from Unmapped Exported Module. Source Pointer: {sourcePointer}");
                _logger.Warn("Because this action is unsupported, it might cause issues with further Module execution.");
                Registers.SetPointer(destinationPointer);
                return;
            }
            else if (destinationPointer.Segment >= 0xFF00)
            {
                _logger.Warn($"Attempting to copy to Unmapped Exported Module. Destination Pointer: {sourcePointer}");
                _logger.Warn("Because this action is unsupported, it might cause issues with further Module execution.");
                Registers.SetPointer(destinationPointer);
                return;
            }

            // Cast to array as the write can overlap and overwrite, mucking up the span read
            var sourceData = Module.Memory.GetArray(sourcePointer, bytesToMove);

            Module.Memory.SetArray(destinationPointer, sourceData);

#if DEBUG
            _logger.Debug($"Copied {bytesToMove} bytes {sourcePointer}->{destinationPointer}");
#endif
            Registers.SetPointer(destinationPointer);
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

        private ReadOnlySpan<byte> version => Module.Memory.GetVariablePointer("VERSION").Data;

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
            _logger.Debug($"Checking access for: {fileName}");
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
                    _logger.Debug("Set opnbtv() mode to Normal");
                    break;
                case 0xFFFF:
                    _logger.Debug("Set opnbtv() mode to Accelerated");
                    break;
                case 0xFFFE:
                    _logger.Debug("Set opnbtv() mode to Read-Only");
                    break;
                case 0xFFFD:
                    _logger.Debug("Set opnbtv() mode to Verify (Read-After-Write)");
                    break;
                case 0xFFFC:
                    _logger.Debug("Set opnbtv() mode to Exclusive");
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
                if (messagePointer.Equals(FarPtr.Empty))
                    break;

                try
                {
                    tokenList.Add(Module.Memory.GetString(messagePointer).ToArray());
                }
                catch
                {
                    _logger.Warn($"Error Reading string at {messagePointer}, terminating list at {tokenList.Count} elements");
                    break;
                }
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
            _logger.Debug($"({Module.ModuleIdentifier}) Returned DOSTOUNIX time: {epochTime}");
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

            var dateStruct = new DateStruct(_clock.Now);

            Module.Memory.SetArray(datePointer, dateStruct.Data);
        }

        /// <summary>
        ///     Capitalizes the first letter after each space in the specified string
        ///
        ///     Signature: void zonkhl(char *stg);
        /// </summary>
        private void zonkhl()
        {
            var inputStringPointer = GetParameterPointer(0);
            var inputString = Module.Memory.GetString(inputStringPointer).ToArray();
            var isSpace = true;

            for (var i = 0; i < inputString.Length; i++)
            {
                if (char.IsUpper((char)inputString[i]))
                    inputString[i] = (byte)char.ToLower((char)inputString[i]);

                if (inputString[i] == (byte)' ')
                    isSpace = true;

                if (char.IsLower((char)inputString[i]) && isSpace)
                {
                    inputString[i] = (byte)char.ToUpper((char)inputString[i]);
                    isSpace = false;
                }
            }

            Module.Memory.SetArray(inputStringPointer, inputString);
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

            if (max < min)
                max = min;

            Registers.AX = (ushort)_random.Next(min, max);
        }

        /// <summary>
        ///     Remove all whitespace characters
        ///
        ///     Signature: void rmvwht(char *string);
        /// </summary>
        private void rmvwht()
        {
            var stringPointer = GetParameterPointer(0);

            var stringToParse = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointer));
            var parsedString = string.Concat(stringToParse.Where(c => !char.IsWhiteSpace(c)));

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

        private ReadOnlySpan<byte> syscyc => Module.Memory.GetVariablePointer("SYSCYC").Data;

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

            var msgScanResultPointer = Module.Memory.GetOrAllocateVariablePointer("MSGSCAN", 0x1000);

            Module.Memory.SetArray(msgScanResultPointer, new byte[0x1000]); //Zero it out
            Module.Memory.SetArray(msgScanResultPointer, msgVariableValue); //Write

            Registers.SetPointer(msgScanResultPointer);
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
            _logger.Debug($"({Module.ModuleIdentifier}) Returned Packed Date for {filePointer.Name} ({fileTime}) AX: {packedTime} DX: {packedDate}");
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

            var pathString = _fileFinder.ResolvePathWithWildcards(Module.ModulePath, GetParameterFilename(0));

            var files = Directory.GetFiles(Module.ModulePath, pathString);
            uint totalBytes = 0;
            foreach (var f in files)
            {
                totalBytes += (uint)new FileInfo(f).Length;
            }

            Module.Memory.SetArray("NUMFILS", BitConverter.GetBytes((uint)files.Length));
            Module.Memory.SetArray("NUMBYTS", BitConverter.GetBytes(totalBytes));
        }

        private ReadOnlySpan<byte> numfils => Module.Memory.GetVariablePointer("NUMFILS").Data;
        private ReadOnlySpan<byte> numbyts => Module.Memory.GetVariablePointer("NUMBYTS").Data;
        private ReadOnlySpan<byte> numbytp => Module.Memory.GetVariablePointer("NUMBYTP").Data;
        private ReadOnlySpan<byte> numdirs => Module.Memory.GetVariablePointer("NUMDIRS").Data;

        /// <summary>
        ///     Closes the Specified Btrieve File
        ///
        ///     Signature: void clsbtv(struct btvblk *bbp)
        /// </summary>
        private void clsbtv()
        {
            var filePointer = GetParameterPointer(0);

#if DEBUG
            _logger.Debug($"Closing BTV File: {filePointer}");
#endif

            BtrieveDeleteProcessor(filePointer);
            _currentMcvFile = null;
        }

        private ReadOnlySpan<byte> nglobs => Module.Memory.GetVariablePointer("NGLOBS").Data;

        /// <summary>
        ///     Retrieves a C-string containing the value of the environment variable whose name is specified in the argument
        ///
        ///     Signature: char* getenv(const char* name);
        /// </summary>
        private void getenv()
        {
            var name = GetParameterFilename(0);

            var resultPointer = Module.Memory.GetOrAllocateVariablePointer("GETENV", 0xFF);

            switch (name)
            {
                case "PATH":
                    Module.Memory.SetArray(resultPointer, Encoding.ASCII.GetBytes("C:\\BBSV6\\\0"));
                    break;
            }

            Registers.SetPointer(resultPointer);
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
            _logger.Debug($"({Module.ModuleIdentifier}) Channel {ChannelNumber} downloding: {fileToSend}");
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
        private ReadOnlySpan<byte> ftgptr => Module.Memory.GetVariablePointer("FTGPTR").Data;

        /// <summary>
        ///     Universal global Tagspec Handler messag
        ///
        ///     Signature: char tshmsg[TSHLEN+1];
        /// </summary>
        private ReadOnlySpan<byte> tshmsg => Module.Memory.GetVariablePointer("TSHMSG").Data;

        /// <summary>
        ///     (File Transfer) Contains fields for external use
        ///
        ///     Signature: struct ftfscb {...};
        /// </summary>
        private ReadOnlySpan<byte> ftfscb => Module.Memory.GetVariablePointer("FTFSCB").Data;

        /// <summary>
        ///     Output buffer size per channel
        ///
        ///     Signature: int outbsz;
        /// </summary>
        private ReadOnlySpan<byte> outbsz => Module.Memory.GetVariablePointer("OUTBSZ").Data;

        /// <summary>
        ///     System-Variable Btrieve Record Layout Struct (1 of 3)
        ///
        ///     Signature: struct sysvbl sv;
        /// </summary>
        private ReadOnlySpan<byte> sv => Module.Memory.GetVariablePointer("SV").Data;

        /// <summary>
        ///     List an ASCII file to the users screen
        ///
        ///     Signature: void listing(char *path, void (*whndun)())
        /// </summary>
        private void listing()
        {
            var fileToSend = GetParameterFilename(0);
            var finishedFunctionPointer = GetParameterPointer(2);

            fileToSend = _fileFinder.FindFile(Module.ModulePath, fileToSend);

#if DEBUG
            _logger.Debug($"Channel {ChannelNumber} listing: {fileToSend}");
#endif

            ChannelDictionary[ChannelNumber].SendToClientRaw(File.ReadAllBytes(Path.Combine(Module.ModulePath, fileToSend)));

            Module.Execute(finishedFunctionPointer, ChannelNumber, true, true,
                null, (ushort)(Registers.SP - 0x800));
        }


        /// <summary>
        ///     Gets a btrieve record relative to the current offset, should be next/prev only
        ///
        ///     Signature: int anpbtv (void *recptr, int anpopt)
        /// </summary>
        private void anpbtv()
        {
            var recordPointer = GetParameterPointer(0);
            var btrieveOperation = (EnumBtrieveOperationCodes)GetParameter(2);

            Registers.AX = (ushort)(anpbtv(recordPointer, caseSensitive: true, btrieveOperation) ? 1 : 0);
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
        private ReadOnlySpan<byte> eurmsk => Module.Memory.GetVariablePointer("EURMSK").Data;

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
        ///     Less Tolerant Update to Current Record
        ///
        ///     Signature: void updbtv (void *recptr);
        /// </summary>
        private void updbtv()
        {
            if (!updateBtv())
                throw new SystemException("Unable to update btrieve record");
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
            var dosFileMode = (EnumOpenFlags)mode;

            var fullPath = Path.Combine(Module.ModulePath, fileName);

            if (dosFileMode.HasFlag(EnumOpenFlags.O_TEXT))
                throw new ArgumentException($"open called with O_TEXT - not yet supported");
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Opening File: {fullPath}");
#endif
            FileMode fileMode;
            FileAccess fileAccess;

            if (dosFileMode.HasFlag(EnumOpenFlags.O_CREAT))
                fileMode = FileMode.OpenOrCreate;
            else
                fileMode = FileMode.Open;

            if (dosFileMode.HasFlag(EnumOpenFlags.O_TRUNC))
                fileMode = FileMode.Truncate;

            if (dosFileMode.HasFlag(EnumOpenFlags.O_RDRW))
                fileAccess = FileAccess.ReadWrite;
            else if (dosFileMode.HasFlag(EnumOpenFlags.O_WRONLY))
                fileAccess = FileAccess.Write;
            else
                fileAccess = FileAccess.Read;
            //Setup the File Stream
            try
            {
                var fileStream = File.Open(fullPath, fileMode, fileAccess);

                var fileStreamPointer = FilePointerDictionary.Allocate(fileStream);

                Registers.AX = (ushort)fileStreamPointer;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Unable to open {fileName} {dosFileMode}");
                Registers.AX = 0xFFFF;
            }
        }

        /// <summary>
        ///     Reads from file.
        ///
        ///     Signature: int read(int handle, void *buf, unsigned len);
        /// </summary>
        private void read()
        {
            var fd = GetParameter(0);
            var destinationPointer = GetParameterPointer(1);
            var length = GetParameter(3);

            if (!FilePointerDictionary.TryGetValue(fd, out var fileStream))
            {
                // TODO sets errno to EBADF;
                Registers.AX = 0xFFFF; // -1
                return;
            }

            if (fileStream.Position >= fileStream.Length)
            {
                Registers.AX = 0;
                return;
            }

            try
            {
                var buffer = new byte[length];
                var bytesRead = fileStream.Read(buffer);
                if (bytesRead > 0)
                    Module.Memory.SetArray(destinationPointer, new ReadOnlySpan<byte>(buffer, 0, bytesRead));

                Registers.AX = (ushort)bytesRead;
            }
            catch (NotSupportedException)
            {
                // TODO set errno appropriately
                Registers.AX = 0xFFFF;
            }
        }

        /// <summary>
        ///     Writes to file.
        ///
        ///     Signature: int write(int handle, void *buf, unsigned len);
        /// </summary>
        private void write()
        {
            var fd = GetParameter(0);
            var sourcePointer = GetParameterPointer(1);
            var length = GetParameter(3);

            if (!FilePointerDictionary.TryGetValue(fd, out var fileStream))
            {
                // TODO sets errno to EBADF;
                Registers.AX = 0xFFFF; // -1
                return;
            }

            try
            {
                var oldPosition = fileStream.Position;
                fileStream.Write(Module.Memory.GetArray(sourcePointer, length));
                Registers.AX = (ushort)(fileStream.Position - oldPosition);
            }
            catch (NotSupportedException)
            {
                // TODO set errno appropriately
                Registers.AX = 0xFFFF;
            }
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

            var packedDate = (ushort)(((_clock.Now.Year - 1980) << 9) + (_clock.Now.Month << 5) + _clock.Now.Day);
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
            if (!FilePointerDictionary.ContainsKey(fileHandle))
            {
                // TODO set errno
                Registers.AX = 0xFFFF;
                return;
            }

            FilePointerDictionary[fileHandle].Close();
            FilePointerDictionary.Remove(fileHandle);

            Registers.AX = 0;
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

            var fsdAnswersPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-Answers-{ChannelNumber}", 0x800);

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
        private ReadOnlySpan<byte> fsdscb => Module.Memory.GetVariablePointer("*FSDSCB").Data;

        /// <summary>
        ///     Current btvu file pointer set
        ///
        ///     Signature: struct btvblk *bb;
        /// </summary>
        private ReadOnlySpan<byte> bb => Module.Memory.GetVariablePointer("BB").Data;

        /// <summary>
        ///     Convert a string to a long integer
        ///
        ///     Signature: long strtol(const char *strP, char **suffixPP, int radix);
        /// </summary>
        private void strtol()
        {
            var stringPointer = GetParameterPointer(0);
            var suffixPointer = GetParameterPointer(2);
            var radix = GetParameter(4);

            var stringContainingLongs = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointer, stripNull: true));

            if (stringContainingLongs == "")
            {
                Registers.DX = 0;
                Registers.AX = 0;

                if (suffixPointer != FarPtr.Empty)
                    Module.Memory.SetPointer(suffixPointer, new FarPtr(stringPointer.Segment, stringPointer.Offset));

                return;
            }

            var longToParse = stringContainingLongs.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            var longToParseLength = longToParse.Length + (stringContainingLongs.Length - stringContainingLongs.TrimStart(' ').Length); //We do this as length might change with logic below and add back in leading spaces

            if (longToParseLength == 0 || (radix == 10 && !longToParse.Any(char.IsDigit)))
            {
                Registers.DX = 0;
                Registers.AX = 0;

                if (suffixPointer != FarPtr.Empty)
                    Module.Memory.SetPointer(suffixPointer, new FarPtr(stringPointer.Segment, stringPointer.Offset));

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

            if (suffixPointer != FarPtr.Empty)
                Module.Memory.SetPointer(suffixPointer,
                    new FarPtr(stringPointer.Segment, (ushort)(stringPointer.Offset + longToParseLength + 1)));
        }

        /// <summary>
        ///     Returns an unmodified copy of the FSD template (set in fsdroom())
        ///
        ///     Signature: char *fsdrft(void);
        /// </summary>
        private void fsdrft()
        {
            var templatePointer = Module.Memory.GetVariablePointer($"FSD-TemplateBuffer-{ChannelNumber}");

            Registers.SetPointer(templatePointer);
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

            var ripHeaderPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-RIPHeader-{ChannelNumber}", 0xFF);

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

            var fsdFieldVerificationRoutinePointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-FieldVerificationRoutine-{ChannelNumber}", FarPtr.Size);
            var fsdWhenDoneRoutinePointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-WhenDoneRoutine-{ChannelNumber}", FarPtr.Size);

            Module.Memory.SetPointer(fsdFieldVerificationRoutinePointer, fieldVerificationPointer);
            Module.Memory.SetPointer(fsdWhenDoneRoutinePointer, whenDoneRoutinePointer);

            ChannelDictionary[ChannelNumber].SessionState = EnumSessionState.EnteringFullScreenDisplay;

            //Update fsdscb struct
            var fsdscbStructPointer = Module.Memory.GetVariablePointer($"FSD-Fsdscb-{ChannelNumber}");
            var fsdscbStruct = new FsdscbStruct(Module.Memory.GetArray(fsdscbStructPointer, FsdscbStruct.Size));
            fsdscbStruct.fldvfy = fieldVerificationPointer;
            Module.Memory.SetArray(fsdscbStructPointer, fsdscbStruct.Data);

#if DEBUG
            _logger.Debug($"Channel {ChannelNumber} entering Full Screen Display (v:{fieldVerificationPointer}, d:{whenDoneRoutinePointer}");
#endif
        }

        /// <summary>
        ///     Error message for fsdppc(), fsdprc(), etc. (Full-Screen Data Entry)
        ///
        ///     Signature: char fsdemg[];
        /// </summary>
        private ReadOnlySpan<byte> fsdemg => Module.Memory.GetVariablePointer("FSDEMG").Data;

        /// <summary>
        ///     Factory-issue field verify routine, for ask-done-at-end scheme
        ///
        ///     Signature: int vfyadn(int fldno, char *answer);
        /// </summary>
        private void vfyadn()
        {
            var fieldNo = GetParameter(0);
            var answer = GetParameterStringSpan(1);

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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved Field {fieldNo}: {fsdStatus.Fields[fieldNo].Value} ({fsdnanPointer})");
#endif

            Registers.SetPointer(fsdnanPointer);
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
            var answerName = GetParameterString(2, true);

            var answerString = Encoding.ASCII.GetString(Module.Memory.GetArray(answerPointer, 0x400));

            //Trim the Answer String to just the component we need
            var answerStart = answerString.IndexOf(answerName, StringComparison.InvariantCultureIgnoreCase);
            var trimmedAnswerString = answerString.Substring(answerStart);
            trimmedAnswerString = trimmedAnswerString.Substring(0, trimmedAnswerString.IndexOf('\0'));

            var answerComponents = trimmedAnswerString.Split('=');
            var fsdxanPointer = Module.Memory.GetOrAllocateVariablePointer("fsdxan-buffer", 0xFF);

            Module.Memory.SetZero(fsdxanPointer, 0xFF);
            Module.Memory.SetArray(fsdxanPointer, Encoding.ASCII.GetBytes($"{answerComponents[1]}"));

            Registers.SetPointer(fsdxanPointer);
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

            var listOfStrings = new List<Tuple<FarPtr, string>>(numberOfStrings);

            //Grab the pointers and the strings they point to, save in a Tuple
            for (var i = 0; i < numberOfStrings; i++)
            {
                var pointerOffset = (ushort)(stringPointerBase.Offset + (i * FarPtr.Size));
                var destinationPointer = Module.Memory.GetPointer(stringPointerBase.Segment, pointerOffset);
                listOfStrings.Add(new Tuple<FarPtr, string>(destinationPointer, Encoding.ASCII.GetString(Module.Memory.GetString(destinationPointer, true))));
            }

            //Order them alphabetically by string
            var sortedStrings = listOfStrings.OrderBy(x => x.Item2).ToList();

            //Write the new string destination pointers
            for (var i = 0; i < numberOfStrings; i++)
            {
                var pointerOffset = (ushort)(stringPointerBase.Offset + (i * FarPtr.Size));
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

            var stringToParse = Encoding.ASCII.GetString(Module.Memory.GetString(stringPointerBase, stripNull: true));

            var lastWordIndex = stringToParse.LastIndexOf(' ');
            lastWordIndex++; //Start on next character after space

            Registers.SetPointer(stringPointerBase + lastWordIndex);
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

                Registers.SetPointer(new FarPtr(stringPointerBase.Segment, i));
                return;
            }

            Registers.SetPointer(stringPointerBase);
        }

        /// <summary>
        ///     Find text variable & return number
        ///
        ///     Signature: int findtvar(char *name);
        /// </summary>
        private void findtvar()
        {
            var name = GetParameterString(0, true);

            //It's a system variable, set the pointer in the TXTVARS array to the 1st record,
            //which will trigger an MBBSEmu only ordinal for variable lookup
            if (_textVariableService.GetVariableIndex(name) > -1)
            {
                Registers.AX = 0;
                return;
            }

            //Check variables registered within the module
            var txtvarMemoryBase = Module.Memory.GetVariablePointer("TXTVARS");
            for (ushort i = 0; i < MaxTextVariables; i++)
            {
                var currentTextVar =
                    new TextvarStruct(Module.Memory.GetArray(txtvarMemoryBase + (i * TextvarStruct.Size),
                        TextvarStruct.Size));

                if (currentTextVar.name != name)
                    continue;

                findtvarValue = i;
                Registers.AX = i;
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
            _logger.Debug($"Disabling Character Interceptor & Enabling Echo on Channel {channelNumber}");
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
        private ReadOnlySpan<byte> syskey => Module.Memory.GetVariablePointer("*SYSKEY").Data;

        /// <summary>
        ///     "strip" blank spaces after input
        ///
        ///     Signature: void stripb(char *stg);
        /// </summary>
        private void stripb() => depad(false);

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
            var routinePointer = GetParameterPointer(0);

            var routine = new RealTimeRoutine(routinePointer);
            var routineNumber = Module.TaskRoutines.Allocate(routine);
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Registered routine {routinePointer}");
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

            if (max < min)
                max = min;

            var randomValue = _random.Next(min, max);

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
            var keynum = GetParameter(4);

            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            Registers.AX = UpdateBB(currentBtrieveFile, recordPointer, (uint)absolutePosition, acquiresData: true, (short)keynum) ? (ushort)1 : (ushort)0;
        }

        /// <summary>
        ///     Checks if an offline user has the specified key
        ///
        ///     Signature: int uidkey (char *uid, char *lock);
        /// </summary>
        private void uidkey()
        {
            var userName = GetParameterString(0, true);
            var accountLock = GetParameterString(2, true);

            if (string.IsNullOrEmpty(accountLock))
            {
                Registers.AX = 1;
                return;
            }

            IEnumerable<string> keys;

            //If the user isnt registered on the system, most likely RLOGIN -- so apply the default keys
            if (_accountRepository.GetAccountByUsername(userName) == null)
            {
                keys = _configuration.DefaultKeys;
            }
            else
            {
                var accountKeys = _accountKeyRepository.GetAccountKeysByUsername(userName);
                keys = accountKeys.Select(x => x.accountKey);
            }

            Registers.AX = (ushort)(keys.Any(k =>
               string.Equals(accountLock, k, StringComparison.InvariantCultureIgnoreCase))
                ? 1
                : 0);
#if DEBUG
            var lockName = Encoding.ASCII.GetString(Module.Memory.GetString(GetParameterPointer(0), true));
            _logger.Debug($"({Module.ModuleIdentifier}) Returning {Registers.AX} for uidkey({userName}, {lockName})");
#endif
        }

        /// <summary>
        ///     Create a new key record
        ///
        ///     Signature: void nkyrec (char *uid);
        /// </summary>
        private void nkyrec()
        {

            var uid = GetParameterString(0, stripNull: true);

#if DEBUG
            _logger.Debug($"New Key Record: {uid}");
#endif
        }

        /// <summary>
        ///     Checks if the other user has the specified key, the one specified by othusn
        ///     and othusp.
        ///
        ///     Signature: int othkey (char *lock);
        /// </summary>
        private void othkey()
        {
            var accountLock = GetParameterString(0, true);
            var userChannel = Module.Memory.GetWord("OTHUSN");

            if (string.IsNullOrEmpty(accountLock))
            {
                Registers.AX = 1;
                return;
            }

            IEnumerable<string> keys;

            //If the user isnt registered on the system, most likely RLOGIN -- so apply the default keys
            if (_accountRepository.GetAccountByUsername(ChannelDictionary[userChannel].Username) == null)
            {
                keys = _configuration.DefaultKeys;
            }
            else
            {
                var accountKeys = _accountKeyRepository.GetAccountKeysByUsername(ChannelDictionary[userChannel].Username);
                keys = accountKeys.Select(x => x.accountKey);
            }

            Registers.AX = keys.Any(k =>
                string.Equals(accountLock, k, StringComparison.InvariantCultureIgnoreCase))
                ? (ushort)1
                : (ushort)0;
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Returning {Registers.AX} for othkey({accountLock})");
#endif
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
            _logger.Debug($"({Module.ModuleIdentifier}) Received value: {outputValue}, string saved to {resultPointer}");
#endif

            Registers.SetPointer(resultPointer);
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
        ///     Teleconference
        ///
        ///     Teleconference 'join from other' rouptr
        ///
        ///     Signature: void (*tjoinrou)();
        /// </summary>
        private ReadOnlySpan<byte> tjoinrou => Module.Memory.GetVariablePointer("TJOINROU").Data;

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

            Module.Memory.SetPointer("NXTCMD", new FarPtr(inputPointer.Segment, (ushort)(inputPointer.Offset + inputStringComponents[0].Length + 1)));
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
            var accountLock = GetParameterString(0, true);
            var userChannel = GetParameter(2);

            if (string.IsNullOrEmpty(accountLock))
            {
                Registers.AX = 1;
                return;
            }

            IEnumerable<string> keys;

            //If the user isnt registered on the system, most likely RLOGIN -- so apply the default keys
            if (_accountRepository.GetAccountByUsername(ChannelDictionary[userChannel].Username) == null)
            {
                keys = _configuration.DefaultKeys;
            }
            else
            {
                var accountKeys = _accountKeyRepository.GetAccountKeysByUsername(ChannelDictionary[userChannel].Username);
                keys = accountKeys.Select(x => x.accountKey);
            }

            Registers.AX = keys.Any(k =>
                string.Equals(accountLock, k, StringComparison.InvariantCultureIgnoreCase))
                ? (ushort)1
                : (ushort)0;
#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Returning {Registers.AX} for gen_haskey({accountLock})");
#endif
        }

        /// <summary>
        ///     Expect a Word from the user (character from the current command)
        ///
        ///     cncwrd() is executed after begincnc(), which runs rstrin() replacing the null separators
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
            Registers.SetPointer(returnPointer);

            //Verify we're not at the end of the input
            if (remainingCharactersInCommand == 0 || Module.Memory.GetByte(nxtcmdPointer) == 0)
            {
                //Write null to output
                Module.Memory.SetByte(returnPointer, 0);
                return;
            }

            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);
            var returnedWord = new MemoryStream(remainingCharactersInCommand);

            //Build Return Word stopping when a space is encountered
            foreach (var b in inputString)
            {
                if (b == ' ' || b == 0)
                    break;

                returnedWord.WriteByte(b);
            }

            //Truncate to 29 bytes
            if (returnedWord.Length > 29)
                returnedWord.SetLength(29);

            returnedWord.WriteByte(0);

            Module.Memory.SetArray(returnPointer, returnedWord.ToArray());

            //Modify the Counters
            remainingCharactersInCommand -= (int)returnedWord.Length;
            nxtcmdPointer.Offset += (ushort)returnedWord.Length;

            //Advance to the next, non-space character
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            Module.Memory.SetPointer("NXTCMD", new FarPtr(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
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

            var records = currentBtrieveFile.GetRecordCount();
            Registers.DX = (ushort)(records >> 16);
            Registers.AX = (ushort)(records & 0xFFFF);
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

            if (size > ushort.MaxValue)
                throw new Exception("Allocation Exceeds Segment Size");

            var realSegmentBase = Module.Memory.AllocateRealModeSegment();

            /*
             * There is some magic in PHAPI for mapping of protected-mode segments to real-mode
             * memory addresses. It translates back that each tile is 8 segments apart.
             * Because of this, we'll go ahead and reserve the # of tiles * 8. While this is technically
             * an over allocation, it shouldn't hurt.
            */
            var numberOfSegments = (size / tsize) * 8;

            for (var i = 0; i < numberOfSegments; i++)
                Module.Memory.AllocateRealModeSegment(tsize);

            _logger.Debug($"({Module.ModuleIdentifier}) Allocated base {realSegmentBase} for {size} bytes ({numberOfSegments} segments)");

            Registers.AX = realSegmentBase.Segment;
        }

        ///     Returns the day of the week 0->6 Sunday->Saturday
        ///
        ///     Signature: int daytoday(void);
        /// </summary>
        public void daytoday()
        {
            Registers.AX = (ushort)_clock.Now.DayOfWeek;
        }

        /// <summary>
        ///     Digits allowed in User-IDs. We default this to TRUE
        ///
        ///     Signature: int digalw;
        /// </summary>
        public ReadOnlySpan<byte> digalw => Module.Memory.GetVariablePointer("DIGALW").Data;

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
            _logger.Debug($"({Module.ModuleIdentifier}) Retrieved option {msgnum} value: {outputValue}");
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
            var username = GetParameterString(0, true);

            //Scan the current channels for any usernames that match the specified one
            Registers.AX = ChannelDictionary.Any(x =>
                string.Equals(x.Value.Username, username, StringComparison.CurrentCultureIgnoreCase)) ? (ushort)1 : (ushort)0;
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

        /// <summary>
        ///     Determines if a Floating Point Co-Processor is present to handle x87 Instructions
        ///
        ///     Because the CpuCore in MBBSEmu supports x87, we'll return 3 which denotes its presence.
        ///
        ///     Values and their Definitions are:
        ///      0 - no coprocessor, or ignored because of SET 87=N.
        ///      1 - 8087 or 80187, or using coprocessor because of SET 87=Y.
        ///      2 - 80287
        ///      3 - 80387
        ///
        ///     Signature: int _RTLENTRY _EXPDATA  _8087 = 3;
        /// </summary>
        private ReadOnlySpan<byte> _8087 => Module.Memory.GetVariablePointer("_8087").Data;

        /// <summary>
        ///     Determines if the specified char is a valid User-ID Character
        ///
        ///     Because MBBSEmu doesn't put restrictions on User-ID Characters, we'll verify it's a standard,
        ///     printable ASCII value (between 32 & 126)
        ///
        ///     Signature: int isuidc(int c);
        /// </summary>
        private void isuidc()
        {
            var characterToCheck = GetParameter(0);
            Registers.AX = (ushort)(characterToCheck >= 32 && characterToCheck <= 126 ? 1 : 0);
        }

        /// <summary>
        ///     Deletes the Btrieve Record at the current Btrieve File Position
        ///
        ///     Signature: void delbtv();
        /// </summary>
        private void delbtv()
        {
            var currentBtrieveFile = BtrieveGetProcessor(Module.Memory.GetPointer("BB"));

            currentBtrieveFile.Delete();
        }

        /// <summary>
        ///     Strips non-printable ASCII characters from the specified string
        ///
        ///     Signature: char* stp4cs(char *buf);
        /// </summary>
        private void stp4cs()
        {
            var stringPointer = GetParameterPointer(0);

            var inputString = Module.Memory.GetString(stringPointer, true);

            var result = new MemoryStream(inputString.Length);

            for (var i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] >= 32 && inputString[i] <= 127)
                    result.WriteByte(inputString[i]);
            }

            result.WriteByte(0);
            Module.Memory.SetArray(stringPointer, result.ToArray());
            Registers.SetPointer(stringPointer);
        }

        /// <summary>
        ///     filelength - gets file size in bytes
        ///
        ///     Signature: long filelength(int handle);
        /// </summary>
        private void filelength()
        {
            var fileHandle = GetParameter(0);
            var result = -1L;

            if (FilePointerDictionary.TryGetValue(fileHandle, out var filePointer))
            {
                result = filePointer.Length;
            }

            Registers.DX = (ushort)(result >> 16);
            Registers.AX = (ushort)(result & 0xFFFF);
        }

        /// <summary>
        ///     Sets Secure Echo to ON for the specified number of input characters. Characters within
        ///     the specified width are echo'd as the secure character.
        ///
        ///     Signature: void echsec(char c, int width);
        /// </summary>
        private void echsec()
        {
            ChannelDictionary[ChannelNumber].EchoSecureEnabled = true;
            ChannelDictionary[ChannelNumber].ExtUsrAcc.ech = (byte)GetParameter(0);
            ChannelDictionary[ChannelNumber].ExtUsrAcc.wid = (byte)GetParameter(1);

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Setting Echo Security ON for {ChannelDictionary[ChannelNumber].ExtUsrAcc.wid} characters with the character {(char)ChannelDictionary[ChannelNumber].ExtUsrAcc.ech}");
#endif
        }

        /// <summary>
        ///     Conditional exit to parent menu for after handling concatenated commands
        ///
        ///     Signature: void condex();
        /// </summary>
        private void condex()
        {
            if (!ChannelDictionary[ChannelNumber].UsrPtr.Flags.IsFlagSet((ushort)EnumRuntimeFlags.Concex)) return;
            Registers.Halt = true;
            ChannelDictionary[ChannelNumber].Status.Clear();
            ChannelDictionary[ChannelNumber].Status.Enqueue(EnumUserStatus.UNUSED);
        }

        /// <summary>
        ///     Handle the entering of a User-Id
        ///
        ///     Signature: int hdluid(char *stg);
        /// </summary>
        private void hdluid()
        {
            var searchUserName = GetParameterString(0, true);
            var userXrefPointer = new FarPtr(Module.Memory.GetVariablePointer("UIDXRF").Data);
            var userXref = new UidxrefStruct(Module.Memory.GetArray(userXrefPointer, UidxrefStruct.Size));

            //Look up user ID
            var userAccount = _accountRepository.GetAccounts().ToList().FirstOrDefault(item => item.userName.Contains(searchUserName, StringComparison.CurrentCultureIgnoreCase));

            if (userAccount != null && searchUserName != "")
            {
                userXref.xrfstg = Encoding.ASCII.GetBytes(searchUserName + "\0");
                userXref.userid = Encoding.ASCII.GetBytes(userAccount.userName + "\0");
                Module.Memory.SetArray(userXrefPointer, userXref.Data);
                Registers.AX = 0;
            }
            else
            {
                userXref.xrfstg = Encoding.ASCII.GetBytes(searchUserName + "\0");
                userXref.userid = Encoding.ASCII.GetBytes(searchUserName + "\0");
                Module.Memory.SetArray(userXrefPointer, userXref.Data);
                Registers.AX = 0xFFFF;
            }

        }

        /// <summary>
        ///     User-id cross reference structure
        ///
        ///     Signature: struct uidxrf;
        /// </summary>
        public ReadOnlySpan<byte> uidxrf => Module.Memory.GetVariablePointer("UIDXRF").Data;

        /// <summary>
        ///     Allocate new space for a string
        ///
        ///     Signature: char *alcdup(char *stg);
        /// </summary>
        private void alcdup()
        {
            var sourceStringPointer = GetParameterPointer(0);
            var inputBuffer = Module.Memory.GetString(sourceStringPointer, stripNull: false);
            var destinationAllocatedPointer = Module.Memory.Malloc((ushort)inputBuffer.Length);

            if (sourceStringPointer.IsNull())
            {
                Module.Memory.SetByte(destinationAllocatedPointer, 0);
#if DEBUG
                _logger.Warn($"({Module.ModuleIdentifier}) Source ({sourceStringPointer}) is NULL");
#endif
            }
            else
            {
                Module.Memory.SetArray(destinationAllocatedPointer, inputBuffer);
            }

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Allocated {inputBuffer.Length} bytes starting at {destinationAllocatedPointer}");
#endif

            Registers.SetPointer(destinationAllocatedPointer);
        }

        /// <summary>
        ///     Expect a forum name, with or without '/' prefix
        ///
        ///     Signature: char *signam=cncsig();
        /// </summary>
        private void cncsig()
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
            var returnPointer = Module.Memory.GetOrAllocateVariablePointer("CNCSIG", 0xA); //max length is 10 characters
            Registers.SetPointer(returnPointer);

            //Verify we're not at the end of the input
            if (remainingCharactersInCommand == 0 || Module.Memory.GetByte(nxtcmdPointer) == 0)
            {
                //Write null to output
                Module.Memory.SetByte(returnPointer, 0);
                return;
            }

            var inputString = Module.Memory.GetArray(nxtcmdPointer, (ushort)remainingCharactersInCommand);

            //Make room for leading forward slash (SIGIDC) if missing
            if (inputString[0] != (byte)'/')
                remainingCharactersInCommand += 1;

            var returnedSig = new MemoryStream(remainingCharactersInCommand);

            //Add leading forward slash (SIGIDC) if missing
            if (inputString[0] != (byte)'/')
            {
                returnedSig.WriteByte((byte)'/');
                nxtcmdPointer--;
            }

            //Build Return Sig stopping when a space is encountered
            foreach (var b in inputString)
            {
                if (b == ' ' || b == 0)
                    break;

                returnedSig.WriteByte(b);
            }

            //Truncate to 9 bytes
            if (returnedSig.Length > 9)
            {
                returnedSig.SetLength(9);
                nxtcmdPointer--;
            }

            returnedSig.WriteByte(0);

            Module.Memory.SetArray(returnPointer, returnedSig.ToArray());

            //Modify the Counters
            remainingCharactersInCommand -= (int)returnedSig.Length;
            nxtcmdPointer.Offset += (ushort)returnedSig.Length;

            //Advance to the next, non-space character
            while (Module.Memory.GetByte(nxtcmdPointer) == ' ' && remainingCharactersInCommand > 0)
            {
                nxtcmdPointer.Offset++;
                remainingCharactersInCommand--;
            }

            Module.Memory.SetPointer("NXTCMD", new FarPtr(nxtcmdPointer.Segment, (ushort)(nxtcmdPointer.Offset)));
        }

        /// <summary>
        ///     The number of Text Variables defined in the system
        /// </summary>
        private ReadOnlySpan<byte> ntvars => Module.Memory.GetVariablePointer("NTVARS").Data;

        /// <summary>
        ///     Base address for the array of TXTVARS Structs
        /// </summary>
        private ReadOnlySpan<byte> txtvars => Module.Memory.GetVariablePointer("*TXTVARS").Data;

        /// <summary>
        ///     Technically TXTVARS is a struct, which holds the system text variables and the offset of the *(varrour)() to be executed.
        ///
        ///     We set he pointer of the delegate to this routine, which will return the value of the variable looked up by a proceeding
        ///     FINDTVAR() call
        /// </summary>
        private void txtvars_delegate()
        {
            var txtvarValue = _textVariableService.GetVariableByIndex(findtvarValue);

            var txtvarsReturnPointer = Module.Memory.GetOrAllocateVariablePointer("TXTVARS_DELEGATE", 0xFF);

            Module.Memory.SetArray(txtvarsReturnPointer, Encoding.ASCII.GetBytes(txtvarValue + '\0'));

            Registers.SetPointer(txtvarsReturnPointer);
        }

        /// <summary>
        ///     setmode - sets mode of open file
        ///
        ///     Signature: int setmode(int handle, int mode);
        ///
        /// FROM: fcntl.h:
        /// * NOTE: Text is the default even if the given _O_TEXT bit is not on. */
        /// #define _O_TEXT		0x4000	/* CR-LF in file becomes LF in memory. */
        /// #define _O_BINARY	0x8000	/* Input and output is not translated. */
        /// </summary>
        private void setmode()
        {
            var fileHandle = GetParameter(0);
            var fileMode = GetParameter(1);

            if (!FilePointerDictionary.TryGetValue(fileHandle, out var fileStreamPointer))
            {
                // TODO sets errno to ???;
                Registers.AX = 0xFFFF; // -1
                return;
            }

            var fileStructPointer = Module.Memory.GetOrAllocateVariablePointer($"FILE_{fileStreamPointer.Name}-{FilePointerDictionary.Count}", FileStruct.Size);
            var fileStruct = new FileStruct(Module.Memory.GetArray(fileStructPointer, FileStruct.Size));

            switch (fileMode)
            {
                case 0x4000: //TEXT
                    fileStruct.flags.ClearFlag((ushort)FileStruct.EnumFileFlags.Binary);
                    Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
                    Registers.AX = 0x8000;
                    break;
                case 0x8000: //BINARY
                    fileStruct.flags |= (ushort)FileStruct.EnumFileFlags.Binary;
                    Module.Memory.SetArray(fileStructPointer, fileStruct.Data);
                    Registers.AX = 0x4000;
                    break;
                default:
                    throw new Exception($"Unknown setmode: {fileMode}");
            }

#if DEBUG
            _logger.Debug($"({Module.ModuleIdentifier}) Setting mode {fileMode} for {fileStreamPointer.Name}");
#endif
        }

        /// <summary>
        ///     gettime - gets MS-DOS time
        ///
        ///     Signature: void gettime(struct time *timep);
        /// </summary>
        private void gettime()
        {
            var timePointer = GetParameterPointer(0);

            var dosTimeStruct = new TimeStruct(_clock.Now);

            Module.Memory.SetArray(timePointer, dosTimeStruct.Data);
        }

        /// <summary>
        ///     Full Screen Editor
        ///
        ///     We re-use the FSD, using a pre-defined custom template, field spec, and formats (in Assets Folder)
        /// </summary>
        private void bgnedt()
        {
            var textLength = GetParameter(0);
            var textPointer = GetParameterPointer(1);
            var topicLength = GetParameter(3);
            var topicPointer = GetParameterPointer(4);
            var onDoneEditing = GetParameterPointer(6);
            var flags = GetParameter(8);

            //This is where we'll build the Answers String to pass into the FSD Template
            var answersString = new StringBuilder($"TOPIC={Encoding.ASCII.GetString(Module.Memory.GetString(topicPointer))}", topicLength + textLength);

            //Take the input text and split it out on new lines so we can hydrate the individual lines on the template
            var rawText = Encoding.ASCII.GetString(Module.Memory.GetString(textPointer, true));
            var split = rawText.Split('\r');
            var lineNumber = 0;
            for (var i = 0; i < split.Length; i++)
            {
                if (split[i].Length < 79)
                {
                    answersString.Append($"LINE{i}={split[i]}\0");
                    lineNumber++;
                    continue;
                }

                //Too much data for one line, we need to "word wrap."
                while (split[i].Length > 79)
                {
                    answersString.Append($" LINE{lineNumber}={split[i].Substring(0, 79)}\0");
                    split[i] = split[i][79..];
                    lineNumber++;
                }
            }

            //Double Null Terminated
            answersString.Append('\0');

            //FSDROOM
            var resourceManager = new ResourceManager();

            var template = resourceManager.GetResource("MBBSEmu.Assets.fseTemplate.ans");
            var fieldSpec = resourceManager.GetResource("MBBSEmu.Assets.fseFieldSpec.txt");

            var fsdBufferPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-TemplateBuffer-{ChannelNumber}", 0x2000);
            var fsdFieldSpecPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-FieldSpec-{ChannelNumber}", 0x2000);

            //Zero out FSD Memory Areas
            Module.Memory.SetZero(fsdBufferPointer, 0x2000);
            Module.Memory.SetZero(fsdFieldSpecPointer, 0x2000);

            //Hydrate FSD Memory Areas with Values
            Module.Memory.SetArray(fsdBufferPointer, template);
            Module.Memory.SetArray(fsdFieldSpecPointer, fieldSpec);

            //Establish a new FSD Status Struct for this Channel
            var channelFsdscb = Module.Memory.GetOrAllocateVariablePointer($"FSD-Fsdscb-{ChannelNumber}", FsdscbStruct.Size);
            var newansPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-Fsdscb-{ChannelNumber}-newans", 0x400);

            //Declare flddat -- allocating enough room for up to 100 fields
            var fsdfldPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-Fsdscb-{ChannelNumber}-flddat", FsdfldStruct.Size * 100);

            var fsdStatus = new FsdscbStruct(Module.Memory.GetArray(channelFsdscb, FsdscbStruct.Size))
            {
                fldspc = fsdFieldSpecPointer,
                flddat = fsdfldPointer,
                newans = newansPointer
            };

            Module.Memory.SetArray($"FSD-Fsdscb-{ChannelNumber}", fsdStatus.Data);

            //FSDAPR
            var fsdAnswersPointer = Module.Memory.GetOrAllocateVariablePointer($"FSD-Answers-{ChannelNumber}", 0x800);
            Module.Memory.SetArray($"FSD-Answers-{ChannelNumber}", Encoding.ASCII.GetBytes(answersString.ToString()));

            //FSDBKG
            ChannelDictionary[ChannelNumber].SendToClient("\x1B[0m\x1B[2J\x1B[0m"); //FSDBBS.C
            ChannelDictionary[ChannelNumber].SendToClient(FormatNewLineCarriageReturn(template));

            //FSDEGO
            ChannelDictionary[ChannelNumber].SessionState = EnumSessionState.EnteringFullScreenEditor;

            //Update fsdscb struct
            var fsdscbStructPointer = Module.Memory.GetVariablePointer($"FSD-Fsdscb-{ChannelNumber}");
            var fsdscbStruct = new FsdscbStruct(Module.Memory.GetArray(fsdscbStructPointer, FsdscbStruct.Size));

            Module.Memory.SetArray(fsdscbStructPointer, fsdscbStruct.Data);

            //Set Exit Routine
            var fsdWhenDoneRoutine = Module.Memory.GetOrAllocateVariablePointer($"FSD-WhenDoneRoutine-{ChannelNumber}", FarPtr.Size);
            Module.Memory.SetPointer(fsdWhenDoneRoutine, onDoneEditing);

            //Set Pointers for Topic & Text
            Module.Memory.GetOrAllocateVariablePointer($"FSE-TextPointer-{ChannelNumber}", FarPtr.Size);
            Module.Memory.SetPointer($"FSE-TextPointer-{ChannelNumber}", textPointer);
            Module.Memory.GetOrAllocateVariablePointer($"FSE-TopicPointer-{ChannelNumber}", FarPtr.Size);
            Module.Memory.SetPointer($"FSE-TopicPointer-{ChannelNumber}", topicPointer);

#if DEBUG
            _logger.Debug($"Channel {ChannelNumber} entering Full Screen Editor");
#endif

            //Because control has been handed over to the FSD, we need to stop execution of the module routine
            Registers.Halt = true;
        }

        /// <summary>
        ///     Expect a Variable Length User-ID (only valid User-ID Characters up to a valid User-ID Length)
        ///
        ///     Signature: char *cncuid(void)
        /// </summary>
        private void cncuid()
        {
            //gets the "next" pointer for the input string
            var nxtcmdPointer = Module.Memory.GetPointer("NXTCMD");

            //create return pointer and zero it out
            var returnPointer = Module.Memory.GetOrAllocateVariablePointer("CNCUID", UserAccount.UIDSIZ);
            Module.Memory.SetZero(returnPointer, UserAccount.UIDSIZ);

            //Iterate through nxtcmd until we hit an invalid character or reach the limit of the uid number of characters
            for (ushort i = 0; i < UserAccount.UIDSIZ; i++)
            {
                //Evaluate Value at pointer, and continue if a valid printable character
                if (Module.Memory.GetByte(nxtcmdPointer + i) is >= 32 and <= 126)
                    continue;

                //Set our Return Values
                Module.Memory.SetArray(returnPointer, Module.Memory.GetArray(nxtcmdPointer, i));
                Module.Memory.SetPointer("NXTCMD", nxtcmdPointer + i);
                break;
            }

            //Sets DX:AX registers to the return value
            Registers.SetPointer(returnPointer);
        }

        /// <summary>
        /// Sets the INPLEN variable based on the length of INPUT
        /// </summary>
        /// <returns>The value written to INPLEN</returns>
        private ushort setINPLEN() => setINPLEN(Module.Memory.GetVariablePointer("INPUT"));

        /// <summary>
        /// Sets the INPLEN variable based on the length of the string from input
        /// </summary>
        /// <param name="input">Null-terminated string pointer</param>
        /// <returns>The value written to INPLEN</returns>
        private ushort setINPLEN(FarPtr input)
        {
            var sz = (ushort)Module.Memory.GetString(input, true).Length;

            Module.Memory.SetWord("INPLEN", sz);

            return sz;
        }

        /// <summary>
        ///     Returns Pointer to the HDLCON Routine
        /// </summary>
        private ReadOnlySpan<byte> hdlcon => Module.Memory.GetVariablePointer("HDLCON").Data;

        /// <summary>
        ///     Sets the Seed for the Pseudo-Random Number Generator
        ///
        ///     While the seed stored in memory is a 32-bit long, only the lower 16-bits are set
        ///     via the srand() method. The high 16-bits are always set to zero.
        ///
        ///     Signature: void srand(int seed)
        /// </summary>
        private void srand()
        {
            var randomSeed = GetParameter(0);

            Module.Memory.SetDWord("RANDSEED", randomSeed);
        }
    }
}

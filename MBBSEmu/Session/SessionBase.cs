using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Server;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.Session
{

    /// <summary>
    ///     Base Class for a User Session
    ///
    ///     This holds the basics for any given user session, and different source
    ///     (web, telnet, console, etc.) can implement this base.
    /// </summary>
    public abstract class SessionBase : IStoppable
    {
        public const int DEFAULT_TERMINAL_COLUMNS = 80;

        protected delegate void SendToClientDelegate(byte[] dataToSend);

        private SendToClientDelegate _sendToClientMethod;

        protected SendToClientDelegate SendToClientMethod
        {
            get => _sendToClientMethod;
            set
            {
                _sendToClientMethod = value;
                _lineBreaker.SendToClientMethod = value.Invoke;
            }
        }

        /// <summary>
        ///     Specifies the Type of Session the user is currently using
        /// </summary>
        public EnumSessionType SessionType { get; set; }

        /// <summary>
        ///     Unique ID for this Session
        /// </summary>
        public readonly string SessionId;

        /// <summary>
        ///     This Users UsrPtr* which is passed in from MajorBBS
        /// </summary>
        public User UsrPtr { get; set; }

        /// <summary>
        ///     This Users UsrAcc* which is pass in from MajorBBS
        /// </summary>
        public UserAccount UsrAcc { get; set; }

        /// <summary>
        ///     This Users Extusr* which is passed in from MajorMBBS
        /// </summary>
        public ExtUser ExtUsrAcc { get; set; }

        /// <summary>
        ///     This Users Number/Channel Number (used to identify target for output)
        /// </summary>
        public ushort Channel { get; set; }

        /// <summary>
        ///     Current Module the user is in
        /// </summary>
        public MbbsModule CurrentModule { get; set; }

        /// <summary>
        ///     MajorBBS User Status
        /// </summary>
        public Queue<EnumUserStatus> Status { get; set; }

        private EnumSessionState _enumSessionState;

        public event EventHandler<EnumSessionState> OnSessionStateChanged;

        public EnumSessionState SessionState {
            get => _enumSessionState;
            set {
                _enumSessionState = value;
                OnSessionStateChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        ///     GSBL Echo Buffer
        ///
        ///     Used to send data "promptly" to the user, bypassing MajorBBS and
        ///     sending the data straight through GSBL
        /// </summary>
        public MemoryStream EchoBuffer { get; set; }

        /// <summary>
        ///     Buffer of Data Received from the Client, unparsed
        /// </summary>
        public MemoryStream InputBuffer { get; set; }

        /// <summary>
        ///     Parsed Command Input
        /// </summary>
        public byte[] InputCommand { get; set; }

        /// <summary>
        ///     Last Character Received from the Client
        /// </summary>
        public byte CharacterReceived { get; set; }

        /// <summary>
        ///     Last Character Received after Processing
        /// </summary>
        public byte CharacterProcessed { get; set; }

        public FarPtr CharacterInterceptor { get; set; }

        /// <summary>
        ///     Routine that is called by BEGIN_POLLING for this user number
        /// </summary>
        public FarPtr PollingRoutine { get; set; }

        /// <summary>
        ///     Prompt Character set by BTUPMT
        /// </summary>
        public byte PromptCharacter { get; set; }

        /// <summary>
        ///     Queue to hold data to be sent async to Client
        /// </summary>
        public BlockingCollection<byte[]> DataToClient { get; set; }

        public BlockingCollection<byte> DataFromClient { get; set; }

        /// <summary>
        ///     Specified that there is data to be processed from this channel
        /// </summary>
        public bool DataToProcess { get; set; }

        /// <summary>
        ///     Specifies if User Input should be Echo'd back or not
        /// </summary>
        public bool TransparentMode { get; set; }

        /// <summary>
        ///     Specifies if a channel is monitored or not, specifically via BTUMON
        /// </summary>
        public bool Monitored { get; set; }

        /// <summary>
        ///     Specifies if a channel is monitored or not, specifically via BTUMON2
        /// </summary>
        public bool Monitored2 { get; set; }

        /// <summary>
        ///     Helper Method to get Username from the UsrAcc struct
        /// </summary>
        public string Username
        {
            get => UsrAcc.GetUserId();
            set => UsrAcc.SetUserId(value);
        }

        /// <summary>
        ///     Users Password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     Users Email Address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Tracks how long a user has been online
        /// </summary>
        public Stopwatch SessionTimer { get; set; }

        /// <summary>
        ///     If enabled, allows data to be sent to the client
        /// </summary>
        public bool OutputEnabled { get; set; }

        /// <summary>
        ///     If enabled, Status 5 is generated after output has completed
        /// </summary>
        public bool OutputEmptyStatus { get; set; }

        /// <summary>
        ///     Specifies if a user is locked out from sending input (GSBL)
        /// </summary>
        public bool InputLockout { get; set; }

        /// <summary>
        ///     Specifies if btuchi() should be invoked in output buffer is empty (GSBL - btuche)
        ///
        ///     Gets set once the output buffer is empty
        /// </summary>
        public bool EchoEmptyInvoke { get; set; }

        /// <summary>
        ///     Specifies if btuchi() should be invoked in output buffer is empty (GSBL - btuche)
        /// </summary>
        public bool EchoEmptyInvokeEnabled { get; set; }

        /// <summary>
        ///     Specifies if ECHSEC has been set/still enabled
        /// </summary>
        public bool EchoSecureEnabled { get; set; }

        /// <summary>
        ///     Volatile Data for this specific Channel/Session
        /// </summary>
        public byte[] VDA { get; set; }

        public Dictionary<string, TextVariableValue.TextVariableValueDelegate> SessionVariables;

        private readonly ITextVariableService _textVariableService;

        private readonly LineBreaker _lineBreaker = new LineBreaker();

        protected readonly IMbbsHost _mbbsHost;

        public int TerminalColumns { get; set; }

        private int _wordWrapWidth = 0;
        public int WordWrapWidth
        {
            get => _wordWrapWidth;
            set
            {
                var changed = _wordWrapWidth != value;
                _wordWrapWidth = value;
                if (changed)
                {
                    _lineBreaker.Reset();
                }
                if (value > 0)
                {
                    _lineBreaker.WordWrapWidth = value;
                }
            }
        }

        /// <summary>
        /// Breaks buffer into lines and calls SendToClientMethod afterwards.
        /// </summary>
        /// <param name="buffer">Raw output buffer going to a client</param>
        private void SendBreakingIntoLines(byte[] buffer)
        {
            if (WordWrapWidth > 0)
            {
                _lineBreaker.SendBreakingIntoLines(buffer);
            }
            else
            {
                SendToClientMethod(buffer);
            }
        }

        /// <summary>
        ///     Helper Method to send data to the client synchronously
        /// </summary>
        /// <param name="dataToSend"></param>
        public void SendToClient(byte[] dataToSend)
        {
            if (!OutputEnabled) return;

            if (_textVariableService == null)
            {
                SendBreakingIntoLines(dataToSend.Where(shouldSendToClient).ToArray());
            }
            else
            {
                var dataToSendSpan = new ReadOnlySpan<byte>(dataToSend);
                var dataToSendProcessed = _textVariableService?.Parse(dataToSendSpan, SessionVariables).ToArray();

                SendBreakingIntoLines(dataToSendProcessed.Where(shouldSendToClient).ToArray());
            }

            if (OutputEmptyStatus && DataToClient.Count == 0)
            {
                //Only queue up the event if there's not one already in the buffer
                if(!Status.Contains(EnumUserStatus.OUTPUT_BUFFER_EMPTY) || GetStatus() == EnumUserStatus.OUTPUT_BUFFER_EMPTY && Status.Count(x=> x == EnumUserStatus.OUTPUT_BUFFER_EMPTY) == 1)
                    Status.Enqueue(EnumUserStatus.OUTPUT_BUFFER_EMPTY);
            }
        }

        public void SendToClient(string dataToSend) => SendToClient(Encoding.ASCII.GetBytes(dataToSend));

        public void SendToClient(ReadOnlySpan<byte> dataToSend) => SendToClient(dataToSend.ToArray());

        /// <summary>
        ///     Helper Method to send data to the client synchronously without checking text variables
        /// </summary>
        /// <param name="dataToSend"></param>
        public void SendToClientRaw(byte[] dataToSend)
        {
            if (OutputEnabled)
                SendBreakingIntoLines(dataToSend.Where(shouldSendToClient).ToArray());
        }

        public abstract void Stop();

        protected SessionBase(IMbbsHost mbbsHost, string sessionId, EnumSessionState startingSessionState, ITextVariableService textVariableService)
        {
            _mbbsHost = mbbsHost;
            _textVariableService = textVariableService;
            SessionId = sessionId;
            TerminalColumns = DEFAULT_TERMINAL_COLUMNS;
            UsrPtr = new User();
            UsrAcc = new UserAccount();
            ExtUsrAcc = new ExtUser();
            Status = new Queue<EnumUserStatus>();
            SessionTimer = new Stopwatch();
            DataToClient = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            DataFromClient = new BlockingCollection<byte>(new ConcurrentQueue<byte>());
            OutputEnabled = true;
            EchoBuffer = new MemoryStream(1024);
            InputBuffer = new MemoryStream(1024);
            InputCommand = new byte[] { 0x0 };
            VDA = new byte[Majorbbs.VOLATILE_DATA_SIZE];
            _enumSessionState = startingSessionState;
            SessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
            {
                {"CHANNEL", () => Channel.ToString()},
                {"USERID", () => Username},
                {"BAUD", () => UsrPtr.Baud.ToString() },
                {"TIME_ONLINE", () => SessionTimer.Elapsed.ToString("hh\\:mm\\:ss") },
                {"CREDITS", () => UsrAcc.creds.ToString() },
                {"CREATION_DATE", () => UsrAcc.credat != 0 ? UsrAcc.credat.FromDosDate().ToShortDateString() : mbbsHost.Clock.Now.ToShortDateString()},
                {"PAGE", () =>
                    {
                        var sessionInfo = _enumSessionState.GetSessionState();

                        return sessionInfo.moduleSession ? CurrentModule.ModuleDescription : sessionInfo.UserOptionSelected;
                    }
                }
            };

            OnSessionStateChanged += (_, _) => mbbsHost.TriggerProcessing();
        }

        public void ProcessDataFromClient()
        {
            if (!DataFromClient.TryTake(out var clientData, TimeSpan.FromSeconds(0)))
                return;

            CharacterReceived = clientData;
            CharacterProcessed = clientData;

            DataToProcess = true;
        }

        private static readonly bool[] IS_CHARACTER_PRINTABLE = CreatePrintableCharacterArray();

        private static bool[] CreatePrintableCharacterArray()
        {
            var printableCharacters = Enumerable.Repeat(true, 256).ToArray();

            printableCharacters[0] = false; // \0
            printableCharacters[1] = false; // mmud crap
            printableCharacters[2] = false;
            printableCharacters[3] = false;
            printableCharacters[17] = false; // DC1   used by T-LORD
            printableCharacters[18] = false; // DC2
            printableCharacters[19] = false; // DC3
            printableCharacters[20] = false; // DC4
            printableCharacters[255] = false; // 0xFF, make Telnet happier

            return printableCharacters;
        }

        public static bool shouldSendToClient(byte b) => IS_CHARACTER_PRINTABLE[b];

        /// <summary>
        ///     Safe Method for returning Status of a Channel
        ///
        ///     If the FIFO Queue is Empty, we return 1 (OK)
        /// </summary>
        /// <returns></returns>
        public EnumUserStatus GetStatus() => Status.Count == 0 ? EnumUserStatus.RINGING : Status.Peek();
    }
}

using System;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Server;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MBBSEmu.Session.Enums;

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
        protected delegate void SendToClientDelegate(byte[] dataToSend);

        protected SendToClientDelegate SendToClientMethod;

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
        public ushort Status { get; set; }

        /// <summary>
        ///     Status State has been changes
        /// </summary>
        public bool StatusChange { get; set; }

        /// <summary>
        ///     Current State of this Users Session
        /// </summary>
        public EnumSessionState SessionState { get; set; }

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
        public byte LastCharacterReceived { get; set; }

        public IntPtr16 CharacterInterceptor { get; set; }

        /// <summary>
        ///     Routine that is called by BEGIN_POLLING for this user number
        /// </summary>
        public IntPtr16 PollingRoutine { get; set; }

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
        ///     Helper Method to enqueue data to be sent to the client Async
        /// </summary>
        /// <param name="dataToSend"></param>
        public bool SendToClientAsync(byte[] dataToSend)
        {
            if (OutputEnabled)
            {
                return DataToClient.TryAdd(dataToSend.Where(shouldSendToClient).ToArray());
            }
            return true;
        }

        /// <summary>
        ///     Helper Method to send data to the client synchronously
        /// </summary>
        /// <param name="dataToSend"></param>
        public void SendToClient(byte[] dataToSend)
        {
            if (OutputEnabled)
            {
                SendToClientMethod(dataToSend.Where(shouldSendToClient).ToArray());
            }
        }

        public void SendToClient(string dataToSend) => SendToClient(Encoding.ASCII.GetBytes(dataToSend));

        public void SendToClient(ReadOnlySpan<byte> dataToSend) => SendToClient(dataToSend.ToArray());

        public abstract void Stop();

        protected SessionBase(string sessionId)
        {
            SessionId = sessionId;
            UsrPtr = new User();
            UsrAcc = new UserAccount();
            ExtUsrAcc = new ExtUser();
            Status = 0;
            SessionTimer = new Stopwatch();
            DataToClient = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            DataFromClient = new BlockingCollection<byte>(new ConcurrentQueue<byte>());
            OutputEnabled = true;
            EchoBuffer = new MemoryStream();
            InputBuffer = new MemoryStream();

            InputCommand = new byte[] { 0x0 };
        }

        public void ProcessDataFromClient()
        {
            if (!DataFromClient.TryTake(out var clientData, TimeSpan.FromSeconds(0)))
                return;

            LastCharacterReceived = clientData;

            //Handling Incoming Characters
            switch (clientData)
            {
                //Backspace
                case 127 when SessionState == EnumSessionState.InModule:
                case 0x8 when SessionState == EnumSessionState.InModule:
                    {
                        if (InputBuffer.Length > 0)
                        {
                            SendToClient(new byte[] { 0x08, 0x20, 0x08 });
                            InputBuffer.SetLength(InputBuffer.Length - 1);
                        }
                        break;
                    }

                //Enter or Return
                case 0xD when !TransparentMode && SessionState != EnumSessionState.InFullScreenDisplay:
                    {
                        //Set Status == 3, which means there is a Command Ready
                        SendToClient(new byte[] { 0xD, 0xA });

                        //If there's a character interceptor, don't write the CR to the buffer,
                        //only mark that there is data ready and it'll process it from LastCharacterReceived
                        if (CharacterInterceptor != null)
                        {
                            DataToProcess = true;
                        }
                        else
                        {
                            Status = 3;
                        }

                        break;
                    }
                //Ignore Linefeed
                case 0xA:
                    break;
                default:
                    {
                        InputBuffer.WriteByte(clientData);
                        DataToProcess = true;
                        break;
                    }
            }
        }

        private static readonly bool[] IS_CHARACTER_PRINTABLE = CreatePrintableCharacterArray();

        private static bool[] CreatePrintableCharacterArray()
        {
            bool[] printableCharacters = Enumerable.Repeat(true, 256).ToArray();

            printableCharacters[0] = false; // \0
            printableCharacters[17] = false; // DC1   used by T-LORD
            printableCharacters[18] = false; // DC2
            printableCharacters[19] = false; // DC3
            printableCharacters[20] = false; // DC4
            printableCharacters[255] = false; // 0xFF, make Telnet happier

            return printableCharacters;
        }

        public static bool shouldSendToClient(byte b) {
            return IS_CHARACTER_PRINTABLE[b];
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;

namespace MBBSEmu.Session
{

    /// <summary>
    ///     Base Class for a User Session
    ///
    ///     This holds the basics for any given user session, and different source
    ///     (web, telnet, console, etc.) can implement this base.
    /// </summary>
    public abstract class UserSession
    {
        /// <summary>
        ///     Unique ID for this Session
        /// </summary>
        public readonly string SessionId;

        /// <summary>
        ///     This Users UsrPtr* which is passed in from MajorBBS
        /// </summary>
        public User UsrPtr;

        /// <summary>
        ///     This Users UsrAcc* which is pass in from MajorBBS
        /// </summary>
        public UserAccount UsrAcc;
        
        /// <summary>
        ///     This Users Number/Channel Number (used to identify target for output)
        /// </summary>
        public ushort Channel;

        /// <summary>
        ///     Module Identifier of the current Module the user is in
        /// </summary>
        public string ModuleIdentifier;

        /// <summary>
        ///     MajorBBS User Status
        /// </summary>
        public ushort Status;

        /// <summary>
        ///     Status State has been changes
        /// </summary>
        public bool StatusChange;

        /// <summary>
        ///     Input Queue of Data from the Client
        /// </summary>
        public Queue<byte[]> DataFromClient;

        /// <summary>
        ///     Output Queue of Data to the Client
        /// </summary>
        public Queue<byte[]> DataToClient;

        /// <summary>
        ///     Current State of this Users Session
        /// </summary>
        public EnumSessionState SessionState;

        /// <summary>
        ///     GSBL Echo Buffer
        ///
        ///     Used to send data "promptly" to the user, bypassing MajorBBS and
        ///     sending the data straight through GSBL
        /// </summary>
        public MemoryStream EchoBuffer;

        public MemoryStream InputBuffer;

        public IntPtr16 CharacterInterceptor;

        public string Username
        {
            get => UsrAcc.GetUserId();
            set => UsrAcc.SetUserId(value);
        }

        public string Password;

        public string email;

        public Stopwatch SessionTimer;

        protected UserSession(string sessionId)
        {
            SessionId = sessionId;
            DataFromClient = new Queue<byte[]>();
            DataToClient = new Queue<byte[]>();
            UsrPtr = new User();
            UsrAcc = new UserAccount();
            Status = 0;
            EchoBuffer = new MemoryStream(256);
            InputBuffer = new MemoryStream(256);
            SessionTimer = new Stopwatch();
        }
    }
}
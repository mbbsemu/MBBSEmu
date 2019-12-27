using MBBSEmu.HostProcess.Models;
using MBBSEmu.Extensions;
using System.Collections.Generic;
using System.IO;

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
        public User UsrPrt;
        
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

        public string Username;

        public string Password;

        protected UserSession(string sessionId)
        {
            SessionId = sessionId;
            DataFromClient = new Queue<byte[]>();
            DataToClient = new Queue<byte[]>();
            UsrPrt = new User();
            Status = 0;
            EchoBuffer = new MemoryStream(256);
            InputBuffer = new MemoryStream(256);
        }
    }
}
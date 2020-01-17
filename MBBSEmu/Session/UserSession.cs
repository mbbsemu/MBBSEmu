using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;

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
        public UserAccount UsrAcc { get; set; }

        /// <summary>
        ///     This Users Number/Channel Number (used to identify target for output)
        /// </summary>
        public ushort Channel;

        /// <summary>
        ///     Current Module the user is in
        /// </summary>
        public MbbsModule CurrentModule;

        /// <summary>
        ///     MajorBBS User Status
        /// </summary>
        public ushort Status;

        /// <summary>
        ///     Status State has been changes
        /// </summary>
        public bool StatusChange;

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

        /// <summary>
        ///     Buffer of Data Received from the Client, unparsed
        /// </summary>
        public MemoryStream InputBuffer;

        /// <summary>
        ///     Parsed Command Input
        /// </summary>
        public byte[] InputCommand;

        /// <summary>
        ///     Last Character Received from the Client
        /// </summary>
        public byte LastCharacterReceived;

        public IntPtr16 CharacterInterceptor;

        public int mArgCount;

        public List<int> mArgv;

        public List<int> mArgn;

        public readonly MemoryStream DataToClient;

        public bool DataToProcess;

        public bool TransparentMode;

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
            UsrPtr = new User();
            UsrAcc = new UserAccount();
            Status = 0;
            SessionTimer = new Stopwatch();
            DataToClient = new MemoryStream();

            EchoBuffer = new MemoryStream();
            InputBuffer = new MemoryStream();

            mArgn = new List<int>();
            mArgv = new List<int>();
        }

        /// <summary>
        ///     Routine Parses the data in the input buffer, builds the command input string
        ///     and assembles all the mArgs
        /// </summary>
        public void parsin()
        {
            InputBuffer.Position = 0;
            InputCommand = new byte[InputBuffer.Length];
            mArgv.Clear();
            mArgn.Clear();

            mArgv.Add(0);
            //Input Command has spaces replaced by null characters
            for (ushort i = 0; i < InputBuffer.Length; i++)
            {
                var inputByte = InputBuffer.ReadByte();

                //We only parse command character on space, otherwise just copy
                if (inputByte != 0x20)
                {
                    InputCommand[i] = (byte)inputByte;
                    continue;
                }

                //Replace the space with null
                InputCommand[i] = 0x0;

                mArgn.Add(i);

                if (i + 1 < InputBuffer.Length)
                    mArgv.Add(i + 1);
            }
            mArgn.Add((int) (InputBuffer.Length -1));
            mArgCount = mArgn.Count;
        }

        /// <summary>
        ///     Restores the Input Command back to it's unparsed date (null's to spaces)
        /// </summary>
        public void rstrin()
        {
            for (var i = 0; i < InputCommand.Length - 1; i++)
            {
                if (InputCommand[i] == 0x0)
                    InputCommand[i] = 0x20;
            }
        }
    }
}
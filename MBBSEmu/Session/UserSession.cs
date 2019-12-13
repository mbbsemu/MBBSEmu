using MBBSEmu.HostProcess.Models;
using System;
using System.Collections.Generic;

namespace MBBSEmu.Session
{
    public class UserSession
    {
        /// <summary>
        ///     Unique ID for this Session
        /// </summary>
        public readonly Guid SessionId;
        
        /// <summary>
        ///     Module this session is currently in
        /// </summary>
        private int ModuleId { get; set; }

        /// <summary>
        ///     This Users UsrPtr* which is passed in from MajorBBS
        /// </summary>
        public User UsrPrt;
        
        /// <summary>
        ///     This Users Number/Channel Number (used to identify target for output)
        /// </summary>
        public ushort Channel;

        public Queue<byte[]> DataFromClient;
        public Queue<byte[]> DataToClient;

        public UserSession()
        {
            SessionId = Guid.NewGuid();
            DataFromClient = new Queue<byte[]>();
            DataToClient = new Queue<byte[]>();
            UsrPrt = new User();
        }

        public void SendToClient(ReadOnlySpan<byte> dataToSend)
        {
            DataToClient.Enqueue(dataToSend.ToArray());
        }

        public byte[] ReceiveFromClient()
        {
            var isData = DataFromClient.TryDequeue(out var result);

            return isData ? result : null;
        }
    }
}

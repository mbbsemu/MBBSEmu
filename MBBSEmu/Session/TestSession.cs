using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Session.Enums;
using NLog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MBBSEmu.Session
{
    public class TestSession : SessionBase
    {
        private BlockingCollection<byte> _data = new BlockingCollection<byte>();

        public TestSession(IMbbsHost host) : base("test")
        {
            SendToClientMethod = Send;
            OutputEnabled = true;

            CurrentModule = host.GetModule("MBBSEMU");

            SessionType = EnumSessionType.Test;
            SessionState = EnumSessionState.EnteringModule;
        }

        public override void Stop() {}

        public string GetLine()
        {
            return GetLine('\n').Trim('\r', '\n');
        }

        public string GetLine(char endingCharacter)
        {
            var line = new MemoryStream();
            while (true)
            {
                var b = _data.Take();

                line.WriteByte(b);

                if (b == endingCharacter)
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(line.ToArray());
        }

        /// <summary>
        ///     Sends data to the connected session
        /// </summary>
        /// <param name="dataToSend"></param>
        public virtual void Send(byte[] dataToSend)
        {
            Console.Write(Encoding.ASCII.GetString(dataToSend));

            foreach(byte b in dataToSend)
            {
              _data.Add(b);
            }
        }

        public void SendToModule(byte[] dataToSend)
        {
            foreach(byte b in dataToSend)
            {
                DataFromClient.Add(b);
            }
        }
    }
}
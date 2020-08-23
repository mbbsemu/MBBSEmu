using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using MBBSEmu.Extensions;
using MBBSEmu.Session.Enums;

namespace MBBSEmu.Session
{
    public abstract class SocketSession : SessionBase
    {
        protected readonly ILogger _logger;

        protected readonly Socket _socket;
        protected readonly Thread _senderThread;
        protected readonly byte[] _socketReceiveBuffer = new byte[9000];
        protected readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        protected SocketSession(Socket socket) : base(socket.RemoteEndPoint.ToString())
        {
            _logger = ServiceResolver.GetService<ILogger>();

            _socket = socket;
            _socket.ReceiveTimeout = (1000 * 60) * 5; //5 Minutes
            _socket.ReceiveBufferSize = _socketReceiveBuffer.Length;
            _socket.SendBufferSize = 64 * 1024;
            _socket.Blocking = true;

            _senderThread = new Thread(SendWorker);
            _senderThread.Start();

            SendToClientMethod = Send;
        }

        public void Start()
        {
            ListenForData();
        }

        public override void Stop()
        {
            CloseSocket("Forced Stop");
        }

        /// <summary>
        ///     Sends data to the connected session
        /// </summary>
        /// <param name="dataToSend"></param>
        public virtual void Send(byte[] dataToSend)
        {
            try
            {
                _socket.Send(dataToSend, SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);
            }
            catch (ObjectDisposedException)
            {
                _logger.Warn($"Channel {Channel}: Attempted to write on a disposed socket");
            }
        }

        // TODO
        protected virtual void PreSend()
        {

        }

        /// <summary>
        ///     Worker for Thread Responsible for Dequeuing data to be sent to the client
        /// </summary>
        private void SendWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _socket.Connected)
            {
                bool tookData;
                byte[] dataToSend;
                try
                {
                    tookData = DataToClient.TryTake(out dataToSend, 500, _cancellationTokenSource.Token);
                } catch (Exception) // either ObjectDisposedException | OperationCanceledException
                {
                    return;
                }

                if (SessionState == EnumSessionState.LoggingOffProcessing)
                {
                    CloseSocket("User-initiated log off");
                    return;
                }

                PreSend();

                if (EchoEmptyInvokeEnabled && DataToClient.Count == 0)
                    EchoEmptyInvoke = true;

                if (!tookData)
                {
                    if (!Heartbeat()) {
                        break;
                    }
                    continue;
                }

                Send(dataToSend);
            }

            //Cleanup if the connection was dropped
            CloseSocket("sending thread natural completion");
        }

        protected virtual bool Heartbeat()
        {
            return true;
        }

        /// <summary>
        ///     Worker for Thread Responsible for Receiving Data from Client
        /// </summary>
        protected void OnReceiveData(IAsyncResult asyncResult)
        {
            int bytesReceived;
            SocketError socketError;
            try {
                bytesReceived = _socket.EndReceive(asyncResult, out socketError);
                if (bytesReceived == 0) {
                    CloseSocket("Client disconnected");
                    return;
                }
            } catch (ObjectDisposedException) {
                SessionState = EnumSessionState.LoggedOff;
                return;
            }

            ValidateSocketState(socketError);
            ProcessIncomingClientData(bytesReceived);
            ListenForData();
        }

        protected abstract (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived);

        protected void ProcessIncomingClientData(int bytesReceived)
        {
            if (bytesReceived == 0 || SessionState == EnumSessionState.LoggedOff) {
                return;
            }

            var (dataFromClient, length) = ProcessIncomingClientData(_socketReceiveBuffer, bytesReceived);

            //Enqueue the incoming bytes for processing
            for (var i = 0; i < length; i++)
                DataFromClient.Add(dataFromClient[i]);
        }

        /// <summary>
        ///     Validates SocketError returned from an operation doesn't put the socket in an error state
        /// </summary>
        /// <param name="socketError"></param>
        protected void ValidateSocketState(SocketError socketError)
        {
            if (socketError != SocketError.Success) {
                CloseSocket($"socket error: {socketError}");
            }
        }

        protected void ListenForData() {
            if (_socket.Connected && SessionState != EnumSessionState.LoggedOff) {
                _socket.BeginReceive(_socketReceiveBuffer, 0, _socketReceiveBuffer.Length, SocketFlags.None, OnReceiveData, this);
            }
        }

        protected void CloseSocket(string reason) {
            if (SessionState != EnumSessionState.LoggedOff) {
                _logger.Warn($"Session {SessionId} (Channel: {Channel}) {reason}");
            }

            _socket.Close();
            SessionState = EnumSessionState.LoggedOff;

            // send signal to the send thread to abort
            _cancellationTokenSource.Cancel();
        }
    }
}

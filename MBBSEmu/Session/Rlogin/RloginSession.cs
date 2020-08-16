using MBBSEmu.DependencyInjection;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using NLog;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MBBSEmu.Session.Rlogin
{
    /// <summary>
    ///     Class for handling inbound RLogin Connections
    /// </summary>
    public class RloginSession : SessionBase
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;

        private readonly Socket _rloginConnection;
        private readonly Thread _sendThread;
        private readonly Thread _receiveThread;
        private readonly byte[] _socketReceiveBuffer = new byte[256];
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public readonly string ModuleIdentifier;

        public RloginSession(Socket rloginConnection, string moduleIdentifier = null) : base(rloginConnection.RemoteEndPoint.ToString())
        {
            ModuleIdentifier = moduleIdentifier;
            SessionType = EnumSessionType.Rlogin;
            SendToClientMethod = Send;
            _host = ServiceResolver.GetService<IMbbsHost>();
            _logger = ServiceResolver.GetService<ILogger>();
            _rloginConnection = rloginConnection;
            _rloginConnection.Blocking = true;
            _rloginConnection.ReceiveTimeout = (1000 * 60) * 5;
            _rloginConnection.ReceiveBufferSize = 128;
            SessionState = EnumSessionState.Negotiating;

            //Start Listeners & Senders
            _sendThread = new Thread(SendWorker);
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveWorker);
            _receiveThread.Start();

            //Add this Session to the Host
            _host.AddSession(this);
        }

        public override void Stop() {
            SessionState = EnumSessionState.LoggedOff;
            _rloginConnection.Close();
            _cancellationTokenSource.Cancel();
            TransparentMode = false;
        }

        /// <summary>
        ///     Method used to directly send data out to the connected socket
        /// </summary>
        /// <param name="dataToSend"></param>
        public void Send(byte[] dataToSend)
        {
            try
            {
                using var msOutputBuffer = new MemoryStream();

                foreach (var b in dataToSend)
                {
                    //When we're in a module, since new lines are stripped from MVC's etc, and only
                    //carriage returns remain, IF we're in a module and encounter a CR, add a NL
                    if (SessionState == EnumSessionState.EnteringModule || SessionState == EnumSessionState.InModule ||
                        SessionState == EnumSessionState.LoginRoutines)
                    {
                        switch (b)
                        {
                            case 0xD:
                                msOutputBuffer.WriteByte(0xA);
                                break;
                            case 0xA:
                                msOutputBuffer.WriteByte(0xD);
                                break;
                            case 0xFF:
                                msOutputBuffer.WriteByte(0xFF);
                                break;
                            case 0x13:
                                continue;
                        }
                    }

                    msOutputBuffer.WriteByte(b);
                }

                _rloginConnection.Send(msOutputBuffer.ToArray(), SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);
            }
            catch (ObjectDisposedException)
            {
                _logger.Warn($"Channel {Channel}: Attempted to write on a disposed socket");
            }
        }

        /// <summary>
        ///     Thread to handle sending data out to the client
        /// </summary>
        private void SendWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _rloginConnection.IsConnected())
            {
                try {
                    while (DataToClient.TryTake(out var dataToSend, /* forever */ -1, _cancellationTokenSource.Token))
                    {
                        Send(dataToSend);
                    }
                }
                catch (Exception) // either ObjectDisposedException | OperationCanceledException
                {
                    break;
                }

                if (SessionState == EnumSessionState.LoggingOffProcessing)
                {
                    SessionState = EnumSessionState.LoggedOff;
                    return;
                }

                if (EchoEmptyInvokeEnabled && DataToClient.Count == 0)
                    EchoEmptyInvoke = true;
            }

            //Cleanup if the connection was dropped
            Stop();
        }

        /// <summary>
        ///     Thread to handle receiving data from the client
        /// </summary>
        private void ReceiveWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _rloginConnection.IsConnected())
            {
                var bytesReceived = _rloginConnection.Receive(_socketReceiveBuffer, SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);

                if (bytesReceived == 0)
                    continue;

                //Parse RLogin Information
                if (SessionState == EnumSessionState.Negotiating && bytesReceived > 2 &&
                    _socketReceiveBuffer[0] == 0x0 )
                {
                    var usernameLength = 0;
                    var startingOrdinal = 1;

                    if (_socketReceiveBuffer[1] == 0)
                        startingOrdinal++;

                    ReadOnlySpan<byte> bufferSpan = _socketReceiveBuffer;

                    //Find End of Username
                    for (var i = startingOrdinal; i < bytesReceived; i++)
                    {
                        if (_socketReceiveBuffer[i] != 0x0) continue;
                        usernameLength = i - startingOrdinal;
                        break;
                    }

                    //Send 0 byte to ACK
                    Send(new byte[] {0x0});
                    Username = Encoding.ASCII.GetString(bufferSpan.Slice(startingOrdinal, usernameLength));

                    _logger.Info($"Rlogin For User: {Username}");

                    if (!string.IsNullOrEmpty(ModuleIdentifier))
                    {
                        CurrentModule = _host.GetModule(ModuleIdentifier);
                        SessionState = EnumSessionState.EnteringModule;
                    }
                    else
                    {
                        SessionState = EnumSessionState.MainMenuDisplay;
                    }

                    continue;
                }

                //Enqueue the incoming bytes for processing
                for (var i = 0; i < bytesReceived; i++)
                    DataFromClient.Add(_socketReceiveBuffer[i]);
            }

            //Cleanup if the connection was dropped
            Stop();
        }


        /// <summary>
        ///     Validates SocketError returned from an operation doesn't put the socket in an error state
        /// </summary>
        /// <param name="socketError"></param>
        private void ValidateSocketState(SocketError socketError)
        {
            switch (socketError)
            {
                case SocketError.Success:
                    return;
                case SocketError.ConnectionReset:
                case SocketError.TimedOut:
                    {
                        _logger.Warn($"Session {SessionId} (Channel: {Channel}) forcefully disconnected: {socketError}");
                        Stop();
                        return;
                    }
                default:
                    throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
            }

        }
    }
}

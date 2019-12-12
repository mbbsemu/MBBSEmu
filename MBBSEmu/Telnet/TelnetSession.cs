using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MBBSEmu.Telnet
{
    public class TelnetSession
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private Socket _telnetConnection;
        private Thread _sessionThread;
        private bool _isTyping;
        private MbbsHost _host;

        public TelnetSession(Socket telnetConnection, MbbsHost host)
        {
            _host = host;
            _telnetConnection = telnetConnection;
            _telnetConnection.ReceiveTimeout = (1000 * 60) * 5;
            _telnetConnection.ReceiveBufferSize = 128;
            _sessionThread = new Thread(SessionWorker);
            _sessionThread.Start();
        }

        private void SessionWorker()
        {
            try
            {
                //Disable Local Telnet Echo on Client
                Send(new byte[] {0xFF, 0xFB, 0x01});
                //Client will respond with 3 byte IAC
                byte[] terminalResponse = new byte[3];
                var bytesReceived = _telnetConnection.Receive(terminalResponse, 0, 3, SocketFlags.None);

                //Kick off Entry
                _host.Run("sttrou", Send);
                _host.Run("stsrou", Send);
                while (true)
                    Thread.Sleep(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                //Only do this if the client is actually still connected!
                if (_telnetConnection.Connected)
                {
                    //Send IAC for Logout (Client Do Close)
                    Send(new byte[] { 0xFF, 0xFD, 0x12 });
                    //Client will respond with 3 byte IAC
                    byte[] terminalResponse = new byte[3];
                    var bytesReceived = _telnetConnection.Receive(terminalResponse, 0, 3, SocketFlags.None);
                }

                //Clean up the socket
                _telnetConnection.Dispose();
            }
            
        }

        private bool Login()
        {
            while(true)
                ReceiveInput();

            return true;
        }

        /// <summary>
        ///     Send a Byte Array to the client
        /// </summary>
        /// <param name="dataToSend"></param>
        private void Send(ReadOnlySpan<byte> dataToSend)
        {
            var bytesSent = _telnetConnection.Send(dataToSend, SocketFlags.None, out var socketState);
            ValidateSocketState(socketState);
        }

        /// <summary>
        ///     Receive Input from the client
        /// </summary>
        /// <param name="isMasked"></param>
        /// <param name="maxLength"></param>
        /// <param name="regExPattern"></param>
        /// <returns></returns>
        private string ReceiveInput(bool isMasked = false, short maxLength = 0, string regExPattern = "[A-Za-z0-9 \\/!@#$%^&*().,?]\\w*")
        {
            var commandBuffer = new StringBuilder();
            var receiveBuffer = new byte[1];
            var inputFilter = new Regex(regExPattern);
            try
            {
                while (_telnetConnection.Connected)
                {
                    var bytesReceived =
                        _telnetConnection.Receive(receiveBuffer, 0, 1, SocketFlags.None, out var socketState);
                    ValidateSocketState(socketState);

                    //This usually only happens when the client disconnects and the socket hasn't timed out yet
                    //To keep the CPU from going nuts, we'll sleep for 10ms between reads until we either receive
                    //data again, or the connection eventually times out
                    if (bytesReceived == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    //Line Feed
                    if (receiveBuffer[0] == 10)
                        continue;

                    //Carriage Return means a command has been entered, return it
                    if (receiveBuffer[0] == 13)
                    {
                        //Send("|RESET|\r\n");
                        return commandBuffer.ToString();
                    }

                    //Handle backspace from the terminal
                    if (receiveBuffer[0] == 8)
                    {


                        //Only handle backspace if there's actually characters to delete
                        if (commandBuffer.Length > 0)
                        {
                            //We handle the last character of a maxlength defined input differently.
                            //Backspace with the last character filled will blank out the last character
                            //Everything else would be handled normally
                            Send(commandBuffer.Length == maxLength
                                ? new byte[] {0x20, 0x08}
                                : new byte[] {0x08, 0x20, 0x08});

                            commandBuffer.Remove(commandBuffer.Length - 1, 1);
                        }

                        continue;
                    }

                    //Max number of input characters allowed, so don't echo out the character typed and keep the cursor from moving forward
                    if (maxLength > 0 && commandBuffer.Length >= maxLength)
                    {
                        Send(new byte[] {0x08});
                        continue;
                    }

                    //If the input passes our regex, add it to the buffer, otherwise, don't allow it
                    if (inputFilter.Match(Encoding.Default.GetString(receiveBuffer)).Success)
                    {
                        commandBuffer.Append((char) receiveBuffer[0]);
                    }

                    _isTyping = commandBuffer.Length > 0;

                    //If it's masked, mask it. If not, echo the input
                    Send(isMasked ? new byte[] {0x2A} : receiveBuffer);

                    //Check to ensure we're not letting the carrot go past a maximum limit
                    if (commandBuffer.Length == maxLength)
                        Send(new byte[] {0x08});
                }
            }
            catch(Exception ex)
            {

            }

            return commandBuffer.ToString();
        }

        private static void ValidateSocketState(SocketError socketError)
        {
            if (socketError != SocketError.Success)
                throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
        }
    }
}

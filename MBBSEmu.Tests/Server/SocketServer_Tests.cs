using FluentAssertions;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Server.Socket;
using MBBSEmu.Session.Enums;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace MBBSEmu.Tests.Server
{
    public class SocketServer_Tests
    {
        /// <summary>
        ///     Stop() disposes the listener; on .NET 10 the pending accept then completes with
        ///     SocketException(OperationAborted) rather than ObjectDisposedException
        /// </summary>
        [Fact]
        public void IsListenerClosed_OperationAborted()
        {
            SocketServer.IsListenerClosed(new SocketException((int)SocketError.OperationAborted)).Should().BeTrue();
        }

        [Fact]
        public void IsListenerClosed_ObjectDisposed()
        {
            SocketServer.IsListenerClosed(new ObjectDisposedException("Socket")).Should().BeTrue();
        }

        [Fact]
        public void IsListenerClosed_OtherSocketErrorsAreNot()
        {
            SocketServer.IsListenerClosed(new SocketException((int)SocketError.ConnectionReset)).Should().BeFalse();
        }

        /// <summary>
        ///     Before the fix, the accept callback threw on a thread-pool thread after Stop() and aborted the
        ///     process, so shutting MBBSEmu down crashed it (leaving the admin console's terminal unrestored)
        /// </summary>
        [Fact]
        public void Stop_WithPendingAccept_DoesNotCrash()
        {
            var logFactory = new LogFactory();
            if (!logFactory.HasLogger<MessageLogger>())
                logFactory.AddLogger(new MessageLogger(new QueueTarget()));

            var server = new SocketServer(logFactory, null, null, null, null);
            server.Start(EnumSessionType.Telnet, "127.0.0.1", FreePort());

            server.Stop();

            //Nothing to observe on success: give the accept callback time to run on the thread pool
            Thread.Sleep(500);
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)probe.LocalEndPoint!).Port;
            probe.Dispose();
            return port;
        }
    }
}

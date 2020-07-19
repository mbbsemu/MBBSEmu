using System.Net.Sockets;
using MBBSEmu.DependencyInjection;
using NLog;

namespace MBBSEmu.Extensions
{
    static class SocketExtensions
    {
        private static readonly ILogger _logger = ServiceResolver.GetService<ILogger>();

        /// <summary>
        ///     Fast routine to determine if a socket is indeed still connected or not
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool IsConnected(this Socket socket)
        {
            try
            {
                if (!socket.Poll(1, SelectMode.SelectRead) || socket.Available != 0) return true;

                _logger.Warn("Socket Polling Failed -- marking Socket as disconnected");
                return false;
            }
            catch (SocketException ex)
            {
                _logger.Warn(ex);
                return false;
            }
        }
    }
}

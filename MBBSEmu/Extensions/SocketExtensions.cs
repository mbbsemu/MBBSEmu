using System.Net.Sockets;

namespace MBBSEmu.Extensions
{
    static class SocketExtensions
    {
        /// <summary>
        ///     Fast routine to determine if a socket is indeed still connected or not
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool IsConnected(this Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }
    }
}

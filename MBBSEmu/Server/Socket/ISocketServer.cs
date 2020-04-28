using MBBSEmu.Session;

namespace MBBSEmu.Server.Socket
{
    public interface ISocketServer
    {
        void Start(EnumSessionType sessionType, int port, string moduleIdentifier = null);
        void Stop();
    }
}
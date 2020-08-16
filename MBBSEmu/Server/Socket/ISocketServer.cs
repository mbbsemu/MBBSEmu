using MBBSEmu.Server;
using MBBSEmu.Session;

namespace MBBSEmu.Server.Socket
{
    public interface ISocketServer : IStoppable
    {
        void Start(EnumSessionType sessionType, int port, string moduleIdentifier = null);
    }
}

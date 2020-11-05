using MBBSEmu.Memory;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;

namespace MBBSEmu.Server.Socket
{
    public interface ISocketServer : IStoppable
    {
        void Start(EnumSessionType sessionType, int port, PointerDictionary<SessionBase> channelDictionary,  string moduleIdentifier = null);
    }
}

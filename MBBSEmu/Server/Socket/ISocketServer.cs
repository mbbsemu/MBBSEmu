using MBBSEmu.Module;
using MBBSEmu.Session.Enums;
using System.Collections.Generic;

namespace MBBSEmu.Server.Socket
{
    public interface ISocketServer : IStoppable
    {
        void Start(EnumSessionType sessionType, string hostIpAddress, int port, List<ModuleConfiguration> moduleConfigurations = null, string moduleIdentifier = null);
    }
}

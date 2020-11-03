using System.Collections.Generic;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess.HostRoutines
{
    public interface IHostRoutine
    {
        bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules, PointerDictionary<SessionBase> channelDictionary);
    }
}
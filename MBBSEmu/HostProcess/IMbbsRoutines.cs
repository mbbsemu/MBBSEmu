using System.Collections.Generic;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess
{
    public interface IMbbsRoutines
    {
        void ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules);
    }
}
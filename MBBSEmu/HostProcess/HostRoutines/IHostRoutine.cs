using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;

namespace MBBSEmu.HostProcess.HostRoutines
{
    public interface IHostRoutine
    {
        bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules);
    }
}

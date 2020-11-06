using System.Collections.Generic;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess.HostRoutines
{
    public interface IHostRoutine
    {
        bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules);
    }
}

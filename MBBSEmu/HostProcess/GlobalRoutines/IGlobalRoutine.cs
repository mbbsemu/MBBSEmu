using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public interface IGlobalRoutine
    {
        bool ProcessCommand(byte[] command, ushort channelNumber, PointerDictionary<SessionBase> sessions,
            Dictionary<string, MbbsModule> modules);
    }
}

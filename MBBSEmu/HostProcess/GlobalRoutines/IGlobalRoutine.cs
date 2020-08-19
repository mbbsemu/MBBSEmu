using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public interface IGlobalRoutine
    {
        /// <summary>
        ///     Processes a Global Command and returns TRUE if the command was handled
        /// </summary>
        /// <param name="command"></param>
        /// <param name="channelNumber"></param>
        /// <param name="sessions"></param>
        /// <param name="modules"></param>
        /// <returns></returns>
        bool ProcessCommand(byte[] command, ushort channelNumber, PointerDictionary<SessionBase> sessions,
            Dictionary<string, MbbsModule> modules);
    }
}

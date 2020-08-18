using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class UsersOnlineGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(byte[] command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');

            if (commandString == "/#")
            {
                sessions[channelNumber].SendToClient("|B||GREEN|LINE   USER-ID                    ....... OPTION SELECTED|RESET|\r\n".EncodeToANSIArray());
                foreach (var s in sessions.Values)
                {
                    sessions[channelNumber].SendToClient($" {s.Channel:D2}   {s.Username}                       ... {s.SessionState}\r\n");

                }

                return true;
            }

            return false;
        }
    }
}

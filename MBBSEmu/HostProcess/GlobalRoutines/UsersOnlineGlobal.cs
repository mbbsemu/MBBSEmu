using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class UsersOnlineGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules, List<ModuleConfiguration> moduleConfigurations)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');

            if (commandString == "/#")
            {
                sessions[channelNumber].SendToClient("|RESET|\r\n|B||GREEN|LINE  USER-ID                    ....... OPTION SELECTED|RESET|\r\n".EncodeToANSIArray());
                foreach (var s in sessions.Values)
                {
                    var sessionInfo = s.SessionState.GetSessionState();

                    var userOptionSelected = sessionInfo.UserOptionSelected;
                    var userName = sessionInfo.userName;

                    if (sessionInfo.userSession)
                        userName = s.Username;

                    if (sessionInfo.moduleSession)
                        userOptionSelected = s.CurrentModule.ModuleDescription;

                    sessions[channelNumber].SendToClient($"|YELLOW||B| {s.Channel:D2}   {userName,-31}... {userOptionSelected}|RESET|\r\n".EncodeToANSIArray());
                }

                return true;
            }

            return false;
        }
    }
}

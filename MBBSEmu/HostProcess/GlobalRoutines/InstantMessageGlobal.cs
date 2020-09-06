using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class InstantMessageGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(byte[] command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');
            
            //Check minimum length of a /P command (one character username/one character message = 6) and make sure there are at least 2 spaces
            if (commandString.Length > 6 && commandString.Substring(0,2).ToUpper() == "/P" && Regex.Matches(commandString, " ").Count > 1)
            {
                //Determine location of first 2 spaces to separate username and message
                var instantMessageSpace1 = commandString.IndexOf(' ');
                var instantMessageSpace2 = commandString.IndexOf(' ', instantMessageSpace1 + 1);

                //Define source username, target username, and message
                var instantMessageSourceUser = sessions[channelNumber].Username;
                var instantMessageTargetUser = commandString.Substring(3, instantMessageSpace2-3);
                var instantMessageText = commandString.Substring(instantMessageSpace2+1);

                //Check to see if the target user matches or matches part of any logged in users
                if (sessions.Values.Count(u => u.Username.StartsWith(instantMessageTargetUser)) == 1)
                {
                    foreach (var s in sessions.Values)
                    {
                        var tempUserName = s.Username;
                        if (tempUserName == instantMessageTargetUser || tempUserName.StartsWith(instantMessageTargetUser))
                        {
                            sessions[channelNumber].SendToClient($"|RESET|\r\n|B||YELLOW|... Paging {tempUserName} ...|RESET|\r\n".EncodeToANSIArray());
                            // TO DO: Need to pull module name, currently says "InModule"
                            sessions[s.Channel].SendToClient($"|RESET|\r\n|B||YELLOW|{instantMessageSourceUser} is paging you from {sessions[channelNumber].SessionState}: {instantMessageText}|RESET|\r\n".EncodeToANSIArray());
                        }
                    }
                    return true;
                }
                sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, {instantMessageTargetUser} is not logged on at the moment.|RESET|\r\n".EncodeToANSIArray());
                return false;
            }
            return false;
        }
    }
}

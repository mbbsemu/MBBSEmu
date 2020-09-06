using System;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class InstantMessageGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(byte[] command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');
            
            //Check minimum length of a /P command (one character username/one character message = 6) and make sure there are at least 2 spaces
            if (commandString.Length > 6 && commandString.StartsWith("/P", StringComparison.InvariantCultureIgnoreCase) && commandString.Count(c=> c == ' ') > 1)
            {
                //Create array of all elements between spaces
                var pageUserInput = commandString.Split(' ');
                //Define source username, target username, and message
                var pageMessageSourceUser = sessions[channelNumber].Username;
                var pageMessageTargetUser = pageUserInput[1];
                var pageMessageText = string.Join(" ",pageUserInput.Skip(2));

                //Check to see if the target user matches or matches part of any logged in users
                var matchingUsers = sessions.Values.Where(u => u.Username.StartsWith(pageMessageTargetUser)).ToList();
                var numberMatchingUsers = matchingUsers.Count();
                
                switch (numberMatchingUsers)
                {
                    case 0:
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, {pageMessageTargetUser} is not logged on at the moment.|RESET|\r\n".EncodeToANSIArray());
                        break;
                    case 1:
                        var matchingSessionNumber = matchingUsers[0];
                        var matchingUserName = matchingSessionNumber.Username;
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||YELLOW|... Paging {matchingUserName} ...|RESET|\r\n".EncodeToANSIArray());
                        // TO DO: Need to pull module name, currently says "InModule" -- i think that might be the only condition, the rest wouldn't apply for messaging a signed in user
                        sessions[matchingSessionNumber.Channel].SendToClient($"|RESET|\r\n|B||YELLOW|{pageMessageSourceUser} is paging you from {sessions[channelNumber].SessionState}: {pageMessageText}|RESET|\r\n".EncodeToANSIArray());
                        break;
                    default:
                        //TO DO: Need to add an "IF" statement to check for exact matches, example: "themaniac" & "themaniac2" -- currently no way to message "themaniac" -- or is there another way?
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, more then 1 user matches your input, please be more specific.|RESET|\r\n".EncodeToANSIArray());
                        break;
                }
                return true;
            }
            return false;
        }
    }
}

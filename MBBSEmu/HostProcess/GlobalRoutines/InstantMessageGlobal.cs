using System;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MBBSEmu.Session.Enums;

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
                
                //Check for exact match -- fixes MajorBBS bug!
                var exactMatch = matchingUsers.FirstOrDefault(u => u.Username.Equals(pageMessageTargetUser, StringComparison.InvariantCultureIgnoreCase));
                if (exactMatch != null)
                {
                    matchingUsers.Clear();
                    matchingUsers.Add(exactMatch);
                }
                
                //# of possible matches
                var numberMatchingUsers = matchingUsers.Count();
                
                switch (numberMatchingUsers)
                {
                    case 0:
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, {pageMessageTargetUser} is not logged on at the moment.|RESET|\r\n".EncodeToANSIArray());
                        break;
                    case 1:
                        var matchingSessionNumber = matchingUsers[0];
                        var matchingUserName = matchingSessionNumber.Username;
                        
                        //if inModule -- change to module name
                        string currentUserOptionSelected;
                        currentUserOptionSelected = sessions[channelNumber].SessionState == EnumSessionState.InModule ? sessions[channelNumber].CurrentModule.ModuleDescription : sessions[channelNumber].SessionState.ToString();
                        
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||YELLOW|... Paging {matchingUserName} ...|RESET|\r\n".EncodeToANSIArray());
                        sessions[matchingSessionNumber.Channel].SendToClient($"|RESET|\r\n|B||YELLOW|{pageMessageSourceUser} is paging you from {currentUserOptionSelected}: {pageMessageText}|RESET|\r\n".EncodeToANSIArray());
                        break;
                    default:
                        sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, more then 1 user matches your input, please be more specific.|RESET|\r\n".EncodeToANSIArray());
                        break;
                }
                return true;
            }
            return false;
        }
    }
}

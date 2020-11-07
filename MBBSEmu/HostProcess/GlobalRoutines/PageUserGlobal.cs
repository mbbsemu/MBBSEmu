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
    public class PageUserGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            //Check minimum length for a /p command else exit
            if(command.Length < 6) return false;
            
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');

            //Check if command begins with "/p" and make sure there are at least 2 spaces
            if (!commandString.StartsWith("/P", StringComparison.InvariantCultureIgnoreCase) ||
                commandString.Count(c => c == ' ') <= 1) return false;
            
            //Create array of all elements between spaces
            var pageUserInput = commandString.Split(' ');
                
            //Define source username, target username, and message
            var pageMessageSourceUser = sessions[channelNumber].Username;
            var pageMessageTargetUser = pageUserInput[1];
            var pageMessageText = string.Join(" ",pageUserInput.Skip(2));

            //Check to see if the target user matches or matches part of any logged in users
            var pageMatchingUsers = sessions.Values.Where(u => u.Username.StartsWith(pageMessageTargetUser)).ToList();
                
            //Check for exact match -- fixes MajorBBS bug!
            var pageExactMatch = pageMatchingUsers.FirstOrDefault(u => u.Username.Equals(pageMessageTargetUser, StringComparison.InvariantCultureIgnoreCase));
            if (pageExactMatch != null)
            {
                pageMatchingUsers.Clear();
                pageMatchingUsers.Add(pageExactMatch);
            }
                
            //# of possible matches
            var pageNumberMatchingUsers = pageMatchingUsers.Count();
                
            switch (pageNumberMatchingUsers)
            {
                case 0:
                    sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, {pageMessageTargetUser} is not logged on at the moment.|RESET|\r\n".EncodeToANSIArray());
                    break;
                case 1:
                    var pageMatchingSessionNumber = pageMatchingUsers[0];
                    var pageMatchingUserName = pageMatchingSessionNumber.Username;
                        
                    //if inModule -- change to module name
                    string currentUserOptionSelected;
                    currentUserOptionSelected = sessions[channelNumber].SessionState == EnumSessionState.InModule ? sessions[channelNumber].CurrentModule.ModuleDescription : sessions[channelNumber].SessionState.ToString();
                        
                    sessions[channelNumber].SendToClient($"|RESET|\r\n|B||YELLOW|... Paging {pageMatchingUserName} ...|RESET|\r\n".EncodeToANSIArray());
                    sessions[pageMatchingSessionNumber.Channel].SendToClient($"|RESET|\r\n|B||YELLOW|{pageMessageSourceUser} is paging you from {currentUserOptionSelected}: {pageMessageText}|RESET|\r\n".EncodeToANSIArray());
                    break;
                default:
                    sessions[channelNumber].SendToClient($"|RESET|\r\n|B||MAGENTA|Sorry, more then 1 user matches your input, please be more specific.|RESET|\r\n".EncodeToANSIArray());
                    break;
            }
            return true;
        }
    }
}

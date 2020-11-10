using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class UsersOnlineGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');

            if (commandString == "/#")
            {
                sessions[channelNumber].SendToClient("|RESET|\r\n|B||GREEN|LINE  USER-ID                    ....... OPTION SELECTED|RESET|\r\n".EncodeToANSIArray());
                foreach (var s in sessions.Values)
                {
                    string userOptionSelected;
                    string userName;
                    switch (s.SessionState)
                    {
                        case EnumSessionState.EnteringFullScreenDisplay:
                        case EnumSessionState.EnteringFullScreenEditor:
                        case EnumSessionState.ExitingFullScreenDisplay:
                        case EnumSessionState.InFullScreenDisplay:
                        case EnumSessionState.InFullScreenEditor:
                        case EnumSessionState.EnteringModule:
                        case EnumSessionState.InModule:
                            userName = s.Username;
                            userOptionSelected = s.CurrentModule.ModuleDescription;
                            break;
                        case EnumSessionState.LoginRoutines:
                        case EnumSessionState.MainMenuDisplay:
                        case EnumSessionState.MainMenuInput:
                        case EnumSessionState.ExitingModule:
                            userName = s.Username;
                            userOptionSelected = "Main Menu";
                            break;
                        case EnumSessionState.SignupUsernameInput:
                        case EnumSessionState.SignupUsernameDisplay:
                        case EnumSessionState.SignupEmailDisplay:
                        case EnumSessionState.SignupEmailInput:
                        case EnumSessionState.SignupPasswordDisplay:
                        case EnumSessionState.SignupPasswordInput:
                        case EnumSessionState.SignupPasswordConfirm:
                        case EnumSessionState.SignupPasswordConfirmDisplay:
                        case EnumSessionState.SignupPasswordConfirmInput:
                            userName = "<<New User>>";
                            userOptionSelected = "New User Signup";
                            break;
                        case EnumSessionState.Negotiating:
                        case EnumSessionState.Unauthenticated:
                        case EnumSessionState.LoginPasswordDisplay:
                        case EnumSessionState.LoginPasswordInput:
                        case EnumSessionState.LoginUsernameDisplay:
                        case EnumSessionState.LoginUsernameInput:
                            userName = "<<Logging In>>";
                            userOptionSelected = "User Login";
                            break;
                        case EnumSessionState.LoggingOffDisplay:
                        case EnumSessionState.LoggingOffProcessing:
                        case EnumSessionState.LoggedOff:
                        case EnumSessionState.ConfirmLogoffInput:
                        case EnumSessionState.ConfirmLogoffDisplay:
                            userName = s.Username;
                            userOptionSelected = "Logging Off";
                            break;
                        default:
                            userOptionSelected = "Online"; //Default Value
                            userName = "*UNKNOWN*";
                            break;

                    }
                    sessions[channelNumber].SendToClient($"|YELLOW||B| {s.Channel:D2}   {userName,-31}... {userOptionSelected}|RESET|\r\n".EncodeToANSIArray());
                }

                return true;
            }

            return false;
        }
    }
}

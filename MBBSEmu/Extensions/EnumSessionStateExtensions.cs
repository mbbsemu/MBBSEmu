using MBBSEmu.Session.Enums;
using System.Runtime.CompilerServices;

namespace MBBSEmu.Extensions
{
    public static class EnumSessionStateExtensions
    {
        /// <summary>
        ///     Helper Method to determine a friendly description for EnumSessionState
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (string userName, string UserOptionSelected, bool userSession, bool moduleSession) GetSessionState(this EnumSessionState b)
        {
            string userName;
            string userOptionSelected;
            bool userSession;
            bool moduleSession;
            
            switch (b)
            {
                case EnumSessionState.EnteringFullScreenDisplay:
                case EnumSessionState.EnteringFullScreenEditor:
                case EnumSessionState.ExitingFullScreenDisplay:
                case EnumSessionState.InFullScreenDisplay:
                case EnumSessionState.InFullScreenEditor:
                case EnumSessionState.EnteringModule:
                case EnumSessionState.RloginEnteringModule:
                case EnumSessionState.InModule:
                    userName = "";
                    userSession = true;
                    userOptionSelected = "";
                    moduleSession = true;
                    break;
                case EnumSessionState.LoginRoutines:
                case EnumSessionState.MainMenuDisplay:
                case EnumSessionState.MainMenuInput:
                case EnumSessionState.MainMenuInputDisplay:
                case EnumSessionState.ExitingModule:
                    userName = "";
                    userSession = true;
                    userOptionSelected = "Main Menu";
                    moduleSession = false;
                    break;
                case EnumSessionState.SignupUsernameInput:
                case EnumSessionState.SignupUsernameDisplay:
                case EnumSessionState.SignupEmailDisplay:
                case EnumSessionState.SignupEmailInput:
                case EnumSessionState.SignupGenderDisplay:
                case EnumSessionState.SignupGenderInput:
                case EnumSessionState.SignupPasswordDisplay:
                case EnumSessionState.SignupPasswordInput:
                case EnumSessionState.SignupPasswordConfirm:
                case EnumSessionState.SignupPasswordConfirmDisplay:
                case EnumSessionState.SignupPasswordConfirmInput:
                    userName = "<<New User>>";
                    userSession = false;
                    userOptionSelected = "New User Sign-up";
                    moduleSession = false;
                    break;
                case EnumSessionState.Negotiating:
                case EnumSessionState.Unauthenticated:
                case EnumSessionState.LoginPasswordDisplay:
                case EnumSessionState.LoginPasswordInput:
                case EnumSessionState.LoginUsernameDisplay:
                case EnumSessionState.LoginUsernameInput:
                    userName = "<<Logging In>>";
                    userSession = false;
                    userOptionSelected = "User Login";
                    moduleSession = false;
                    break;
                case EnumSessionState.LoggingOffDisplay:
                case EnumSessionState.LoggingOffProcessing:
                case EnumSessionState.LoggedOff:
                case EnumSessionState.ConfirmLogoffInput:
                case EnumSessionState.ConfirmLogoffDisplay:
                    userName = "";
                    userSession = true;
                    userOptionSelected = "Logging Off";
                    moduleSession = false;
                    break;
                default:
                    userName = "*UNKNOWN*";
                    userSession = false;
                    userOptionSelected = "*UNKNOWN*"; //Default Value
                    moduleSession = false;
                    break;
            }

            return (userName, userOptionSelected, userSession, moduleSession);
        }
    }
}
